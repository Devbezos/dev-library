using dev_library.Data;
using Microsoft.Playwright;
using Serilog;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{
    public class EBGamesClient
    {
        private static readonly ILogger Logger = Log.ForContext<EBGamesClient>();
        private const string DefaultPokemonUrl = "https://www.ebgames.ca/SearchResult/Quicksearch?productType=5&q=pok%C3%A9mon&shippingMethod=1&variantType=1&page={0}";
        private readonly ITcgSourceUrlRepository? _sourceUrlRepo;
        private readonly PlaywrightBrowser? _browser;

        public EBGamesClient(PlaywrightBrowser? browser = null)
            : this(null, browser)
        {
        }

        public EBGamesClient(ITcgSourceUrlRepository? sourceUrlRepo, PlaywrightBrowser? browser = null)
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
            Logger.Information("GetProducts: START");
            var sourceUrls = _sourceUrlRepo?
                .GetAll("pokemon", "EBGames", enabledOnly: true)
                .Select(u => (u.Url, u.Category))
                .ToList();

            if (_sourceUrlRepo != null && sourceUrls is { Count: 0 })
                return [];

            if (sourceUrls == null || sourceUrls.Count == 0)
                sourceUrls = [(DefaultPokemonUrl, "Pokemon Search")];

            var results = await browser.WithPageAsync(async page =>
            {
                var allProducts = new List<Product>();
                foreach (var source in sourceUrls)
                {
                    try
                    {
                        var products = await GetProductsFromSource(page, source.Url);
                        allProducts.AddRange(products);
                        Logger.Information("GetProducts: {Category} found {Count} product(s)", source.Category, products.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "GetProducts: Error fetching {Category}", source.Category);
                    }
                }

                return allProducts
                    .GroupBy(p => p.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }, "en-CA,en-GB;q=0.9,en-US;q=0.8,en;q=0.7", blockStylesheets: true);

            Logger.Information("GetProducts: END - {Count} total products", results.Count);
            return results.Count > 0
                ? new List<Search> { new Search("Pokemon", "EBGames", results) }
                : new List<Search>();
        }

        private static async Task<List<Product>> GetProductsFromSource(IPage page, string sourceUrl)
        {
            var allProducts = new List<Product>();
            var isPaged = sourceUrl.Contains("{0}", StringComparison.Ordinal);

            foreach (var url in BuildPageUrls(sourceUrl).Take(10))
            {
                var pageProducts = await GetProductsFromPage(page, url);
                if (pageProducts.Count == 0 && isPaged)
                    break;

                allProducts.AddRange(pageProducts);

                if (!isPaged)
                    break;
            }

            return allProducts
                .GroupBy(p => p.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static IEnumerable<string> BuildPageUrls(string sourceUrl)
        {
            if (!sourceUrl.Contains("{0}", StringComparison.Ordinal))
            {
                yield return sourceUrl;
                yield break;
            }

            for (var page = 1; page <= 10; page++)
                yield return string.Format(CultureInfo.InvariantCulture, sourceUrl, page);
        }

        private static async Task<List<Product>> GetProductsFromPage(IPage page, string url)
        {
            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            if (response != null && response.Status >= 400)
            {
                Logger.Warning("GetProducts: search returned HTTP {Status} for {Url}", response.Status, url);
                return [];
            }

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
            }
            catch (TimeoutException)
            {
                Logger.Information("GetProducts: network idle timed out; parsing current DOM");
            }

            try
            {
                await LoadMoreResults(page);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "GetProducts: failed while loading additional results; parsing current DOM");
            }

            var candidates = await page.EvaluateAsync<ProductCandidate[]>(
                """
                () => {
                    const priceRegex = /\$\s?\d+(?:,\d{3})*(?:\.\d{2})?/;
                    const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                    const fold = value => normalize(value)
                        .normalize('NFD')
                        .replace(/[\u0300-\u036f]/g, '')
                        .toLowerCase();
                    const isPokemon = value => {
                        const text = fold(value);
                        return text.includes('pokemon') || text.includes('pok mon');
                    };
                    const isTcg = value => /trading cards|trading card game|\btcg\b|booster|deck|binder|collection|poster collection|portfolio|tin|etb|elite trainer|checklane/i.test(value);
                    const isUnavailable = value => /out of stock|sold out|not available|unavailable/i.test(value);

                    const productLinks = [...document.querySelectorAll('a[href]')]
                        .map(anchor => {
                            const name = normalize(anchor.innerText || anchor.textContent || anchor.getAttribute('title') || anchor.getAttribute('aria-label'));
                            const href = anchor.href;
                            if (!name || !href || !isPokemon(name)) return null;

                            let node = anchor;
                            let cardText = name;
                            for (let i = 0; i < 8 && node; i++) {
                                node = node.parentElement;
                                const text = normalize(node?.innerText || node?.textContent);
                                if (text && text.includes(name) && (priceRegex.test(text) || isTcg(text))) {
                                    cardText = text;
                                    break;
                                }
                            }

                            const combined = `${cardText} ${name} ${href}`;
                            if (!isTcg(combined) || isUnavailable(combined)) return null;

                            const price = cardText.match(priceRegex)?.[0]?.replace(/\s+/g, '') || '';
                            if (!price) return null;
                            return { name, price, url: href };
                        })
                        .filter(Boolean);

                    return [...new Map(productLinks.map(product => [`${product.name}|${product.url}`, product])).values()];
                }
                """);

            var products = new List<Product>();
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.Name)
                    || string.IsNullOrWhiteSpace(candidate.Price)
                    || string.IsNullOrWhiteSpace(candidate.Url))
                {
                    continue;
                }

                var name = candidate.Name.Trim();
                if (!IsPokemonProduct(name)) continue;

                products.Add(new Product(name, candidate.Price.Trim(), candidate.Url.Trim()));
            }

            Logger.Information("GetProducts: Parsed {Count} product(s) from {Url}", products.Count, url);
            return products;
        }

        private static async Task LoadMoreResults(IPage page)
        {
            const int maxClicks = 10;
            for (var i = 0; i < maxClicks; i++)
            {
                var button = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("show more|load more", RegexOptions.IgnoreCase) });
                if (await button.CountAsync() == 0 || !await button.First.IsVisibleAsync())
                    return;

                await button.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
                await Task.Delay(500);
            }
        }

        private static bool IsPokemonProduct(string name)
        {
            var normalized = RemoveDiacritics(name).ToLowerInvariant();
            return normalized.Contains("pokemon", StringComparison.Ordinal)
                || normalized.Contains("pok mon", StringComparison.Ordinal);
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

        private sealed class ProductCandidate
        {
            public string Name { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }
    }
}
