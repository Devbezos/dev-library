using Microsoft.Playwright;

namespace dev_library.Clients
{
    public sealed class PlaywrightBrowser : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;

        private PlaywrightBrowser(IPlaywright playwright, IBrowser browser)
        {
            _playwright = playwright;
            _browser = browser;
        }

        public static async Task<PlaywrightBrowser> CreateAsync()
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });

            return new PlaywrightBrowser(playwright, browser);
        }

        public async Task<T> WithPageAsync<T>(
            Func<IPage, Task<T>> action,
            string acceptLanguage = "en-CA,en-US;q=0.9,en;q=0.8",
            bool blockStylesheets = false)
        {
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                Locale = "en-CA",
                ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = acceptLanguage,
                }
            });

            try
            {
                var page = await context.NewPageAsync();
                await page.RouteAsync("**/*", async route =>
                {
                    var type = route.Request.ResourceType;
                    if (type is "image" or "media" or "font" || (blockStylesheets && type == "stylesheet"))
                        await route.AbortAsync();
                    else
                        await route.ContinueAsync();
                });

                return await action(page);
            }
            finally
            {
                await context.CloseAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _browser.DisposeAsync();
            _playwright.Dispose();
        }
    }
}
