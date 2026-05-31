using dev_library.Data;
using HtmlAgilityPack;
using Serilog;

namespace dev_library.Clients
{

    public class ChimeraClient
    {
        private static readonly HttpClient client = new();
        private static readonly string ChimeraSearchUrl = "https://chimeragamingonline.com/search?options%5Bprefix%5D=last&type=product&q={0}";
        private static readonly string ChimeraBaseUrl = "https://chimeragamingonline.com";
        private static readonly List<string> Keywords = new()
        {
            "Prismatic Evolutions Booster",
            "Obsidian Flames Booster",
            "Surging Sparks Booster",
            "Journey Together Booster",
            "Stellar Crown Booster",
            "Shrouded Fable Booster",
            "Twilight Masquerade Booster",
            "Temportal Forces Booster"
        };

        public async Task<List<Search>> GetPokemon()
        {
            Log.Information("ChimeraClient.GetProducts: START");
            var searchList = new List<Search>();
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                foreach (var keyword in Keywords)
                {
                    var content = await client.GetStringAsync(string.Format(ChimeraSearchUrl, keyword));
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'grid-view-item__link')]");
                    var inStockProducts = new List<Product>();

                    if (products == null || products.Count == 0)
                    {
                        Log.Information("No {Keyword} found", keyword);
                        continue;
                    }

                    foreach (var product in products)
                    {
                        var name = product.SelectSingleNode(".//div[@class='h4 grid-view-item__title']").InnerText.Trim();
                        var price = product.SelectSingleNode(".//span[contains(@class, 'product-price__price') and contains(@class, 'is-bold') and contains(@class, 'qv-regularprice')]").InnerText.Trim();
                        var url = ChimeraBaseUrl + product.SelectSingleNode(".//a[contains(@href, '/products/')]").GetAttributeValue("href", "");
                        var availability = product.SelectSingleNode(".//span[@class='value']");
                        var isSoldOut = availability.InnerText.Trim().ToUpper().Contains("SOLD OUT");

                        if (name.ToUpper().Contains("POKEMON") && double.Parse(price[1..]) > 1 && !isSoldOut)
                        {
                            inStockProducts.Add(new Product(name, price, url));
                        }
                    }

                    if (inStockProducts.Count > 0)
                    {
                        searchList.Add(new Search(keyword, "Chimera", inStockProducts));
                    }

                    Log.Information("ChimeraClient.GetProducts: Found {Total} {Keyword} products with {InStock} in stock", products.Count, keyword, inStockProducts.Count);
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChimeraClient.GetProducts: Error fetching webpage");
            }
            finally
            {
                Log.Information("ChimeraClient.GetProducts: END");
            }
            return searchList;
        }
    }
}