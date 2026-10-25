using DevClient.Data;
using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Globalization;
using RestSharp;
using System.Net;
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

        /// <summary>A Droptimizer report never changes once it's done.</summary>
        public static readonly TimeSpan ReportTtl = TimeSpan.FromDays(1);
        public static readonly TimeSpan ScheduleTtl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan RosterTtl = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan AbsenceTtl = TimeSpan.FromMinutes(5);

        private readonly RestClient _http;
        private readonly ApiResponseCache _cache;

        /// <summary>Reads are cached (<see cref="ApiResponseCache"/>) per group and API key; imports and tracking never are.</summary>
        public WoWUtilsClient(RestClient? http = null, ApiResponseCache? cache = null)
        {
            _http = http ?? SharedHttp.Client;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        // A group's data is private to its API key, so the key's fingerprint is part of the cache key.
        private Task<string> CachedGet(string kind, string url, string apiKey, TimeSpan ttl, string failure) =>
            _cache.GetOrFetch(CacheKey(kind, url, apiKey), ttl, async () =>
            {
                var response = await _http.SendAsync(BuildRequest(url, apiKey));
                var responseJson = response.Content ?? string.Empty;
                if (!response.IsSuccessStatusCode)
                    throw CreateApiException($"{failure} ({(int)response.StatusCode}): {responseJson}", response.StatusCode, responseJson);
                return responseJson;
            });

        private static readonly string[] _wowClasses =
        [
            "deathknight", "demonhunter", "druid", "evoker", "hunter",
            "mage", "monk", "paladin", "priest", "rogue", "shaman", "warlock", "warrior"
        ];

        public async Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId)
        {
            Log.Information("WoWUtilsClient.GetDroptimizerReport: START {ReportId}", reportId);
            var url = $"{Constants.WoW.WoWUtils.BaseUrl}/api/droptimizer/fetch?reportId={reportId}&file=report";
            var body = await _cache.GetOrFetch("wowutils:report:" + url, ReportTtl, async () =>
                (await _http.SendAsync(BuildRequest(url))).EnsureSuccessStatusCode().Content ?? string.Empty);
            var result = JsonConvert.DeserializeObject<WoWUtilsFetchResponse>(body);
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
            var responseJson = await CachedGet("roster", RosterUrl(groupId), apiKey,
                RosterTtl, "WoW Utils roster fetch failed");

            var members = ParseRosterMembers(responseJson);
            Log.Information("WoWUtilsClient.GetRosterMembers: END {Count}", members.Count);
            return members;
        }

        public async Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string groupId, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils groupId is required for raid schedule");

            Log.Information("WoWUtilsClient.GetRaidSchedule: START {GroupId}", groupId);
            var responseJson = await CachedGet("schedule", $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/calendar-events", apiKey,
                ScheduleTtl, "WoW Utils raid schedule fetch failed");

            var raids = ParseRaidSchedule(responseJson);
            Log.Information("WoWUtilsClient.GetRaidSchedule: END {Count}", raids.Count);
            return raids;
        }

        /// <summary>
        /// The group's declared absences. UNVERIFIED: only the web-app path (/api/groups/{id}/absences) is confirmed to exist;
        /// the key-authenticated /v1 path may 404, which callers must handle (<see cref="WoWUtilsApiException"/>).
        /// </summary>
        public async Task<IReadOnlyList<WoWUtilsAbsence>> GetAbsences(string groupId, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils groupId is required for absences");

            Log.Information("WoWUtilsClient.GetAbsences: START {GroupId}", groupId);
            var responseJson = await CachedGet("absences", $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/absences", apiKey,
                AbsenceTtl, "WoW Utils absences fetch failed");

            var absences = ParseAbsences(responseJson);
            Log.Information("WoWUtilsClient.GetAbsences: END {Count}", absences.Count);
            return absences;
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

            var url = $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/droptimizers";

            var response = await _http.SendAsync(BuildRequest(url, apiKey, Method.Post).AddJsonBody(body));
            var responseJson = response.Content ?? string.Empty;

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

        // Deliberately does NOT use GetDroptimizerReport/WoW Utils' own report cache: that
        // cache is only populated by a *successful* import, but this recovery path only
        // ever runs after an import fails because the character isn't on the roster yet -
        // so the cache entry can never exist for the case this method exists to handle.
        // Fetch the character's identity straight from Raidbots instead.
        private async Task<string?> TryTrackCharacterForImport(string groupId, string reportUrlOrId, string apiKey)
        {
            var reportId = ExtractReportId(reportUrlOrId);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                Log.Warning("WoWUtilsClient.ImportDroptimizer: could not determine report id from {Report}", reportUrlOrId);
                return null;
            }

            var identity = await FetchRaidbotsCharacterIdentity(reportId);
            if (identity == null)
            {
                Log.Warning("WoWUtilsClient.ImportDroptimizer: could not determine character identity from Raidbots input for {ReportId}; cannot auto-add roster member", reportId);
                return null;
            }

            var characterId = BuildCharacterSlug(identity.Value.Name, identity.Value.Realm);
            await TrackWoWUtilsCharacter(groupId, apiKey, characterId);
            return characterId;
        }

        private async Task<(string Name, string Realm)?> FetchRaidbotsCharacterIdentity(string reportId)
        {
            var response = await _http.SendAsync(BuildRequest($"https://www.raidbots.com/simbot/report/{reportId}/input.txt"));
            if (!response.IsSuccessStatusCode)
                return null;

            var simcText = response.Content;
            if (string.IsNullOrWhiteSpace(simcText))
                return null;

            // "Import from armory" reports carry no simc class/server lines at all - just
            // armory=<region>,<realm-slug>,<name> - so that has to be checked separately
            // from the full simc-addon-export format ParseSimcCharacter/ParseRealm handle.
            var armoryMatch = Regex.Match(simcText, @"^armory=[^,]+,([^,]+),(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (armoryMatch.Success)
                return (armoryMatch.Groups[2].Value.Trim(), armoryMatch.Groups[1].Value.Trim());

            var (name, realm, _, _) = ParseSimcCharacter(simcText);
            realm ??= ParseRealm(simcText);
            return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(realm)
                ? (name, realm)
                : null;
        }

        private async Task TrackWoWUtilsCharacter(string groupId, string apiKey, string characterId)
        {
            var response = await _http.SendAsync(
                BuildRequest($"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/roster/members", apiKey, Method.Post)
                    .AddJsonBody(new WoWUtilsTrackCharacterRequest { CharacterId = characterId }));
            var responseBody = response.Content ?? string.Empty;

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                throw new HttpRequestException(
                    $"WoW Utils character tracking hit {(int)response.StatusCode}: {responseBody}",
                    null,
                    response.StatusCode);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"WoW Utils character tracking failed for {characterId} ({(int)response.StatusCode}): {responseBody}");
            _cache.Remove(CacheKey("roster", RosterUrl(groupId), apiKey));
        }

        private static string RosterUrl(string groupId) => $"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups/{groupId}/roster";

        private static string CacheKey(string kind, string url, string apiKey) => $"wowutils:{kind}:{url}:{ApiResponseCache.Fingerprint(apiKey)}";

        // GET /v1/groups/{groupId}/roster returns one entry per *person* (a raider),
        // each carrying that person's rank/mainRole plus a `characters` array (their
        // main + alts). We flatten that into one WoWUtilsRosterMember per non-inactive
        // character, since WoW Audit tracks characters, not people.
        private static IReadOnlyList<WoWUtilsRosterMember> ParseRosterMembers(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return [];

            var root = JToken.Parse(responseJson) as JObject;
            var people = root?["members"] as JArray;
            if (people == null)
                return [];

            return people
                .OfType<JObject>()
                .SelectMany(ParseRosterPersonCharacters)
                .Where(member => !string.IsNullOrWhiteSpace(member.Name) && !string.IsNullOrWhiteSpace(member.Realm))
                .GroupBy(member => $"{member.Name}|{member.Realm}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<WoWUtilsRosterMember> ParseRosterPersonCharacters(JObject person)
        {
            // person["rank"] is the person's *guild* rank (GM/Officer/Raider) - not the
            // Main/Alt designation WoW Audit's character Rank field expects. That comes
            // from each character's own "status" (main/alt/inactive) instead.
            var mainRole = person["mainRole"]?.Value<string>();
            var memberId = FirstString(person, "memberId", "id", "_id");
            var personName = FirstString(person, "displayName", "name");
            var characters = person["characters"] as JArray ?? [];

            foreach (var characterToken in characters.OfType<JObject>())
            {
                var status = characterToken["status"]?.Value<string>();
                if (IsInactiveRosterCharacter(characterToken, status))
                    continue;

                var name = characterToken["name"]?.Value<string>()?.Trim();
                var realm = characterToken["realm"]?.Value<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(realm))
                    continue;

                yield return new WoWUtilsRosterMember
                {
                    CharacterId = characterToken["playerId"]?.Value<string>()?.Trim() ?? BuildCharacterSlug(name, realm),
                    Name = name,
                    Realm = realm,
                    Class = characterToken["class"]?.Value<string>(),
                    Spec = characterToken["spec"]?.Value<string>(),
                    Role = mainRole,
                    Rank = NormalizeRosterCharacterRank(status),
                    MemberId = memberId,
                    PersonName = personName
                };
            }
        }

        private static bool IsInactiveRosterCharacter(JObject character, string? status)
        {
            var inactive = character["inactive"];
            if (inactive?.Type == JTokenType.Boolean)
                return inactive.Value<bool>();

            return string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeRosterCharacterRank(string? status) =>
            status?.Trim().ToLowerInvariant() switch
            {
                "main" => "Main",
                "alt" => "Alt",
                _ => null
            };

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

        // Shape (from the web app): {"absences":[{"memberId":"member_...","startDate":"2026-10-25","endDate":"2026-11-01","displayName":"Desma"}]}.
        // Dates are inclusive yyyy-MM-dd. Entries without a member id and display name, or without a valid start date, are skipped.
        private static IReadOnlyList<WoWUtilsAbsence> ParseAbsences(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return [];

            // Dates stay strings: the default parser would turn "2026-10-30T00:00:00Z" into a DateTime and reformat it.
            var token = JsonConvert.DeserializeObject<JToken>(responseJson, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
            var array = token["absences"] as JArray ?? FindRaidArray(token);
            if (array == null)
                return [];

            var absences = new List<WoWUtilsAbsence>();
            foreach (var item in array.OfType<JObject>())
            {
                var memberId = FirstString(item, "memberId");
                var displayName = FirstString(item, "displayName");
                var start = ParseDate(FirstString(item, "startDate"));
                if (start == null || (memberId == null && displayName == null))
                    continue;
                var end = ParseDate(FirstString(item, "endDate")) ?? start.Value;
                absences.Add(new WoWUtilsAbsence(memberId, displayName, start.Value, end < start ? start.Value : end));
            }

            return absences;
        }

        private static DateOnly? ParseDate(string? value) =>
            value is { Length: >= 10 } && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date : null;

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

        private static RestRequest BuildRequest(string url, string? apiKey = null, Method method = Method.Get)
        {
            var request = new RestRequest(url, method).AddHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.AddHeader("Authorization", $"Bearer {apiKey}");
                request.AddHeader("X-API-Key", apiKey);
            }
            return request;
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


