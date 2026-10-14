namespace DevClient.Data;

// Single-row settings + last-seen state for the RXPGuides watcher: the bot reads the first
// post of ThreadUrl, follows its f2h.io link, hashes the archive and posts to ChannelId when
// the hash differs from Sha256.
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
    string? LastError);

public interface IRxpGuidesWatchRepository
{
    void EnsureTable(string defaultThreadUrl);
    RxpGuidesWatch Get();
    void UpdateSettings(string threadUrl, ulong channelId);
    void RecordChanged(string f2hUrl, string fileName, string sha256, long sizeBytes, string? postText, DateTime checkedAtUtc);
    void RecordChecked(DateTime checkedAtUtc, string? error);
}
