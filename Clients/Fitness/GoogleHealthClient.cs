using dev_library.Data;
using dev_library.Data.Fitness;
using dev_refined.Clients;
using Discord;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http.Headers;
using ICustomDiscordClient = dev_refined.Clients.IDiscordClient;

namespace dev_library.Clients.Fitness
{
    public class GoogleHealthClient
    {
        private readonly ICustomDiscordClient _discordClient;
        private readonly GoogleHealthUserSettings _settings;
        private readonly IFitnessRepository? _fitnessRepository;

        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public GoogleHealthClient(GoogleHealthUserSettings settings) : this(settings, new DiscordClient(), null) { }
        public GoogleHealthClient(GoogleHealthUserSettings settings, IFitnessRepository fitnessRepository) : this(settings, new DiscordClient(), fitnessRepository) { }

        public GoogleHealthClient(GoogleHealthUserSettings settings, ICustomDiscordClient discordClient, IFitnessRepository? fitnessRepository = null)
        {
            _settings = settings;
            _discordClient = discordClient;
            _fitnessRepository = fitnessRepository;
        }

        private async Task<string> GetAccessToken()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return _accessToken;

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

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return _accessToken;
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

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("GoogleHealthClient fetch {DataType} failed: {Json}", dataType, json);
                return null;
            }

            return json;
        }

        private async Task<T?> FetchDataType<T>(string dataType, string? filter = null, string? pageToken = null) where T : class
        {
            var json = await FetchRaw(dataType, filter, pageToken);
            if (json == null) return null;
            Log.Debug("GoogleHealthClient [{DataType}] raw response: {Json}", dataType, json);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<List<GoogleHealthDataPoint>> Get24HourExercises()
        {
            var since = DateTime.Today.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss");
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{since}\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        private async Task<long> FetchAllSteps(string filter)
        {
            long total = 0;
            string? nextPage = null;
            do
            {
                var result = await FetchDataType<GoogleHealthStepsResponse>("steps", filter, nextPage);
                if (result == null) break;
                if (result.DataPoints.Count > 0)
                    total += result.DataPoints.Sum(dp => dp.Steps.GetCount());
                nextPage = result.NextPageToken;
            } while (!string.IsNullOrEmpty(nextPage));
            return total;
        }

        public async Task<long> Get24HourStepCount()
        {
            var since = DateTime.Today.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss");
            return await FetchAllSteps($"steps.interval.civil_start_time >= \"{since}\"");
        }

        public async Task<List<GoogleHealthSleepDataPoint>> Get24HourSleep()
        {
            var cutoff = DateTime.Today.AddDays(-1);
            var result = await FetchDataType<GoogleHealthSleepResponse>("sleep");
            if (result == null) return new List<GoogleHealthSleepDataPoint>();
            var filtered = result.DataPoints
                .Where(dp =>
                    !dp.Sleep.Metadata.Nap &&
                    DateTime.TryParse(dp.Sleep.Interval.StartTime, out var t) && t >= cutoff)
                .ToList();
            return filtered;
        }

        public async Task<List<GoogleHealthDataPoint>> Get7DayExercises()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ss");
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{sevenDaysAgo}\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        public async Task<long> Get7DayStepCount()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ss");
            return await FetchAllSteps($"steps.interval.civil_start_time >= \"{sevenDaysAgo}\"");
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
            var since = GetStartOfWeek().ToString("yyyy-MM-ddTHH:mm:ss");
            var result = await FetchDataType<GoogleHealthDataPointsResponse>("exercise",
                $"exercise.interval.civil_start_time >= \"{since}\"");
            return result?.DataPoints ?? new List<GoogleHealthDataPoint>();
        }

        public async Task<long> GetWeekSoFarStepCount()
        {
            var since = GetStartOfWeek().ToString("yyyy-MM-ddTHH:mm:ss");
            return await FetchAllSteps($"steps.interval.civil_start_time >= \"{since}\"");
        }

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

        public async Task<DailyFitnessSnapshot> GetDailySnapshot()
        {
            var exercisesTask = Get24HourExercises();
            var stepsTask     = Get24HourStepCount();
            var sleepTask     = Get24HourSleep();
            var hrTask        = GetRestingHeartRate(1);
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, hrTask);

            var mainSleep  = sleepTask.Result.OrderByDescending(s => s.Sleep.Summary.MinutesAsleep).FirstOrDefault();
            var sleepHours = mainSleep?.Sleep.GetDurationHours() is double h && h > 0 ? h : (double?)null;

            var latestHr = hrTask.Result.LastOrDefault();
            var hrBpm    = latestHr is not null && latestHr.DailyRestingHeartRate.BeatsPerMinute > 0
                ? latestHr.DailyRestingHeartRate.BeatsPerMinute : (int?)null;

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

            return new DailyFitnessSnapshot
            {
                SleepHours   = sleepHours,
                Steps        = stepsTask.Result,
                RestingHrBpm = hrBpm,
                Activities   = activities,
            };
        }

        public async Task<WeeklyFitnessSnapshot> GetWeeklySnapshot()
        {
            var exercisesTask = Get7DayExercises();
            var stepsTask     = Get7DayStepCount();
            var sleepTask     = Get7DaySleep();
            var weightTask    = GetRecentWeight(7);
            var hrTask        = GetRestingHeartRate(7);
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, weightTask, hrTask);

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

            return new WeeklyFitnessSnapshot
            {
                AvgSleepHours        = avgSleep,
                AvgStepsPerDay       = stepsTask.Result / 7,
                AvgRestingHrBpm      = avgHr,
                WeightDeltaLbs       = weightDelta,
                TotalActivityMinutes = totalMins,
                Activities           = activities,
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
            var weeklyEmbed = BuildWeeklyEmbed(_settings.Username, exercisesTask.Result, stepsTask.Result, sleepTask.Result, weightTask.Result, hrTask.Result, _settings.HighestWeightLbs);
            await _discordClient.PostEmbed(_settings.ChannelId, weeklyEmbed);
            _fitnessRepository?.LogPost(_settings.Username, "weekly");
            Log.Information("GoogleHealthClient.PostWeeklyFitnessStats: END");
        }

        public async Task PostDailyFitnessStats()
        {
            Log.Information("GoogleHealthClient.PostDailyFitnessStats: START");

            var exercisesTask = Get24HourExercises();
            var stepsTask = Get24HourStepCount();
            var sleepTask = Get24HourSleep();
            var hrTask = GetRestingHeartRate(1);
            await Task.WhenAll(exercisesTask, stepsTask, sleepTask, hrTask);

            var dailyEmbed = BuildDailyEmbed(_settings.Username, exercisesTask.Result, stepsTask.Result, sleepTask.Result, hrTask.Result);
            await _discordClient.PostEmbed(_settings.ChannelId, dailyEmbed);
            _fitnessRepository?.LogPost(_settings.Username, "daily");
            Log.Information("GoogleHealthClient.PostDailyFitnessStats: END");
        }

        private static Embed BuildDailyEmbed(
            string username,
            List<GoogleHealthDataPoint> exercises,
            long steps,
            List<GoogleHealthSleepDataPoint> sleep,
            List<GoogleHealthRestingHrDataPoint> heartRate)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"{username} — Daily Fitness — {DateTime.Today.AddDays(-1):MMM d, yyyy}")
                .WithColor(new Color(0x2ECC71))
                .WithTimestamp(DateTimeOffset.UtcNow);

            // Sleep: pick the session with the most sleep minutes
            string sleepStr = "N/A";
            var mainSleep = sleep
                .OrderByDescending(s => s.Sleep.Summary.MinutesAsleep)
                .FirstOrDefault();
            if (mainSleep != null)
            {
                var hours = mainSleep.Sleep.GetDurationHours();
                if (hours > 0) sleepStr = $"{hours:0.0} hrs";
            }

            // Steps
            string stepsStr = steps > 0 ? $"{steps:N0}" : "N/A";

            // Activity: grouped by category with total duration per category
            string activityStr = FormatActivityBreakdown(exercises);

            // Resting heart rate: most recent data point
            string hrStr = "N/A";
            var latestHr = heartRate.LastOrDefault();
            if (latestHr != null && latestHr.DailyRestingHeartRate.BeatsPerMinute > 0)
                hrStr = $"{latestHr.DailyRestingHeartRate.BeatsPerMinute} bpm";

            builder.AddField("😴 Sleep", sleepStr, true);
            builder.AddField("👟 Steps", stepsStr, true);
            builder.AddField("❤️ Resting HR", hrStr, true);
            builder.AddField("🏃 Activity", activityStr, false);

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
            builder.AddField("❤️ Resting HR", hrStr, true);
            builder.AddField("⚖️ Weight", weightStr, true);
            builder.AddField("🏃 Activity", weekActivityStr, false);

            return builder.Build();
        }

        private static readonly HashSet<string> WorkoutNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Strength training", "Workout", "Weight training", "Resistance training"
        };

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
