using MySqlConnector;

namespace dev_library.Data.Discord
{
    public record AppChannelEntry(string GuildName, ulong ChannelId);

    public record TrackedApplication(ulong MessageId, ulong ChannelId, ulong ArchiveCategoryId, ulong[] DenyUserIds, string GuildName);

    public static class ApplicationChannelCache
    {
        public static string ConnectionString { get; set; } = "Server=localhost;Port=3306;Database=dev_bot;Uid=root;Pwd=;";

        public static void EnsureTable()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_channels (
                    channel_id BIGINT UNSIGNED NOT NULL,
                    guild_name VARCHAR(255) NOT NULL,
                    PRIMARY KEY (channel_id)
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public static List<AppChannelEntry> Load()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT channel_id, guild_name FROM app_channels";
            using var reader = cmd.ExecuteReader();
            var result = new List<AppChannelEntry>();
            while (reader.Read())
                result.Add(new AppChannelEntry(reader.GetString("guild_name"), reader.GetUInt64("channel_id")));
            return result;
        }

        public static void Add(string guildName, ulong channelId)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_channels (channel_id, guild_name) VALUES (@channelId, @guildName)
                ON DUPLICATE KEY UPDATE guild_name = @guildName
                """;
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.Parameters.AddWithValue("@guildName", guildName);
            cmd.ExecuteNonQuery();
        }

        public static void Remove(ulong channelId)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM app_channels WHERE channel_id = @channelId";
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.ExecuteNonQuery();
        }
    }
}
