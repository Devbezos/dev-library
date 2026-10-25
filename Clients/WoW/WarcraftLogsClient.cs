using DevClient.Data;
using DevClient.Data.WoW.WarcraftLogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace DevClient.Clients
{
    /// <summary>
    /// Warcraft Logs' GraphQL API. Rankings and guild report lists are cached (<see cref="ApiResponseCache"/>) for
    /// <see cref="ListTtl"/>, a report's fights per <see cref="ReportTtl"/>; cache keys carry the client id, so answers
    /// fetched with someone's own keys stay theirs. <see cref="QueryAsync"/> is not cached; its callers cache what they read.
    /// </summary>
    public class WarcraftLogsClient : IWarcraftLogsClient
    {
        public static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MissingTtl = TimeSpan.FromMinutes(2);
        /// <summary>How long a report keeps a live log's answers; it can still grow while it's being uploaded.</summary>
        public static readonly TimeSpan LiveReportTtl = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan FinishedReportTtl = TimeSpan.FromDays(1);
        /// <summary>A report whose last event is older than this is taken as finished (logging stopped).</summary>
        public static readonly TimeSpan FinishedAfter = TimeSpan.FromHours(2);

        // Per client id: the dashboard can use someone's own Warcraft Logs keys, which get their own token.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Token, DateTime Expiry)> Tokens = new();

        private readonly WarcraftLogsSettings? _settings;
        private readonly ApiResponseCache _cache;

        /// <param name="settings">Explicit settings (dev-api binds these from IConfiguration); falls back to AppSettings.WarcraftLogs.</param>
        public WarcraftLogsClient(WarcraftLogsSettings? settings = null, ApiResponseCache? cache = null)
        {
            _settings = settings;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        /// <summary>
        /// How long to keep an answer about a report whose last event was at <paramref name="endTimeMs"/> (Unix ms):
        /// a day once it's finished, a minute while it may still be live or when the end time is unknown.
        /// </summary>
        public static TimeSpan ReportTtl(long? endTimeMs, DateTimeOffset? now = null) =>
            endTimeMs is > 0 && (now ?? DateTimeOffset.UtcNow) - DateTimeOffset.FromUnixTimeMilliseconds(endTimeMs.Value) > FinishedAfter
                ? FinishedReportTtl
                : LiveReportTtl;

        private WarcraftLogsSettings Settings =>
            _settings ?? AppSettings.WarcraftLogs
            ?? throw new InvalidOperationException("warcraftLogs settings are not configured.");

        private async Task<string> GetOAuthToken()
        {
            var settings = Settings;
            if (Tokens.TryGetValue(settings.ClientId, out var cached) && DateTime.UtcNow < cached.Expiry)
                return cached.Token;

            var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));

            var request = new RestRequest(Constants.WoW.WarcraftLogs.TokenUrl, Method.Post)
                .AddHeader("Authorization", $"Basic {base64Auth}")
                .AddParameter("grant_type", "client_credentials");

            var response = await SharedHttp.Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = response.Content ?? string.Empty;
            var tokenResponse = JsonConvert.DeserializeObject<WarcraftLogsOAuthResponse>(json)!;

            Tokens[settings.ClientId] = (tokenResponse.AccessToken, DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60));
            return tokenResponse.AccessToken;
        }

        public async Task<WarcraftLogsCharacterZoneRankings?> GetCharacterRankings(string? characterName, string? realm, string? region, string characterUrl, int? characterId = null)
        {
            if (characterId.HasValue)
                Log.Information("WarcraftLogsClient.GetCharacterRankings: id={Id}", characterId);
            else
                Log.Information("WarcraftLogsClient.GetCharacterRankings: {Name} {Realm} {Region}", characterName, realm, region);

            try
            {
                var token = await GetOAuthToken();
                var zones = Settings.Zones ?? Array.Empty<WarcraftLogsZone>();

                string zoneFields = zones.Length == 0
                    ? "defaultZone: zoneRankings"
                    : string.Join("\n", zones.Select(z => $"zone_{z.Id}: zoneRankings(zoneID: {z.Id})"));

                var characterArg = characterId.HasValue
                    ? $"id: {characterId.Value}"
                    // JSON string encoding is valid GraphQL string syntax, so a quote in a name can't change the query.
                    : $"name: {JsonConvert.ToString(characterName)}, serverSlug: {JsonConvert.ToString(realm)}, serverRegion: {JsonConvert.ToString(region)}";

                var query = $$"""
                    {
                      characterData {
                        character({{characterArg}}) {
                          name
                          {{zoneFields}}
                        }
                      }
                    }
                    """;

                var json = await _cache.GetOrFetch($"wcl:{Settings.ClientId}:{query}", ListTtl, async () =>
                {
                    var request = SharedHttp.Bearer(Method.Post, Constants.WoW.WarcraftLogs.ApiUrl, token).AddJsonBody(new { query });
                    var response = await SharedHttp.Client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    return response.Content ?? string.Empty;
                });

                var jObject = JObject.Parse(json);
                var characterNode = jObject["data"]?["characterData"]?["character"];

                if (characterNode == null || characterNode.Type == JTokenType.Null)
                {
                    Log.Warning("WarcraftLogsClient.GetCharacterRankings: Character not found (id={Id}, name={Name})", characterId, characterName);
                    return null;
                }

                var result = new WarcraftLogsCharacterZoneRankings
                {
                    CharacterName = characterNode["name"]?.ToString() ?? characterName ?? characterId?.ToString() ?? "Unknown",
                    Url = characterUrl
                };

                if (zones.Length == 0)
                {
                    var rankingsNode = characterNode["defaultZone"];
                    if (rankingsNode != null && rankingsNode.Type != JTokenType.Null)
                    {
                        var rankings = rankingsNode.ToObject<WarcraftLogsZoneRankings>();
                        if (rankings?.Rankings?.Count > 0)
                            result.ZoneRankings.Add(("Current Tier", rankings));
                    }
                }
                else
                {
                    foreach (var zone in zones)
                    {
                        var rankingsNode = characterNode[$"zone_{zone.Id}"];
                        if (rankingsNode != null && rankingsNode.Type != JTokenType.Null)
                        {
                            var rankings = rankingsNode.ToObject<WarcraftLogsZoneRankings>();
                            if (rankings?.Rankings?.Count > 0)
                                result.ZoneRankings.Add((zone.Name, rankings));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WarcraftLogsClient.GetCharacterRankings: Error for {Name}", characterName);
                return null;
            }
        }

        public async Task<WarcraftLogsReportPage> GetGuildReports(string guildName, string realm, string region, int page = 1, int limit = 25)
        {
            var serverSlug = ToRealmSlug(realm);
            Log.Information("WarcraftLogsClient.GetGuildReports: {Guild} {Realm} {Region} page={Page}", guildName, serverSlug, region, page);

            const string query = """
                query ($guildName: String!, $serverSlug: String!, $region: String!, $page: Int!, $limit: Int!) {
                  reportData {
                    reports(guildName: $guildName, guildServerSlug: $serverSlug, guildServerRegion: $region, page: $page, limit: $limit) {
                      total
                      per_page
                      current_page
                      last_page
                      has_more_pages
                      data {
                        code
                        title
                        startTime
                        endTime
                        zone { id name }
                        owner { name }
                      }
                    }
                  }
                }
                """;

            var data = await PostQuery(query, new
            {
                guildName,
                serverSlug,
                region = region.Trim().ToLowerInvariant(),
                page = Math.Max(1, page),
                limit = Math.Clamp(limit, 1, 100),
            }, _ => ListTtl);

            return data["reportData"]?["reports"]?.ToObject<WarcraftLogsReportPage>() ?? new WarcraftLogsReportPage();
        }

        public async Task<WarcraftLogsReport> GetReportFights(string code)
        {
            Log.Information("WarcraftLogsClient.GetReportFights: {Code}", code);

            // killType: Encounters returns boss pulls only (kills and wipes), no trash.
            const string query = """
                query ($code: String!) {
                  reportData {
                    report(code: $code) {
                      code
                      title
                      startTime
                      endTime
                      zone { id name }
                      owner { name }
                      fights(killType: Encounters) {
                        id
                        encounterID
                        name
                        kill
                        difficulty
                        size
                        startTime
                        endTime
                        bossPercentage
                        fightPercentage
                        lastPhase
                        lastPhaseIsIntermission
                        averageItemLevel
                      }
                    }
                  }
                }
                """;

            var data = await PostQuery(query, new { code }, data => data["reportData"]?["report"] is { Type: JTokenType.Object } report
                ? ReportTtl(report["endTime"]?.Value<long?>())
                : MissingTtl);

            return data["reportData"]?["report"]?.ToObject<WarcraftLogsReport>()
                ?? throw new WarcraftLogsApiException("This report does not exist.");
        }

        public async Task<IReadOnlyList<WarcraftLogsReportPlayer>> GetReportPlayers(string code)
        {
            Log.Information("WarcraftLogsClient.GetReportPlayers: {Code}", code);

            // playerDetails is a JSON scalar: { data: { playerDetails: { tanks: [], healers: [], dps: [] } } }.
            const string query = """
                query ($code: String!) {
                  reportData {
                    report(code: $code) {
                      endTime
                      playerDetails(killType: Encounters)
                    }
                  }
                }
                """;

            var data = await PostQuery(query, new { code }, data => data["reportData"]?["report"] is { Type: JTokenType.Object } report
                ? ReportTtl(report["endTime"]?.Value<long?>())
                : MissingTtl);

            if (data["reportData"]?["report"] is not { Type: JTokenType.Object } found)
                throw new WarcraftLogsApiException("This report does not exist.");
            return ParsePlayerDetails(found["playerDetails"]);
        }

        public async Task<IReadOnlyList<WarcraftLogsReportPlayerStats>> GetReportPlayerStats(string code)
        {
            Log.Information("WarcraftLogsClient.GetReportPlayerStats: {Code}", code);

            // rankings and table are JSON scalars. UNVERIFIED shapes, parsed tolerantly (see ParsePlayerStats):
            // rankings { data: [ { roles: { tanks|healers|dps: { characters: [ { name, server, rankPercent } ] } } } ] }
            // and the deaths table { data: { entries: [ { name } ] } } (one entry per death).
            // Tanks and dps are ranked by damage, healers by healing, hence the two rankings.
            const string query = """
                query ($code: String!) {
                  reportData {
                    report(code: $code) {
                      endTime
                      dpsRankings: rankings(playerMetric: dps, compare: Parses)
                      hpsRankings: rankings(playerMetric: hps, compare: Parses)
                      deaths: table(dataType: Deaths, killType: Encounters)
                    }
                  }
                }
                """;

            var data = await PostQuery(query, new { code }, data => data["reportData"]?["report"] is { Type: JTokenType.Object } report
                ? ReportTtl(report["endTime"]?.Value<long?>())
                : MissingTtl);

            if (data["reportData"]?["report"] is not { Type: JTokenType.Object } found)
                throw new WarcraftLogsApiException("This report does not exist.");
            return ParsePlayerStats(found["dpsRankings"], found["hpsRankings"], found["deaths"]);
        }

        private static JToken? Unwrap(JToken? scalar) =>
            scalar?.Type == JTokenType.String ? JToken.Parse(scalar.Value<string>()!) : scalar;

        /// <summary>
        /// Per-player parse totals and death counts of one report. Parses are the tanks' and dps' damage rankings plus the
        /// healers' healing rankings (rankPercent, else bracketPercent); deaths count the deaths table's entries per name.
        /// Deaths are null for everyone when the deaths table is missing; players only in the deaths table get no parses.
        /// </summary>
        public static IReadOnlyList<WarcraftLogsReportPlayerStats> ParsePlayerStats(JToken? dpsRankings, JToken? hpsRankings, JToken? deathsTable)
        {
            var parses = new Dictionary<string, (string Name, string? Server, double Total, int Count)>(StringComparer.OrdinalIgnoreCase);
            void AddParses(JToken? rankings, params string[] roles)
            {
                var fights = Unwrap(rankings) is { } root ? root["data"] as JArray ?? root as JArray : null;
                foreach (var role in roles)
                    foreach (var character in (fights?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                        .SelectMany(f => (f["roles"]?[role]?["characters"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()))
                    {
                        var name = character["name"]?.Value<string>()?.Trim();
                        var percent = (character["rankPercent"] ?? character["bracketPercent"])?.Value<double?>();
                        if (string.IsNullOrEmpty(name) || percent == null)
                            continue;
                        var server = character["server"] is JObject s ? s["name"]?.Value<string>() ?? s["slug"]?.Value<string>() : character["server"]?.Value<string>();
                        var key = $"{name}|{server}";
                        parses[key] = parses.TryGetValue(key, out var p) ? (p.Name, p.Server, p.Total + percent.Value, p.Count + 1) : (name, server, percent.Value, 1);
                    }
            }
            AddParses(dpsRankings, "tanks", "dps");
            AddParses(hpsRankings, "healers");

            var deathsRoot = Unwrap(deathsTable);
            var entries = (deathsRoot?["data"]?["entries"] ?? deathsRoot?["entries"]) as JArray;
            var deaths = entries?.OfType<JObject>()
                .Select(e => e["name"]?.Value<string>()?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .GroupBy(n => n!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var stats = parses.Values
                .Select(p => new WarcraftLogsReportPlayerStats(p.Name, p.Server, p.Total, p.Count, deaths == null ? null : deaths.GetValueOrDefault(p.Name, 0)))
                .ToList();
            if (deaths != null)
                stats.AddRange(deaths.Where(d => !stats.Any(s => string.Equals(s.Name, d.Key, StringComparison.OrdinalIgnoreCase)))
                    .Select(d => new WarcraftLogsReportPlayerStats(d.Key, null, 0, 0, d.Value)));
            return stats;
        }

        /// <summary>Flattens a report's playerDetails scalar (tanks, healers and dps) into one list of distinct players.</summary>
        public static IReadOnlyList<WarcraftLogsReportPlayer> ParsePlayerDetails(JToken? playerDetails)
        {
            var details = playerDetails?.Type == JTokenType.String
                ? JToken.Parse(playerDetails.Value<string>()!)
                : playerDetails;
            var roles = details?["data"]?["playerDetails"] ?? details?["playerDetails"] ?? details;
            if (roles is not JObject roleObject)
                return [];

            return roleObject.Properties()
                .Select(role => role.Value)
                .OfType<JArray>()
                .SelectMany(players => players.OfType<JObject>())
                .Select(player => new WarcraftLogsReportPlayer(
                    player["name"]?.Value<string>()?.Trim() ?? string.Empty,
                    player["server"]?.Value<string>()?.Trim(),
                    player["type"]?.Value<string>()?.Trim()))
                .Where(player => player.Name.Length > 0)
                .DistinctBy(player => $"{player.Name}|{player.Server}", StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Converts a display realm name to the Blizzard/WCL slug, e.g. "Zul'jin" -> "zuljin", "Area 52" -> "area-52".
        /// </summary>
        public static string ToRealmSlug(string realm)
        {
            if (string.IsNullOrWhiteSpace(realm))
                return string.Empty;

            var decomposed = realm.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (char.IsWhiteSpace(c) || c == '-')
                    sb.Append('-');
                // Apostrophes, parentheses and combining accent marks are dropped.
            }

            return Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        }

        public Task<JToken> QueryAsync(string query, object? variables = null) => PostQuery(query, variables ?? new { });

        // With ttl, the data object is kept (by client id, query and variables) for as long as ttl says; errors never are.
        private async Task<JToken> PostQuery(string query, object variables, Func<JToken, TimeSpan>? ttl = null)
        {
            if (ttl is null) return await Send(query, variables);
            var json = await _cache.GetOrFetch($"wcl:{Settings.ClientId}:{query}:{JsonConvert.SerializeObject(variables)}",
                async () => (await Send(query, variables)).ToString(Formatting.None),
                body => ttl(JToken.Parse(body)));
            return JToken.Parse(json);
        }

        private async Task<JToken> Send(string query, object variables)
        {
            var token = await GetOAuthToken();

            var request = SharedHttp.Bearer(Method.Post, Constants.WoW.WarcraftLogs.ApiUrl, token).AddJsonBody(new { query, variables });

            var response = await SharedHttp.Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var jObject = JObject.Parse(response.Content ?? string.Empty);

            // GraphQL reports failures like "No guild exists for this name/server/region." as HTTP 200 with an errors array.
            if (jObject["errors"] is JArray errors && errors.Count > 0)
            {
                var message = string.Join("; ", errors.Select(e => e["message"]?.ToString()).Where(m => !string.IsNullOrWhiteSpace(m)));
                throw new WarcraftLogsApiException(string.IsNullOrWhiteSpace(message) ? "Warcraft Logs returned an error." : message);
            }

            return jObject["data"] ?? new JObject();
        }
    }
}





