namespace DevClient.Clients
{
    public interface IBattleNetClient
    {
        Task<BlizzardRealmResponse> GetZuljinData();
        Task<string> GetItemName(string itemId);
    }
}





