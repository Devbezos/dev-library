using dev_library.Data;
using HtmlAgilityPack;

namespace dev_library.Clients
{
    public class CanadaComputersClient
    {
        private static readonly HttpClient client = new();
        private static readonly string ccSearchUrl = "https://www.canadacomputers.com/en/search?id_category=914&s={0}&a=0&b=0";
        // Trading cards / boosters category URL (with and without ship filter).
        private static readonly string ccTradingCardsUrl = "https://www.canadacomputers.com/en/2022/trading-cards-boosters?id_manufacturer=3041";
        private static readonly string ccTradingCardsUrlShip = ccTradingCardsUrl + "&ship=1";
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

        public async Task<List<Search>> GetPokemon()
        {
            Console.WriteLine("CanadaComputersClient.GetPokemon: START");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            var results = new List<Product>();
            try
            {
                // Try the ship-enabled URL first (the one you requested).
                var urlsToTry = new[] { ccTradingCardsUrlShip, ccTradingCardsUrl };

                foreach (var url in urlsToTry)
                {
                    try
                    {
                        var html = await client.GetStringAsync(url);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        // Attempt to find product tiles. Canada Computers uses a few patterns,
                        // so try a set of likely container class names.
                        var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'product') or contains(@class,'js-product') or contains(@class,'product-item') or contains(@class,'product-card')]");
                        if (productNodes == null || productNodes.Count == 0)
                        {
                            Console.WriteLine($"CanadaComputersClient.GetPokemon: no product nodes found at {url}");
                            continue;
                        }

                        foreach (var node in productNodes)
                        {
                            var nameNode = node.SelectSingleNode(".//a[contains(@class,'product-title') or contains(@class,'product-name') or .//h2//a or .//h3//a]");
                            var priceNode = node.SelectSingleNode(".//span[contains(@class,'price') or contains(@class,'product-price')]");
                            var availabilityText = node.SelectSingleNode(".//small//b|.//span[contains(@class,'availability')]|.//*[contains(@class,'availability')]")?.InnerText ?? string.Empty;
                            var linkNode = node.SelectSingleNode(".//a[contains(@href,'/en/') or contains(@href,'/products/') or contains(@href,'/product/')]");

                            if (nameNode == null || linkNode == null) continue;

                            var name = HtmlEntity.DeEntitize(nameNode.InnerText).Trim();
                            var href = linkNode.GetAttributeValue("href", "").Trim();
                            var urlResolved = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : "https://www.canadacomputers.com" + href;
                            var price = priceNode != null ? HtmlEntity.DeEntitize(priceNode.InnerText).Trim() : string.Empty;

                            if (!availabilityText.Contains("NOT AVAILABLE FOR ORDER", StringComparison.OrdinalIgnoreCase)
                                && !availabilityText.Contains("Out of stock", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(name))
                            {
                                results.Add(new Product(name, price, urlResolved));
                            }
                        }

                        // If we found results on this URL prefer them and stop trying others.
                        if (results.Count > 0) break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CanadaComputersClient.GetPokemon: error fetching {url}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CanadaComputersClient.GetPokemon: top-level error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("CanadaComputersClient.GetPokemon: END");
            }

            return results.Count > 0
                ? new List<Search> { new Search("Pokemon", "CanadaComputers", results) }
                : new List<Search>();
        }
    }
}