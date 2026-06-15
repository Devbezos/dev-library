using DevClient.Data.WoW.WoWUtils;

namespace DevClient.Clients
{
    public interface IWoWUtilsClient
    {
        Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId);
        Task<WoWUtilsImportResponse> ImportDroptimizer(string? groupId, string reportUrlOrId, string apiKey, string? profileKey = null);
        string GetCharacterSlug(WoWUtilsFetchResponse report);
    }
}





