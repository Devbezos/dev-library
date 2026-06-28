using DevClient.Data.WoW.WoWUtils;

namespace DevClient.Clients
{
    public interface IWoWUtilsClient
    {
        Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId);
        Task<WoWUtilsImportResponse> ImportDroptimizer(string? groupId, string reportUrlOrId, string apiKey, string? profileKey = null);
        Task<IReadOnlyList<WoWUtilsRosterMember>> GetRosterMembers(string groupId, string apiKey);
        string GetCharacterSlug(WoWUtilsFetchResponse report);
    }
}
