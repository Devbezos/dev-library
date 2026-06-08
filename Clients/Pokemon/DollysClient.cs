using DevClient.Data;
using HtmlAgilityPack;
using Serilog;
using System.Text.RegularExpressions;

namespace DevClient.Clients
{
    public class DollysClient
    {
        private static readonly ILogger Logger = Log.ForContext<DollysClient>();
        private static readonly HttpClient _client = new();
        private static readonly string DollysBaseUrl = "https://www.dollys.ca";

        private static readonly (string Url, string Category)[] Catalogs =
        [
            ("https://www.dollys.ca/catalog/pokemon_products-pokemon_elite_trainer_boxes/6218?filter_by_stock=in-stock", "ETBs"),
            ("https://www.dollys.ca/catalog/pokemon_products-pokemon_booster_boxes/4033?filter_by_stock=in-stock", "Booster Boxes"),
            ("https://www.dollys.ca/catalog/pokemon_products-pokemon_box_sets/3473?filter_by_stock=in-stock", "Box Sets / Bundles"),
        ];

        private static readonly (string Url, string Category)[] GundamCatalogs =
        [
            ("https://www.dollys.ca/catalog/gundam_card_game_products-gundam_card_game_booster_boxes/6764?filter_by_stock=in-stock", "Booster Boxes"),
        ];

        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

        public DollysClient(ITcgSourceUrlRepository? sourceUrlRepo = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        private (string Url, string Category)[] GetCatalogs(string game, (string Url, string Category)[] defaults)
        {
            if (_sourceUrlRepo == null) return defaults;
            var configured = _sourceUrlRepo
                .GetAll(game, "Dollys", enabledOnly: true)
                .Select(x => (x.Url, string.IsNullOrWhiteSpace(x.Category) ? "Catalog" : x.Category.Trim()))
                .ToArray();
            return configured;
        }

        private static void EnsureHeaders()
        {
            if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
                _client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        private static bool HasPreorderSignal(HtmlNode node)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            return Regex.IsMatch(text, @"\bpre[\s\-_/\.]*orders?\b|\bpreorders?\b", RegexOptions.IgnoreCase);
        }

        private async Task<List<Search>> FetchCatalogs((string Url, string Category)[] catalogs, string keyword, string logPrefix)
        {
            var results = new List<Search>();
            EnsureHeaders();

            foreach (var (url, category) in catalogs)
            {
                var products = new List<Product>();
                try
                {
                    var content = await _client.GetStringAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var nodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'product')]");
                    if (nodes == null || nodes.Count == 0)
                    {
                        Logger.Information("{LogPrefix}: No products found in {Category}", logPrefix, category);
                        continue;
                    }

                    foreach (var node in nodes)
                    {
                        var nameNode = node.SelectSingleNode(".//h4[contains(@class, 'name')]");
                        var priceNode = node.SelectSingleNode(".//div[contains(@class, 'product-price-qty')]//span[contains(@class, 'price')]");
                        var linkNode = node.SelectSingleNode(".//a[@itemprop='url']");
                        if (nameNode == null || priceNode == null || linkNode == null) continue;

                        var name = nameNode.InnerText.Trim();
                        if (HasPreorderSignal(node) && !TcgPreorderClassifier.IsPreorder(name))
                            name = $"Pre-Order {name}";

                        var raw = priceNode.InnerText.Trim();
                        var price = "$" + Regex.Replace(raw, @"^[^\d]*", "").Trim();
                        var href = linkNode.GetAttributeValue("href", "");
                        var url2 = href.StartsWith("http") ? href : DollysBaseUrl + href;
                        products.Add(new Product(name, price, url2));
                    }

                    Logger.Information("{LogPrefix}: {Category} - {Count} products", logPrefix, category, products.Count);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "{LogPrefix}: Error fetching {Category}", logPrefix, category);
                }

                if (products.Count > 0)
                    results.Add(new Search(category, keyword, products));
            }

            return results;
        }

        public async Task<List<Search>> GetPokemon()
        {
            Logger.Information("GetPokemon: START");
            var results = await FetchCatalogs(GetCatalogs("pokemon", Catalogs), "Dollys", "DollysClient.GetPokemon");
            Logger.Information("GetPokemon: END");
            return results;
        }

        public async Task<List<Search>> GetGundam()
        {
            Logger.Information("GetGundam: START");
            var results = await FetchCatalogs(GetCatalogs("gundam", GundamCatalogs), "Dollys", "DollysClient.GetGundam");
            Logger.Information("GetGundam: END");
            return results;
        }
    }
}





