using DevClient.Data.WoW.WoWAudit;
using DevClient.Data.WoW;

namespace DevClient.Clients
{
    public interface IWoWAuditClient
    {
        Task<List<WoWAuditCharacter>> GetCharacters(string guild);
        Task<IReadOnlyList<RaidScheduleEvent>> GetRaidSchedule(string guild);
        Task<WoWAuditWishlistResponse> UpdateWishlist(string reportId, string guild);
        Task<WoWAuditCharacter> TrackCharacter(string guild, WoWAuditTrackCharacterRequest request);
        Task UpdateCharacter(string guild, int characterId, WoWAuditUpdateCharacterRequest request);
        Task UntrackCharacter(string guild, int characterId);
    }
}