using System.Text.Json;
using System.Text.RegularExpressions;
using DevClient.Clients;

namespace DevClient.Data;

/// <summary>The jobs the home PC scrapes for the bot (the dev-scrape tool's argument); each has one agent alias, "tcg-" + the job.</summary>
public static class TcgScrapeJobs
{
    public const string Pokemon = "pokemon";
    public const string Gundam = "gundam";
    public const string PokemonPreorder = "pokemon-preorder";
    public const string GundamPreorder = "gundam-preorder";
    public const string PokemonCenterSecurity = "pokecenter-security";

    public static readonly string[] All = [Pokemon, Gundam, PokemonPreorder, GundamPreorder, PokemonCenterSecurity];

    public static bool IsKnown(string? job) => job != null && All.Contains(job, StringComparer.Ordinal);

    public static string AgentAlias(string job) => "tcg-" + job;
}

public sealed class TcgScrapeProduct
{
    public string Name { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    /// <summary>The plain store URL (Product.Url holds a markdown link built from name, price and this).</summary>
    public string Url { get; set; } = string.Empty;
}

public sealed class TcgScrapeSearch
{
    public string Keyword { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public List<TcgScrapeProduct> Products { get; set; } = [];
}

/// <summary>What the Pokemon Center queue/captcha check saw (the fields of <see cref="PokemonCenterSecuritySnapshot"/>).</summary>
public sealed class TcgScrapeSnapshot
{
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string FinalUrl { get; set; } = string.Empty;
    public bool QueueDetected { get; set; }
    public bool CaptchaDetected { get; set; }
    public string[] Markers { get; set; } = [];
    public string ServerHeader { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public static TcgScrapeSnapshot From(PokemonCenterSecuritySnapshot s) => new()
    {
        Url = s.Url, StatusCode = s.StatusCode, FinalUrl = s.FinalUrl, QueueDetected = s.QueueDetected,
        CaptchaDetected = s.CaptchaDetected, Markers = s.Markers, ServerHeader = s.ServerHeader,
        Fingerprint = s.Fingerprint, Summary = s.Summary,
    };

    public PokemonCenterSecuritySnapshot ToSnapshot() =>
        new(Url, StatusCode, FinalUrl, QueueDetected, CaptchaDetected, Markers, ServerHeader, Fingerprint, Summary);
}

/// <summary>
/// One scrape handed from the home PC to the API (PUT /api/agent/tcg/results) and from the API to the bot. Product jobs carry
/// <see cref="Searches"/>; the security job carries <see cref="Snapshot"/>. <see cref="FailedStores"/> names the stores
/// (as they appear in <see cref="Search.Store"/>) whose scraper threw, so the bot keeps their last known products.
/// </summary>
public sealed class TcgScrapePayload
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Job { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; }
    public List<TcgScrapeSearch> Searches { get; set; } = [];
    public List<string> FailedStores { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public TcgScrapeSnapshot? Snapshot { get; set; }

    public int ProductCount => Searches.Sum(s => s.Products.Count);

    public static TcgScrapePayload? Parse(string json) => JsonSerializer.Deserialize<TcgScrapePayload>(json, JsonOptions);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static List<TcgScrapeSearch> FromSearches(IEnumerable<Search> searches) =>
        searches.Select(s => new TcgScrapeSearch
        {
            Keyword = s.Keyword,
            Store = s.Store,
            Products = s.Products.Select(p => new TcgScrapeProduct { Name = p.Name, Price = p.Price, Url = RawUrl(p) }).ToList(),
        }).ToList();

    /// <summary>Rebuilds the scrapers' own types; empty result sets are dropped like the bot always did.</summary>
    public List<Search> ToSearches() =>
        Searches
            .Where(s => s.Products.Count > 0)
            .Select(s => new Search(s.Keyword, s.Store, s.Products.Select(p => new Product(p.Name, p.Price, p.Url)).ToList()))
            .ToList();

    /// <summary>The URL inside <c>Product.Url</c> ("[name](url) price"), without the markdown.</summary>
    public static string RawUrl(Product product)
    {
        var prefix = $"[{product.Name}](";
        var suffix = $") {product.Price}\n";
        var value = product.Url;
        if (value.Length >= prefix.Length + suffix.Length && value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(suffix, StringComparison.Ordinal))
            return value[prefix.Length..^suffix.Length];
        var match = Regex.Match(value, @"\[.*?\]\((https?://[^)]+)\)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : value;
    }
}

/// <summary>The newest upload for a job, as stored. <see cref="Json"/> is a <see cref="TcgScrapePayload"/>.</summary>
public sealed record TcgScrapeResult(string Job, DateTime ScrapedAtUtc, DateTime ReceivedAtUtc, string Json, string[] Errors, int ProductCount);

/// <summary>A job's newest upload without its (large) JSON, for status pages.</summary>
public sealed record TcgScrapeSummary(string Job, DateTime ScrapedAtUtc, DateTime ReceivedAtUtc, string[] Errors, int ProductCount);

public interface ITcgScrapeResultRepository
{
    /// <summary>The job's newest upload, or null when the home agent has never delivered one.</summary>
    TcgScrapeResult? Get(string job);
    List<TcgScrapeSummary> GetSummaries();
    /// <summary>Replaces the job's stored upload; only the newest is kept.</summary>
    void Save(TcgScrapeResult result);
}

/// <summary>The bot's small TCG state blobs (seen products, last posted sets, alert message ids), one JSON value per key.</summary>
public interface ITcgBotStateRepository
{
    /// <summary>The key's stored value, or null when there is none.</summary>
    string? Get(string key);
    void Set(string key, string value);
}
