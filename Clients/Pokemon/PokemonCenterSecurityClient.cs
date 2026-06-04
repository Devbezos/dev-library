using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DevClient.Clients;

public sealed record PokemonCenterSecuritySnapshot(
    string Url,
    int StatusCode,
    string FinalUrl,
    bool QueueDetected,
    bool CaptchaDetected,
    string[] Markers,
    string ServerHeader,
    string Fingerprint,
    string Summary);

public sealed class PokemonCenterSecurityClient
{
    private static readonly Uri Url = new("https://www.pokemoncenter.com/en-ca");
    private static readonly HttpClient Client = CreateClient();

    private static readonly string[] MarkerPatterns =
    [
        "queue-it",
        "queueit",
        "waiting room",
        "waitingroom",
        "_incapsula_resource",
        "imperva",
        "waiting room | pok",
        "psyduck_waitingroom",
        "cwudnsai",
        "swwrgts",
        "perimeterx",
        "px-captcha",
        "captcha",
        "capcha",
        "datadome",
        "access denied",
        "bot detection",
        "blocked"
    ];

    public async Task<PokemonCenterSecuritySnapshot> GetSnapshot(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-CA,en;q=0.9");

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? Url.ToString();
        var combined = $"{finalUrl}\n{HeadersToString(response)}\n{content}";

        var markers = MarkerPatterns
            .Where(pattern => combined.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        var queueDetected = markers.Any(m =>
            m.Equals("queue-it", StringComparison.OrdinalIgnoreCase) ||
            m.Equals("queueit", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("waiting", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("psyduck_waitingroom", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("cwudnsai", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("swwrgts", StringComparison.OrdinalIgnoreCase));
        var captchaDetected = markers.Any(m =>
            m.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("capcha", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("perimeter", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("_incapsula_resource", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("imperva", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("datadome", StringComparison.OrdinalIgnoreCase));

        var serverHeader = string.Join(", ", response.Headers.Server.Select(x => x.ToString()));
        var normalizedTitle = ExtractTitle(content);
        var fingerprintText = string.Join('\n',
            (int)response.StatusCode,
            finalUrl,
            serverHeader,
            string.Join(",", markers),
            normalizedTitle);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintText))).ToLowerInvariant();

        var summary = BuildSummary((int)response.StatusCode, finalUrl, queueDetected, captchaDetected, markers, serverHeader, normalizedTitle);

        return new PokemonCenterSecuritySnapshot(
            Url.ToString(),
            (int)response.StatusCode,
            finalUrl,
            queueDetected,
            captchaDetected,
            markers,
            serverHeader,
            fingerprint,
            summary);
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private static string HeadersToString(HttpResponseMessage response)
    {
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}");
        return string.Join('\n', headers);
    }

    private static string ExtractTitle(string content)
    {
        var match = Regex.Match(content, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success
            ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim())
            : string.Empty;
    }

    private static string BuildSummary(
        int statusCode,
        string finalUrl,
        bool queueDetected,
        bool captchaDetected,
        string[] markers,
        string serverHeader,
        string title)
    {
        var lines = new List<string>
        {
            $"Status: {statusCode}",
            $"Final URL: {finalUrl}",
            $"Queue detected: {(queueDetected ? "yes" : "no")}",
            $"Captcha/security challenge detected: {(captchaDetected ? "yes" : "no")}",
            $"Markers: {(markers.Length == 0 ? "none" : string.Join(", ", markers))}",
        };

        if (!string.IsNullOrWhiteSpace(serverHeader))
            lines.Add($"Server: {serverHeader}");
        if (!string.IsNullOrWhiteSpace(title))
            lines.Add($"Title: {title}");

        return string.Join('\n', lines);
    }
}





