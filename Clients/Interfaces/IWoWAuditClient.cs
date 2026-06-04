using DevClient.Data.WoW.WoWAudit;
using DevClient.Data.WoW;

namespace DevClient.Clients
{
    public interface IWoWAuditClient
    {
        Task<List<WoWAuditCharacter>> GetCharacters(string guild);
        Task<WoWAuditWishlistResponse> UpdateWishlist(string reportId, string guild);
    }
}





