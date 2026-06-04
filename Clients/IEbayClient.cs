namespace DevClient.Clients
{
    public interface IEbayClient
    {
        // Returns (avgPrice, sampleCount, currency)
        Task<(decimal? avgPrice, int count, string currency)> GetAveragePriceForQueryAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);
    }
}





