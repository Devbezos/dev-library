using DevClient.Data;
using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace DevClient.Clients
{
    public sealed class WoWUtilsApiException : HttpRequestException
    {
        public WoWUtilsApiException(string message, HttpStatusCode statusCode, string? apiMessage = null)
            : base(message, null, statusCode)
        {
            ApiMessage = apiMessage;
        }

        public string? ApiMessage { get; }
    }

    public class WoWUtilsClient : IWoWUtilsClient
    {
        private sealed class WoWUtilsTrackCharacterRequest
        {
            [JsonProperty("characterId")]
            public string CharacterId { get; set; } = string.Empty;
        }

        private readonly IHttpClientFactory _httpClientFactory;

        public WoWUtilsClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        private static readonly string[] _wowClasses =
        [
            "deathknight", "demonhunter", "druid", "evoker", "hunter",
            "mage", "monk", "paladin", "priest", "rogue", "shaman", "warlock", "warrior"
        ];

        public async Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId)
        {
            Log.Information("WoWUtilsClient.GetDroptimizerReport: START {ReportId}", reportId);
            using var client = BuildHttpClient();
            var json = await client.GetStringAsync(
                $"{Constants.WoW.WoWUtils.BaseUrl}/api/droptimizer/fetch?reportId={reportId}&file=report");
            var result = JsonConvert.DeserializeObject<WoWUtilsFetchResponse>(json);
            Log.Information("WoWUtilsClient.GetDroptimizerReport: END");
            return result!;
        }

        public Task<WoWUtilsImportResponse> ImportDroptimizer(
            string? groupId, string reportUrlOrId, string apiKey, string? profileKey = null) =>
            ImportDroptimizer(groupId, reportUrlOrId, apiKey, profileKey, allowRosterRecovery: true);

        public async Task<IReadOnlyList<WoWUtilsRosterMember>> GetRosterMembers(string groupId, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils groupId is required for roster sync");

            Log.Information("WoWUtilsClient.GetRosterMembers: START {GroupId}", groupId);
            using var client = BuildHttpClient(apiKey);
            using var response = await client.GetAsync($"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/roster/members");
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw CreateApiException(
                    $"WoW Utils roster fetch failed ({(int)response.StatusCode}): {responseJson}",
                    response.StatusCode,
                    responseJson);
            }

            var members = ParseRosterMembers(responseJson);
            Log.Information("WoWUtilsClient.GetRosterMembers: END {Count}", members.Count);
            return members;
        }

        public async Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string groupId, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils groupId is required for raid schedule");

            Log.Information("WoWUtilsClient.GetRaidSchedule: START {GroupId}", groupId);
            using var client = BuildHttpClient(apiKey);
            using var response = await client.GetAsync($"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/calendar-events");
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw CreateApiException(
                    $"WoW Utils raid schedule fetch failed ({(int)response.StatusCode}): {responseJson}",
                    response.StatusCode,
                    responseJson);
            }

            var raids = ParseRaidSchedule(responseJson);
            Log.Information("WoWUtilsClient.GetRaidSchedule: END {Count}", raids.Count);
            return raids;
        }

        private async Task<WoWUtilsImportResponse> ImportDroptimizer(
            string? groupId,
            string reportUrlOrId,
            string apiKey,
            string? profileKey,
            bool allowRosterRecovery)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils groupId is required for droptimizer imports");

            Log.Information("WoWUtilsClient.ImportDroptimizer: START {GroupId} {Report}", groupId, reportUrlOrId);

            var body = new
            {
                url = reportUrlOrId,
                profileKey
            };

            using var client = BuildHttpClient(apiKey);
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var url = $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/droptimizers";

            var response = await client.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            Log.Information("WoWUtilsClient.ImportDroptimizer: END status={Status}", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var apiException = CreateApiException(
                    $"WoW Utils import failed ({(int)response.StatusCode}): {responseJson}",
                    response.StatusCode,
                    responseJson);

                if (allowRosterRecovery && IsMissingWoWUtilsRosterError(apiException.ApiMessage ?? apiException.Message))
                {
                    Log.Information("WoWUtilsClient.ImportDroptimizer: missing roster character for {Report}; attempting roster auto-add", reportUrlOrId);
                    var trackedCharacterId = await TryTrackCharacterForImport(groupId, reportUrlOrId, apiKey);
                    if (!string.IsNullOrWhiteSpace(trackedCharacterId))
                    {
                        Log.Information("WoWUtilsClient.ImportDroptimizer: tracked {CharacterId} in group {GroupId}; retrying import", trackedCharacterId, groupId);
                        return await ImportDroptimizer(groupId, reportUrlOrId, apiKey, profileKey, allowRosterRecovery: false);
                    }
                }

                throw apiException;
            }

            return JsonConvert.DeserializeObject<WoWUtilsImportResponse>(responseJson)!;
        }

        public string GetCharacterSlug(WoWUtilsFetchResponse report)
        {
            var (name, realm, _, _) = ParseSimcCharacter(report.RawFormData?.Text);

            name ??= report.CharacterName;
            realm ??= ParseRealm(report.RawFormData?.Text);

            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Could not determine character name from droptimizer report");
            if (string.IsNullOrEmpty(realm))
                throw new InvalidOperationException("Could not determine realm from droptimizer report");

            return $"{name.ToLower()}-{realm.ToLower()}";
        }

        private async Task<string?> TryTrackCharacterForImport(string groupId, string reportUrlOrId, string apiKey)
        {
            var reportId = ExtractReportId(reportUrlOrId);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                Log.Warning("WoWUtilsClient.ImportDroptimizer: could not determine report id from {Report}", reportUrlOrId);
                return null;
            }

            WoWUtilsFetchResponse report;
            try
            {
                report = await GetDroptimizerReport(reportId);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Warning("WoWUtilsClient.ImportDroptimizer: report fetch returned 404 for {ReportId}; cannot auto-add roster member", reportId);
                return null;
            }

            string characterId;
            try
            {
                characterId = GetCharacterSlug(report);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WoWUtilsClient.ImportDroptimizer: could not determine character identity for report {ReportId}", reportId);
                return null;
            }

            await TrackWoWUtilsCharacter(groupId, apiKey, characterId);
            return characterId;
        }

        private async Task TrackWoWUtilsCharacter(string groupId, string apiKey, string characterId)
        {
            using var client = BuildHttpClient(apiKey);
            var body = new StringContent(
                JsonConvert.SerializeObject(new WoWUtilsTrackCharacterRequest { CharacterId = characterId }),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(
                $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/roster/members",
                body);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                throw new HttpRequestException(
                    $"WoW Utils character tracking hit {(int)response.StatusCode}: {responseBody}",
                    null,
                    response.StatusCode);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"WoW Utils character tracking failed for {characterId} ({(int)response.StatusCode}): {responseBody}");
        }

        private static IReadOnlyList<WoWUtilsRosterMember> ParseRosterMembers(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return [];

            var token = JToken.Parse(responseJson);
            var array = FindRosterArray(token);
            if (array == null)
                return [];

            return array
                .OfType<JToken>()
                .Select(ParseRosterMember)
                .Where(member => member != null)
                .Select(member => member!)
                .Where(member => !string.IsNullOrWhiteSpace(member.Name) && !string.IsNullOrWhiteSpace(member.Realm))
                .GroupBy(member => $"{member.Name}|{member.Realm}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<RaidScheduleEvent> ParseRaidSchedule(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return [];

            var token = JToken.Parse(responseJson);
            var array = FindRaidArray(token);
            if (array == null)
                return [];

            return array
                .OfType<JObject>()
                .Select(ParseRaidEvent)
                .Where(raid => raid != null)
                .Select(raid => raid!)
                .OrderBy(raid => raid.StartsAtUtc)
                .ToList();
        }

        private static JArray? FindRosterArray(JToken token)
        {
            if (token is JArray directArray && LooksLikeRosterArray(directArray))
                return directArray;

            foreach (var path in new[] { "data", "members", "roster.members", "roster", "items" })
            {
                if (token.SelectToken(path) is JArray namedArray && LooksLikeRosterArray(namedArray, allowEmpty: true))
                    return namedArray;
            }

            foreach (var property in token is JContainer container ? container.DescendantsAndSelf().OfType<JProperty>() : Enumerable.Empty<JProperty>())
            {
                if (property.Value is JArray array && LooksLikeRosterArray(array))
                    return array;
            }

            return null;
        }
        private static JArray? FindRaidArray(JToken token)
        {
            if (token is JArray directArray)
                return directArray;

            foreach (var path in new[] { "data", "events", "calendarEvents", "items", "results" })
            {
                if (token.SelectToken(path) is JArray namedArray)
                    return namedArray;
            }

            foreach (var property in token is JContainer container ? container.DescendantsAndSelf().OfType<JProperty>() : Enumerable.Empty<JProperty>())
            {
                if (property.Value is JArray array)
                    return array;
            }

            return null;
        }

        private static RaidScheduleEvent? ParseRaidEvent(JObject item)
        {
            var date = FirstString(item, "date", "startDate");
            var startTime = FirstString(item, "startTime", "time", "start");
            if (!TryParseRaidStartUtc(date, startTime, out var startsAtUtc))
                return null;

            return new RaidScheduleEvent
            {
                Provider = "WoWUtils",
                ExternalId = FirstString(item, "eventId", "id", "scheduleId") ?? Guid.NewGuid().ToString("N"),
                Name = FirstString(item, "name", "title") ?? "Raid",
                StartsAtUtc = startsAtUtc,
                Difficulty = FirstString(item, "difficulty"),
                Status = FirstString(item, "status")
            };
        }

        private static bool TryParseRaidStartUtc(string? date, string? startTime, out DateTime startsAtUtc)
        {
            if (string.IsNullOrWhiteSpace(date))
            {
                startsAtUtc = default;
                return false;
            }

            var combined = string.IsNullOrWhiteSpace(startTime) ? date : $"{date} {startTime}";
            if (!DateTime.TryParse(combined, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localStart))
            {
                startsAtUtc = default;
                return false;
            }

            startsAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            return true;
        }

        private static bool LooksLikeRosterArray(JArray array, bool allowEmpty = false)
        {
            if (array.Count == 0)
                return allowEmpty;

            return array.OfType<JObject>().Any(item =>
                HasAnyValue(item, "characterId", "character_id", "slug", "name", "character.name", "realm", "character.realm"));
        }

        private static WoWUtilsRosterMember? ParseRosterMember(JToken token)
        {
            if (token is not JObject item)
                return null;

            var characterId = FirstString(item,
                "characterId",
                "character_id",
                "slug",
                "character.slug",
                "character.characterId",
                "character.id");

            var name = FirstString(item,
                "name",
                "characterName",
                "character.name",
                "character.characterName");

            var realm = FirstString(item,
                "realm",
                "realmSlug",
                "characterRealm",
                "character.realm",
                "character.realmSlug");

            if ((string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(realm)) && !string.IsNullOrWhiteSpace(characterId))
            {
                var parsed = TryParseCharacterSlug(characterId);
                name ??= parsed.Name;
                realm ??= parsed.Realm;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(realm))
                return null;

            return new WoWUtilsRosterMember
            {
                CharacterId = characterId ?? BuildCharacterSlug(name, realm),
                Name = name,
                Realm = realm,
                Class = FirstString(item, "class", "characterClass", "character.class"),
                Spec = FirstString(item, "spec", "characterSpec", "activeSpec", "character.spec"),
                Role = FirstString(item, "role", "characterRole", "character.role"),
                Rank = FirstString(item, "rank", "groupRank", "character.rank")
            };
        }

        private static bool HasAnyValue(JObject item, params string[] paths) =>
            paths.Any(path => !string.IsNullOrWhiteSpace(FirstString(item, path)));

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

        private static (string? Name, string? Realm) TryParseCharacterSlug(string? characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return (null, null);

            var separatorIndex = characterId.IndexOf('-');
            if (separatorIndex <= 0 || separatorIndex >= characterId.Length - 1)
                return (null, null);

            return (
                characterId[..separatorIndex].Trim(),
                characterId[(separatorIndex + 1)..].Trim());
        }

        private static string BuildCharacterSlug(string name, string realm) =>
            $"{name.Trim().ToLowerInvariant()}-{realm.Trim().ToLowerInvariant()}";

        private static bool IsMissingWoWUtilsRosterError(string? errorMessage) =>
            !string.IsNullOrWhiteSpace(errorMessage)
            && (errorMessage.Contains("not on this group's roster", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("not on this group's roster", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("add the character to the roster first", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("nowhere to land", StringComparison.OrdinalIgnoreCase));

        private static string? ExtractReportId(string reportUrlOrId)
        {
            if (string.IsNullOrWhiteSpace(reportUrlOrId))
                return null;

            var trimmed = reportUrlOrId.Trim().TrimEnd('/');
            if (!trimmed.Contains('/'))
                return trimmed;

            return trimmed.Split('/').LastOrDefault();
        }

        private static (string? name, string? realm, string? characterClass, string? spec) ParseSimcCharacter(string? simcText)
        {
            if (string.IsNullOrEmpty(simcText))
                return (null, null, null, null);

            string? name = null, realm = null, characterClass = null, spec = null;

            foreach (var line in simcText.Split('\n'))
            {
                var trimmed = line.Trim();

                if (characterClass == null)
                {
                    foreach (var cls in _wowClasses)
                    {
                        if (trimmed.StartsWith($"{cls}=\"", StringComparison.OrdinalIgnoreCase))
                        {
                            characterClass = cls;
                            var m = Regex.Match(trimmed, "\"([^\"]+)\"");
                            if (m.Success) name = m.Groups[1].Value;
                            break;
                        }
                    }
                }

                if (trimmed.StartsWith("server=", StringComparison.OrdinalIgnoreCase))
                    realm = trimmed[7..].Trim();

                if (trimmed.StartsWith("spec=", StringComparison.OrdinalIgnoreCase))
                    spec = trimmed[5..].Trim();
            }

            return (name, realm, characterClass, spec);
        }

        private static string? ParseRealm(string? simcText)
        {
            if (string.IsNullOrEmpty(simcText)) return null;
            var m = Regex.Match(simcText, @"^server=(\S+)", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        private HttpClient BuildHttpClient(string? apiKey = null)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);
            }
            return client;
        }

        private static WoWUtilsApiException CreateApiException(string fallbackMessage, HttpStatusCode statusCode, string responseJson)
        {
            var apiMessage = TryGetApiMessage(responseJson);
            return new WoWUtilsApiException(apiMessage ?? fallbackMessage, statusCode, apiMessage);
        }

        private static string? TryGetApiMessage(string responseJson)
        {
            try
            {
                return JsonConvert.DeserializeObject<WoWUtilsErrorResponse>(responseJson)?.Error?.Message;
            }
            catch
            {
                return null;
            }
        }
    }
}


