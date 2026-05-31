using MySqlConnector;

namespace dev_library.Data.Discord
{
    public static class GuildRepository
    {
        // ── Schema ────────────────────────────────────────────────────────

        public static void EnsureTable()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();

            // Migrate: drop old JSON-blob schema if present
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'guilds'
                      AND COLUMN_NAME  = 'config'
                    """;
                if (Convert.ToInt64(cmd.ExecuteScalar()) > 0)
                    Exec(conn, "DROP TABLE IF EXISTS guilds");
            }

            // Migrate: add nickname column if missing
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'guilds'
                      AND COLUMN_NAME  = 'nickname'
                    """;
                if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                    Exec(conn, "ALTER TABLE guilds ADD COLUMN nickname VARCHAR(255) NOT NULL DEFAULT '' AFTER name");
            }

            Exec(conn, """
                CREATE TABLE IF NOT EXISTS guilds (
                    name                         VARCHAR(255) NOT NULL PRIMARY KEY,
                    nickname                     VARCHAR(255) NOT NULL DEFAULT '',
                    feature_droptimizer          TINYINT(1)   NOT NULL DEFAULT 0,
                    feature_droptimizer_reminder TINYINT(1)   NOT NULL DEFAULT 0,
                    feature_key_audit            TINYINT(1)   NOT NULL DEFAULT 0,
                    feature_server_availability  TINYINT(1)   NOT NULL DEFAULT 0,
                    drop_token    VARCHAR(500) NULL,
                    drop_start    DATE         NULL,
                    drop_end      DATE         NULL,
                    google_sheet_name  VARCHAR(255) NULL,
                    google_sheet_id    VARCHAR(255) NULL,
                    google_sheet_tab   VARCHAR(255) NULL,
                    google_sheet_creds VARCHAR(500) NULL,
                    app_sheet_id  VARCHAR(255) NULL,
                    app_sheet_tab VARCHAR(255) NULL,
                    is_deleted    TINYINT(1)   NOT NULL DEFAULT 0
                )
                """);

            // Migrate: add is_deleted column if missing
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'guilds'
                      AND COLUMN_NAME  = 'is_deleted'
                    """;
                if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                    Exec(conn, "ALTER TABLE guilds ADD COLUMN is_deleted TINYINT(1) NOT NULL DEFAULT 0");
            }

            Exec(conn, """
                CREATE TABLE IF NOT EXISTS guild_channels (
                    guild_name  VARCHAR(255)    NOT NULL,
                    channel_key VARCHAR(255)    NOT NULL,
                    channel_id  BIGINT UNSIGNED NOT NULL,
                    PRIMARY KEY (guild_name, channel_key),
                    FOREIGN KEY (guild_name) REFERENCES guilds(name) ON DELETE CASCADE
                )
                """);

            Exec(conn, """
                CREATE TABLE IF NOT EXISTS guild_roles (
                    guild_name VARCHAR(255) NOT NULL,
                    role_id    VARCHAR(255) NOT NULL,
                    PRIMARY KEY (guild_name, role_id),
                    FOREIGN KEY (guild_name) REFERENCES guilds(name) ON DELETE CASCADE
                )
                """);

            Exec(conn, """
                CREATE TABLE IF NOT EXISTS guild_deny_users (
                    guild_name VARCHAR(255)    NOT NULL,
                    user_id    BIGINT UNSIGNED NOT NULL,
                    PRIMARY KEY (guild_name, user_id),
                    FOREIGN KEY (guild_name) REFERENCES guilds(name) ON DELETE CASCADE
                )
                """);
        }

        // ── Read ──────────────────────────────────────────────────────────

        public static List<GuildSettingsDto> GetAll()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();

            var map = new Dictionary<string, GuildSettingsDto>(StringComparer.Ordinal);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM guilds WHERE is_deleted = 0 ORDER BY name";
                using var r = cmd.ExecuteReader();
                while (r.Read()) { var dto = ReadRow(r); map[dto.Name] = dto; }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, channel_key, channel_id FROM guild_channels";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (map.TryGetValue(r.GetString("guild_name"), out var dto))
                        dto.Channels[r.GetString("channel_key")] = r.GetUInt64("channel_id").ToString();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, role_id FROM guild_roles";
                var roles = new Dictionary<string, List<string>>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var gn = r.GetString("guild_name");
                    if (!roles.TryGetValue(gn, out var list)) roles[gn] = list = [];
                    list.Add(r.GetString("role_id"));
                }
                foreach (var (gn, list) in roles)
                    if (map.TryGetValue(gn, out var dto))
                        dto.RolesToPing = [.. list];
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, user_id FROM guild_deny_users";
                var denies = new Dictionary<string, List<string>>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var gn = r.GetString("guild_name");
                    if (!denies.TryGetValue(gn, out var list)) denies[gn] = list = [];
                    list.Add(r.GetUInt64("user_id").ToString());
                }
                foreach (var (gn, list) in denies)
                    if (map.TryGetValue(gn, out var dto))
                        dto.DenyUserIds = [.. list];
            }

            return [.. map.Values];
        }

        public static List<GuildSettingsDto> GetAllIncludingDeleted()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();

            var map = new Dictionary<string, GuildSettingsDto>(StringComparer.Ordinal);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM guilds ORDER BY name";
                using var r = cmd.ExecuteReader();
                while (r.Read()) { var dto = ReadRow(r); map[dto.Name] = dto; }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, channel_key, channel_id FROM guild_channels";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (map.TryGetValue(r.GetString("guild_name"), out var dto))
                        dto.Channels[r.GetString("channel_key")] = r.GetUInt64("channel_id").ToString();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, role_id FROM guild_roles";
                var roles = new Dictionary<string, List<string>>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var gn = r.GetString("guild_name");
                    if (!roles.TryGetValue(gn, out var list)) roles[gn] = list = [];
                    list.Add(r.GetString("role_id"));
                }
                foreach (var (gn, list) in roles)
                    if (map.TryGetValue(gn, out var dto))
                        dto.RolesToPing = [.. list];
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT guild_name, user_id FROM guild_deny_users";
                var denies = new Dictionary<string, List<string>>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var gn = r.GetString("guild_name");
                    if (!denies.TryGetValue(gn, out var list)) denies[gn] = list = [];
                    list.Add(r.GetUInt64("user_id").ToString());
                }
                foreach (var (gn, list) in denies)
                    if (map.TryGetValue(gn, out var dto))
                        dto.DenyUserIds = [.. list];
            }

            return [.. map.Values];
        }

        // ── Write ─────────────────────────────────────────────────────────

        public static void Upsert(GuildSettingsDto dto)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            UpsertRow(conn, tx, dto);

            foreach (var tbl in (string[])["guild_channels", "guild_roles", "guild_deny_users"])
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"DELETE FROM {tbl} WHERE guild_name = @name";
                cmd.Parameters.AddWithValue("@name", dto.Name);
                cmd.ExecuteNonQuery();
            }

            InsertChildren(conn, tx, dto);
            tx.Commit();
        }

        public static void Delete(string name)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE guilds SET is_deleted = 1 WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }

        /// Seeds guilds from appsettings. Skips guilds that already exist so the DB stays authoritative.
        public static void SyncFromSettings(GuildSettings[] settings)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            foreach (var guild in settings)
            {
                var dto = GuildSettingsDto.From(guild);
                using var tx = conn.BeginTransaction();
                long inserted = InsertGuildRow(conn, tx, dto, ignore: true);
                if (inserted > 0) InsertChildren(conn, tx, dto);
                tx.Commit();
            }
        }

        public static GuildSettings[] LoadAsGuildSettings() =>
            GetAll().Select(d => d.ToGuildSettings()).ToArray();

        // ── Helpers ───────────────────────────────────────────────────────

        private static void UpsertRow(MySqlConnection conn, MySqlTransaction tx, GuildSettingsDto dto)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO guilds
                    (name, nickname, feature_droptimizer, feature_droptimizer_reminder, feature_key_audit, feature_server_availability,
                     drop_token, drop_start, drop_end,
                     google_sheet_name, google_sheet_id, google_sheet_tab, google_sheet_creds,
                     app_sheet_id, app_sheet_tab)
                VALUES
                    (@name, @nickname, @fdrop, @fremind, @fkey, @favail,
                     @dtoken, @dstart, @dend,
                     @gsname, @gsid, @gstab, @gscreds,
                     @asid, @astab)
                ON DUPLICATE KEY UPDATE
                    nickname      = @nickname,
                    feature_droptimizer          = @fdrop,  feature_droptimizer_reminder = @fremind,
                    feature_key_audit            = @fkey,   feature_server_availability  = @favail,
                    drop_token    = @dtoken, drop_start    = @dstart, drop_end    = @dend,
                    google_sheet_name = @gsname, google_sheet_id = @gsid,
                    google_sheet_tab  = @gstab,  google_sheet_creds = @gscreds,
                    app_sheet_id  = @asid,   app_sheet_tab  = @astab,
                    is_deleted    = 0
                """;
            AddParams(cmd, dto);
            cmd.ExecuteNonQuery();
        }

        private static long InsertGuildRow(MySqlConnection conn, MySqlTransaction tx, GuildSettingsDto dto, bool ignore)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            var kw = ignore ? "INSERT IGNORE" : "INSERT";
            cmd.CommandText = $"""
                {kw} INTO guilds
                    (name, nickname, feature_droptimizer, feature_droptimizer_reminder, feature_key_audit, feature_server_availability,
                     drop_token, drop_start, drop_end,
                     google_sheet_name, google_sheet_id, google_sheet_tab, google_sheet_creds,
                     app_sheet_id, app_sheet_tab)
                VALUES
                    (@name, @nickname, @fdrop, @fremind, @fkey, @favail,
                     @dtoken, @dstart, @dend,
                     @gsname, @gsid, @gstab, @gscreds,
                     @asid, @astab)
                """;
            AddParams(cmd, dto);
            return cmd.ExecuteNonQuery();
        }

        private static void InsertChildren(MySqlConnection conn, MySqlTransaction tx, GuildSettingsDto dto)
        {
            foreach (var (key, val) in dto.Channels)
            {
                if (!ulong.TryParse(val, out var cid)) continue;
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO guild_channels (guild_name, channel_key, channel_id) VALUES (@gn, @k, @id)";
                cmd.Parameters.AddWithValue("@gn", dto.Name);
                cmd.Parameters.AddWithValue("@k",  key);
                cmd.Parameters.AddWithValue("@id", cid);
                cmd.ExecuteNonQuery();
            }

            foreach (var role in dto.RolesToPing)
            {
                if (string.IsNullOrWhiteSpace(role)) continue;
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO guild_roles (guild_name, role_id) VALUES (@gn, @role)";
                cmd.Parameters.AddWithValue("@gn",   dto.Name);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.ExecuteNonQuery();
            }

            foreach (var userId in dto.DenyUserIds)
            {
                if (!ulong.TryParse(userId, out var uid)) continue;
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO guild_deny_users (guild_name, user_id) VALUES (@gn, @uid)";
                cmd.Parameters.AddWithValue("@gn",  dto.Name);
                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.ExecuteNonQuery();
            }
        }

        private static void AddParams(MySqlCommand cmd, GuildSettingsDto dto)
        {
            var feat    = dto.Features ?? new GuildFeatures();
            var drop    = dto.Droptimizer;
            var gs      = dto.GoogleSheet;
            var asSheet = dto.ApplicationSheet;

            cmd.Parameters.AddWithValue("@name",     dto.Name);
            cmd.Parameters.AddWithValue("@nickname",  dto.Nickname ?? string.Empty);
            cmd.Parameters.AddWithValue("@fdrop",   feat.Droptimizer);
            cmd.Parameters.AddWithValue("@fremind", feat.DroptimizerReminder);
            cmd.Parameters.AddWithValue("@fkey",    feat.KeyAudit);
            cmd.Parameters.AddWithValue("@favail",  feat.ServerAvailability);
            cmd.Parameters.AddWithValue("@dtoken",  NullIfEmpty(drop?.Token));
            cmd.Parameters.AddWithValue("@dstart",  (object?)drop?.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dend",    (object?)drop?.EndDate   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@gsname",  NullIfEmpty(gs?.Name));
            cmd.Parameters.AddWithValue("@gsid",    NullIfEmpty(gs?.Id));
            cmd.Parameters.AddWithValue("@gstab",   NullIfEmpty(gs?.SheetName));
            cmd.Parameters.AddWithValue("@gscreds", NullIfEmpty(gs?.CredentialsPath));
            cmd.Parameters.AddWithValue("@asid",    NullIfEmpty(asSheet?.Id));
            cmd.Parameters.AddWithValue("@astab",   NullIfEmpty(asSheet?.SheetName));
        }

        private static GuildSettingsDto ReadRow(MySqlDataReader r)
        {
            var dto = new GuildSettingsDto
            {
                Name     = r.GetString("name"),
                Nickname = Str(r, "nickname") ?? string.Empty,
                IsDeleted = r.GetBoolean("is_deleted"),
                Features = new GuildFeatures
                {
                    Droptimizer         = r.GetBoolean("feature_droptimizer"),
                    DroptimizerReminder = r.GetBoolean("feature_droptimizer_reminder"),
                    KeyAudit            = r.GetBoolean("feature_key_audit"),
                    ServerAvailability  = r.GetBoolean("feature_server_availability")
                }
            };

            var dToken = Str(r, "drop_token");
            var dStart = Dt(r, "drop_start");
            var dEnd   = Dt(r, "drop_end");
            if (dToken != null || dStart != null || dEnd != null)
                dto.Droptimizer = new DroptimizerSettings
                    { Token = dToken ?? string.Empty, StartDate = dStart, EndDate = dEnd };

            var gsId = Str(r, "google_sheet_id");
            if (gsId != null)
                dto.GoogleSheet = new GoogleSheetsSettings
                {
                    Name            = Str(r, "google_sheet_name")  ?? string.Empty,
                    Id              = gsId,
                    SheetName       = Str(r, "google_sheet_tab")   ?? string.Empty,
                    CredentialsPath = Str(r, "google_sheet_creds") ?? string.Empty
                };

            var asId = Str(r, "app_sheet_id");
            if (asId != null)
                dto.ApplicationSheet = new ApplicationSheetSettings
                    { Id = asId, SheetName = Str(r, "app_sheet_tab") ?? string.Empty };

            return dto;
        }

        private static string?   Str(MySqlDataReader r, string col) =>
            r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetString(col);
        private static DateTime? Dt(MySqlDataReader r, string col) =>
            r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetDateTime(col);
        private static object NullIfEmpty(string? s) =>
            string.IsNullOrEmpty(s) ? DBNull.Value : s;

        private static void Exec(MySqlConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
