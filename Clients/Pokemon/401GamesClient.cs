using dev_library.Data;
using HtmlAgilityPack;

namespace dev_library.Clients
{
    public class _401GamesClient
    {
        private static readonly HttpClient client = new();
        private static readonly string _401SearchUrl = "https://store.401games.ca/collections/pokemon-trading-cards?sort=price_max_to_min&filters=Product+Type,Product+Type_Booster+Boxes,Price_from_to,66-400,In+Stock,True";
        private static readonly string _401BaseUrl = "https://store.401games.ca";

        public async Task<List<Search>> GetPokemon()
        {
            var allProducts = new List<Product>();
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                string content = await client.GetStringAsync(_401SearchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-container')]");
                if (products == null || products.Count == 0)
                {
                    Console.WriteLine("401Games: No products found");
                }
                else
                {
                    foreach (var product in products)
                    {
                        var nameNode  = product.SelectSingleNode(".//span[contains(@class,'product-title')]");
                        var priceNode = product.SelectSingleNode(".//div[contains(@class,'fs-price')]");
                        var linkNode  = product.SelectSingleNode(".//a[contains(@href,'/products/')]");
                        if (nameNode == null || priceNode == null || linkNode == null) continue;

                        var name  = nameNode.InnerText.Trim();
                        var price = priceNode.InnerText.Trim();
                        var href  = linkNode.GetAttributeValue("href", "");
                        var url   = href.StartsWith("http") ? href : _401BaseUrl + href;
                        allProducts.Add(new Product(name, price, url));
                    }
                    Console.WriteLine($"401Games: {products.Count} products found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"401Games: Error fetching webpage: {ex.Message}");
            }

            return allProducts.Count > 0
                ? new List<Search> { new Search("Pokemon", "401", allProducts) }
                : new List<Search>();
        }
    }
}

