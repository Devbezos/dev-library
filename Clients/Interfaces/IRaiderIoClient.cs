using DevClient.Data.WoW;

namespace DevClient.Clients
{
    public interface IRaiderIoClient
    {
        Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy);
    }
}





