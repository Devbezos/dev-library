using Microsoft.Playwright;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DevClient.Clients;

public sealed record RxpGuidesPost(string Text, string[] Links, string LastEdited, string? F2hUrl);

public sealed record RxpGuidesArchive(string FileName, string Sha256, long SizeBytes);

public sealed record F2hDownloadForm(string Action, int WaitSeconds);

// Reads the first post of the RXPGuides OwnedCore thread and downloads the archive its f2h.io
// link points at, so the bot can tell when a new version has been uploaded.
public sealed class RxpGuidesClient
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private const string FirstPostSelector = "div[id^='post_message_']";
    private const int PageTimeoutMs = 60_000;

    private static readonly Regex F2hLinkRegex = new(@"https?://(?:www\.)?f2h\.io/[A-Za-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FormActionRegex = new(@"<form[^>]*id='myform'[^>]*action='([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WaitSecondsRegex = new(@"var\s+maxSec\s*=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex DirectFileRegex = new(@"href=""(https?://[^""]+/files/[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<RxpGuidesPost> GetFirstPost(string threadUrl, CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();
        // The forum sits behind a Cloudflare managed challenge that headless Chromium never
        // clears; a headed window does. Park it off-screen so it doesn't pop up on the desktop.
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--window-position=-32000,-32000",
            ]
        });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
        });
        var page = await context.NewPageAsync();
        await page.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");

        await page.GotoAsync(threadUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = PageTimeoutMs });

        // The challenge page navigates to the real thread once it passes, which can destroy the
        // execution context mid-read; retry the read rather than failing on that race.
        Exception? lastError = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await page.WaitForSelectorAsync(FirstPostSelector, new PageWaitForSelectorOptions { Timeout = PageTimeoutMs });
                var raw = await page.EvaluateAsync<RawPost>("""
                    () => {
                        const post = document.querySelector("div[id^='post_message_']");
                        const edited = document.querySelector('blockquote.postcontent.lastedited');
                        return {
                            text: post ? post.innerText : '',
                            links: post ? [...post.querySelectorAll('a')].map(a => a.href) : [],
                            lastEdited: edited ? edited.innerText : ''
                        };
                    }
                    """);

                var text = raw.Text.Trim();
                var lastEdited = Regex.Replace(raw.LastEdited, @"\s+", " ").Trim();
                return new RxpGuidesPost(text, raw.Links, lastEdited, FindF2hLink(raw.Links, text));
            }
            catch (PlaywrightException ex)
            {
                lastError = ex;
                await Task.Delay(1000, cancellationToken);
            }
        }

        var title = await page.TitleAsync();
        throw new InvalidOperationException($"Could not read the first post (page title \"{title}\"): {lastError?.Message}", lastError);
    }

    public async Task<RxpGuidesArchive> DownloadArchive(string f2hUrl, CancellationToken cancellationToken = default)
    {
        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var filePage = await http.GetStringAsync(f2hUrl, cancellationToken);
        var form = ParseDownloadForm(filePage)
            ?? throw new InvalidOperationException($"No download form on {f2hUrl}; the file may have been removed");

        // Honour the page's countdown so the request flow looks like a normal visitor's.
        await Task.Delay(TimeSpan.FromSeconds(form.WaitSeconds + 1), cancellationToken);

        using var thanksRequest = new HttpRequestMessage(HttpMethod.Post, form.Action)
        {
            Content = new FormUrlEncodedContent([]),
        };
        thanksRequest.Headers.Referrer = new Uri(f2hUrl);
        using var thanksResponse = await http.SendAsync(thanksRequest, cancellationToken);
        thanksResponse.EnsureSuccessStatusCode();
        var thanksPage = await thanksResponse.Content.ReadAsStringAsync(cancellationToken);
        var directUrl = ParseDirectFileUrl(thanksPage)
            ?? throw new InvalidOperationException("Could not find the direct file link on the f2h download page");

        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, directUrl);
        fileRequest.Headers.Referrer = new Uri(form.Action);
        using var fileResponse = await http.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        fileResponse.EnsureSuccessStatusCode();

        var disposition = fileResponse.Content.Headers.ContentDisposition;
        var fileName = (disposition?.FileNameStar ?? disposition?.FileName ?? Path.GetFileName(new Uri(directUrl).LocalPath)).Trim('"');

        using var sha = SHA256.Create();
        await using var stream = await fileResponse.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        long size = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            size += read;
        }
        sha.TransformFinalBlock([], 0, 0);

        if (size == 0)
            throw new InvalidOperationException($"f2h returned an empty file for {f2hUrl}");

        return new RxpGuidesArchive(fileName, Convert.ToHexString(sha.Hash!).ToLowerInvariant(), size);
    }

    public static string? FindF2hLink(IEnumerable<string> links, string postText)
    {
        foreach (var candidate in links.Append(postText))
        {
            var match = F2hLinkRegex.Match(candidate ?? string.Empty);
            if (match.Success)
                return match.Value;
        }

        return null;
    }

    public static F2hDownloadForm? ParseDownloadForm(string html)
    {
        var action = FormActionRegex.Match(html);
        if (!action.Success)
            return null;

        var wait = WaitSecondsRegex.Match(html);
        var waitSeconds = wait.Success ? int.Parse(wait.Groups[1].Value) : 10;
        return new F2hDownloadForm(WebUtility.HtmlDecode(action.Groups[1].Value), waitSeconds);
    }

    public static string? ParseDirectFileUrl(string html)
    {
        var match = DirectFileRegex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private sealed class RawPost
    {
        public string Text { get; set; } = string.Empty;
        public string[] Links { get; set; } = [];
        public string LastEdited { get; set; } = string.Empty;
    }
}
