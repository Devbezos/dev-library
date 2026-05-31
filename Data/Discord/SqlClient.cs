using dev_library.Data;
using dev_library.Data.Fitness;
using MySqlConnector;

namespace dev_library.Data.Discord
{
    public record AppChannelEntry(string GuildName, ulong ChannelId, string ChannelName = "");

    public record TrackedApplication(ulong MessageId, ulong ChannelId, ulong ArchiveCategoryId, ulong[] DenyUserIds, string GuildName, string ChannelName = "");

    public static class SqlClient
    {
        public static string ConnectionString { get; set; } = "Server=localhost;Port=3306;Database=dev_bot;Uid=root;Pwd=;";

        public static void EnsureTable()
        {
            var builder = new MySqlConnectionStringBuilder(ConnectionString);
            var dbName = builder.Database;
            builder.Database = string.Empty;

            using var adminConn = new MySqlConnection(builder.ConnectionString);
            adminConn.Open();
            using var createDb = adminConn.CreateCommand();
            createDb.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}`";
            createDb.ExecuteNonQuery();

            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_channels (
                    channel_id   BIGINT UNSIGNED NOT NULL,
                    guild_name   VARCHAR(255)    NOT NULL,
                    channel_name VARCHAR(255)    NOT NULL DEFAULT '',
                    PRIMARY KEY (channel_id)
                )
                """;
            cmd.ExecuteNonQuery();

            // Migrate: add channel_name column if missing
            using (var migrate = conn.CreateCommand())
            {
                migrate.CommandText = """
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'app_channels'
                      AND COLUMN_NAME  = 'channel_name'
                    """;
                if (Convert.ToInt64(migrate.ExecuteScalar()) == 0)
                {
                    migrate.CommandText = "ALTER TABLE app_channels ADD COLUMN channel_name VARCHAR(255) NOT NULL DEFAULT ''";
                    migrate.ExecuteNonQuery();
                }
            }

            GuildRepository.EnsureTable();
            FitnessRepository.EnsureTable();
            FitnessRepository.EnsureUsersTable(AppSettings.GoogleHealth);
            JobRepository.EnsureTable();
        }

        public static List<AppChannelEntry> Load()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT channel_id, guild_name, channel_name FROM app_channels";
            using var reader = cmd.ExecuteReader();
            var result = new List<AppChannelEntry>();
            while (reader.Read())
                result.Add(new AppChannelEntry(reader.GetString("guild_name"), reader.GetUInt64("channel_id"), reader.GetString("channel_name")));
            return result;
        }

        public static void Add(string guildName, ulong channelId, string channelName = "")
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_channels (channel_id, guild_name, channel_name) VALUES (@channelId, @guildName, @channelName)
                ON DUPLICATE KEY UPDATE guild_name = @guildName, channel_name = @channelName
                """;
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.Parameters.AddWithValue("@guildName", guildName);
            cmd.Parameters.AddWithValue("@channelName", channelName);
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
