using dev_library.Data;
using Serilog;
using System.Text.Json;

namespace dev_library.Clients
{
    public class HobbiesvilleClient
    {
        private static readonly ILogger Logger = Log.ForContext<HobbiesvilleClient>();
        private static readonly HttpClient Client = new();
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
        private static readonly (string Url, string Category)[] PokemonPreOrderDefaults =
        [
            ("https://hobbiesville.com/collections/pokemon-pre-orders-1", "Pre-Order"),
        ];

        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

        static HobbiesvilleClient()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        public HobbiesvilleClient(ITcgSourceUrlRepository? sourceUrlRepo = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        public async Task<List<Search>> GetPokemonPreOrders()
        {
            Logger.Information("GetPokemonPreOrders: START");

            var sourceUrls = _sourceUrlRepo?
                .GetAll("pokemon", "Hobbiesville", enabledOnly: true)
                .Where(u => u.Category.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    && u.Category.Contains("order", StringComparison.OrdinalIgnoreCase))
                .Select(u => (u.Url, u.Category))
                .ToList();

            if (sourceUrls == null || sourceUrls.Count == 0)
                sourceUrls = PokemonPreOrderDefaults.ToList();

            var allProducts = new List<Product>();

            foreach (var source in sourceUrls)
            {
                try
                {
                    var products = await GetProductsFromCollectionJson(source.Url);
                    allProducts.AddRange(products);
                    Logger.Information("{Category}: {Count} products found", source.Category, products.Count);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "{Category}: Error fetching webpage", source.Category);
                }
            }

            var distinct = allProducts.DistinctBy(p => p.Url).ToList();
            Logger.Information("GetPokemonPreOrders: END - {Count} total products", distinct.Count);

            return distinct.Count > 0
                ? [new Search("Pokemon Pre-Order", "Hobbiesville", distinct)]
                : [];
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
