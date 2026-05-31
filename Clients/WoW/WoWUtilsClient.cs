using dev_library.Data;
using dev_library.Data.WoW.WoWUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace dev_refined.Clients
{
    public class WoWUtilsClient : IWoWUtilsClient
    {
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
            return result;
        }

        /// <summary>
        /// Imports a droptimizer report into a WoW Utils group wishlist.
        /// POST /api/groups/{groupId}/wishlists/{characterSlug}/droptimizer
        /// The server automatically routes item gains into the correct difficulty slot (N/H/M/M+).
        /// </summary>
        public async Task<WoWUtilsImportResponse> ImportDroptimizer(
            string groupId, string characterSlug, WoWUtilsFetchResponse report, string reportId, string sessionCookie)
        {
            Log.Information("WoWUtilsClient.ImportDroptimizer: START {CharacterSlug} {ReportId}", characterSlug, reportId);

            var (parsedName, _, parsedClass, parsedSpec) = ParseSimcCharacter(report.RawFormData?.Text);

            var body = new JObject
            {
                ["characterName"]  = report.CharacterName  ?? parsedName,
                ["characterClass"] = report.CharacterClass ?? parsedClass,
                ["characterSpec"]  = report.CharacterSpec  ?? parsedSpec,
                ["reportId"]       = reportId,
                ["reportUrl"]      = $"https://www.raidbots.com/simbot/report/{reportId}",
                ["baselineDps"]    = report.BaselineDps,
                ["simSettings"]    = report.SimSettings,
                ["itemGains"]      = report.ItemGains
            };

            using var client = BuildHttpClient(sessionCookie);
            var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var url = $"{Constants.WoW.WoWUtils.BaseUrl}/api/groups/{groupId}/wishlists/{characterSlug}/droptimizer";

            var response = await client.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            Log.Information("WoWUtilsClient.ImportDroptimizer: END status={Status}", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"WoW Utils import failed ({(int)response.StatusCode}): {responseJson}");

            return JsonConvert.DeserializeObject<WoWUtilsImportResponse>(responseJson);
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

        private static (string name, string realm, string characterClass, string spec) ParseSimcCharacter(string simcText)
        {
            if (string.IsNullOrEmpty(simcText))
                return (null, null, null, null);

            string name = null, realm = null, characterClass = null, spec = null;

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

        private static string ParseRealm(string simcText)
        {
            if (string.IsNullOrEmpty(simcText)) return null;
            var m = Regex.Match(simcText, @"^server=(\S+)", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static HttpClient BuildHttpClient(string sessionCookie = null)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
            if (!string.IsNullOrEmpty(sessionCookie))
                client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
            return client;
        }
    }
}
