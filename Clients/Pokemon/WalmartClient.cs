using dev_library.Data;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Serilog;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{
    public class WalmartClient
    {
        private static readonly ILogger Logger = Log.ForContext<WalmartClient>();
        private const string DefaultPokemonUrl = "https://www.walmart.ca/en/browse/toys/trading-cards/pokemon-cards/10011_31745_6000204969672?facet=fulfillment_method%3ADelivery%7C%7Cretailer_type%3AWalmart";
        private const string WalmartBaseUrl = "https://www.walmart.ca";
        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;
        private readonly PlaywrightBrowser? _browser;

        public WalmartClient(ITcgSourceUrlRepository? sourceUrlRepo = null, PlaywrightBrowser? browser = null)
        {
            _sourceUrlRepo = sourceUrlRepo;
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
            Logger.Information("GetPokemon: START");
            var allProducts = new List<Product>();
            var sourceUrls = _sourceUrlRepo?
                .GetAll("pokemon", "Walmart", enabledOnly: true)
                .Select(u => (u.Url, u.Category))
                .ToList();

            if (sourceUrls == null || sourceUrls.Count == 0)
                sourceUrls = [(DefaultPokemonUrl, "Pokemon Cards")];

            await browser.WithPageAsync(async page =>
            {
                foreach (var source in sourceUrls)
                {
                    try
                    {
                        var products = await GetProductsFromPage(page, source.Url);
                        allProducts.AddRange(products);
                        Logger.Information("GetPokemon: {Category} found {Count} products", source.Category, products.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "GetPokemon: Error fetching {Category}", source.Category);
                    }
                }

                return true;
            });

            Logger.Information("GetPokemon: END - {Count} total products", allProducts.Count);
            return allProducts.Count > 0
                ? new List<Search> { new Search("Pokemon", "Walmart", allProducts.DistinctBy(p => p.Url).ToList()) }
                : new List<Search>();
        }

        private static async Task<List<Product>> GetProductsFromPage(IPage page, string url)
        {
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            if (response != null && response.Status >= 400)
            {
                Logger.Warning("GetPokemon: page returned HTTP {Status}", response.Status);
                return [];
            }

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

            var content = await page.ContentAsync();
            if (IsBotCheckPage(content))
            {
                Logger.Warning("GetPokemon: Walmart bot-check page detected for {Url}; attempting challenge", url);
                await TrySolvePerimeterXChallenge(page);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });
                content = await page.ContentAsync();

                if (IsBotCheckPage(content))
                {
                    Logger.Warning("GetPokemon: Walmart bot-check challenge not solved for {Url}", url);
                    return [];
                }
            }

            try
            {
                await page.WaitForSelectorAsync(
                    "[data-automation-id='product-title'], [data-testid='product-title'], a[href*='/en/ip/']",
                    new PageWaitForSelectorOptions { Timeout = 20000 });
            }
            catch (TimeoutException)
            {
                Logger.Information("GetPokemon: No product tiles found");
                return [];
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var productNodes = doc.DocumentNode.SelectNodes("//*[@role='group' and @data-item-id]");
            var fallbackLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/en/ip/') or contains(@href, '/ip/')]");
            if ((productNodes == null || productNodes.Count == 0) && (fallbackLinks == null || fallbackLinks.Count == 0))
                return [];

            var products = new List<Product>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (productNodes != null)
            {
                foreach (var tile in productNodes)
                {
                    if (IsSoldOut(tile)) continue;

                    var link = tile.SelectSingleNode(".//a[contains(@href, '/en/ip/') or contains(@href, '/ip/')]");
                    if (link == null) continue;

                    var href = link.GetAttributeValue("href", "");
                    var productUrl = NormalizeUrl(href);
                    if (string.IsNullOrWhiteSpace(href) || !seenUrls.Add(productUrl)) continue;

                    var name = GetText(tile, ".//*[@data-automation-id='product-title']")
                        ?? GetText(tile, ".//*[@data-testid='product-title']")
                        ?? link.GetAttributeValue("aria-label", "").Trim()
                        ?? link.GetAttributeValue("title", "").Trim();
                    var price = GetText(tile, ".//*[@data-automation-id='product-price']//*[@aria-hidden='true' and contains(., '$')]")
                        ?? GetText(tile, ".//*[@data-automation-id='product-price']")
                        ?? GetText(tile, ".//*[@data-testid='product-price']")
                        ?? GetText(tile, ".//*[contains(@class, 'price')]");

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(price)) continue;
                    if (!IsPokemonProduct(name)) continue;

                    products.Add(new Product(name, CleanPrice(price), productUrl));
                }
            }

            if (products.Count == 0 && fallbackLinks != null)
            {
                foreach (var link in fallbackLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    var productUrl = NormalizeUrl(href);
                    if (string.IsNullOrWhiteSpace(href) || !seenUrls.Add(productUrl)) continue;

                    var card = link.SelectSingleNode("ancestor::*[@role='group' or self::article or contains(@class, 'tile') or contains(@class, 'product')][1]") ?? link.ParentNode;
                    if (card == null || IsSoldOut(card)) continue;

                    var name = link.GetAttributeValue("aria-label", "").Trim();
                    if (string.IsNullOrWhiteSpace(name)) name = link.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(name)) name = GetText(card, ".//*[@data-automation-id='product-title']") ?? GetText(card, ".//*[@data-testid='product-title']") ?? link.InnerText.Trim();

                    var rawBlockText = HtmlEntity.DeEntitize(card.InnerText);
                    var priceMatch = Regex.Match(rawBlockText, @"\$\s*\d+(?:,\d{3})*(?:\.\d{2})?");
                    var price = priceMatch.Success ? priceMatch.Value : string.Empty;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(price)) continue;
                    if (!IsPokemonProduct(name)) continue;

                    products.Add(new Product(name, CleanPrice(price), productUrl));
                }
            }

            Logger.Information("GetPokemon: Parsed {Count} Walmart product(s)", products.Count);
            return products;
        }

        private static bool IsBotCheckPage(string content) =>
            content.Contains("We like real shoppers, not robots!", StringComparison.OrdinalIgnoreCase)
            || content.Contains("px-captcha", StringComparison.OrdinalIgnoreCase)
            || content.Contains("verify yourself", StringComparison.OrdinalIgnoreCase);

        private static async Task TrySolvePerimeterXChallenge(IPage page)
        {
            try
            {
                var captcha = page.Locator("#px-captcha").First;
                await captcha.WaitForAsync(new LocatorWaitForOptions { Timeout = 12000, State = WaitForSelectorState.Visible });

                var box = await captcha.BoundingBoxAsync();
                if (box == null || box.Width <= 1 || box.Height <= 1) return;

                var x = box.X + (box.Width / 2);
                var y = box.Y + (box.Height / 2);

                await page.Mouse.MoveAsync(x, y);
                await page.Mouse.DownAsync();
                await page.WaitForTimeoutAsync(9000);
                await page.Mouse.UpAsync();
                await page.WaitForTimeoutAsync(2000);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "GetPokemon: Failed to perform Walmart captcha hold action");
            }
        }

        private static bool IsSoldOut(HtmlNode tile)
        {
            var hasAddToCart = tile.SelectSingleNode(".//*[@data-automation-id='add-to-cart']") != null;
            if (hasAddToCart) return false;

            var text = HtmlEntity.DeEntitize(tile.InnerText);
            return text.Contains("out of stock", StringComparison.OrdinalIgnoreCase)
                || text.Contains("sold out", StringComparison.OrdinalIgnoreCase)
                || text.Contains("not available", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetText(HtmlNode node, string xpath)
        {
            var text = node.SelectSingleNode(xpath)?.InnerText;
            if (string.IsNullOrWhiteSpace(text)) return null;
            return HtmlEntity.DeEntitize(text).Trim();
        }

        private static string NormalizeUrl(string href)
        {
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
            if (href.StartsWith("//", StringComparison.Ordinal)) return "https:" + href;
            return WalmartBaseUrl + (href.StartsWith("/", StringComparison.Ordinal) ? href : "/" + href);
        }

        private static string CleanPrice(string price)
        {
            var cleaned = HtmlEntity.DeEntitize(price)
                .Replace("current price", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Now", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var match = Regex.Match(cleaned, @"\$\s*\d+(?:,\d{3})*(?:\.\d{2})?");
            if (match.Success)
                return Regex.Replace(match.Value, @"\s+", "");

            return cleaned.StartsWith("$", StringComparison.Ordinal) ? cleaned : "$" + cleaned.TrimStart('$');
        }

        private static bool IsPokemonProduct(string name)
        {
            var normalized = RemoveDiacritics(HtmlEntity.DeEntitize(name)).ToLowerInvariant();
            return normalized.Contains("pokemon", StringComparison.Ordinal)
                || normalized.Contains("pok mon", StringComparison.Ordinal)
                || normalized.Contains("pokamon", StringComparison.Ordinal);
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
