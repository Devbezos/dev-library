using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWUtils;

namespace DevClient.Clients
{
    public interface IWoWUtilsClient
    {
        Task<WoWUtilsFetchResponse> GetDroptimizerReport(string reportId);
        Task<WoWUtilsImportResponse> ImportDroptimizer(string? groupId, string reportUrlOrId, string apiKey, string? profileKey = null);
        Task<IReadOnlyList<WoWUtilsRosterMember>> GetRosterMembers(string groupId, string apiKey);
        Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string groupId, string apiKey);
        /// <summary>Declared absences from GET /v1/groups/{groupId}/absences (unverified path; may throw a 404 <see cref="WoWUtilsApiException"/>).</summary>
        Task<IReadOnlyList<WoWUtilsAbsence>> GetAbsences(string groupId, string apiKey);
        string GetCharacterSlug(WoWUtilsFetchResponse report);
    }
}