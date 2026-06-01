using dev_library.Data;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Serilog;
using System.Text.Json;

namespace dev_library.Clients
{
    public class EBGamesClient
    {
        private static readonly ILogger Logger = Log.ForContext<EBGamesClient>();
        private const string EBGamesUrl = "https://www.ebgames.ca/SearchResult/QuickSearch?q=Pok%C3%A9mon%20&platform=361&rootGenre=99&shippingMethod=1&release=1&page={0}";
        private const string EBGamesBaseUrl = "https://www.ebgames.ca";
        private readonly PlaywrightBrowser? _browser;

        public EBGamesClient(PlaywrightBrowser? browser = null)
        {
            _browser = browser;
        }

        public async Task<List<Search>> GetPokemon()
        {
            if (_browser != null)
                return await GetPokemon(_browser);

            await using var browser = await PlaywrightBrowser.CreateAsync();
            return await GetPokemon(browser);
        }

        private async Task<List<Search>> GetPokemon(PlaywrightBrowser browser)
        {
            Logger.Information("GetProducts: START");
            var allProducts = new List<Product>();

            var results = await browser.WithPageAsync(async page =>
            {
                try
                {
                    const int maxPages = 25;
                    for (int pageNum = 1; pageNum <= maxPages; pageNum++)
                    {
                        var url = string.Format(EBGamesUrl, pageNum);
                        string content;
                        if (pageNum == 1)
                        {
                            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                            if (response != null && response.Status >= 400)
                            {
                                Logger.Warning("GetProducts: page {Page} returned HTTP {Status}, stopping", pageNum, response.Status);
                                break;
                            }

                            try
                            {
                                await page.WaitForSelectorAsync("div.searchProductTile", new PageWaitForSelectorOptions { Timeout = 10000 });
                            }
                            catch (TimeoutException)
                            {
                                Logger.Information("GetProducts: No product tiles on page {Page}", pageNum);
                                break;
                            }

                            content = await page.ContentAsync();
                        }
                        else
                        {
                            content = await page.EvaluateAsync<string>("async pageUrl => { const response = await fetch(pageUrl, { credentials: 'include' }); if (!response.ok) return ''; return await response.text(); }", url);
                            if (string.IsNullOrWhiteSpace(content))
                            {
                                Logger.Warning("GetProducts: page {Page} returned an empty response, stopping after {Count} product(s)", pageNum, allProducts.Count);
                                break;
                            }
                        }

                        var doc = new HtmlDocument();
                        doc.LoadHtml(content);

                        var products = doc.DocumentNode.SelectNodes("//div[contains(@class,'searchProductTile') and contains(@class,'searchTileLayout')]");
                        if (products == null || products.Count == 0)
                        {
                            Logger.Information("GetProducts: No products on page {Page}, stopping after {Count} product(s)", pageNum, allProducts.Count);
                            break;
                        }

                        foreach (var product in products)
                        {
                            var dataJson = HtmlEntity.DeEntitize(product.GetAttributeValue("data-product", ""));
                            if (string.IsNullOrEmpty(dataJson)) continue;

                            string name, price;
                            try
                            {
                                var arr = JsonSerializer.Deserialize<JsonElement[]>(dataJson);
                                if (arr == null || arr.Length == 0) continue;
                                name = arr[0].GetProperty("name").GetString()?.Trim() ?? "";
                                price = "$" + arr[0].GetProperty("price").GetString()?.Trim();
                            }
                            catch
                            {
                                continue;
                            }

                            var linkNode = product.SelectSingleNode(".//h3[contains(@class,'searchProductTitle')]//a")
                                ?? product.SelectSingleNode(".//div[contains(@class,'searchProductImage')]//a");
                            if (linkNode == null) continue;

                            var href = linkNode.GetAttributeValue("href", "");
                            var productUrl = href.StartsWith("http") ? href : EBGamesBaseUrl + href;

                            allProducts.Add(new Product(name, price, productUrl));
                        }

                        Logger.Information("GetProducts: Page {Page} - {Count} products", pageNum, products.Count);
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "GetProducts: Error fetching page");
                }

                return allProducts;
            }, "en-CA,en-GB;q=0.9,en-US;q=0.8,en;q=0.7", blockStylesheets: true);

            Logger.Information("GetProducts: END - {Count} total products", results.Count);
            return results.Count > 0
                ? new List<Search> { new Search("Pokemon", "EBGames", results) }
                : new List<Search>();
        }
    }
}
