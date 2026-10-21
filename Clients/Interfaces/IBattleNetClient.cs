namespace DevClient.Clients
{
    /// <summary>A realm from Battle.net's realm index.</summary>
    public sealed record BattleNetRealm(string Slug, string Name);

    /// <summary>A realm's name and its connected realm's status, "UP" or "DOWN".</summary>
    public sealed record BattleNetRealmStatus(string Name, string Status);

    public interface IBattleNetClient
    {
        /// <summary>The realm's current status. Throws when Battle.net can't be reached or doesn't know the realm.</summary>
        Task<BattleNetRealmStatus> GetRealmStatus(string region, string realmSlug);
        /// <summary>Every realm in the region, sorted by name; cached for a few hours.</summary>
        Task<IReadOnlyList<BattleNetRealm>> GetRealms(string region);
        Task<string> GetItemName(string itemId);
    }
}
