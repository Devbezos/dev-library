namespace DevClient.Clients
{
    /// <summary>
    /// One HttpClient for the clients that call third-party APIs with per-request credentials (Google Health, Google
    /// Calendar, Warcraft Logs). A client per call leaves a socket behind each time on a long-running bot; this one
    /// pools connections and recycles them every few minutes so DNS changes are still picked up. Credentials go on the
    /// HttpRequestMessage, never on DefaultRequestHeaders, because the client is shared.
    /// </summary>
    public static class SharedHttp
    {
        public static HttpClient Client { get; } = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

        /// <summary>A request carrying a bearer token (and JSON accept header) for <see cref="Client"/>.</summary>
        public static HttpRequestMessage Bearer(HttpMethod method, string url, string token, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url) { Content = content };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }
    }
}
