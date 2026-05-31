using dev_library.Data.Discord;
using MySqlConnector;

namespace dev_library.Data.Fitness
{
    public class FitnessUser
    {
        public string Username  { get; set; } = string.Empty;
        public ulong  ChannelId { get; set; }
        public bool   Enabled   { get; set; }
    }

    public static class FitnessRepository
    {
        public static void EnsureTable()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS fitness_posts (
                    id        INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                    username  VARCHAR(255) NOT NULL,
                    post_type VARCHAR(10)  NOT NULL,
                    posted_at DATETIME     NOT NULL
                )
                """;
            cmd.ExecuteNonQuery();

            // fitness_users — create if not present (includes credential columns)
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS fitness_users (
                    username      VARCHAR(255)    NOT NULL PRIMARY KEY,
                    channel_id    BIGINT UNSIGNED NOT NULL DEFAULT 0,
                    enabled       TINYINT(1)      NOT NULL DEFAULT 1,
                    client_id     VARCHAR(255)    NOT NULL DEFAULT '',
                    client_secret VARCHAR(255)    NOT NULL DEFAULT '',
                    refresh_token VARCHAR(2048)   NOT NULL DEFAULT '',
                    is_deleted    TINYINT(1)      NOT NULL DEFAULT 0
                )
                """;
            cmd.ExecuteNonQuery();

            // Migration: add credential columns to existing tables that predate this change
            foreach (var (col, def) in new[]
            {
                ("client_id",          "VARCHAR(255)   NOT NULL DEFAULT ''"),
                ("client_secret",      "VARCHAR(255)   NOT NULL DEFAULT ''"),
                ("refresh_token",      "VARCHAR(2048)  NOT NULL DEFAULT ''"),
                ("is_deleted",         "TINYINT(1)     NOT NULL DEFAULT 0"),
                ("highest_weight_lbs", "DECIMAL(6,2)   NULL"),
            })
            {
                cmd.CommandText = $"""
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'fitness_users'
                      AND COLUMN_NAME  = '{col}'
                    """;
                if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                {
                    cmd.CommandText = $"ALTER TABLE fitness_users ADD COLUMN {col} {def}";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void LogPost(string username, string postType)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO fitness_posts (username, post_type, posted_at) VALUES (@username, @postType, @postedAt)";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@postType", postType);
            cmd.Parameters.AddWithValue("@postedAt", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Seeds fitness_users from appsettings GoogleHealth config (one-time migration).
        /// Credentials are only written to rows that currently have empty credentials.
        /// </summary>
        public static void EnsureUsersTable(GoogleHealthUserSettings[] users)
        {
            if (users.Length == 0) return;
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            foreach (var user in users)
            {
                using var seed = conn.CreateCommand();
                seed.CommandText = """
                    INSERT INTO fitness_users (username, channel_id, enabled, client_id, client_secret, refresh_token)
                    VALUES (@username, @channelId, 1, @clientId, @clientSecret, @refreshToken)
                    ON DUPLICATE KEY UPDATE
                        client_id     = IF(client_id     = '', @clientId,     client_id),
                        client_secret = IF(client_secret = '', @clientSecret, client_secret),
                        refresh_token = IF(refresh_token = '', @refreshToken, refresh_token)
                    """;
                seed.Parameters.AddWithValue("@username",     user.Username);
                seed.Parameters.AddWithValue("@channelId",    user.ChannelId);
                seed.Parameters.AddWithValue("@clientId",     user.ClientId);
                seed.Parameters.AddWithValue("@clientSecret", user.ClientSecret);
                seed.Parameters.AddWithValue("@refreshToken", user.RefreshToken);
                seed.ExecuteNonQuery();
            }
        }

        public static GoogleHealthUserSettings[] GetGoogleHealthSettings()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT username, channel_id, enabled, client_id, client_secret, refresh_token, highest_weight_lbs FROM fitness_users WHERE is_deleted = 0 ORDER BY username";
            using var reader = cmd.ExecuteReader();
            var result = new List<GoogleHealthUserSettings>();
            while (reader.Read())
                result.Add(new GoogleHealthUserSettings
                {
                    Username        = reader.GetString("username"),
                    ChannelId       = reader.GetUInt64("channel_id"),
                    Enabled         = reader.GetBoolean("enabled"),
                    ClientId        = reader.GetString("client_id"),
                    ClientSecret    = reader.GetString("client_secret"),
                    RefreshToken    = reader.GetString("refresh_token"),
                    HighestWeightLbs = reader.IsDBNull(reader.GetOrdinal("highest_weight_lbs")) ? null : reader.GetDouble("highest_weight_lbs"),
                });
            return [.. result];
        }

        public static GoogleHealthUserSettings[] GetGoogleHealthSettingsAll()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT username, channel_id, enabled, client_id, client_secret, refresh_token, is_deleted, highest_weight_lbs FROM fitness_users ORDER BY username";
            using var reader = cmd.ExecuteReader();
            var result = new List<GoogleHealthUserSettings>();
            while (reader.Read())
                result.Add(new GoogleHealthUserSettings
                {
                    Username         = reader.GetString("username"),
                    ChannelId        = reader.GetUInt64("channel_id"),
                    Enabled          = reader.GetBoolean("enabled"),
                    ClientId         = reader.GetString("client_id"),
                    ClientSecret     = reader.GetString("client_secret"),
                    RefreshToken     = reader.GetString("refresh_token"),
                    IsDeleted        = reader.GetBoolean("is_deleted"),
                    HighestWeightLbs = reader.IsDBNull(reader.GetOrdinal("highest_weight_lbs")) ? null : reader.GetDouble("highest_weight_lbs"),
                });
            return [.. result];
        }

        public static void UpsertFitnessUser(string username, ulong channelId, bool enabled,
            string clientId, string clientSecret, string refreshToken, double? highestWeightLbs = null)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO fitness_users (username, channel_id, enabled, client_id, client_secret, refresh_token, highest_weight_lbs)
                VALUES (@username, @channelId, @enabled, @clientId, @clientSecret, @refreshToken, @highestWeightLbs)
                ON DUPLICATE KEY UPDATE
                    channel_id         = @channelId,
                    enabled            = @enabled,
                    client_id          = @clientId,
                    client_secret      = @clientSecret,
                    refresh_token      = @refreshToken,
                    highest_weight_lbs = @highestWeightLbs,
                    is_deleted         = 0
                """;
            cmd.Parameters.AddWithValue("@username",         username);
            cmd.Parameters.AddWithValue("@channelId",        channelId);
            cmd.Parameters.AddWithValue("@enabled",          enabled);
            cmd.Parameters.AddWithValue("@clientId",         clientId);
            cmd.Parameters.AddWithValue("@clientSecret",     clientSecret);
            cmd.Parameters.AddWithValue("@refreshToken",     refreshToken);
            cmd.Parameters.AddWithValue("@highestWeightLbs", (object?)highestWeightLbs ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static List<FitnessUser> GetUsers()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT username, channel_id, enabled FROM fitness_users WHERE enabled = 1 AND is_deleted = 0";
            using var reader = cmd.ExecuteReader();
            var result = new List<FitnessUser>();
            while (reader.Read())
                result.Add(new FitnessUser
                {
                    Username  = reader.GetString("username"),
                    ChannelId = reader.GetUInt64("channel_id"),
                    Enabled   = reader.GetBoolean("enabled"),
                });
            return result;
        }

        public static List<FitnessUser> GetAllUsers()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT username, channel_id, enabled FROM fitness_users WHERE is_deleted = 0 ORDER BY username";
            using var reader = cmd.ExecuteReader();
            var result = new List<FitnessUser>();
            while (reader.Read())
                result.Add(new FitnessUser
                {
                    Username  = reader.GetString("username"),
                    ChannelId = reader.GetUInt64("channel_id"),
                    Enabled   = reader.GetBoolean("enabled"),
                });
            return result;
        }

        public static void UpdateUser(string username, ulong channelId, bool enabled)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO fitness_users (username, channel_id, enabled)
                VALUES (@username, @channelId, @enabled)
                ON DUPLICATE KEY UPDATE channel_id = @channelId, enabled = @enabled
                """;
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.Parameters.AddWithValue("@enabled", enabled);
            cmd.ExecuteNonQuery();
        }

        public static List<FitnessPost> GetRecentPosts(int limit = 50)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, post_type, posted_at FROM fitness_posts ORDER BY posted_at DESC LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            var result = new List<FitnessPost>();
            while (reader.Read())
                result.Add(new FitnessPost
                {
                    Id        = reader.GetInt32("id"),
                    Username  = reader.GetString("username"),
                    PostType  = reader.GetString("post_type"),
                    PostedAt  = reader.GetDateTime("posted_at"),
                });
            return result;
        }

        public static void DeletePost(int id)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM fitness_posts WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteUser(string username)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE fitness_users SET is_deleted = 1 WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateRefreshToken(string username, string refreshToken)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE fitness_users SET refresh_token = @refreshToken WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@refreshToken", refreshToken);
            cmd.ExecuteNonQuery();
        }
    }

    public class FitnessPost
    {
        public int      Id       { get; set; }
        public string   Username { get; set; } = string.Empty;
        public string   PostType { get; set; } = string.Empty;
        public DateTime PostedAt { get; set; }
    }
}
