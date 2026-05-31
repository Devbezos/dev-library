using dev_library.Data;
using HtmlAgilityPack;
using Serilog;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{

    public class JJClient
    {
        private static readonly HttpClient client = new();
        private static readonly string jjSearchUrl = "https://shop.jjcards.com/search.asp?keyword={0}+tcg&sortby=2&page=1&catid=";
        private static readonly string jjAddToCartUrl = "https://shop.jjcards.com/add_cart.asp?quick=1&item_id={0}&cat_id=0";
        private static readonly List<string> Keywords = new()
        {
            "Prismatic Evolutions",
            "Obsidian Flames",
            "Surging Sparks",
            "Journey Together",
            "Stellar Crown",
            "Shrouded Fable",
            "Twilight Masquerade",
            "Temportal Forces"
        };

        public async Task<List<Search>> GetProducts()
        {
            Log.Information("JJClient.GetProducts: START");
            var searchList = new List<Search>();
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                foreach (var keyword in Keywords)
                {
                    var searchUrl = string.Format(jjSearchUrl, keyword);
                    string content = await client.GetStringAsync(searchUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-content')]");
                    var inStockProducts = new List<Product>();

                    if (products == null || products.Count == 0)
                    {
                        Log.Information("JJClient.GetProducts: No {Keyword} found", keyword);
                        continue;
                    }

                    foreach (var product in products)
                    {
                        var nameNode = product.SelectSingleNode(".//a");
                        var priceNode = product.SelectSingleNode(".//span[contains(@class, 'price')]");
                        var availabilityNode = product.SelectSingleNode(".//span[contains(@class, 'availability')]");

                        if (nameNode != null && priceNode != null && availabilityNode != null)
                        {
                            string productName = nameNode.InnerText.Trim();
                            var baseUrl = nameNode.Attributes["href"].Value;
                            var itemId = Regex.Match(baseUrl, @"_(\d+)\.html$");

                            var url = string.Format(jjAddToCartUrl, itemId.Groups[1].Value);
                            string productPrice = priceNode.InnerText.Trim();
                            string availability = availabilityNode.InnerText.Trim();

                            if (availability.Trim().ToUpper() == "IN STOCK.")
                            {
                                inStockProducts.Add(new Product(productName, productPrice, url));
                            }
                        }
                    }

                    if (inStockProducts.Count > 0)
                    {
                        searchList.Add(new Search(keyword, "JJ", inStockProducts));
                    }

                    Log.Information("JJClient.GetProducts: Found {Total} {Keyword} products with {InStock} in stock", products.Count, keyword, inStockProducts.Count);
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "JJClient.GetProducts: Error fetching webpage");
            }
            finally
            {
                Log.Information("JJClient.GetProducts: END");
            }
            return searchList;
        }
    }
}