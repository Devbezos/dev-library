using DevClient.Data;
using HtmlAgilityPack;

namespace DevClient.Clients
{
    public class PokemonCenterClient
    {
        private static readonly HttpClient client = new();
        private static readonly string searchUrl = "https://www.pokemoncenter.com/en-ca/search/{0}";
        private static readonly List<string> Keywords = new List<string>() { "prismatic-evolutions" };

        public async Task<List<Search>> GetPokemon()
        {
            var searchList = new List<Search>();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                foreach (var keyword in Keywords)
                {
                    var keywordSearchUrl = string.Format(searchUrl, keyword);
                    string content = await client.GetStringAsync(keywordSearchUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-grid-item')]");
                    var inStockProducts = new List<Product>();

                    if (products == null || products.Count == 0)
                    {
                        Console.WriteLine($"No {keyword} found");
                        continue;
                    }

                    foreach (var product in products)
                    {
                        var nameNode = product.SelectSingleNode(".//h1[contains(@class, 'product-title--lz7HX')]");
                        var priceNode = product.SelectSingleNode(".//span[contains(@class, 'product-price--uqHtS')]");
                        var availabilityNode = product.SelectSingleNode(".//div[contains(@class, 'product-image-oos--Lae0t')]");

                        // Extract values
                        string? name = nameNode?.InnerText.Trim();
                        string? price = priceNode?.InnerText.Trim();
                        string test = availabilityNode != null ? "Out of Stock" : "In Stock";

                        if (nameNode != null && priceNode != null)
                        {
                            string productName = nameNode.InnerText.Trim();
                            string productUrl = "https://www.pokemoncenter.com" + nameNode.Attributes["href"].Value;
                            string productPrice = priceNode.InnerText.Trim();
                            string availability = availabilityNode?.InnerText.Trim() ?? "In Stock";

                            if (!availability.ToLower().Contains("out of stock"))
                            {
                                inStockProducts.Add(new Product(productName, productPrice, productUrl));
                            }
                        }
                    }
                    searchList.Add(new Search(keyword, "PC", inStockProducts));
                    Console.WriteLine($"Found {products.Count} {keyword} products with {inStockProducts.Count} in stock");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching webpage: {ex.Message}");
            }
            return searchList;
        }
    }
}





