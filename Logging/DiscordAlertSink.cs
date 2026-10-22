using DevClient.Clients;
using RestSharp;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace DevClient.Logging;

/// <summary>
/// Posts Error and Fatal log events to a Discord webhook (config <c>alerts:discordWebhookUrl</c>). Only the message text
/// (cut short, with anything token-like masked) and the exception's type are sent, never a stack trace. The same alert is
/// posted at most once per <see cref="DedupeWindow"/>, and at most <see cref="BurstLimit"/> alerts go out per
/// <see cref="BurstWindow"/>, so a failure loop can't flood the channel. Posting never blocks or throws into the caller.
/// </summary>
public sealed class DiscordAlertSink : ILogEventSink, IDisposable
{
    public static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BurstWindow = TimeSpan.FromMinutes(10);
    public const int BurstLimit = 10;
    /// <summary>Longest message text kept in an alert; Discord's own limit is 2000 characters for the whole post.</summary>
    public const int MaxMessageLength = 800;

    private readonly Uri _webhook;
    private readonly string _source;
    private readonly RestClient _http;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTimeOffset Last, int Repeats)> _seen = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _sent = new();
    private int _heldBack;
    private readonly Channel<string> _outbox = Channel.CreateBounded<string>(
        new BoundedChannelOptions(BurstLimit * 2) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly Task _pump;

    public DiscordAlertSink(Uri webhook, string source, HttpMessageHandler? handler = null, Func<DateTimeOffset>? now = null)
    {
        _webhook = webhook;
        _source = source;
        _http = SharedHttp.Create(handler ?? new SocketsHttpHandler(), TimeSpan.FromSeconds(10));
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _pump = Task.Run(PumpAsync);
    }

    /// <summary>A sink for this webhook, or null when it's empty or not an https URL (alerts off).</summary>
    public static DiscordAlertSink? Create(string? webhookUrl, string source) =>
        Uri.TryCreate(webhookUrl?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? new DiscordAlertSink(uri, source)
            : null;

    /// <summary>
    /// The webhook from config <c>alerts:discordWebhookUrl</c>, or the <c>alerts__discordWebhookUrl</c> environment
    /// variable (which wins, as it does in ASP.NET config).
    /// </summary>
    public static string? WebhookUrl(string? configured) =>
        Environment.GetEnvironmentVariable("alerts__discordWebhookUrl") is { Length: > 0 } env ? env : configured;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error) return;
        var context = logEvent.Properties.TryGetValue("SourceContext", out var sc) && sc is ScalarValue { Value: string s } ? s : null;
        var message = Clean(logEvent.RenderMessage());
        var key = $"{logEvent.Level}|{context}|{message}|{logEvent.Exception?.GetType().FullName}";
        if (!Admit(key, out var repeats, out var heldBack)) return;

        var text = new StringBuilder()
            .Append("**").Append(_source).Append("** ").Append(logEvent.Level == LogEventLevel.Fatal ? "FATAL" : "ERROR");
        if (context is not null) text.Append(" `").Append(context.Replace("`", "")).Append('`');
        text.Append('\n').Append(message);
        if (logEvent.Exception is { } ex) text.Append("\nException: `").Append(ex.GetType().FullName).Append('`');
        if (repeats > 0) text.Append($"\n(seen {repeats} more time(s) in the last {DedupeWindow.TotalMinutes:0} minutes)");
        if (heldBack > 0) text.Append($"\n({heldBack} other alert(s) were held back to avoid flooding)");
        _outbox.Writer.TryWrite(text.ToString());
    }

    /// <summary>Posts an info line (e.g. "started"); subject to the burst limit but not de-duplicated.</summary>
    public void Notify(string message)
    {
        if (Admit(null, out _, out _)) _outbox.Writer.TryWrite($"**{_source}** {Clean(message)}");
    }

    /// <summary>Waits briefly for queued alerts to go out, then stops.</summary>
    public void Dispose()
    {
        _outbox.Writer.TryComplete();
        try { _pump.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _http.Dispose();
    }

    private bool Admit(string? key, out int repeats, out int heldBack)
    {
        repeats = 0;
        heldBack = 0;
        lock (_gate)
        {
            var now = _now();
            if (key is not null && _seen.TryGetValue(key, out var seen) && now - seen.Last < DedupeWindow)
            {
                _seen[key] = (seen.Last, seen.Repeats + 1);
                return false;
            }
            while (_sent.Count > 0 && now - _sent.Peek() >= BurstWindow) _sent.Dequeue();
            if (_sent.Count >= BurstLimit)
            {
                _heldBack++;
                return false;
            }

            if (key is not null)
            {
                if (_seen.TryGetValue(key, out var previous)) repeats = previous.Repeats;
                _seen[key] = (now, 0);
                if (_seen.Count > 500)
                    foreach (var stale in _seen.Where(kv => now - kv.Value.Last >= DedupeWindow).Select(kv => kv.Key).ToList())
                        _seen.Remove(stale);
            }
            _sent.Enqueue(now);
            heldBack = _heldBack;
            _heldBack = 0;
            return true;
        }
    }

    private async Task PumpAsync()
    {
        await foreach (var content in _outbox.Reader.ReadAllAsync())
        {
            try
            {
                // No mentions: an alert quoting "@everyone" mustn't ping anyone.
                var response = await _http.SendAsync(new RestRequest(_webhook, Method.Post)
                    .AddJsonBody(new { content, allowed_mentions = new { parse = Array.Empty<string>() } }));
                if (!response.IsSuccessStatusCode)
                    SelfLog.WriteLine("Discord alert webhook answered {0}", (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                // Never logged through Serilog: a failing webhook would alert about itself.
                SelfLog.WriteLine("Discord alert webhook failed: {0}", ex.GetType().Name);
            }
        }
    }

    private static readonly Regex[] Secrets =
    [
        new(@"https?://(?:\w+\.)?discord(?:app)?\.com/api/webhooks/\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bBearer\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(?:password|pwd|secret|token|api[_-]?key|access[_-]?key|client[_-]?secret|authorization|refresh[_-]?token|access[_-]?token)\b\s*[=:]\s*[^\s;,&""']+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Discord bot tokens and JWTs (three dot-separated parts), then other long opaque strings (API keys).
        new(@"[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{4,}\.[A-Za-z0-9_\-]{20,}", RegexOptions.CultureInvariant),
        new(@"[A-Za-z0-9_\-+=]{32,}", RegexOptions.CultureInvariant),
    ];
    private static readonly Regex QueryString = new(@"(https?://[^\s?#]+)\?\S*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Masks anything token-like, drops URL query strings and code fences, and cuts the text short.</summary>
    public static string Clean(string message)
    {
        var text = QueryString.Replace(message, "$1?***");
        foreach (var pattern in Secrets) text = pattern.Replace(text, "***");
        text = text.Replace("```", "'''");
        return text.Length <= MaxMessageLength ? text : text[..MaxMessageLength] + "... (cut)";
    }
}
