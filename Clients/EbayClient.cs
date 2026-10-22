using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Text;

namespace DevClient.Clients
{
    public class EbayClient : IEbayClient
    {
        private readonly RestClient _http;
        private readonly IConfiguration _config;
        private readonly SemaphoreSlim _tokenLock = new(1,1);
        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public EbayClient(IConfiguration config, RestClient? http = null)
        {
            _http = http ?? SharedHttp.Client;
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

                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                var req = new RestRequest(TokenUrl, Method.Post)
                    .AddHeader("Authorization", $"Basic {auth}")
                    .AddParameter("grant_type", "client_credentials")
                    .AddParameter("scope", _config.GetValue<string>("eBay:Scopes") ?? "https://api.ebay.com/oauth/api_scope");
                var res = await _http.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                    return;
                var doc = JObject.Parse(res.Content ?? string.Empty);
                if (doc["access_token"] is { } at)
                {
                    _accessToken = at.Value<string>();
                    if (doc["expires_in"] is { } ex)
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(ex.Value<int>());
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
            var apiBase = _config.GetValue<bool>("eBay:UseSandbox", false)
                ? "https://api.sandbox.ebay.com/buy/browse/v1/item_summary/search"
                : "https://api.ebay.com/buy/browse/v1/item_summary/search";

            var url = $"{apiBase}?q={Uri.EscapeDataString(query)}&limit={maxResults}";
            var res = await _http.SendAsync(new RestRequest(url).AddHeader("Authorization", $"Bearer {_accessToken}"), cancellationToken);
            if (!res.IsSuccessStatusCode)
                return (null, 0, "USD");

            var doc = JObject.Parse(res.Content ?? string.Empty);
            if (doc["itemSummaries"] is not JArray items)
                return (null, 0, "USD");

            var prices = new List<decimal>();
            string currency = "USD";
            foreach (var item in items)
            {
                if (item["price"] is JObject priceEl && priceEl["value"] is { } val)
                {
                    if (decimal.TryParse(val.Value<string>(), out var d))
                    {
                        prices.Add(d);
                    }
                    if (priceEl["currency"] is { } curEl)
                        currency = curEl.Value<string>() ?? currency;
                }
            }

            if (prices.Count == 0)
                return (null, 0, currency);
            var avg = prices.Average();
            return (Math.Round(avg, 2), prices.Count, currency);
        }
    }
}





