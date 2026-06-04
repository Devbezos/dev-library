
namespace DevClient.Data
{
    public interface ITcgMessageStateRepository
    {
        void EnsureTable();
        ulong[] GetMessageIds(ulong channelId);
        void SetMessageIds(ulong channelId, ulong[] messageIds);
    }
}



