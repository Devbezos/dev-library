using MySqlConnector;

namespace dev_library.Data.Discord
{
    public record AppChannelEntry(string GuildName, ulong ChannelId, string ChannelName = "", bool IsDeleted = false);

    public record TrackedApplication(ulong MessageId, ulong ChannelId, ulong ArchiveCategoryId, ulong[] DenyUserIds, string GuildName, string ChannelName = "");

    public class SqlClient : IAppChannelRepository
    {
        private readonly string _connectionString;

        public SqlClient(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var dbName = builder.Database;
            builder.Database = string.Empty;

            using var adminConn = new MySqlConnection(builder.ConnectionString);
            adminConn.Open();
            using var createDb = adminConn.CreateCommand();
            createDb.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}`";
            createDb.ExecuteNonQuery();

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_channels (
                    channel_id   BIGINT UNSIGNED NOT NULL,
                    guild_name   VARCHAR(255)    NOT NULL,
                    channel_name VARCHAR(255)    NOT NULL DEFAULT '',
                    is_deleted   TINYINT(1)      NOT NULL DEFAULT 0,
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

            // Migrate: add is_deleted column if missing
            using (var migrate = conn.CreateCommand())
            {
                migrate.CommandText = """
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'app_channels'
                      AND COLUMN_NAME  = 'is_deleted'
                    """;
                if (Convert.ToInt64(migrate.ExecuteScalar()) == 0)
                {
                    migrate.CommandText = "ALTER TABLE app_channels ADD COLUMN is_deleted TINYINT(1) NOT NULL DEFAULT 0";
                    migrate.ExecuteNonQuery();
                }
            }

        }

        public List<AppChannelEntry> Load()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT channel_id, guild_name, channel_name FROM app_channels WHERE is_deleted = 0";
            using var reader = cmd.ExecuteReader();
            var result = new List<AppChannelEntry>();
            while (reader.Read())
                result.Add(new AppChannelEntry(reader.GetString("guild_name"), reader.GetUInt64("channel_id"), reader.GetString("channel_name")));
            return result;
        }

        public List<AppChannelEntry> LoadAllIncludingDeleted()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT channel_id, guild_name, channel_name, is_deleted FROM app_channels";
            using var reader = cmd.ExecuteReader();
            var result = new List<AppChannelEntry>();
            while (reader.Read())
                result.Add(new AppChannelEntry(
                    reader.GetString("guild_name"),
                    reader.GetUInt64("channel_id"),
                    reader.GetString("channel_name"),
                    reader.GetBoolean("is_deleted")));
            return result;
        }

        public void Add(string guildName, ulong channelId, string channelName = "")
        {
            using var conn = new MySqlConnection(_connectionString);
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

        public void Remove(ulong channelId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE app_channels SET is_deleted = 1 WHERE channel_id = @channelId";
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.ExecuteNonQuery();
        }
    }
}
