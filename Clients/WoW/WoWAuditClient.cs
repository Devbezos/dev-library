using DevClient.Data;
using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWAudit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Serilog;
using System.Globalization;

namespace DevClient.Clients
{
    public class WoWAuditClient : IWoWAuditClient
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly RestClient _http;

        private readonly ApiResponseCache _cache;

        /// <summary>
        /// The raid schedule is cached (<see cref="ApiResponseCache"/>) per guild token; the character list isn't, since it's
        /// read straight after this client tracks and untracks characters.
        /// </summary>
        public WoWAuditClient(RestClient? http = null, ApiResponseCache? cache = null)
        {
            _http = http ?? SharedHttp.Client;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        public Task<List<WoWAuditCharacter>> GetCharacters(string guild) =>
            GetCharacters(guild, GetGuildToken(guild));

        public async Task<List<WoWAuditCharacter>> GetCharacters(string guild, string token)
        {
            Log.Information("WoWAuditClient.GetCharacters: START {Guild}", guild);
            try
            {
                var httpResponse = await _http.SendAsync(CreateAuthorizedRequest(token, Method.Get, $"{Constants.WoW.WoWAudit.Url}/characters"));
                var response = httpResponse.Content ?? string.Empty;
                httpResponse.EnsureSuccessStatusCode();

                var guildies = JsonConvert.DeserializeObject<List<WoWAuditCharacter>>(response) ?? [];

                Log.Information("WoWAuditClient.GetCharacters: found {Count}", guildies.Count);
                Log.Information("WoWAuditClient.GetCharacters: END");
                return guildies;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WoWAuditClient.GetCharacters: failed");
                throw;
            }
        }

        public Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string guild) =>
            GetRaidSchedule(guild, GetGuildToken(guild));

        public async Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string guild, string token)
        {
            Log.Information("WoWAuditClient.GetRaidSchedule: START {Guild}", guild);
            var response = await _cache.GetOrFetch($"wowaudit:raids:{ApiResponseCache.Fingerprint(token)}", WoWUtilsClient.ScheduleTtl, async () =>
            {
                var httpResponse = await _http.SendAsync(CreateAuthorizedRequest(token, Method.Get, $"{Constants.WoW.WoWAudit.Url}/raids"));
                var content = httpResponse.Content ?? string.Empty;
                httpResponse.EnsureSuccessStatusCode();
                return content;
            });

            var raids = ParseRaidSchedule(response);
            Log.Information("WoWAuditClient.GetRaidSchedule: END {Count}", raids.Count);
            return raids;
        }

        public async Task<WoWAuditWishlistResponse> UpdateWishlist(string reportId, string guild)
        {
            var response = string.Empty;

            Log.Information("WoWAuditClient.UpdateWishlist: START");
            try
            {
                var request = CreateAuthorizedRequest(GetGuildToken(guild), Method.Post, $"{Constants.WoW.WoWAudit.Url}/wishlists",
                    new WoWAuditWishlistRequest(reportId));
                var httpResponse = await _http.SendAsync(request);
                response = httpResponse.Content ?? string.Empty;
                httpResponse.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<WoWAuditWishlistResponse>(response)!;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WoWAuditClient.UpdateWishlist: failed. Body: {Body}", response);
                throw;
            }
            finally
            {
                Log.Information("WoWAuditClient.UpdateWishlist: END");
            }
        }

        public Task<WoWAuditCharacter> TrackCharacter(string guild, WoWAuditTrackCharacterRequest request) =>
            TrackCharacter(guild, GetGuildToken(guild), request);

        public async Task<WoWAuditCharacter> TrackCharacter(string guild, string token, WoWAuditTrackCharacterRequest request)
        {
            Log.Information("WoWAuditClient.TrackCharacter: START {Guild} {Character}", guild, request.Character.Name);
            var response = await _http.SendAsync(
                CreateAuthorizedRequest(token, Method.Post, $"{Constants.WoW.WoWAudit.Url}/characters", request));
            var responseBody = response.Content ?? string.Empty;

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"WoW Audit character tracking failed ({(int)response.StatusCode}): {responseBody}",
                    null,
                    response.StatusCode);

            return JsonConvert.DeserializeObject<WoWAuditCharacter>(responseBody) ?? new WoWAuditCharacter();
        }

        public async Task UpdateCharacter(string guild, int characterId, WoWAuditUpdateCharacterRequest request)
        {
            Log.Information("WoWAuditClient.UpdateCharacter: START {Guild} {CharacterId}", guild, characterId);
            var response = await _http.SendAsync(
                CreateAuthorizedRequest(GetGuildToken(guild), Method.Put, $"{Constants.WoW.WoWAudit.Url}/characters/{characterId}", request));
            response.EnsureSuccessStatusCode();
        }

        public Task UntrackCharacter(string guild, int characterId) =>
            UntrackCharacter(guild, GetGuildToken(guild), characterId);

        public async Task UntrackCharacter(string guild, string token, int characterId)
        {
            Log.Information("WoWAuditClient.UntrackCharacter: START {Guild} {CharacterId}", guild, characterId);
            var response = await _http.SendAsync(
                CreateAuthorizedRequest(token, Method.Delete, $"{Constants.WoW.WoWAudit.Url}/characters/{characterId}"));

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = response.Content ?? string.Empty;
                throw new HttpRequestException(
                    $"WoW Audit character untracking failed ({(int)response.StatusCode}): {responseBody}",
                    null,
                    response.StatusCode);
            }
        }

        private static IReadOnlyList<RaidScheduleEvent> ParseRaidSchedule(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return [];

            var token = JToken.Parse(response);
            var array = FindRaidArray(token);
            if (array == null)
                return [];

            return array
                .OfType<JObject>()
                .Select(ParseRaid)
                .Where(raid => raid != null)
                .Select(raid => raid!)
                .OrderBy(raid => raid.StartsAtUtc)
                .ToList();
        }

        private static JArray? FindRaidArray(JToken token)
        {
            if (token is JArray directArray)
                return directArray;

            foreach (var path in new[] { "data", "raids", "items", "results" })
            {
                if (token.SelectToken(path) is JArray namedArray)
                    return namedArray;
            }

            return (token as JContainer)?.DescendantsAndSelf()
                .OfType<JProperty>()
                .Select(property => property.Value)
                .OfType<JArray>()
                .FirstOrDefault();
        }

        private static RaidScheduleEvent? ParseRaid(JObject raid)
        {
            var startsAtUtc = ParseStartsAtUtc(raid);
            if (!startsAtUtc.HasValue)
                return null;

            return new RaidScheduleEvent
            {
                Provider = "WoWAudit",
                ExternalId = FirstString(raid, "id", "raidId", "eventId") ?? Guid.NewGuid().ToString("N"),
                Name = FirstString(raid, "name", "title", "instance.name") ?? "Raid",
                StartsAtUtc = startsAtUtc.Value,
                Difficulty = FirstString(raid, "difficulty", "raidDifficulty", "instance.difficulty"),
                Status = FirstString(raid, "status", "state")
            };
        }

        private static DateTime? ParseStartsAtUtc(JObject raid)
        {
            foreach (var path in new[]
            {
                "startsAt", "starts_at",
                "startAt", "start_at",
                "start",
                "scheduledFor", "scheduled_for",
                "scheduledAt", "scheduled_at",
                "dateTime", "date_time",
                "startDateTime", "start_date_time",
                "beginAt", "begin_at",
                "beginsAt", "begins_at"
            })
            {
                var value = FirstString(raid, path);
                if (TryParseDateTimeValue(value, out var startsAtUtc))
                    return startsAtUtc;
            }

            var date = FirstString(raid, "date", "startDate", "start_date");
            var time = FirstString(
                raid,
                "startTime", "start_time",
                "time",
                "scheduledTime", "scheduled_time",
                "beginTime", "begin_time");

            if (string.IsNullOrWhiteSpace(time))
                time = FindTimeLikeValue(raid);

            return TryParseDateAndTime(date, time, out var combinedUtc)
                ? combinedUtc
                : null;
        }

        private static bool TryParseDateTimeValue(string? value, out DateTime utc)
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                utc = dto.UtcDateTime;
                return true;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                utc = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                utc = TimeZoneInfo.ConvertTimeToUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
                return true;
            }

            utc = default;
            return false;
        }

        private static bool TryParseDateAndTime(string? date, string? time, out DateTime utc)
        {
            if (string.IsNullOrWhiteSpace(date))
            {
                utc = default;
                return false;
            }

            var combined = string.IsNullOrWhiteSpace(time) ? date : $"{date} {time}";
            if (!DateTime.TryParse(combined, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                utc = default;
                return false;
            }

            utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            return true;
        }

        private static string? FirstString(JToken token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var value = token.SelectToken(path)?.Value<string>()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private static string? FindTimeLikeValue(JToken token)
        {
            return (token as JContainer)?.DescendantsAndSelf()
                .OfType<JProperty>()
                .Where(property => property.Value.Type == JTokenType.String)
                .Select(property => new
                {
                    Name = property.Name,
                    Value = property.Value.Value<string>()?.Trim()
                })
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Value) &&
                    item.Name.Contains("time", StringComparison.OrdinalIgnoreCase) &&
                    DateTime.TryParseExact(
                        item.Value,
                        new[] { "H:mm", "HH:mm", "h:mm tt", "hh:mm tt" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                .Select(item => item.Value)
                .FirstOrDefault();
        }

        private static RestRequest CreateAuthorizedRequest(string token, Method method, string url, object? payload = null)
        {
            var trimmed = token.Trim();
            var request = new RestRequest(url, method)
                .AddHeader("Accept", "application/json")
                .AddHeader("Authorization", trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? trimmed : $"Bearer {trimmed}");
            if (payload != null)
                request.AddStringBody(JsonConvert.SerializeObject(payload, SerializerSettings), ContentType.Json);
            return request;
        }

        private static string GetGuildToken(string guild)
        {
            var token = AppSettings.Guilds
                .FirstOrDefault(g => string.Equals(g.Name, guild, StringComparison.OrdinalIgnoreCase))
                ?.Droptimizer?.Token;

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException($"WoWAudit token is not configured for guild '{guild}'");

            return token;
        }
    }
}

