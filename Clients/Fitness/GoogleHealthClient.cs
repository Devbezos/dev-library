using DevClient.Data;
using DevClient.Data.Fitness;
using DevClient.Clients;
using Discord;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http.Headers;
using ICustomDiscordClient = DevClient.Clients.IDiscordClient;

namespace DevClient.Clients.Fitness
{
    public class GoogleHealthClient
    {
        private readonly ICustomDiscordClient _discordClient;
        private readonly GoogleHealthUserSettings _settings;
        private readonly IFitnessRepository? _fitnessRepository;

        // Access tokens last an hour and a client is created per request, so they're shared by refresh token across
        // instances; the lock stops a snapshot's parallel calls from each refreshing at once.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Token, DateTime Expiry)> SharedTokens = new();
        private static readonly SemaphoreSlim TokenRefreshLock = new(1, 1);

        public GoogleHealthClient(GoogleHealthUserSettings settings) : this(settings, new DiscordClient(), null) { }
        public GoogleHealthClient(GoogleHealthUserSettings settings, IFitnessRepository fitnessRepository) : this(settings, new DiscordClient(), fitnessRepository) { }

        public GoogleHealthClient(GoogleHealthUserSettings settings, ICustomDiscordClient discordClient, IFitnessRepository? fitnessRepository = null)
        {
            _settings = settings;
            _discordClient = discordClient;
            _fitnessRepository = fitnessRepository;
        }

        // Public so other Google APIs granted to the same sign-in (e.g. Calendar) can share the token.
        public async Task<string> GetAccessToken()
        {
            if (TryGetSharedToken() is { } cached)
                return cached;

            await TokenRefreshLock.WaitAsync();
            try
            {
                return TryGetSharedToken() ?? await RefreshAccessToken();
            }
            finally
            {
                TokenRefreshLock.Release();
            }
        }

        private string? TryGetSharedToken() =>
            SharedTokens.TryGetValue(_settings.RefreshToken, out var shared) && DateTime.UtcNow < shared.Expiry.AddMinutes(-5) ? shared.Token : null;

        private async Task<string> RefreshAccessToken()
        {
            Log.Information("GoogleHealthClient: Refreshing access token");

            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["refresh_token"] = _settings.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

            var response = await client.PostAsync(Constants.GoogleHealth.TokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to refresh Google Health token: {json}");

            var tokenResponse = JsonConvert.DeserializeObject<GoogleHealthTokenResponse>(json)
                ?? throw new InvalidOperationException("Failed to parse token response");

            SharedTokens[_settings.RefreshToken] = (tokenResponse.AccessToken, DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn));
            return tokenResponse.AccessToken;
        }

        private async Task<string?> FetchRaw(string dataType, string? filter = null, string? pageToken = null)
        {
            var token = await GetAccessToken();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = $"{Constants.GoogleHealth.BaseUrl}/users/me/dataTypes/{dataType}/dataPoints";
            var queryParts = new List<string>();
            if (filter != null) queryParts.Add($"filter={filter}");
            if (pageToken != null) queryParts.Add($"pageToken={pageToken}");
            if (queryParts.Count > 0) url += "?" + string.Join("&", queryParts);

            // A day's snapshot fires several reads at once; rate limits and server hiccups get a couple of retries
            // so a passing failure doesn't come back as "no data".
            for (var attempt = 1; ; attempt++)
            {
                using var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return json;
                var retryable = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                if (!retryable || attempt == 3)
                {
                    Log.Warning("GoogleHealthClient fetch {DataType} failed ({Status}): {Json}", dataType, (int)response.StatusCode, json);
                    return null;
                }
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }

        private async Task<T?> FetchDataType<T>(string dataType, string? filter = null, string? pageToken = null) where T : class
        {
            var json = await FetchRaw(dataType, filter, pageToken);
            if (json == null) return null;
            Log.Debug("GoogleHealthClient [{DataType}] raw response: {Json}", dataType, json);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// When set, workouts come from here (e.g. Hevy) instead of Google Health's exercise records. Everything else
        /// (steps, sleep, heart rate, food, weight) still comes from Google.
        /// </summary>
        public IWorkoutSource? WorkoutSource { get; set; }

        /// <summary>
        /// Calories a day takes at rest and everyday movement; when set, the daily post shows calories burnt as this plus
        /// what the day's activity burned.
        /// </summary>
        public double? MaintenanceCalories { get; set; }

        // Outside workouts in the same shape as Google's exercise records, so posts, calorie estimates and history
        // treat them alike. They're strength sessions (Hevy is a lifting app) with their full duration as active time.
        private async Task<List<GoogleHealthDataPoint>> GetSourceWorkouts(DateOnly from, DateOnly toExclusive) =>
            (await WorkoutSource!.GetWorkouts(from, toExclusive))
                .Select(w => new GoogleHealthDataPoint
                {
                    Exercise = new GoogleHealthExercise
                    {
                        DisplayName = w.Name,
                        ExerciseType = "STRENGTH_TRAINING",
                        ActiveDuration = $"{(int)Math.Max(0, (w.EndUtc - w.StartUtc).TotalSeconds)}s",
                        Interval = new GoogleHealthInterval
                        {
                            StartTime = w.StartUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            EndTime = w.EndUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        },
                    },
                })
                .ToList();

        /// <summary>
        /// Average heart rate Google recorded between <paramref name="startUtc"/> and <paramref name="endUtc"/>
        /// (e.g. during a workout), or null when there's no reading or Google couldn't be read.
        /// </summary>
        public async Task<int?> GetAverageHeartRate(DateTime startUtc, DateTime endUtc)
        {
            var seconds = (int)(endUtc - startUtc).TotalSeconds;
            if (seconds < 1 || seconds > 14 * 24 * 3600) return null;
            var json = await PostRaw("users/me/dataTypes/heart-rate/dataPoints:rollUp", new
            {
                range = new { startTime = startUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), endTime = endUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") },
                windowSize = $"{seconds}s",
            });
            if (json == null) return null;
            var avg = Newtonsoft.Json.Linq.JObject.Parse(json)["rollupDataPoints"]?.FirstOrDefault()?["heartRate"]?["beatsPerMinuteAvg"]?.ToObject<double?>();
            return avg is > 0 ? (int)Math.Round(avg.Value) : null;
        }

        /// <summary>
        /// Exercises that started on <paramref name="day"/> (phone's local time). With a <see cref="WorkoutSource"/>,
        /// its workouts plus Google's stretching sessions, since stretches are only ever logged to Google.
        /// </summary>
        public async Task<List<GoogleHealthDataPoint>> GetDayExercises(DateOnly day)
        {
            if (WorkoutSource == null)
                return await GetGoogleDayExercises(day);
            var sourceTask = GetSourceWorkouts(day, day.AddDays(1));
            var stretchesTask = GetGoogleStretches(day);
            await Task.WhenAll(sourceTask, stretchesTask);
            return [.. sourceTask.Result, .. stretchesTask.Result];
        }

        // Best effort: the source's workouts still count when Google can't be read.
        private async Task<List<GoogleHealthDataPoint>> GetGoogleStretches(DateOnly day)
        {
            if (string.IsNullOrEmpty(_settings.RefreshToken))
                return [];
            try
            {
                return (await GetGoogleDayExercises(day)).Where(e => FitnessActivityKinds.IsStretching(
                    !string.IsNullOrWhiteSpace(e.Exercise.DisplayName) ? e.Exercise.DisplayName : e.Exercise.ExerciseType)).ToList();
            }
            catch (Exception ex)
            {
                Log.Warning("GoogleHealthClient: could not read stretches for {Username}: {Error}", _settings.Username, ex.Message);
                return [];
            }
        }

        private async Task<List<GoogleHealthDataPoint>> GetGoogleDayExercises(DateOnly day)
        {
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{day:yyyy-MM-dd}T00:00:00\" AND exercise.interval.civil_start_time < \"{day.AddDays(1):yyyy-MM-dd}T00:00:00\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        public Task<List<GoogleHealthDataPoint>> Get24HourExercises() =>
            GetDayExercises(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));

        private async Task<string?> PostRaw(string path, object body)
        {
            var token = await GetAccessToken();
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{Constants.GoogleHealth.BaseUrl}/{path}", content);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("GoogleHealthClient POST {Path} failed: {Json}", path, json);
                return null;
            }
            return json;
        }

        private static object CivilDate(DateOnly d) => new { date = new { year = d.Year, month = d.Month, day = d.Day } };

        /// <summary>
        /// Steps per day from <paramref name="from"/> up to (not including) <paramref name="toExclusive"/>, using
        /// Google's daily roll-up. The roll-up merges overlapping sources (watch, phone, Health Connect apps) the way
        /// the Fitbit and Google apps do; adding up raw step records double-counts them. Days without steps are
        /// left out. Returns null when Google Health couldn't be read.
        /// </summary>
        public async Task<Dictionary<DateOnly, long>?> GetDailyStepTotals(DateOnly from, DateOnly toExclusive)
        {
            var totals = new Dictionary<DateOnly, long>();
            // The roll-up accepts at most 90 days per request.
            for (var start = from; start < toExclusive; start = start.AddDays(90))
            {
                var end = start.AddDays(90) < toExclusive ? start.AddDays(90) : toExclusive;
                string? pageToken = null;
                do
                {
                    var json = await PostRaw("users/me/dataTypes/steps/dataPoints:dailyRollUp", new Dictionary<string, object?>
                    {
                        ["range"] = new { start = CivilDate(start), end = CivilDate(end) },
                        ["pageToken"] = pageToken,
                    });
                    if (json == null) return null;
                    var parsed = JsonConvert.DeserializeObject<GoogleHealthStepsRollupResponse>(json);
                    foreach (var point in parsed?.RollupDataPoints ?? [])
                        if (point.Steps is { } steps && long.TryParse(steps.CountSum, out var count))
                            totals[point.CivilStartTime.Date.ToDateOnly()] = count;
                    pageToken = parsed?.NextPageToken;
                } while (!string.IsNullOrEmpty(pageToken));
            }
            return totals;
        }

        private async Task<long?> TrySumSteps(DateOnly from, DateOnly toExclusive) =>
            await GetDailyStepTotals(from, toExclusive) is { } totals ? totals.Values.Sum() : null;

        /// <summary>
        /// Steps on <paramref name="day"/> (phone's local date), or null when Google Health couldn't be read, so a
        /// failed fetch isn't reported as zero steps.
        /// </summary>
        public Task<long?> TryGetStepCountForDay(DateOnly day) => TrySumSteps(day, day.AddDays(1));

        /// <summary>Yesterday's steps (the daily post is a summary of yesterday).</summary>
        public async Task<long> Get24HourStepCount()
        {
            var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            return await TrySumSteps(yesterday, yesterday.AddDays(1)) ?? 0;
        }

        public async Task<List<GoogleHealthSleepDataPoint>> Get24HourSleep()
        {
            var cutoff = DateTime.Today.AddDays(-1);
            var result = await FetchDataType<GoogleHealthSleepResponse>("sleep");
            if (result == null) return new List<GoogleHealthSleepDataPoint>();

            // Include sleep sessions that either start within the last 24 hours
            // or overlap the window (i.e., end after the cutoff). This ensures
            // sleeps that begin before midnight but continue into the target
            // day are counted for the daily snapshot.
            var filtered = result.DataPoints
                .Where(dp => !dp.Sleep.Metadata.Nap &&
                    (
                        (DateTime.TryParse(dp.Sleep.Interval.StartTime, out var s) && s >= cutoff) ||
                        (DateTime.TryParse(dp.Sleep.Interval.EndTime, out var e) && e >= cutoff)
                    ))
                .ToList();
            return filtered;
        }

        public async Task<List<GoogleHealthDataPoint>> Get7DayExercises()
        {
            if (WorkoutSource != null)
                return await GetSourceWorkouts(DateOnly.FromDateTime(DateTime.Today.AddDays(-7)), DateOnly.FromDateTime(DateTime.Today).AddDays(1));
            var sevenDaysAgo = DateTime.Now.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ss");
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{sevenDaysAgo}\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        public async Task<long> Get7DayStepCount()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return await TrySumSteps(today.AddDays(-7), today.AddDays(1)) ?? 0;
        }

        public async Task<List<GoogleHealthSleepDataPoint>> Get7DaySleep()
        {
            var cutoff = DateTime.Now.AddDays(-7).Date;
            var result = await FetchDataType<GoogleHealthSleepResponse>("sleep");
            if (result == null) return new List<GoogleHealthSleepDataPoint>();
            var filtered = result.DataPoints
                .Where(dp =>
                    !dp.Sleep.Metadata.Nap &&
                    DateTime.TryParse(dp.Sleep.Interval.StartTime, out var t) && t.Date >= cutoff)
                .ToList();
            return filtered;
        }

        private async Task<List<GoogleHealthWeightDataPoint>> GetRecentWeight(int daysBack)
        {
            var cutoff = DateTime.Now.AddDays(-daysBack);
            var json = await FetchRaw("weight");
            if (json == null) return new List<GoogleHealthWeightDataPoint>();
            var result = JsonConvert.DeserializeObject<GoogleHealthWeightResponse>(json);
            if (result?.DataPoints == null) return new List<GoogleHealthWeightDataPoint>();
            return result.DataPoints
                .Where(dp => DateTime.TryParse(dp.Weight.SampleTime.PhysicalTime, out var t) && t >= cutoff)
                .OrderBy(dp => dp.Weight.SampleTime.PhysicalTime)
                .ToList();
        }

        private async Task<List<GoogleHealthRestingHrDataPoint>> GetRestingHeartRate(int daysBack)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysBack));
            var result = await FetchDataType<GoogleHealthRestingHrResponse>("daily-resting-heart-rate");
            if (result?.DataPoints == null) return new List<GoogleHealthRestingHrDataPoint>();
            return result.DataPoints
                .Where(dp => dp.DailyRestingHeartRate.BeatsPerMinute > 0 &&
                             dp.DailyRestingHeartRate.Date.ToDateOnly() >= cutoff)
                .OrderBy(dp => dp.DailyRestingHeartRate.Date.ToDateOnly())
                .ToList();
        }

        private static DateTime GetStartOfWeek()
        {
            var daysToMonday = ((int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return DateTime.Today.AddDays(-daysToMonday);
        }

        public async Task<List<GoogleHealthDataPoint>> GetWeekSoFarExercises()
        {
            if (WorkoutSource != null)
                return await GetSourceWorkouts(DateOnly.FromDateTime(GetStartOfWeek()), DateOnly.FromDateTime(DateTime.Today).AddDays(1));
            var since = GetStartOfWeek().ToString("yyyy-MM-ddTHH:mm:ss");
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{since}\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        public async Task<long> GetWeekSoFarStepCount() =>
            await TrySumSteps(DateOnly.FromDateTime(GetStartOfWeek()), DateOnly.FromDateTime(DateTime.Today).AddDays(1)) ?? 0;

        public async Task<List<GoogleHealthSleepDataPoint>> GetWeekSoFarSleep()
        {
            var cutoff = GetStartOfWeek();
            var result = await FetchDataType<GoogleHealthSleepResponse>("sleep");
            return result?.DataPoints
                .Where(dp =>
                    !dp.Sleep.Metadata.Nap &&
                    DateTime.TryParse(dp.Sleep.Interval.StartTime, out var t) && t.Date >= cutoff)
                .ToList() ?? new List<GoogleHealthSleepDataPoint>();
        }

        public async Task<List<GoogleHealthNutritionDataPoint>> Get7DayNutrition()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7).Date.ToString("yyyy-MM-dd") + "T00:00:00";
            var result = await FetchDataType<GoogleHealthNutritionResponse>("nutrition-log",
                $"nutrition_log.interval.civil_start_time >= \"{sevenDaysAgo}\"");
            return result?.DataPoints ?? new List<GoogleHealthNutritionDataPoint>();
        }

        public async Task<List<GoogleHealthNutritionDataPoint>> GetDayNutrition(DateOnly date)
        {
            var start = date.ToString("yyyy-MM-dd") + "T00:00:00";
            var end   = date.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00";
            var result = await FetchDataType<GoogleHealthNutritionResponse>("nutrition-log",
                $"nutrition_log.interval.civil_start_time >= \"{start}\" AND nutrition_log.interval.civil_start_time < \"{end}\"");
            return result?.DataPoints ?? new List<GoogleHealthNutritionDataPoint>();
        }

        public async Task<string?> GetDayNutritionRaw(DateOnly date)
        {
            var start = date.ToString("yyyy-MM-dd") + "T00:00:00";
            var end   = date.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00";
            return await FetchRaw("nutrition-log",
                $"nutrition_log.interval.civil_start_time >= \"{start}\" AND nutrition_log.interval.civil_start_time < \"{end}\"");
        }

        public Task<List<GoogleHealthNutritionDataPoint>> GetTodayNutrition() =>
            GetDayNutrition(DateOnly.FromDateTime(DateTime.Today));

        public async Task<double?> GetMostRecentWeightLbs()
        {
            var weights = await GetRecentWeight(90);
            if (weights.Count == 0) return null;
            var latest = weights.OrderByDescending(w => w.Weight.SampleTime.PhysicalTime).First();
            return latest.Weight.WeightKg.HasValue ? latest.Weight.WeightKg!.Value * 2.20462 : null;
        }

        /// <summary>
        /// Yesterday's stats for the daily post. Sleep is the night that ended yesterday morning, the same as the stored
        /// history and the Today tab: the post goes out just after midnight, before last night's sleep exists.
        /// </summary>
        public Task<DailyFitnessSnapshot> GetDailySnapshot()
        {
            var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
            return GetDaySnapshot(yesterday, sleepWakeDate: yesterday);
        }

        /// <summary>
        /// Resting heart rate Google computed for <paramref name="day"/>.
        /// </summary>
        private async Task<List<GoogleHealthRestingHrDataPoint>> GetDayRestingHeartRate(DateOnly day)
        {
            var result = await FetchDataType<GoogleHealthRestingHrResponse>("daily-resting-heart-rate",
                $"daily_resting_heart_rate.date >= \"{day:yyyy-MM-dd}\" AND daily_resting_heart_rate.date < \"{day.AddDays(1):yyyy-MM-dd}\"");
            return result?.DataPoints.Where(dp => dp.DailyRestingHeartRate.BeatsPerMinute > 0).ToList() ?? new List<GoogleHealthRestingHrDataPoint>();
        }

        /// <summary>
        /// Main (non-nap) sleep sessions that ended on <paramref name="wakeDate"/>, local time. Sleep can't be filtered
        /// by date, so this pages back (newest first) until it's past that night.
        /// </summary>
        public async Task<List<GoogleHealthSleepDataPoint>> GetSleepWokenOn(DateOnly wakeDate)
        {
            var found = new List<GoogleHealthSleepDataPoint>();
            string? pageToken = null;
            for (var page = 0; page < 20; page++)
            {
                var result = await FetchDataType<GoogleHealthSleepResponse>("sleep", null, pageToken);
                if (result == null) break;
                found.AddRange(result.DataPoints.Where(dp => !dp.Sleep.Metadata.Nap
                    && DateTime.TryParse(dp.Sleep.Interval.EndTime, out var end) && DateOnly.FromDateTime(end) == wakeDate));
                var oldestStart = result.DataPoints
                    .Select(dp => DateTime.TryParse(dp.Sleep.Interval.StartTime, out var s) ? s : DateTime.MaxValue)
                    .DefaultIfEmpty(DateTime.MaxValue).Min();
                pageToken = result.NextPageToken;
                if (string.IsNullOrEmpty(pageToken) || DateOnly.FromDateTime(oldestStart) < wakeDate.AddDays(-2))
                    break;
            }
            return found;
        }

        /// <summary>The last weigh-in on <paramref name="day"/> in lbs, or null when there wasn't one.</summary>
        public async Task<double?> GetDayWeightLbs(DateOnly day)
        {
            var result = await FetchDataType<GoogleHealthWeightResponse>("weight",
                $"weight.sample_time.civil_time >= \"{day:yyyy-MM-dd}T00:00:00\" AND weight.sample_time.civil_time < \"{day.AddDays(1):yyyy-MM-dd}T00:00:00\"");
            var latest = result?.DataPoints
                .Where(dp => dp.Weight.WeightKg.HasValue)
                .OrderByDescending(dp => dp.Weight.SampleTime.PhysicalTime)
                .FirstOrDefault();
            return latest?.Weight.WeightKg * 2.20462;
        }

        /// <summary>
        /// Stats for one calendar <paramref name="day"/> (phone's local time): steps, exercises, food, resting heart
        /// rate and weigh-in from that day, plus the sleep you woke up from on <paramref name="sleepWakeDate"/>.
        /// </summary>
        public async Task<DailyFitnessSnapshot> GetDaySnapshot(DateOnly day, DateOnly sleepWakeDate)
        {
            var exercisesTask  = GetDayExercises(day);
            var stepsTask      = TryGetStepCountForDay(day);
            var sleepTask      = GetSleepWokenOn(sleepWakeDate);
            var hrTask         = GetDayRestingHeartRate(day);
            var nutritionTask  = GetDayNutrition(day);
            var weightTask     = GetMostRecentWeightLbs();
            var dayWeightTask  = GetDayWeightLbs(day);
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, hrTask, nutritionTask, weightTask, dayWeightTask);
            var steps = stepsTask.Result ?? 0;

            var mainSleep  = sleepTask.Result.OrderByDescending(s => s.Sleep.Summary.MinutesAsleep).FirstOrDefault();
            var sleepHours = mainSleep?.Sleep.GetDurationHours() is double h && h > 0 ? h : (double?)null;

            var latestHr = hrTask.Result.LastOrDefault();
            var hrBpm    = latestHr is not null && latestHr.DailyRestingHeartRate.BeatsPerMinute > 0
                ? latestHr.DailyRestingHeartRate.BeatsPerMinute : (int?)null;

            // Google's average heart rate during each workout, combined per activity weighted by minutes.
            var heartRates = await Task.WhenAll(exercisesTask.Result.Select(async e =>
                DateTime.TryParse(e.Exercise.Interval.StartTime, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var start)
                && DateTime.TryParse(e.Exercise.Interval.EndTime, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var end)
                    ? (Exercise: e, Bpm: await GetAverageHeartRate(start, end))
                    : (Exercise: e, Bpm: (int?)null)));

            var activities = heartRates
                .GroupBy(x => NormalizeActivityName(
                    !string.IsNullOrWhiteSpace(x.Exercise.Exercise.DisplayName)
                        ? x.Exercise.Exercise.DisplayName
                        : x.Exercise.Exercise.ExerciseType))
                .Select(g =>
                {
                    var withHr = g.Where(x => x.Bpm.HasValue).Select(x => (Bpm: x.Bpm!.Value, Minutes: Math.Max(1, ParseDurationSeconds(x.Exercise.Exercise.ActiveDuration) / 60))).ToList();
                    return new FitnessActivityEntry
                    {
                        Name            = g.Key,
                        Minutes         = g.Sum(x => ParseDurationSeconds(x.Exercise.Exercise.ActiveDuration) / 60),
                        AvgHeartRateBpm = withHr.Count == 0 ? null : (int)Math.Round(withHr.Sum(x => (double)x.Bpm * x.Minutes) / withHr.Sum(x => x.Minutes)),
                    };
                })
                .ToList();

            var caloriesFromMetrics = exercisesTask.Result.Sum(e => e.Exercise.MetricsSummary.CaloriesKcal ?? 0);
            var caloriesEaten = nutritionTask.Result.Sum(p => p.Nutrition.Nutrients.Calories ?? 0);

            double? caloriesBurnt = null;
            if (caloriesFromMetrics > 0)
            {
                caloriesBurnt = caloriesFromMetrics;
            }
            else
            {
                // Estimate calories from activity durations and weight when metrics are not provided.
                var weightLbs = weightTask.Result;
                double? weightKg = weightLbs.HasValue ? weightLbs.Value / 2.20462 : (double?)null;
                var totalMinutes = exercisesTask.Result.Sum(e => ParseDurationSeconds(e.Exercise.ActiveDuration) / 60);
                if (totalMinutes > 0 && weightKg.HasValue)
                {
                    double EstimateCaloriesForActivity(GoogleHealthDataPoint dp)
                    {
                        var name = !string.IsNullOrWhiteSpace(dp.Exercise.DisplayName) ? dp.Exercise.DisplayName : dp.Exercise.ExerciseType;
                        var mins = ParseDurationSeconds(dp.Exercise.ActiveDuration) / 60;
                        double met = 4.0; // default moderate intensity
                        var n = name.ToLowerInvariant();
                        if (n.Contains("run") || n.Contains("jog")) met = 9.8;
                        else if (n.Contains("walk")) met = 3.5;
                        else if (n.Contains("bike") || n.Contains("cycling")) met = 7.5;
                        else if (n.Contains("swim")) met = 8.0;
                        else if (n.Contains("strength") || n.Contains("workout") || n.Contains("weight") || dp.Exercise.ExerciseType == "STRENGTH_TRAINING") met = 6.0;
                        // calories = mins * (MET * 3.5 * weightKg) / 200
                        return mins * (met * 3.5 * weightKg.Value) / 200.0;
                    }

                    var estimated = exercisesTask.Result.Sum(dp => EstimateCaloriesForActivity(dp));
                    if (estimated > 0) caloriesBurnt = estimated;
                }

                // Fallback: approximate from steps (avg ~0.04 kcal/step)
                if (!caloriesBurnt.HasValue && steps > 0)
                {
                    caloriesBurnt = steps * 0.04;
                }
            }

            return new DailyFitnessSnapshot
            {
                SleepHours    = sleepHours,
                Steps         = steps,
                RestingHrBpm  = hrBpm,
                CaloriesBurnt = caloriesBurnt.HasValue && caloriesBurnt.Value > 0 ? caloriesBurnt : null,
                CaloriesEaten = caloriesEaten > 0 ? caloriesEaten : null,
                Activities    = activities,
                WeightLbs     = dayWeightTask.Result,
            };
        }

        public async Task<WeeklyFitnessSnapshot> GetWeeklySnapshot()
        {
            var exercisesTask  = Get7DayExercises();
            var stepsTask      = Get7DayStepCount();
            var sleepTask      = Get7DaySleep();
            var weightTask     = GetRecentWeight(7);
            var hrTask         = GetRestingHeartRate(7);
            var nutritionTask  = Get7DayNutrition();
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, weightTask, hrTask, nutritionTask);

            var sleepDurations = sleepTask.Result.Select(dp => dp.Sleep.GetDurationHours()).Where(h => h > 0).ToList();
            double? avgSleep = sleepDurations.Count > 0 ? sleepDurations.Average() : null;

            var hrValues = hrTask.Result.Select(h => h.DailyRestingHeartRate.BeatsPerMinute).Where(v => v > 0).ToList();
            double? avgHr = hrValues.Count > 0 ? hrValues.Average() : null;

            double? weightDelta = null;
            var ordered = weightTask.Result.OrderBy(w => w.Weight.SampleTime.PhysicalTime).ToList();
            if (ordered.Count >= 2)
            {
                var first = ordered[0].Weight.WeightKg.HasValue  ? ordered[0].Weight.WeightKg!.Value * 2.20462 : (double?)null;
                var last  = ordered[^1].Weight.WeightKg.HasValue ? ordered[^1].Weight.WeightKg!.Value * 2.20462 : (double?)null;
                if (first.HasValue && last.HasValue)
                    weightDelta = last.Value - first.Value;
            }

            int totalMins  = exercisesTask.Result.Sum(dp => ParseDurationSeconds(dp.Exercise.ActiveDuration) / 60);
            var activities = exercisesTask.Result
                .GroupBy(e => NormalizeActivityName(
                    !string.IsNullOrWhiteSpace(e.Exercise.DisplayName)
                        ? e.Exercise.DisplayName
                        : e.Exercise.ExerciseType))
                .Select(g => new FitnessActivityEntry
                {
                    Name    = g.Key,
                    Minutes = g.Sum(e => ParseDurationSeconds(e.Exercise.ActiveDuration) / 60),
                })
                .ToList();

            var caloriesByDay = nutritionTask.Result
                .GroupBy(p => DateTime.TryParse(p.Nutrition.Interval.StartTime, out var t) ? t.Date : DateTime.MinValue)
                .Where(g => g.Key != DateTime.MinValue)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Nutrition.Nutrients.Calories ?? 0));

            int calorieLoggedDays = caloriesByDay.Values.Count(c => c >= 1000);
            var totalCaloriesFromMetrics = exercisesTask.Result.Sum(e => e.Exercise.MetricsSummary.CaloriesKcal ?? 0);
            var totalCaloriesEaten = nutritionTask.Result.Sum(p => p.Nutrition.Nutrients.Calories ?? 0);

            double totalCaloriesBurnt = 0;
            if (totalCaloriesFromMetrics > 0)
            {
                totalCaloriesBurnt = totalCaloriesFromMetrics;
            }
            else
            {
                // Estimate using per-exercise durations and most recent weight in the window
                double? weightKg = null;
                var orderedWeights = weightTask.Result.OrderBy(w => w.Weight.SampleTime.PhysicalTime).ToList();
                if (orderedWeights.Count > 0)
                {
                    var last = orderedWeights[^1];
                    if (last.Weight.WeightKg.HasValue) weightKg = last.Weight.WeightKg.Value;
                }

                if (weightKg.HasValue)
                {
                    double EstimateCaloriesForActivity(GoogleHealthDataPoint dp)
                    {
                        var name = !string.IsNullOrWhiteSpace(dp.Exercise.DisplayName) ? dp.Exercise.DisplayName : dp.Exercise.ExerciseType;
                        var mins = ParseDurationSeconds(dp.Exercise.ActiveDuration) / 60;
                        double met = 4.0;
                        var n = name.ToLowerInvariant();
                        if (n.Contains("run") || n.Contains("jog")) met = 9.8;
                        else if (n.Contains("walk")) met = 3.5;
                        else if (n.Contains("bike") || n.Contains("cycling")) met = 7.5;
                        else if (n.Contains("swim")) met = 8.0;
                        else if (n.Contains("strength") || n.Contains("workout") || n.Contains("weight") || dp.Exercise.ExerciseType == "STRENGTH_TRAINING") met = 6.0;
                        return mins * (met * 3.5 * weightKg.Value) / 200.0;
                    }

                    totalCaloriesBurnt = exercisesTask.Result.Sum(dp => EstimateCaloriesForActivity(dp));
                }
                else
                {
                    // Fallback to steps-based estimate if weight isn't available
                    totalCaloriesBurnt = stepsTask.Result * 0.04;
                }
            }

            double? avgDailyDeficit = (totalCaloriesBurnt > 0 || totalCaloriesEaten > 0)
                ? (totalCaloriesBurnt - totalCaloriesEaten) / 7.0
                : null;

            return new WeeklyFitnessSnapshot
            {
                AvgSleepHours          = avgSleep,
                AvgStepsPerDay         = stepsTask.Result / 7,
                AvgRestingHrBpm        = avgHr,
                WeightDeltaLbs         = weightDelta,
                TotalActivityMinutes   = totalMins,
                AvgDailyCalorieDeficit = avgDailyDeficit,
                CalorieLoggedDays      = calorieLoggedDays,
                Activities             = activities,
            };
        }

        public async Task PostWeeklyFitnessStats()
        {
            Log.Information("GoogleHealthClient.PostWeeklyFitnessStats: START");
            var exercisesTask = Get7DayExercises();
            var stepsTask = Get7DayStepCount();
            var sleepTask = Get7DaySleep();
            var weightTask = GetRecentWeight(7);
            var hrTask = GetRestingHeartRate(7);
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, weightTask, hrTask);
            // Use the snapshot builder so we include estimated calories/deficit
            var snapshot = await GetWeeklySnapshot();
            var weeklyEmbed = BuildWeeklyEmbed(_settings.Username, snapshot, stepsTask.Result, weightTask.Result, hrTask.Result, _settings.HighestWeightLbs);
            await _discordClient.PostEmbed(_settings.ChannelId, weeklyEmbed);
            _fitnessRepository?.LogPost(_settings.Username, "weekly");
            Log.Information("GoogleHealthClient.PostWeeklyFitnessStats: END");
        }

        public async Task PostDailyFitnessStats()
        {
            Log.Information("GoogleHealthClient.PostDailyFitnessStats: START");
            var snapshot = await GetDailySnapshot();
            var dailyEmbed = BuildDailyEmbed(_settings.Username, snapshot, MaintenanceCalories);
            await _discordClient.PostEmbed(_settings.ChannelId, dailyEmbed);
            _fitnessRepository?.LogPost(_settings.Username, "daily");
            Log.Information("GoogleHealthClient.PostDailyFitnessStats: END");
        }

        private static Embed BuildDailyEmbed(
            string username,
            DailyFitnessSnapshot snapshot,
            double? maintenanceCalories)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"{username} — Daily Fitness — {DateTime.Today.AddDays(-1):MMM d, yyyy}")
                .WithColor(new Color(0x2ECC71))
                .WithTimestamp(DateTimeOffset.UtcNow);

            string sleepStr = "N/A";
            // snapshot doesn't include raw sleep list here; assume its SleepHours is representative
            if (snapshot.SleepHours.HasValue)
                sleepStr = $"{snapshot.SleepHours.Value:0.0} hrs";

            string stepsStr = snapshot.Steps > 0 ? $"{snapshot.Steps:N0}" : "N/A";

            string activityStr = snapshot.Activities.Count > 0 ? string.Join(", ", snapshot.Activities.Select(FormatActivity)) : "N/A";

            string hrStr = snapshot.RestingHrBpm.HasValue ? $"{snapshot.RestingHrBpm.Value} bpm" : "N/A";

            var active = snapshot.CaloriesBurnt ?? 0;
            string caloriesBurntStr = maintenanceCalories is { } maintenance
                ? $"{maintenance + active:0} kcal\n-# {maintenance:0} maintenance + {active:0} active"
                : snapshot.CaloriesBurnt.HasValue ? $"{snapshot.CaloriesBurnt.Value:0} kcal" : "N/A";
            string caloriesEatenStr = snapshot.CaloriesEaten.HasValue ? $"{snapshot.CaloriesEaten.Value:0} kcal" : "N/A";

            builder.AddField("😴 Sleep", sleepStr, true);
            builder.AddField("👟 Steps", stepsStr, true);
            builder.AddField("❤️ Average HR", hrStr, true);
            builder.AddField("🔥 Calories Burnt", caloriesBurntStr, true);
            builder.AddField("🍽️ Calories Eaten", caloriesEatenStr, true);
            builder.AddField("🏃 Activity", activityStr, false);

            return builder.Build();
        }

        private static Embed BuildWeeklyEmbed(
            string username,
            WeeklyFitnessSnapshot snapshot,
            long totalSteps,
            List<GoogleHealthWeightDataPoint> weightPoints,
            List<GoogleHealthRestingHrDataPoint> heartRate,
            double? highestWeightLbs = null)
        {
            var start = DateTime.Now.AddDays(-7);
            var end = DateTime.Now;

            var builder = new EmbedBuilder()
                .WithTitle($"{username} — Weekly Fitness Summary — {start:MMM d} – {end:MMM d, yyyy}")
                .WithColor(new Color(0x00B36B))
                .WithTimestamp(DateTimeOffset.UtcNow);

            int totalActivityMinutes = snapshot.Activities.Sum(a => a.Minutes);

            var avgSleepStr = snapshot.AvgSleepHours.HasValue ? $"{snapshot.AvgSleepHours:0.0} hrs/night" : "N/A";
            var avgSteps = totalSteps / 7;
            var avgStepsStr = avgSteps > 0 ? $"{avgSteps:N0}/day" : "N/A";

            string weekActivityStr = snapshot.Activities.Count > 0 ? $"{FormatMinutes(totalActivityMinutes)} — {string.Join(", ", snapshot.Activities.Select(a => a.Minutes > 0 ? $"{a.Name} ({FormatMinutes(a.Minutes)})" : a.Name))}" : "N/A";

            // Weight: replicate previous behavior
            string weightStr = "N/A";
            var ordered = weightPoints.OrderBy(w => w.Weight.SampleTime.PhysicalTime).ToList();
            if (ordered.Count >= 2)
            {
                var first = ordered[0].Weight.WeightKg.HasValue ? (double?)(ordered[0].Weight.WeightKg!.Value * 2.20462) : null;
                var last  = ordered[^1].Weight.WeightKg.HasValue ? (double?)(ordered[^1].Weight.WeightKg!.Value * 2.20462) : null;
                if (first.HasValue && last.HasValue)
                {
                    var delta = last.Value - first.Value;
                    weightStr = $"{(delta >= 0 ? "+" : "")}{delta:0.0} lbs";
                    if (highestWeightLbs.HasValue && last.Value > 0)
                    {
                        var lifetimeLoss = highestWeightLbs.Value - last.Value;
                        if (lifetimeLoss > 0)
                            weightStr += $" ({lifetimeLoss:0.0} lbs lost total)";
                    }
                }
            }

            string hrStr = snapshot.AvgRestingHrBpm.HasValue ? $"{snapshot.AvgRestingHrBpm:0} bpm avg" : "N/A";


            string avgDeficitStr = snapshot.AvgDailyCalorieDeficit.HasValue ? $"{snapshot.AvgDailyCalorieDeficit.Value:0} kcal/day" : "N/A";
            string calorieLoggedStr = snapshot.CalorieLoggedDays > 0 ? snapshot.CalorieLoggedDays.ToString() : "0";

            builder.AddField("😴 Avg Sleep", avgSleepStr, true);
            builder.AddField("👟 Avg Steps", avgStepsStr, true);
            builder.AddField("❤️ Average HR", hrStr, true);
            builder.AddField("⚖️ Weight", weightStr, true);
            builder.AddField("🔥 Avg Daily Deficit", avgDeficitStr, true);
            builder.AddField("📅 Calorie Logged Days", calorieLoggedStr, true);
            builder.AddField("🏃 Activity", weekActivityStr, false);

            return builder.Build();
        }

        private static Embed BuildWeeklyEmbed(
            string username,
            List<GoogleHealthDataPoint> exercises,
            long totalSteps,
            List<GoogleHealthSleepDataPoint> sleep,
            List<GoogleHealthWeightDataPoint> weightPoints,
            List<GoogleHealthRestingHrDataPoint> heartRate,
            double? highestWeightLbs = null)
        {
            var start = DateTime.Now.AddDays(-7);
            var end = DateTime.Now;

            var builder = new EmbedBuilder()
                .WithTitle($"{username} — Weekly Fitness Summary — {start:MMM d} – {end:MMM d, yyyy}")
                .WithColor(new Color(0x00B36B))
                .WithTimestamp(DateTimeOffset.UtcNow);

            int totalActivityMinutes = exercises.Sum(dp => ParseDurationSeconds(dp.Exercise.ActiveDuration) / 60);

            var sleepDurations = sleep.Select(dp => dp.Sleep.GetDurationHours()).Where(h => h > 0).ToList();
            double? avgSleepHours = sleepDurations.Count > 0 ? sleepDurations.Average() : null;

            // Activity: grouped by category with total duration per category
            string weekActivityStr = exercises.Count > 0
                ? $"{FormatMinutes(totalActivityMinutes)} — {FormatActivityBreakdown(exercises)}"
                : "N/A";

            // Weight: show only the delta (first vs last reading in the window)
            string weightStr = "N/A";
            var ordered = weightPoints.OrderBy(w => w.Weight.SampleTime.PhysicalTime).ToList();
            if (ordered.Count >= 2)
            {
                var first = ordered[0].Weight.WeightKg.HasValue ? (double?)(ordered[0].Weight.WeightKg!.Value * 2.20462) : null;
                var last  = ordered[^1].Weight.WeightKg.HasValue ? (double?)(ordered[^1].Weight.WeightKg!.Value * 2.20462) : null;
                if (first.HasValue && last.HasValue)
                {
                    var delta = last.Value - first.Value;
                    weightStr = $"{(delta >= 0 ? "+" : "")}{delta:0.0} lbs";
                    if (highestWeightLbs.HasValue && last.Value > 0)
                    {
                        var lifetimeLoss = highestWeightLbs.Value - last.Value;
                        if (lifetimeLoss > 0)
                            weightStr += $" ({lifetimeLoss:0.0} lbs lost total)";
                    }
                }
            }

            // Resting heart rate: average over the window
            string hrStr = "N/A";
            var hrValues = heartRate.Select(h => h.DailyRestingHeartRate.BeatsPerMinute).Where(v => v > 0).ToList();
            if (hrValues.Count > 0)
                hrStr = $"{hrValues.Average():0} bpm avg";

            var avgSteps = totalSteps / 7;

            var avgSleepStr = avgSleepHours.HasValue ? $"{avgSleepHours:0.0} hrs/night" : "N/A";
            var avgStepsStr = avgSteps > 0 ? $"{avgSteps:N0}/day" : "N/A";

            builder.AddField("😴 Avg Sleep", avgSleepStr, true);
            builder.AddField("👟 Avg Steps", avgStepsStr, true);
            builder.AddField("❤️ Average HR", hrStr, true);
            builder.AddField("⚖️ Weight", weightStr, true);
            builder.AddField("🏃 Activity", weekActivityStr, false);

            return builder.Build();
        }

        private static readonly HashSet<string> WorkoutNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Strength training", "Workout", "Weight training", "Resistance training"
        };

        /// <summary>"Push Day (52 min · avg 118 bpm)", leaving out whatever isn't known.</summary>
        internal static string FormatActivity(FitnessActivityEntry a)
        {
            var details = new List<string>();
            if (a.Minutes > 0) details.Add(FormatMinutes(a.Minutes));
            if (a.AvgHeartRateBpm is { } bpm) details.Add($"avg {bpm} bpm");
            return details.Count == 0 ? a.Name : $"{a.Name} ({string.Join(" · ", details)})";
        }

        private static string NormalizeActivityName(string name) =>
            WorkoutNames.Contains(name) ? "Workout" : name;

        private static string FormatActivityBreakdown(List<GoogleHealthDataPoint> exercises)
        {
            if (exercises.Count == 0) return "N/A";
            var grouped = exercises
                .GroupBy(e => NormalizeActivityName(
                    !string.IsNullOrWhiteSpace(e.Exercise.DisplayName)
                        ? e.Exercise.DisplayName
                        : e.Exercise.ExerciseType))
                .Select(g =>
                {
                    var totalMins = g.Sum(e => ParseDurationSeconds(e.Exercise.ActiveDuration) / 60);
                    return totalMins > 0 ? $"{g.Key} ({FormatMinutes(totalMins)})" : g.Key;
                });
            return string.Join(", ", grouped);
        }

        private static string FormatMinutes(int minutes)
        {
            if (minutes < 60) return $"{minutes} min";
            var h = minutes / 60;
            var m = minutes % 60;
            return m > 0 ? $"{h}h {m}m" : $"{h}h";
        }

        private static int ParseDurationSeconds(string duration)
        {
            // Format: "900s"
            if (string.IsNullOrEmpty(duration)) return 0;
            var trimmed = duration.TrimEnd('s');
            return int.TryParse(trimmed, out var secs) ? secs : 0;
        }
    }
}






