using dev_library.Data;
using HtmlAgilityPack;
using Serilog;

namespace dev_library.Clients
{
    public class AtlasClient
    {
        private static readonly ILogger Logger = Log.ForContext<AtlasClient>();
        private static readonly HttpClient client = new();
        private static readonly (string Url, string Category)[] AtlasPokemonDefaults =
        [
            ("https://www.atlascollectables.com/catalog/pokemon-pokemon_sealed_products-pokemon_booster_boxes/386?filter_by_stock=in-stock", "Booster Boxes"),
        ];
        private static readonly (string Url, string Category)[] AtlasGundamDefaults =
        [
            ("https://www.atlascollectables.com/catalog/gundam_card_game-gundam_card_game__sealed-gundam_card_game__booster_boxes/16227?filter_by_stock=in-stock", "Booster Boxes"),
        ];
        private static readonly string AtlasBaseUrl = "https://www.atlascollectables.com";
        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

        public AtlasClient(ITcgSourceUrlRepository? sourceUrlRepo = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        private (string Url, string Category)[] GetCatalogs(string game, (string Url, string Category)[] defaults)
        {
            if (_sourceUrlRepo == null) return defaults;
            var configured = _sourceUrlRepo
                .GetAll(game, "Atlas", enabledOnly: true)
                .Select(x => (x.Url, string.IsNullOrWhiteSpace(x.Category) ? "Booster Boxes" : x.Category.Trim()))
                .ToArray();
            return configured.Length > 0 ? configured : defaults;
        }

        private static void EnsureHeaders()
        {
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        private async Task<List<Product>> FetchProducts(string searchUrl, string logPrefix)
        {
            var allProducts = new List<Product>();
            EnsureHeaders();

            var content = await client.GetStringAsync(searchUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var products = doc.DocumentNode.SelectNodes("//li[contains(@class, 'product')]");
            if (products == null || products.Count == 0)
            {
                Logger.Information("{LogPrefix}: No products found", logPrefix);
                return allProducts;
            }

            foreach (var product in products)
            {
                var nameNode = product.SelectSingleNode(".//h4[contains(@class, 'name')]");
                var priceNode = product.SelectSingleNode(".//div[contains(@class, 'product-price-qty')]//span[contains(@class, 'price')]");
                var linkNode = product.SelectSingleNode(".//a[@itemprop='url']");
                if (nameNode == null || priceNode == null || linkNode == null) continue;

                var name = nameNode.InnerText.Trim();
                var price = priceNode.InnerText.Trim();
                var href = linkNode.GetAttributeValue("href", "");
                var url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href
                    : AtlasBaseUrl + href;

                allProducts.Add(new Product(name, price[4..], url));
            }

            Logger.Information("{LogPrefix}: {Count} products found", logPrefix, products.Count);
            return allProducts;
        }

        public async Task<List<Search>> GetPokemon()
        {
            Logger.Information("GetPokemon: START");
            var results = new List<Search>();

            try
            {
                foreach (var (url, category) in GetCatalogs("pokemon", AtlasPokemonDefaults))
                {
                    var products = await FetchProducts(url, "AtlasClient.GetPokemon");
                    if (products.Count > 0)
                        results.Add(new Search(category, "Atlas", products));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetPokemon: Error fetching webpage");
            }
            finally
            {
                Logger.Information("GetPokemon: END — {Count} total products", results.Sum(r => r.Products.Count));
            }

            return results;
        }

        public async Task<List<Search>> GetGundam()
        {
            Logger.Information("GetGundam: START");
            var results = new List<Search>();

            try
            {
                foreach (var (url, category) in GetCatalogs("gundam", AtlasGundamDefaults))
                {
                    var products = await FetchProducts(url, "AtlasClient.GetGundam");
                    if (products.Count > 0)
                        results.Add(new Search(category, "Atlas", products));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetGundam: Error fetching webpage");
            }
            finally
            {
                Logger.Information("GetGundam: END — {Count} total products", results.Sum(r => r.Products.Count));
            }

            return results;
        }
    }
}
