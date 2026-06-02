using dev_library.Data;
using Microsoft.Playwright;
using Serilog;
using System.Text.RegularExpressions;

namespace dev_library.Clients
{
    public class EBGamesClient
    {
        private static readonly ILogger Logger = Log.ForContext<EBGamesClient>();
        private const string EBGamesUrl = "https://www.ebgames.ca/SearchResult/QuickSearch?platform=361&q=pokemon&rootGenre=99&shippingMethod=1";
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

            var results = await browser.WithPageAsync(async page =>
            {
                var allProducts = new List<Product>();
                try
                {
                    var response = await page.GotoAsync(EBGamesUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });

                    if (response != null && response.Status >= 400)
                    {
                        Logger.Warning("GetProducts: search returned HTTP {Status}", response.Status);
                        return allProducts;
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
                            const priceRegex = /\$\s?\d+(?:\.\d{2})?/;
                            const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                            const productLinks = [...document.querySelectorAll('a[href]')]
                                .map(anchor => {
                                    const name = normalize(anchor.innerText || anchor.textContent);
                                    const href = anchor.href;
                                    if (!name || !href) return null;
                                    if (!/pokemon|pokémon/i.test(name)) return null;

                                    let node = anchor;
                                    let cardText = name;
                                    for (let i = 0; i < 7 && node; i++) {
                                        node = node.parentElement;
                                        const text = normalize(node?.innerText || node?.textContent);
                                        if (text && text.includes(name) && priceRegex.test(text)) {
                                            cardText = text;
                                            break;
                                        }
                                    }

                                    if (!/trading cards|card game|booster|deck|binder|collection|poster collection|portfolio|tin|etb|elite trainer/i.test(cardText + ' ' + name + ' ' + href)) return null;

                                    const price = cardText.match(priceRegex)?.[0]?.replace(/\s+/g, '') || '';
                                    if (!price) return null;
                                    return { name, price, url: href };
                                })
                                .filter(Boolean);

                            return [...new Map(productLinks.map(product => [`${product.name}|${product.url}`, product])).values()];
                        }
                        """);

                    foreach (var candidate in candidates)
                    {
                        if (string.IsNullOrWhiteSpace(candidate.Name)
                            || string.IsNullOrWhiteSpace(candidate.Price)
                            || string.IsNullOrWhiteSpace(candidate.Url))
                        {
                            continue;
                        }

                        allProducts.Add(new Product(candidate.Name.Trim(), candidate.Price.Trim(), candidate.Url.Trim()));
                    }

                    Logger.Information("GetProducts: Parsed {Count} product(s)", allProducts.Count);
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

        private static async Task LoadMoreResults(IPage page)
        {
            const int maxClicks = 10;
            for (var i = 0; i < maxClicks; i++)
            {
                var button = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("show more", RegexOptions.IgnoreCase) });
                if (await button.CountAsync() == 0 || !await button.First.IsVisibleAsync())
                    return;

                await button.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
                await Task.Delay(500);
            }
        }

        private sealed class ProductCandidate
        {
            public string Name { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }
    }
}
