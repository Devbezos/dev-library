using DevClient.Data;
using HtmlAgilityPack;
using Serilog;
using System.Net;
using System.Text.Json;

namespace DevClient.Clients
{
    public class _401GamesClient
    {
        private static readonly ILogger Logger = Log.ForContext<_401GamesClient>();
        private static readonly HttpClient client = new(new HttpClientHandler { UseCookies = false });
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
        private static readonly (string Url, string Category)[] DefaultUrls =
        [
            ("https://store.401games.ca/collections/pokemon-trading-cards?sort=price_max_to_min&filters=Product+Type,Product+Type_Booster+Boxes,Price_from_to,66-400,In+Stock,True", "Booster Boxes"),
            ("https://store.401games.ca/collections/pokemon-new-releases?sort=price_max_to_min&filters=In+Stock,True,Category,Pokemon+Sealed+Product", "New Releases"),
        ];
        private static readonly string _401BaseUrl = "https://store.401games.ca";
        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

        static _401GamesClient()
        {
            // DefaultRequestHeaders is not safe to mutate during concurrent sends.
            // Configure it once up-front to avoid races under parallel API requests.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        public _401GamesClient(ITcgSourceUrlRepository? sourceUrlRepo = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        public async Task<List<Search>> GetPokemon()
        {
            var sourceUrls = _sourceUrlRepo?
                .GetAll("pokemon", "401Games", enabledOnly: true)
                .Select(u => (u.Url, u.Category))
                .ToList();

            if (_sourceUrlRepo != null && sourceUrls is { Count: 0 })
                return [];

            if (sourceUrls == null || sourceUrls.Count == 0)
                sourceUrls = DefaultUrls.ToList();

            var allProducts = await GetProductsFromSourceUrls(sourceUrls);

            return allProducts.Count > 0
                ? new List<Search> { new Search("Pokemon", "401Games", allProducts) }
                : new List<Search>();
        }

        public async Task<List<Search>> GetPokemonPreOrders()
        {
            var sourceUrls = GetConfiguredPreOrderSourceUrls("pokemon");
            var allProducts = await GetProductsFromSourceUrls(sourceUrls, preferCollectionJson: true);

            return allProducts.Count > 0
                ? new List<Search> { new Search("Pokemon Pre-Order", "401Games", allProducts) }
                : new List<Search>();
        }

        public async Task<List<Search>> GetGundamPreOrders()
        {
            var sourceUrls = GetConfiguredPreOrderSourceUrls("gundam");
            var allProducts = await GetProductsFromSourceUrls(sourceUrls, preferCollectionJson: true);

            return allProducts.Count > 0
                ? new List<Search> { new Search("Gundam Pre-Order", "401Games", allProducts) }
                : new List<Search>();
        }

        private List<(string Url, string Category)> GetConfiguredPreOrderSourceUrls(string game)
        {
            return _sourceUrlRepo?
                .GetAll(game, "401Games", enabledOnly: true)
                .Where(u => u.Category.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    && u.Category.Contains("order", StringComparison.OrdinalIgnoreCase))
                .Select(u => (u.Url, u.Category))
                .ToList()
                ?? [];
        }

        private static async Task<List<Product>> GetProductsFromSourceUrls(IEnumerable<(string Url, string Category)> sourceUrls, bool preferCollectionJson = false)
        {
            var allProducts = new List<Product>();

            foreach (var source in sourceUrls)
            {
                try
                {
                    if (preferCollectionJson)
                    {
                        var jsonProducts = await TryGetProductsFromCollectionJson(source.Url);
                        if (jsonProducts != null)
                        {
                            allProducts.AddRange(jsonProducts);
                            Logger.Information("{Category}: {Count} products found with collection JSON", source.Category, jsonProducts.Count);
                            continue;
                        }
                    }

                    string content = await GetProductGridHtml(source.Url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-container')]");
                    if (products == null || products.Count == 0)
                    {
                        Logger.Information("{Category}: No products found", source.Category);
                    }
                    else
                    {
                        var inStockCount = 0;
                        foreach (var product in products)
                        {
                            var nameNode = product.SelectSingleNode(".//span[contains(@class,'product-title')]");
                            var priceNode = product.SelectSingleNode(".//div[contains(@class,'fs-price')]");
                            var linkNode = product.SelectSingleNode(".//a[contains(@href,'/products/')]");
                            if (nameNode == null || priceNode == null || linkNode == null) continue;
                            if (!IsInStock(product)) continue;

                            var name = nameNode.InnerText.Trim();
                            var price = priceNode.InnerText.Trim();
                            var href = linkNode.GetAttributeValue("href", "");
                            var url = href.StartsWith("http") ? href : _401BaseUrl + href;
                            allProducts.Add(new Product(name, price, url));
                            inStockCount++;
                        }
                        Logger.Information("{Category}: {Count} products found with {InStockCount} in stock", source.Category, products.Count, inStockCount);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "{Category}: Error fetching webpage", source.Category);
                }
            }

            return allProducts;
        }

        private static async Task<List<Product>?> TryGetProductsFromCollectionJson(string sourceUrl)
        {
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri)) return null;
            if (!sourceUri.Host.Contains("401games.ca", StringComparison.OrdinalIgnoreCase)) return null;

            var path = sourceUri.AbsolutePath.TrimEnd('/');
            if (!path.StartsWith("/collections/", StringComparison.OrdinalIgnoreCase)) return null;

            var jsonUrl = $"{sourceUri.Scheme}://{sourceUri.Host}{path}/products.json?limit=250";
            using var response = await client.GetAsync(jsonUrl);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("products", out var productsElement)
                || productsElement.ValueKind != JsonValueKind.Array)
            {
                return new List<Product>();
            }

            var products = new List<Product>();
            foreach (var productElement in productsElement.EnumerateArray())
            {
                if (!productElement.TryGetProperty("variants", out var variantsElement)
                    || variantsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                string priceValue = string.Empty;
                var isAvailable = false;

                foreach (var variantElement in variantsElement.EnumerateArray())
                {
                    if (!variantElement.TryGetProperty("available", out var availableElement)
                        || availableElement.ValueKind != JsonValueKind.True)
                    {
                        continue;
                    }

                    isAvailable = true;
                    if (variantElement.TryGetProperty("price", out var priceElement)
                        && priceElement.ValueKind == JsonValueKind.String)
                    {
                        priceValue = priceElement.GetString() ?? string.Empty;
                    }
                    break;
                }

                if (!isAvailable) continue;

                var title = productElement.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
                    ? (titleElement.GetString() ?? "Untitled Product").Trim()
                    : "Untitled Product";

                var handle = productElement.TryGetProperty("handle", out var handleElement) && handleElement.ValueKind == JsonValueKind.String
                    ? (handleElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(handle)) continue;
                var price = string.IsNullOrWhiteSpace(priceValue) ? string.Empty : $"${priceValue}";

                products.Add(new Product(title, price, $"{sourceUri.Scheme}://{sourceUri.Host}/products/{handle}"));
            }

            return products;
        }

        private static async Task<string> GetProductGridHtml(string url)
        {
            var content = await client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-container')]");
            if (products != null && products.Count > 1)
                return content;

            var gridUrls = doc.DocumentNode
                .SelectNodes("//link[@rel='preload' and contains(@href, 'ssr-grid.fastsimon.com')]")
                ?.Select(n => WebUtility.HtmlDecode(n.GetAttributeValue("href", "")))
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .ToList();

            var gridUrl = gridUrls?
                .FirstOrDefault(h => h.Contains("device=desktop", StringComparison.OrdinalIgnoreCase))
                ?? gridUrls?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(gridUrl))
                return content;

            if (gridUrl.StartsWith("//", StringComparison.Ordinal))
                gridUrl = "https:" + gridUrl;

            return await client.GetStringAsync(gridUrl);
        }

        private static bool IsInStock(HtmlNode product)
        {
            var availability = product
                .SelectSingleNode(".//*[@itemprop='availability']")
                ?.GetAttributeValue("content", "");

            if (!string.IsNullOrWhiteSpace(availability))
                return availability.Contains("InStock", StringComparison.OrdinalIgnoreCase);

            var productClass = product.GetAttributeValue("class", "");
            if (productClass.Contains("sold-out", StringComparison.OrdinalIgnoreCase)
                || productClass.Contains("out-of-stock", StringComparison.OrdinalIgnoreCase))
                return false;

            var productText = WebUtility.HtmlDecode(product.InnerText).Trim();
            return !productText.Contains("Sold out", StringComparison.OrdinalIgnoreCase)
                && !productText.Contains("Out of stock", StringComparison.OrdinalIgnoreCase);
        }
    }
}






