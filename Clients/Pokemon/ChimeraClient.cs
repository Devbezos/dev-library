using dev_library.Data;
using HtmlAgilityPack;
using Serilog;

namespace dev_library.Clients
{
    public class ChimeraClient
    {
        private static readonly ILogger Logger = Log.ForContext<ChimeraClient>();
        private static readonly HttpClient Client = new();
        private const string DefaultCollectionUrl = "https://chimeragamingonline.com/collections/pokemon?filter.v.availability=1&filter.v.price.gte=20&filter.v.price.lte=&page={0}";
        private const string ChimeraBaseUrl = "https://chimeragamingonline.com";
        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;

        public ChimeraClient(ITcgSourceUrlRepository? sourceUrlRepo = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
        }

        public async Task<List<Search>> GetPokemon()
        {
            Logger.Information("GetProducts: START");
            var allProducts = new List<Product>();
            var sourceUrls = _sourceUrlRepo?
                .GetAll("pokemon", "Chimera", enabledOnly: true)
                .Select(u => u.Url)
                .ToList();

            if (_sourceUrlRepo != null && sourceUrls is { Count: 0 })
                return [];

            if (sourceUrls == null || sourceUrls.Count == 0)
                sourceUrls = [DefaultCollectionUrl];

            if (!Client.DefaultRequestHeaders.Contains("User-Agent"))
                Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                foreach (var sourceUrl in sourceUrls)
                {
                    var page = 1;
                    while (true)
                    {
                        var url = sourceUrl.Contains("{0}", StringComparison.Ordinal)
                            ? string.Format(sourceUrl, page)
                            : sourceUrl;
                        var content = await Client.GetStringAsync(url);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(content);

                        var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'grid-view-item__link')]");
                        if (products == null || products.Count == 0)
                        {
                            Logger.Information("GetProducts: No products on page {Page}", page);
                            break;
                        }

                        foreach (var product in products)
                        {
                            var nameNode = product.SelectSingleNode(".//div[contains(@class,'grid-view-item__title')]");
                            var priceNode = product.SelectSingleNode(".//span[contains(@class,'product-price__price') and contains(@class,'is-bold')]");
                            var linkNode = product.SelectSingleNode(".//a[contains(@href,'/products/')]");
                            if (nameNode == null || priceNode == null || linkNode == null) continue;

                            var name = nameNode.InnerText.Trim();
                            var price = priceNode.InnerText.Trim();
                            var href = linkNode.GetAttributeValue("href", "");
                            var productUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? href
                                : ChimeraBaseUrl + href;

                            allProducts.Add(new Product(name, price, productUrl));
                        }

                        Logger.Information("GetProducts: Page {Page} - {Count} products", page, products.Count);

                        var nextPage = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'pagination__next') or (@aria-label='Next page')]");
                        if (nextPage == null || !sourceUrl.Contains("{0}", StringComparison.Ordinal)) break;

                        page++;
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetProducts: Error fetching collection page");
            }
            finally
            {
                Logger.Information("GetProducts: END - {Count} total products", allProducts.Count);
            }

            var distinct = allProducts.DistinctBy(p => p.Url).ToList();
            return distinct.Count > 0
                ? new List<Search> { new Search("Pokemon", "Chimera", distinct) }
                : new List<Search>();
        }
    }
}
