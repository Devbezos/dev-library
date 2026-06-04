using DevClient.Data.WoW.WoWUtils;

namespace DevClient.Clients
{
    public interface IWoWUtilsClient
    {
        Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId);
        Task<WoWUtilsImportResponse> ImportDroptimizer(string groupId, string characterSlug, WoWUtilsFetchResponse report, string reportId, string sessionCookie);
        string GetCharacterSlug(WoWUtilsFetchResponse report);
    }
}





