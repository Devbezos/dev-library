
namespace DevClient.Data
{
    public interface ITcgMessageStateRepository
    {
        ulong[] GetMessageIds(ulong channelId);
        void SetMessageIds(ulong channelId, ulong[] messageIds);
    }
}



