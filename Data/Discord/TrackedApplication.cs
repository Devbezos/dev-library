namespace DevClient.Data.Discord;

public record TrackedApplication(
    ulong MessageId,
    ulong ChannelId,
    ulong ArchiveCategoryId,
    ulong[] DenyUserIds,
    string GuildName,
    string ChannelName = "");
