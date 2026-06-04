using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DevClient.Clients
{
    public class EbayClient : IEbayClient
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly SemaphoreSlim _tokenLock = new(1,1);
        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public EbayClient(IHttpClientFactory http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        private string TokenUrl => _config.GetValue<bool>("eBay:UseSandbox", false)
            ? "https://api.sandbox.ebay.com/identity/v1/oauth2/token"
            : "https://api.ebay.com/identity/v1/oauth2/token";

        private async Task EnsureTokenAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(1))
                return;

            await _tokenLock.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(1))
                    return;

                var clientId = _config["eBay:ClientId"];
                var clientSecret = _config["eBay:ClientSecret"];
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                    return; // not configured

                var http = _http.CreateClient();
                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                var body = new Dictionary<string,string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = _config.GetValue<string>("eBay:Scopes") ?? "https://api.ebay.com/oauth/api_scope"
                };
                var content = new FormUrlEncodedContent(body);
                var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                req.Content = content;
                var res = await http.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                    return;
                using var s = await res.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("access_token", out var at))
                {
                    _accessToken = at.GetString();
                    if (doc.RootElement.TryGetProperty("expires_in", out var ex))
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(ex.GetInt32());
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task<(decimal? avgPrice, int count, string currency)> GetAveragePriceForQueryAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            await EnsureTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(_accessToken))
                return (null, 0, "USD");

            // Use Browse API search as an approximation (note: completed/sold items access may require different API)
            var http = _http.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var apiBase = _config.GetValue<bool>("eBay:UseSandbox", false)
                ? "https://api.sandbox.ebay.com/buy/browse/v1/item_summary/search"
                : "https://api.ebay.com/buy/browse/v1/item_summary/search";

            var url = $"{apiBase}?q={Uri.EscapeDataString(query)}&limit={maxResults}";
            var res = await http.GetAsync(url, cancellationToken);
            if (!res.IsSuccessStatusCode)
                return (null, 0, "USD");

            using var s = await res.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(s, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("itemSummaries", out var items))
                return (null, 0, "USD");

            var prices = new List<decimal>();
            string currency = "USD";
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("price", out var priceEl) && priceEl.TryGetProperty("value", out var val))
                {
                    if (decimal.TryParse(val.GetString(), out var d))
                    {
                        prices.Add(d);
                    }
                    if (priceEl.TryGetProperty("currency", out var curEl))
                        currency = curEl.GetString() ?? currency;
                }
            }

            if (prices.Count == 0)
                return (null, 0, currency);
            var avg = prices.Average();
            return (Math.Round(avg, 2), prices.Count, currency);
        }
    }
}





