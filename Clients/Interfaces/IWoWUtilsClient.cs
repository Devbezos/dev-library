using dev_library.Data.WoW.WoWUtils;

namespace dev_refined.Clients
{
    public interface IWoWUtilsClient
    {
        Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId);
        Task<WoWUtilsImportResponse> ImportDroptimizer(string groupId, string characterSlug, WoWUtilsFetchResponse report, string reportId, string sessionCookie);
        string GetCharacterSlug(WoWUtilsFetchResponse report);
    }
}
