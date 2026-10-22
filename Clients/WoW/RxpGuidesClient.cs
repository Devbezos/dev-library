using Microsoft.Playwright;
using RestSharp;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DevClient.Clients;

public sealed record RxpGuidesPost(string Text, string[] Links, string LastEdited, string? F2hUrl);

public sealed record RxpGuidesArchive(string FileName, string Sha256, long SizeBytes, string FilePath);

public sealed record F2hDownloadForm(string Action, int WaitSeconds);

// Reads the first post of the RXPGuides OwnedCore thread and downloads the archive its f2h.io
// link points at, so the bot can tell when a new version has been uploaded.
public sealed class RxpGuidesClient
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private static readonly RestClient ArchiveClient = SharedHttp.Create(new HttpClientHandler
    {
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
    }, TimeSpan.FromMinutes(5));
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

    // Saves the archive into destinationDirectory (the caller owns and cleans it up).
    public async Task<RxpGuidesArchive> DownloadArchive(string f2hUrl, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        var cookies = new CookieContainer();
        RestRequest Request(string url, Method method = Method.Get) =>
            new RestRequest(url, method) { CookieContainer = cookies }.AddHeader("User-Agent", UserAgent);
        var filePage = (await ArchiveClient.SendAsync(Request(f2hUrl), cancellationToken)).EnsureSuccessStatusCode().Content ?? string.Empty;
        var form = ParseDownloadForm(filePage)
            ?? throw new InvalidOperationException($"No download form on {f2hUrl}; the file may have been removed");

        // Honour the page's countdown so the request flow looks like a normal visitor's.
        await Task.Delay(TimeSpan.FromSeconds(form.WaitSeconds + 1), cancellationToken);

        var thanksRequest = Request(form.Action, Method.Post)
            .AddHeader("Referer", f2hUrl)
            .AddStringBody(string.Empty, "application/x-www-form-urlencoded");
        var thanksResponse = await ArchiveClient.SendAsync(thanksRequest, cancellationToken);
        thanksResponse.EnsureSuccessStatusCode();
        var thanksPage = thanksResponse.Content ?? string.Empty;
        var directUrl = ParseDirectFileUrl(thanksPage)
            ?? throw new InvalidOperationException("Could not find the direct file link on the f2h download page");

        string? fileName = null;
        string? filePath = null;
        long size = 0;
        using var sha = SHA256.Create();
        var fileRequest = Request(directUrl).AddHeader("Referer", form.Action);
        fileRequest.CompletionOption = HttpCompletionOption.ResponseHeadersRead;
        fileRequest.AdvancedResponseWriter = (response, request) =>
        {
            response.EnsureSuccessStatusCode();
            var disposition = response.Content.Headers.ContentDisposition;
            fileName = Path.GetFileName((disposition?.FileNameStar ?? disposition?.FileName ?? new Uri(directUrl).LocalPath).Trim('"'));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "RXPGuides.rar";
            Directory.CreateDirectory(destinationDirectory);
            filePath = Path.Combine(destinationDirectory, fileName);
            using var stream = response.Content.ReadAsStream(cancellationToken);
            using var file = File.Create(filePath);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.ReadAsync(buffer, cancellationToken).AsTask().GetAwaiter().GetResult()) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
                file.Write(buffer, 0, read);
                size += read;
            }
            return new RestResponse(request) { StatusCode = response.StatusCode };
        };
        (await ArchiveClient.SendAsync(fileRequest, cancellationToken)).EnsureSuccessStatusCode();
        sha.TransformFinalBlock([], 0, 0);

        if (size == 0)
            throw new InvalidOperationException($"f2h returned an empty file for {f2hUrl}");

        return new RxpGuidesArchive(fileName!, Convert.ToHexString(sha.Hash!).ToLowerInvariant(), size, filePath!);
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
