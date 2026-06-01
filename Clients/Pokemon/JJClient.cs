using dev_library.Data;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Serilog;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{
    public class JJClient
    {
        private static readonly ILogger Logger = Log.ForContext<JJClient>();
        private const string JjSearchUrl = "https://shop.jjcards.com/search.asp?keyword=pokemon+booster+box&catid=";
        private const string JjBaseUrl = "https://shop.jjcards.com";
        private const string JjAddToCartUrl = "https://shop.jjcards.com/add_cart.asp?quick=1&item_id={0}&cat_id=0";
        private readonly PlaywrightBrowser? _browser;

        public JJClient(PlaywrightBrowser? browser = null)
        {
            _browser = browser;
        }

        public async Task<List<Search>> GetProducts()
        {
            if (_browser != null)
                return await GetProducts(_browser);

            await using var browser = await PlaywrightBrowser.CreateAsync();
            return await GetProducts(browser);
        }

        private async Task<List<Search>> GetProducts(PlaywrightBrowser browser)
        {
            Logger.Information("GetProducts: START");

            var inStockProducts = await browser.WithPageAsync(async page =>
            {
                var productsFound = 0;
                var products = new List<Product>();
                try
                {
                    var response = await page.GotoAsync(JjSearchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                    if (response != null && response.Status >= 400)
                    {
                        Logger.Warning("GetProducts: search page returned HTTP {Status}", response.Status);
                        return products;
                    }

                    var content = await page.ContentAsync();
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-content')]");
                    if (nodes == null || nodes.Count == 0)
                    {
                        Logger.Information("GetProducts: No products found");
                        return products;
                    }

                    productsFound = nodes.Count;
                    foreach (var product in nodes)
                    {
                        var nameNode = product.SelectSingleNode(".//a");
                        var priceNode = product.SelectSingleNode(".//span[contains(@class, 'price')]");
                        var availabilityNode = product.SelectSingleNode(".//span[contains(@class, 'availability')]");

                        if (nameNode == null || priceNode == null || availabilityNode == null) continue;

                        var productName = nameNode.InnerText.Trim();
                        var baseUrl = nameNode.Attributes["href"]?.Value ?? "";
                        var itemId = Regex.Match(baseUrl, @"_(\d+)\.html$");
                        var url = itemId.Success
                            ? string.Format(JjAddToCartUrl, itemId.Groups[1].Value)
                            : (baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseUrl : JjBaseUrl + baseUrl);
                        var productPrice = priceNode.InnerText.Trim();
                        var availability = availabilityNode.InnerText.Trim();

                        if (availability.Equals("IN STOCK.", StringComparison.OrdinalIgnoreCase))
                            products.Add(new Product(productName, productPrice, url));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "GetProducts: Error fetching webpage");
                }

                Logger.Information("GetProducts: Found {Total} products with {InStock} in stock", productsFound, products.Count);
                return products;
            });

            Logger.Information("GetProducts: END");
            return inStockProducts.Count > 0
                ? new List<Search> { new Search("Pokemon", "JJ", inStockProducts) }
                : new List<Search>();
        }
    }
}
