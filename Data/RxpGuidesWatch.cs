namespace DevClient.Data;

// Single-row settings + last-seen state for the RXPGuides watcher: the home agent reads the first
// post of ThreadUrl, follows its f2h.io link and hashes the archive; the bot posts to ChannelId when
// the hash differs from Sha256. PinnedChannelId/PinnedMessageId track the bot's currently
// pinned update post so it can be unpinned when the next one goes up.
public sealed record RxpGuidesWatch(
    string ThreadUrl,
    ulong ChannelId,
    string? F2hUrl,
    string? FileName,
    string? Sha256,
    long? SizeBytes,
    string? PostText,
    DateTime? LastCheckedAtUtc,
    DateTime? LastChangedAtUtc,
    string? LastError,
    ulong PinnedChannelId = 0,
    ulong PinnedMessageId = 0);

public interface IRxpGuidesWatchRepository
{
    /// <summary>Adds the settings row with the configured thread URL if there isn't one yet.</summary>
    void EnsureDefault(string defaultThreadUrl);
    RxpGuidesWatch Get();
    void UpdateSettings(string threadUrl, ulong channelId);
    void RecordChanged(string f2hUrl, string fileName, string sha256, long sizeBytes, string? postText, DateTime checkedAtUtc);
    void RecordChecked(DateTime checkedAtUtc, string? error);
    // Forgets the last posted file (keeping the f2h link as a fallback) so the next check posts it again.
    void ResetLastSeen();
    // Pass 0/0 to clear.
    void RecordPinned(ulong channelId, ulong messageId);
}

// The newest archive the home agent handed over (single row), kept in MySQL because the bot has no volume.
public sealed record RxpGuidesArchive(
    string FileName,
    string Sha256,
    long SizeBytes,
    byte[] Content,
    string? F2hUrl,
    string? PostText,
    string? LastEdited,
    DateTime ReceivedAtUtc);

public interface IRxpGuidesArchiveRepository
{
    /// <summary>The stored archive including its bytes, or null when the agent hasn't delivered one yet.</summary>
    RxpGuidesArchive? Get();
    /// <summary>The stored archive's SHA-256 without loading its bytes, or null when none is stored.</summary>
    string? GetSha256();
    /// <summary>Replaces the stored archive; only the newest is kept.</summary>
    void Save(RxpGuidesArchive archive);
}
