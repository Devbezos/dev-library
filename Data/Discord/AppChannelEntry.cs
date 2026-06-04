namespace DevClient.Data.Discord;

public record AppChannelEntry(string GuildName, ulong ChannelId, string ChannelName = "", bool IsDeleted = false);
