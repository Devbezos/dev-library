using dev_library.Data;
using HtmlAgilityPack;

namespace dev_library.Clients
{
    public class CanadaComputersClient
    {
        private static readonly HttpClient client = new();
        private static readonly string ccSearchUrl = "https://www.canadacomputers.com/en/search?id_category=914&s={0}&a=0&b=0";
        private static readonly List<string> Keywords = new List<string>() { "gigabyte rtx 5080", "prismatic evolution" };

        public async Task<List<Search>> GetProducts()
        {
            Console.WriteLine("CanadaComputersClient.GetProducts: START");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            var searchList = new List<Search>();

            try
            {
                foreach (var keyword in Keywords)
                {
                    var searchUrl = string.Format(ccSearchUrl, keyword);
                    var content = await client.GetStringAsync(searchUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'js-product')]");
                    var inStockProducts = new List<Product>();

                    if (products == null || products.Count == 0)
                    {
                        Console.WriteLine($"CanadaComputersClient.GetProducts: No {keyword} found");
                        continue;
                    }

                    foreach (var product in products)
                    {
                        var nameNode = doc.DocumentNode.SelectSingleNode("//h2[@class='h3 product-title mb-1']//a");
                        var priceNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'price')]");
                        var availabilityNode = doc.DocumentNode.SelectSingleNode("//small//b");
                        var linkNode = doc.DocumentNode.SelectSingleNode("//h2[@class='h3 product-title mb-1']//a");

                        if (nameNode != null && priceNode != null && availabilityNode != null && linkNode != null)
                        {
                            var productName = nameNode.InnerText.Trim();
                            var url = linkNode.GetAttributeValue("href", string.Empty);
                            var productPrice = priceNode.InnerText.Trim();
                            var availability = availabilityNode.InnerText.Trim();

                            if (availability.Trim().ToUpper() != "NOT AVAILABLE FOR ORDER")
                            {
                                inStockProducts.Add(new Product(productName, productPrice, url));
                            }
                        }
                    }

                    if (inStockProducts.Count > 0)
                    {
                        searchList.Add(new Search(keyword, "CC", inStockProducts));
                    }

                    Console.WriteLine($"CanadaComputersClient.GetProducts: Found {products.Count} {keyword} products with {inStockProducts.Count} in stock");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CanadaComputersClient.GetProducts: Error fetching webpage: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("CanadaComputersClient.GetProducts: END");
            }
            return searchList;
        }
    }
}