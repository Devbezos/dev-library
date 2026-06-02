using dev_library.Data;
using Serilog;
using System.Text.Json;

namespace dev_library.Clients
{
    public class ShopifyCollectionClient
    {
        private static readonly ILogger Logger = Log.ForContext<ShopifyCollectionClient>();
        private static readonly HttpClient Client = new();
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";

        private readonly ITcgSourceUrlRepository _sourceUrlRepo;

        static ShopifyCollectionClient()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        public ShopifyCollectionClient(ITcgSourceUrlRepository sourceUrlRepo)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        public Task<List<Search>> GetPokemon(params string[] stores) =>
            GetProducts("pokemon", "Pokemon", stores);

        public Task<List<Search>> GetGundam(params string[] stores) =>
            GetProducts("gundam", "Gundam", stores);

        private async Task<List<Search>> GetProducts(string game, string keyword, IReadOnlyCollection<string> stores)
        {
            var searches = new List<Search>();
            foreach (var store in stores)
            {
                var sourceUrls = _sourceUrlRepo
                    .GetAll(game, store, enabledOnly: true)
                    .Select(u => (u.Url, u.Category))
                    .ToList();

                var products = new List<Product>();
                foreach (var source in sourceUrls)
                {
                    try
                    {
                        var sourceProducts = await GetProductsFromCollectionJson(source.Url);
                        products.AddRange(sourceProducts);
                        Logger.Information("{Store} {Category}: {Count} products found", store, source.Category, sourceProducts.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "{Store} {Category}: Error fetching Shopify collection", store, source.Category);
                    }
                }

                var distinct = products.DistinctBy(p => p.Url).ToList();
                if (distinct.Count > 0)
                    searches.Add(new Search(keyword, store, distinct));
            }

            return searches;
        }

        private static async Task<List<Product>> GetProductsFromCollectionJson(string sourceUrl)
        {
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri)) return [];

            var path = sourceUri.AbsolutePath.TrimEnd('/');
            if (!path.StartsWith("/collections/", StringComparison.OrdinalIgnoreCase)) return [];

            var jsonUrl = $"{sourceUri.Scheme}://{sourceUri.Host}{path}/products.json?limit=250";
            using var response = await Client.GetAsync(jsonUrl);
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("products", out var productsElement)
                || productsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
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
    }
}
