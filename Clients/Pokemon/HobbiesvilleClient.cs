using DevClient.Data;
using Serilog;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace DevClient.Clients
{
    public class HobbiesvilleClient
    {
        private static readonly ILogger Logger = Log.ForContext<HobbiesvilleClient>();
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
        private static readonly RestClient Client = ScrapingNetwork.CreateRestClient(UserAgent);
        private static readonly (string Url, string Category)[] PokemonPreOrderDefaults =
        [
            ("https://hobbiesville.com/collections/pokemon-pre-orders-1", "Pre-Order"),
        ];

        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

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

            if (_sourceUrlRepo != null && sourceUrls is { Count: 0 })
                return [];

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
            var response = await Client.SendAsync(new RestRequest(jsonUrl));
            if (!response.IsSuccessStatusCode) return [];

            var document = RawJson.Parse(response.Content ?? string.Empty);

            if (document["products"] is not JArray productsElement)
            {
                return [];
            }

            var products = new List<Product>();
            foreach (var productElement in productsElement)
            {
                if (productElement["variants"] is not JArray variantsElement)
                {
                    continue;
                }

                string priceValue = string.Empty;
                var isAvailable = false;

                foreach (var variantElement in variantsElement)
                {
                    if (variantElement["available"] is not { Type: JTokenType.Boolean } availableElement
                        || !availableElement.Value<bool>())
                    {
                        continue;
                    }

                    isAvailable = true;
                    priceValue = variantElement.Str("price") ?? priceValue;
                    break;
                }

                if (!isAvailable) continue;

                var title = (productElement.Str("title") ?? "Untitled Product").Trim();

                var handle = (productElement.Str("handle") ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(handle)) continue;
                var price = string.IsNullOrWhiteSpace(priceValue) ? string.Empty : $"${priceValue}";

                products.Add(new Product(title, price, $"{sourceUri.Scheme}://{sourceUri.Host}/products/{handle}"));
            }

            return products;
        }
    }
}





