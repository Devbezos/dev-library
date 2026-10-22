using System.Runtime.ExceptionServices;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace DevClient.Clients
{
    /// <summary>
    /// One RestClient for the clients that call third-party APIs with per-request credentials (Google Health, Google
    /// Calendar, Warcraft Logs, WoW Audit, ...). A client per call leaves a socket behind each time on a long-running bot;
    /// this one pools connections and recycles them every few minutes so DNS changes are still picked up. Credentials go
    /// on the RestRequest, never on default headers, because the client is shared.
    /// </summary>
    public static class SharedHttp
    {
        public static RestClient Client { get; } = Create(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

        /// <summary>
        /// A long-lived RestClient over its own HttpClient for <paramref name="handler"/> (a proxy, cookies or TLS settings).
        /// Keep it in a static or singleton; never make one per call.
        /// </summary>
        public static RestClient Create(HttpMessageHandler handler, TimeSpan? timeout = null) =>
            Create(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, timeout, disposeHttpClient: true);

        /// <summary>
        /// A RestClient over <paramref name="http"/>. JSON bodies go through Newtonsoft with its default settings (the same
        /// as JsonConvert), and RestSharp adds no User-Agent or JSON/XML Accept list of its own, so requests look as they did
        /// with HttpClient. <paramref name="timeout"/> defaults to RestSharp's 100 seconds.
        /// </summary>
        public static RestClient Create(HttpClient http, TimeSpan? timeout = null, bool disposeHttpClient = false)
        {
            var client = new RestClient(http, new RestClientOptions { UserAgent = null, Timeout = timeout }, disposeHttpClient,
                config => config.UseNewtonsoftJson(new JsonSerializerSettings()));
            client.AcceptedContentTypes = ["*/*"];
            return client;
        }

        /// <summary>A request carrying a bearer token (and JSON accept header).</summary>
        public static RestRequest Bearer(Method method, string url, string token) =>
            new RestRequest(url, method)
                .AddHeader("Authorization", $"Bearer {token}")
                .AddHeader("Accept", "application/json");

        /// <summary>
        /// Executes the request the way HttpClient.SendAsync did: any HTTP response comes back (check IsSuccessStatusCode),
        /// while a network failure, timeout or cancellation is thrown instead of coming back as a status-0 response.
        /// </summary>
        public static async Task<RestResponse> SendAsync(this RestClient client, RestRequest request, CancellationToken cancellationToken = default)
        {
            var response = await client.ExecuteAsync(request, cancellationToken);
            if (response.StatusCode == 0)
                ExceptionDispatchInfo.Throw(response.ErrorException ?? new HttpRequestException(response.ErrorMessage ?? "The request failed."));
            return response;
        }

        /// <summary>Throws an HttpRequestException carrying the status code unless the response was a success.</summary>
        public static RestResponse EnsureSuccessStatusCode(this RestResponse response) =>
            response.IsSuccessStatusCode
                ? response
                : throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusDescription}).",
                    null, response.StatusCode);

        /// <summary>The body of a GET, throwing on failure the way HttpClient.GetStringAsync did.</summary>
        public static async Task<string> GetStringAsync(this RestClient client, string url, CancellationToken cancellationToken = default) =>
            (await client.SendAsync(new RestRequest(url), cancellationToken)).EnsureSuccessStatusCode().Content ?? string.Empty;
    }
}
