using dev_library.Data;
using HtmlAgilityPack;
using Serilog;

namespace dev_library.Clients
{
    public class AtlasClient
    {
        private static readonly HttpClient client = new();
        private static readonly string AtlasSearchUrl = "https://www.atlascollectables.com/catalog/pokemon-pokemon_sealed_products-pokemon_booster_boxes/386?filter_by_stock=in-stock";
        private static readonly string AtlasBaseUrl = "https://www.atlascollectables.com";
        private static readonly List<string> Keywords = new()
        {
            "Pokemon Booster Boxes",
        };

        public async Task<List<Search>> GetPokemon()
        {
            Log.Information("AtlasClient.GetProducts: START");
            var searchList = new List<Search>();
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                foreach (var keyword in Keywords)
                {
                    var content = await client.GetStringAsync(AtlasSearchUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//li[contains(@class, 'product')]");
                    var inStockProducts = new List<Product>();

                    if (products == null || products.Count == 0)
                    {
                        Log.Information("No {Keyword} found", keyword);
                        continue;
                    }

                    foreach (var product in products)
                    {
                        var name = product.SelectSingleNode(".//h4[contains(@class, 'name')]").InnerText.Trim();
                        var price = product.SelectSingleNode(".//div[contains(@class, 'product-price-qty')]//span[contains(@class, 'price')]").InnerText.Trim();
                        var url = AtlasBaseUrl + product.SelectSingleNode(".//a[@itemprop='url']").GetAttributeValue("href", "");

                        inStockProducts.Add(new Product(name, price[4..], url));
                    }

                    if (inStockProducts.Count > 0)
                    {
                        searchList.Add(new Search(keyword, "Atlas", inStockProducts));
                    }

                    Log.Information("AtlasClient.GetProducts: Found {Total} {Keyword} products with {InStock} in stock", products.Count, keyword, inStockProducts.Count);
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AtlasClient.GetProducts: Error fetching webpage");
            }
            finally
            {
                Log.Information("AtlasClient.GetProducts: END");
            }
            return searchList;
        }
    }
}
