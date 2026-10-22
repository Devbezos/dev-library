using DevClient.Data;
using DevClient.Data.WoW.Blizzard;
using DevClient.Data.WoW.Raidbots;
using DevClient;
using DevClient.Clients;
using Newtonsoft.Json;
using RestSharp;
using Serilog;
using System.Net.Security;
using System.Security.Authentication;

namespace DevClient.Clients
{
    public class RaidBotsClient
    {
        // Raidbots only gets TLS 1.2/1.3; one long-lived client so connections are pooled.
        private static readonly RestClient Http = SharedHttp.Create(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        });

        /// <summary>A finished Raidbots report never changes.</summary>
        public static readonly TimeSpan ReportTtl = TimeSpan.FromDays(1);

        private readonly IBattleNetClient _battleNetClient;
        private readonly ApiResponseCache _cache;

        public RaidBotsClient(IBattleNetClient battleNetClient, ApiResponseCache? cache = null)
        {
            _battleNetClient = battleNetClient;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        // A report file's text, kept a day; a failed fetch throws and isn't kept.
        private Task<string> GetReportFile(string url) =>
            _cache.GetOrFetch("raidbots:" + url, ReportTtl, async () =>
            {
                var response = await Http.SendAsync(new RestRequest(url).AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"));
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("RaidBotsClient: HTTP {StatusCode}", response.StatusCode);
                    throw new HttpRequestException($"Raidbots returned {(int)response.StatusCode}.", null, response.StatusCode);
                }
                return response.Content ?? string.Empty;
            });


        public async Task<bool> IsValidReport(string url)
        {
            Log.Information("RaidBotsClient.IsValidReport: START");

            var content = string.Empty;

            try
            {
                content = await GetReportFile(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RaidBotsClient.IsValidReport: exception");
            }

            if (content.ToUpper().Contains("HERO 6/6") || content.ToUpper().Contains("MYTH 6/6"))
            {
                return true;
            }

            Log.Information("RaidBotsClient.IsValidReport: END");
            return false;
        }

        public async Task<List<ItemUpgrade>> GetItemUpgrades(List<ItemUpgrade> itemUpgrades, string reportId)
        {
            Log.Information("RaidBotsClient.GetItemUpgrades: START");

            var items = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText($"{AppSettings.BasePath}/{Constants.WoW.RaidBots.CacheName}")) ?? new List<Item>();
            var lastUpdated = DateTime.Now;

            var url = string.Format(Constants.WoW.RaidBots.FileUrlBase, reportId);
            var content = string.Empty;

            try
            {
                content = await GetReportFile(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RaidBotsClient.GetItemUpgrades: exception");
            }

            content = content.Replace('\t', ',');

            var rows = content.Split('\n');
            var playerRow = rows[1].Split(new char[] { '/', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var playerName = playerRow[0].ToTitleCase();
            var baseDps = double.Parse(playerRow[1]);

            for (int i = 2; i < rows.Length - 2; i++)
            {
                try
                {
                    var parts = rows[i].Split(new char[] { '/', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var difficulty = Helpers.GetDifficulty(parts[2]);
                    var dpsGain = Math.Round(double.Parse(parts[^5]) - baseDps, 0);
                    var trueDpsGain = difficulty.Contains("Dungeon") ? dpsGain * 1.1 : dpsGain;

                    if (dpsGain < 0)
                    {
                        continue;
                    }

                    var itemName = string.Empty;
                    var item = items.FirstOrDefault(i => i.Id == parts[3]);

                    if (item == null)
                    {
                        itemName = await _battleNetClient.GetItemName(parts[3]);
                        items.Add(new Item(itemName, parts[3]));
                    }
                    else
                    {
                        itemName = item.Name;
                    }

                    File.WriteAllText($"{AppSettings.BasePath}/{Constants.WoW.RaidBots.CacheName}", JsonConvert.SerializeObject(items));
                    var slot = Helpers.GetItemSlot(parts[6]);

                    var itemUpgrade = new ItemUpgrade(playerName, slot, difficulty, itemName, trueDpsGain, lastUpdated);
                    var existingItemIndex = itemUpgrades.FindIndex(i => i.ItemName.ToUpper().Trim() == itemName.ToUpper().Trim());

                    if (existingItemIndex != -1 && trueDpsGain > itemUpgrades[existingItemIndex].DpsGain)
                    {
                        itemUpgrades[existingItemIndex] = itemUpgrade;
                    }
                    else if (existingItemIndex == -1)
                    {
                        itemUpgrades.Add(itemUpgrade);
                    }
                }
                catch (Exception)
                {
                }
            }

            Log.Information("RaidBotsClient.GetItemUpgrades: END");

            return itemUpgrades;
        }
    }
}





