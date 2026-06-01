using dev_library.Data;
using HtmlAgilityPack;
using Serilog;

namespace dev_library.Clients
{

    public class ChimeraClient
    {
        private static readonly ILogger Logger = Log.ForContext<ChimeraClient>();
        private static readonly HttpClient client = new();
        private static readonly string ChimeraCollectionUrl = "https://chimeragamingonline.com/collections/pokemon?filter.v.availability=1&filter.v.price.gte=20&filter.v.price.lte=&page={0}";
        private static readonly string ChimeraBaseUrl = "https://chimeragamingonline.com";

        public async Task<List<Search>> GetPokemon()
        {
            Logger.Information("GetProducts: START");
            var allProducts = new List<Product>();
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                int page = 1;
                while (true)
                {
                    var content = await client.GetStringAsync(string.Format(ChimeraCollectionUrl, page));
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
                        var nameNode  = product.SelectSingleNode(".//div[contains(@class,'grid-view-item__title')]");
                        var priceNode = product.SelectSingleNode(".//span[contains(@class,'product-price__price') and contains(@class,'is-bold')]");
                        var linkNode  = product.SelectSingleNode(".//a[contains(@href,'/products/')]");
                        if (nameNode == null || priceNode == null || linkNode == null) continue;

                        var name  = nameNode.InnerText.Trim();
                        var price = priceNode.InnerText.Trim();
                        var url   = ChimeraBaseUrl + linkNode.GetAttributeValue("href", "");

                        allProducts.Add(new Product(name, price, url));
                    }

                    Logger.Information("GetProducts: Page {Page} — {Count} products", page, products.Count);

                    var nextPage = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'pagination__next') or (@aria-label='Next page')]");
                    if (nextPage == null) break;

                    page++;
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetProducts: Error fetching collection page");
            }
            finally
            {
                Logger.Information("GetProducts: END — {Count} total products", allProducts.Count);
            }

            return allProducts.Count > 0
                ? new List<Search> { new Search("Pokemon", "Chimera", allProducts) }
                : new List<Search>();
        }
    }
}