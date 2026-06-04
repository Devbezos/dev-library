using DevClient.Data;
using DevClient.Data.WoW.WarcraftLogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace DevClient.Clients
{
    public class WarcraftLogsClient
    {


        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private async Task<string> GetOAuthToken()
        {
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var settings = AppSettings.WarcraftLogs!;
            var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));

            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, Constants.WoW.WarcraftLogs.TokenUrl);
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64Auth}");
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<WarcraftLogsOAuthResponse>(json)!;

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

            return _cachedToken;
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
                var zones = AppSettings.WarcraftLogs!.Zones ?? Array.Empty<WarcraftLogsZone>();

                string zoneFields = zones.Length == 0
                    ? "defaultZone: zoneRankings"
                    : string.Join("\n", zones.Select(z => $"zone_{z.Id}: zoneRankings(zoneID: {z.Id})"));

                var characterArg = characterId.HasValue
                    ? $"id: {characterId.Value}"
                    : $"name: \"{characterName}\", serverSlug: \"{realm}\", serverRegion: \"{region}\"";

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

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var payload = JsonConvert.SerializeObject(new { query });
                using var request = new HttpRequestMessage(HttpMethod.Post, Constants.WoW.WarcraftLogs.ApiUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

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
    }
}





