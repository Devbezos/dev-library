using DevClient.Data;
using DevClient.Data.WoW.WoWUtils;
using Newtonsoft.Json;
using Serilog;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace DevClient.Clients
{
    public class WoWUtilsClient : IWoWUtilsClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, string> _groupIdByApiKey = new(StringComparer.Ordinal);

        public WoWUtilsClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        private static readonly string[] _wowClasses =
        [
            "deathknight", "demonhunter", "druid", "evoker", "hunter",
            "mage", "monk", "paladin", "priest", "rogue", "shaman", "warlock", "warrior"
        ];

        /// <summary>
        /// Fetches the full parsed droptimizer report from WoW Utils.
        /// GET /api/droptimizer/fetch?reportId={reportId}&amp;file=report
        /// </summary>
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

        public async Task<WoWUtilsImportResponse> ImportDroptimizer(
            string? groupId, string reportUrlOrId, string apiKey, string? profileKey = null)
        {
            groupId = await ResolveGroupId(groupId, apiKey);
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
                throw new HttpRequestException(
                    $"WoW Utils import failed ({(int)response.StatusCode}): {responseJson}",
                    null,
                    response.StatusCode);

            return JsonConvert.DeserializeObject<WoWUtilsImportResponse>(responseJson)!;
        }

        private async Task<string> ResolveGroupId(string? configuredGroupId, string apiKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredGroupId))
                return configuredGroupId;

            if (_groupIdByApiKey.TryGetValue(apiKey, out var cachedGroupId))
                return cachedGroupId;

            using var client = BuildHttpClient(apiKey);
            var response = await client.GetAsync($"{Constants.WoW.WoWUtils.BaseUrl}/v1/groups");
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"WoW Utils group lookup failed ({(int)response.StatusCode}): {responseJson}",
                    null,
                    response.StatusCode);

            var groups = JsonConvert.DeserializeObject<WoWUtilsGroupListResponse>(responseJson);
            var groupId = groups?.Data?.FirstOrDefault()?.GroupId;
            if (string.IsNullOrWhiteSpace(groupId))
                throw new InvalidOperationException("WoW Utils group lookup returned no groupId");

            _groupIdByApiKey[apiKey] = groupId;
            return groupId;
        }

        /// <summary>
        /// Determines the character slug ({name}-{realm}) used in the import URL.
        /// Parses the SimC text in rawFormData since it reliably contains server and character name.
        /// </summary>
        public string GetCharacterSlug(WoWUtilsFetchResponse report)
        {
            var (name, realm, _, _) = ParseSimcCharacter(report.RawFormData?.Text);

            // Fall back to top-level fields if SimC parse missed something
            name  ??= report.CharacterName;
            realm ??= ParseRealm(report.RawFormData?.Text);

            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Could not determine character name from droptimizer report");
            if (string.IsNullOrEmpty(realm))
                throw new InvalidOperationException("Could not determine realm from droptimizer report");

            return $"{name.ToLower()}-{realm.ToLower()}";
        }

        // ── Private helpers ──────────────────────────────────────────────

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
                    realm = trimmed.Substring(7).Trim();

                if (trimmed.StartsWith("spec=", StringComparison.OrdinalIgnoreCase))
                    spec = trimmed.Substring(5).Trim();
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
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);
            }
            return client;
        }
    }
}





