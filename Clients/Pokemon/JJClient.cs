using dev_library.Data;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Serilog;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{

    public class JJClient
    {
        private static readonly string jjSearchUrl = "https://shop.jjcards.com/search.asp?keyword=pokemon+booster+box&catid=";
        private static readonly string jjBaseUrl = "https://shop.jjcards.com";
        private static readonly string jjAddToCartUrl = "https://shop.jjcards.com/add_cart.asp?quick=1&item_id={0}&cat_id=0";

        public async Task<List<Search>> GetProducts()
        {
            Log.Information("JJClient.GetProducts: START");
            var searchList = new List<Search>();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "en-CA,en-US;q=0.9,en;q=0.8",
                }
            });

            var page = await context.NewPageAsync();

            // Abort heavy asset requests; HTML still includes product nodes.
            await page.RouteAsync("**/*", async route =>
            {
                var type = route.Request.ResourceType;
                if (type is "image" or "media" or "font")
                    await route.AbortAsync();
                else
                    await route.ContinueAsync();
            });

            try
            {
                var response = await page.GotoAsync(jjSearchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                if (response != null && response.Status >= 400)
                {
                    Log.Warning("JJClient.GetProducts: search page returned HTTP {Status}", response.Status);
                    return searchList;
                }

                string content = await page.ContentAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                var products = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-content')]");
                var inStockProducts = new List<Product>();

                if (products == null || products.Count == 0)
                {
                    Log.Information("JJClient.GetProducts: No products found");
                    return searchList;
                }

                foreach (var product in products)
                {
                    var nameNode = product.SelectSingleNode(".//a");
                    var priceNode = product.SelectSingleNode(".//span[contains(@class, 'price')]");
                    var availabilityNode = product.SelectSingleNode(".//span[contains(@class, 'availability')]");

                    if (nameNode == null || priceNode == null || availabilityNode == null) continue;

                    string productName = nameNode.InnerText.Trim();
                    var baseUrl = nameNode.Attributes["href"]?.Value ?? "";
                    var itemId = Regex.Match(baseUrl, @"_(\d+)\.html$");
                    var url = itemId.Success
                        ? string.Format(jjAddToCartUrl, itemId.Groups[1].Value)
                        : (baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseUrl : jjBaseUrl + baseUrl);
                    string productPrice = priceNode.InnerText.Trim();
                    string availability = availabilityNode.InnerText.Trim();

                    if (availability.Equals("IN STOCK.", StringComparison.OrdinalIgnoreCase))
                    {
                        inStockProducts.Add(new Product(productName, productPrice, url));
                    }
                }

                if (inStockProducts.Count > 0)
                {
                    searchList.Add(new Search("Pokemon", "JJ", inStockProducts));
                }

                Log.Information("JJClient.GetProducts: Found {Total} products with {InStock} in stock", products.Count, inStockProducts.Count);
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