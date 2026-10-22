namespace DevClient.Data;

/// <summary>
/// A guild's server availability settings: which Battle.net realm to watch, where to post when its status changes
/// and which roles to ping when it comes back up. Enabled mirrors the guild's ServerAvailability feature flag.
/// </summary>
public sealed record GuildServerAvailability(
    string GuildName,
    bool Enabled,
    string? Region,
    string? RealmSlug,
    string? RealmName,
    string? ChannelId,
    string[] RoleIds);

/// <summary>Last known status ("UP" or "DOWN") of one realm, and when it last changed.</summary>
public sealed record RealmStatus(string Region, string RealmSlug, string RealmName, string Status, DateTime ChangedAtUtc);

public interface IServerAvailabilityRepository
{
    /// <summary>The guild's settings, or a disabled empty one when nothing is saved.</summary>
    GuildServerAvailability Get(string guildName);
    /// <summary>Saves the settings and sets the guild's ServerAvailability feature flag to Enabled.</summary>
    void Save(GuildServerAvailability settings);
    /// <summary>Enabled, fully set up settings of every active guild.</summary>
    List<GuildServerAvailability> GetMonitored();
    RealmStatus? GetStatus(string region, string realmSlug);
    void SaveStatus(RealmStatus status);
}

public static class ServerAvailabilityRegions
{
    /// <summary>Battle.net regions a guild can monitor.</summary>
    public static readonly string[] All = ["us", "eu", "kr", "tw"];
}
