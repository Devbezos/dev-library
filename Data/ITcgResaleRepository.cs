namespace DevClient.Data;

public interface ITcgResaleRepository
{
    void EnsureTable();
    void UpsertResaleValue(string itemName, decimal? avgPrice, int sampleCount, string currency, DateTime updatedAt, string updatedBy = "job");
    List<TcgResaleValue> GetLatest(int limit = 100);
}
