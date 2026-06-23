using DevClient.Data;
using HtmlAgilityPack;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevClient.Clients
{
    public class ShopifyCollectionClient
    {
        private static readonly ILogger Logger = Log.ForContext<ShopifyCollectionClient>();
        private static readonly HttpClient Client = new();
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
        private const int MaxSearchPages = 20;

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
                var sourceUrls = GetSourceUrls(game, store);
                if (sourceUrls.Count == 0)
                    continue;

                var products = new List<Product>();
                foreach (var source in sourceUrls)
                {
                    try
                    {
                        var sourceProducts = await GetProductsFromSourceUrl(source.Url);
                        products.AddRange(sourceProducts);
                        Logger.Information("{Store} {Category}: {Count} products found", store, source.Category, sourceProducts.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "{Store} {Category}: Error fetching Shopify source", store, source.Category);
                    }
                }

                var distinct = products.DistinctBy(p => p.Url).ToList();
                if (distinct.Count > 0)
                    searches.Add(new Search(keyword, store, distinct));
            }

            return searches;
        }

        private List<(string Url, string Category)> GetSourceUrls(string game, string store)
        {
            var configured = _sourceUrlRepo
                .GetAll(game, store, enabledOnly: true)
                .Select(u => (u.Url, string.IsNullOrWhiteSpace(u.Category) ? "Catalog" : u.Category.Trim()))
                .ToList();

            if (configured.Count > 0)
                return configured;

            return GetDefaultSourceUrls(game, store).ToList();
        }

        private static IEnumerable<(string Url, string Category)> GetDefaultSourceUrls(string game, string store)
        {
            if (game.Equals("gundam", StringComparison.OrdinalIgnoreCase)
                && store.Equals("TKOToyCo", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("https://tkotoyco.com/search?page=1&q=%2Agundam+card+game%2A", "Search");
            }
        }

        private static Task<List<Product>> GetProductsFromSourceUrl(string sourceUrl)
        {
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
                return Task.FromResult(new List<Product>());

            var path = sourceUri.AbsolutePath.TrimEnd('/');
            if (path.StartsWith("/collections/", StringComparison.OrdinalIgnoreCase))
                return GetProductsFromCollectionJson(sourceUri);

            if (path.Equals("/search", StringComparison.OrdinalIgnoreCase)
                || sourceUri.Query.Contains("q=", StringComparison.OrdinalIgnoreCase))
            {
                return GetProductsFromSearchPage(sourceUri);
            }

            return Task.FromResult(new List<Product>());
        }

        private static async Task<List<Product>> GetProductsFromCollectionJson(Uri sourceUri)
        {
            var path = sourceUri.AbsolutePath.TrimEnd('/');
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

        private static async Task<List<Product>> GetProductsFromSearchPage(Uri sourceUri)
        {
            var products = new List<Product>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pagedSource = sourceUri.Query.Contains("page=", StringComparison.OrdinalIgnoreCase);

            for (var page = 1; page <= MaxSearchPages; page++)
            {
                var pageUrl = pagedSource ? BuildSearchPageUrl(sourceUri, page) : sourceUri.ToString();
                using var response = await Client.GetAsync(pageUrl);
                if (!response.IsSuccessStatusCode)
                    break;

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var productLinks = doc.DocumentNode
                    .SelectNodes("//a[contains(@href, '/products/')]")?
                    .Where(node => !string.IsNullOrWhiteSpace(node.GetAttributeValue("href", "")))
                    .ToList() ?? [];

                if (productLinks.Count == 0)
                    break;

                var addedOnPage = 0;
                foreach (var linkNode in productLinks)
                {
                    var href = linkNode.GetAttributeValue("href", "").Trim();
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    var productUri = new Uri(sourceUri, href);
                    var productUrl = productUri.GetLeftPart(UriPartial.Path);
                    if (!seenUrls.Add(productUrl))
                        continue;

                    var container = linkNode.SelectSingleNode("ancestor-or-self::*[contains(@class,'card-wrapper') or contains(@class,'grid__item') or contains(@class,'product-card') or contains(@class,'card')][1]")
                        ?? linkNode.ParentNode;

                    var title = NormalizeWhitespace(linkNode.InnerText);
                    if (string.IsNullOrWhiteSpace(title) && container != null)
                    {
                        title = NormalizeWhitespace(container
                            .SelectSingleNode(".//*[contains(@class,'card__heading') or contains(@class,'card-information__text') or contains(@class,'full-unstyled-link')]")?
                            .InnerText ?? string.Empty);
                    }

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var price = ExtractPrice(container ?? linkNode);
                    products.Add(new Product(title, price, productUrl));
                    addedOnPage++;
                }

                if (!pagedSource)
                    break;

                if (addedOnPage == 0)
                    break;
            }

            return products;
        }

        private static string BuildSearchPageUrl(Uri sourceUri, int page)
        {
            var parameters = ParseQueryString(sourceUri.Query);
            parameters["page"] = page.ToString();

            var query = string.Join("&", parameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var builder = new UriBuilder(sourceUri)
            {
                Query = query,
            };

            return builder.Uri.ToString();
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var trimmed = query.TrimStart('?');
            if (string.IsNullOrWhiteSpace(trimmed))
                return values;

            foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = segment.IndexOf('=');
                var rawKey = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
                var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
                var key = Uri.UnescapeDataString(rawKey.Replace('+', ' '));
                var value = Uri.UnescapeDataString(rawValue.Replace('+', ' '));
                values[key] = value;
            }

            return values;
        }

        private static string ExtractPrice(HtmlNode node)
        {
            var text = NormalizeWhitespace(node.InnerText);
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var match = Regex.Match(text, @"\$[\d,]+(?:\.\d{2})?");
            return match.Success ? match.Value : string.Empty;
        }

        private static string NormalizeWhitespace(string value) =>
            Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
    }
}
