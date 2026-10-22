namespace DevClient.Data;

// Single-row settings + last-seen state for the RXPGuides watcher: the bot reads the first
// post of ThreadUrl, follows its f2h.io link, hashes the archive and posts to ChannelId when
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
