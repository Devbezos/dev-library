using dev_library.Data;
using dev_library.Data.WoW.Blizzard;
using dev_library.Data.WoW.Raidbots;
using dev_refined;
using dev_refined.Clients;
using Newtonsoft.Json;
using Serilog;
using System.Net.Security;
using System.Security.Authentication;

namespace dev_library.Clients
{
    public class RaidBotsClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBattleNetClient _battleNetClient;

        public RaidBotsClient(IHttpClientFactory httpClientFactory, IBattleNetClient battleNetClient)
        {
            _httpClientFactory = httpClientFactory;
            _battleNetClient = battleNetClient;
        }


        public async Task<bool> IsValidReport(string url)
        {
            Log.Information("RaidBotsClient.IsValidReport: START");

            var content = string.Empty;

            using (var httpClient = _httpClientFactory.CreateClient("raidbots"))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        content = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        Log.Warning("RaidBotsClient.IsValidReport: HTTP {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "RaidBotsClient.IsValidReport: exception");
                }
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

            var items = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText($"{AppSettings.BasePath}/{Constants.WoW.RaidBots.CacheName}"));
            var lastUpdated = DateTime.Now;

            var url = string.Format(Constants.WoW.RaidBots.FileUrlBase, reportId);
            var content = string.Empty;

            using (var httpClient = _httpClientFactory.CreateClient("raidbots"))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        content = await response.Content.ReadAsStringAsync();
                        // Log.Debug(content);
                    }
                    else
                    {
                        Log.Warning("RaidBotsClient.GetItemUpgrades: HTTP {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "RaidBotsClient.GetItemUpgrades: exception");
                }
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
