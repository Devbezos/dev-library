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
    public class WarcraftLogsClient : IWarcraftLogsClient
    {
        // Per client id: the dashboard can use someone's own Warcraft Logs keys, which get their own token.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Token, DateTime Expiry)> Tokens = new();

        private readonly WarcraftLogsSettings? _settings;

        /// <param name="settings">Explicit settings (dev-api binds these from IConfiguration); falls back to AppSettings.WarcraftLogs.</param>
        public WarcraftLogsClient(WarcraftLogsSettings? settings = null) => _settings = settings;

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

                var request = SharedHttp.Bearer(Method.Post, Constants.WoW.WarcraftLogs.ApiUrl, token).AddJsonBody(new { query });

                var response = await SharedHttp.Client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = response.Content ?? string.Empty;

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
            });

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

            var data = await PostQuery(query, new { code });

            return data["reportData"]?["report"]?.ToObject<WarcraftLogsReport>()
                ?? throw new WarcraftLogsApiException("This report does not exist.");
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

        private async Task<JToken> PostQuery(string query, object variables)
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





