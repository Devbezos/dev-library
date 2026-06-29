using DevClient.Data;
using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWAudit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace DevClient.Clients
{
    public class WoWAuditClient : IWoWAuditClient
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public WoWAuditClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        public async Task<List<WoWAuditCharacter>> GetCharacters(string guild)
        {
            Log.Information("WoWAuditClient.GetCharacters: START {Guild}", guild);
            try
            {
                using var client = CreateAuthorizedClient(guild);
                using var httpResponse = await client.GetAsync($"{Constants.WoW.WoWAudit.Url}/characters");
                var response = await httpResponse.Content.ReadAsStringAsync();
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

        public async Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string guild)
        {
            Log.Information("WoWAuditClient.GetRaidSchedule: START {Guild}", guild);
            using var client = CreateAuthorizedClient(guild);
            using var httpResponse = await client.GetAsync($"{Constants.WoW.WoWAudit.Url}/raids");
            var response = await httpResponse.Content.ReadAsStringAsync();
            httpResponse.EnsureSuccessStatusCode();

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
                using var client = CreateAuthorizedClient(guild);
                var requestBody = CreateJsonContent(new WoWAuditWishlistRequest(reportId));
                using var httpResponse = await client.PostAsync($"{Constants.WoW.WoWAudit.Url}/wishlists", requestBody);
                response = await httpResponse.Content.ReadAsStringAsync();
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

        public async Task<WoWAuditCharacter> TrackCharacter(string guild, WoWAuditTrackCharacterRequest request)
        {
            Log.Information("WoWAuditClient.TrackCharacter: START {Guild} {Character}", guild, request.Character.Name);
            using var client = CreateAuthorizedClient(guild);
            using var response = await client.PostAsync(
                $"{Constants.WoW.WoWAudit.Url}/characters",
                CreateJsonContent(request));
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<WoWAuditCharacter>(responseBody) ?? new WoWAuditCharacter();
        }

        public async Task UpdateCharacter(string guild, int characterId, WoWAuditUpdateCharacterRequest request)
        {
            Log.Information("WoWAuditClient.UpdateCharacter: START {Guild} {CharacterId}", guild, characterId);
            using var client = CreateAuthorizedClient(guild);
            using var message = new HttpRequestMessage(HttpMethod.Put, $"{Constants.WoW.WoWAudit.Url}/characters/{characterId}")
            {
                Content = CreateJsonContent(request)
            };
            using var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();
        }

        public async Task UntrackCharacter(string guild, int characterId)
        {
            Log.Information("WoWAuditClient.UntrackCharacter: START {Guild} {CharacterId}", guild, characterId);
            using var client = CreateAuthorizedClient(guild);
            using var response = await client.DeleteAsync($"{Constants.WoW.WoWAudit.Url}/characters/{characterId}");
            response.EnsureSuccessStatusCode();
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
            foreach (var path in new[] { "startsAt", "startAt", "start", "scheduledFor", "scheduledAt", "dateTime", "startDateTime" })
            {
                var value = FirstString(raid, path);
                if (TryParseDateTimeValue(value, out var startsAtUtc))
                    return startsAtUtc;
            }

            var date = FirstString(raid, "date", "startDate");
            var time = FirstString(raid, "startTime", "time");
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

        private HttpClient CreateAuthorizedClient(string guild)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(GetGuildToken(guild));
            return client;
        }

        private static AuthenticationHeaderValue CreateAuthorizationHeader(string token)
        {
            var trimmed = token.Trim();
            if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticationHeaderValue.Parse(trimmed);

            return new AuthenticationHeaderValue("Bearer", trimmed);
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

        private static StringContent CreateJsonContent(object payload) =>
            new(JsonConvert.SerializeObject(payload, SerializerSettings), Encoding.UTF8, "application/json");
    }
}
