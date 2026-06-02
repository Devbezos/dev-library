using MySqlConnector;

namespace dev_library.Data
{
    public class ScheduledJob
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public int? DayOfWeek { get; set; }   // null = every day; 0=Sun … 6=Sat
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int? IntervalMinutes { get; set; } // null = exact scheduled time; otherwise repeat after start time
        public DateTime? LastRun { get; set; } // stored/compared as UTC
    }

    public class JobRepository : IJobRepository
    {
        private readonly string _connectionString;

        public JobRepository(string connectionString) => _connectionString = connectionString;

        // Seed rows — INSERT IGNORE, so existing rows (user-edited) are preserved
        private static readonly (string Name, int? DayOfWeek, int Hour, int Minute, int? IntervalMinutes)[] _defaults =
        [
            (Constants.Jobs.FitnessDaily,        null, 0,  0, null),
            (Constants.Jobs.FitnessWeekly,       0,    0,  0, null), // 0 = Sunday
            (Constants.Jobs.DroptimizerReminder, 2,    17, 0, null), // 2 = Tuesday
            (Constants.Jobs.ServerAvailability,  null, 0,  0, 1),    // every minute
            (Constants.Jobs.KeyAudit,            null, 0,  0, null), // timing controlled by Helpers.IsKeyAuditTime
            (Constants.Jobs.PokemonTcg,          null, 10, 0, 60),   // every hour after 10:00
            (Constants.Jobs.GundamTcg,           null, 10, 0, 60),   // every hour after 10:00
            (Constants.Jobs.PokemonPreorderTcg,  null, 10, 0, 60),   // every hour after 10:00
            (Constants.Jobs.GundamPreorderTcg,   null, 10, 0, 60),   // every hour after 10:00
            (Constants.Jobs.PokemonCenterSecurity, null, 0, 0, 1),    // every minute
        ];

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS scheduled_jobs (
                    name        VARCHAR(100)     NOT NULL PRIMARY KEY,
                    enabled     TINYINT(1)       NOT NULL DEFAULT 1,
                    day_of_week TINYINT UNSIGNED NULL,
                    hour        TINYINT UNSIGNED NOT NULL,
                    minute      TINYINT UNSIGNED NOT NULL DEFAULT 0,
                    interval_minutes INT UNSIGNED NULL,
                    last_run    DATETIME         NULL
                )
                """;
            cmd.ExecuteNonQuery();

            EnsureColumn(conn, "interval_minutes", "INT UNSIGNED NULL");
            MigrateLegacyTcgJobs(conn);

            foreach (var (name, dayOfWeek, hour, minute, intervalMinutes) in _defaults)
            {
                using var seed = conn.CreateCommand();
                seed.CommandText = """
                    INSERT IGNORE INTO scheduled_jobs (name, enabled, day_of_week, hour, minute, interval_minutes)
                    VALUES (@name, 1, @dow, @hour, @minute, @intervalMinutes)
                    """;
                seed.Parameters.AddWithValue("@name", name);
                seed.Parameters.AddWithValue("@dow", (object?)dayOfWeek ?? DBNull.Value);
                seed.Parameters.AddWithValue("@hour", hour);
                seed.Parameters.AddWithValue("@minute", minute);
                seed.Parameters.AddWithValue("@intervalMinutes", (object?)intervalMinutes ?? DBNull.Value);
                seed.ExecuteNonQuery();
            }

            using var migrateIntervals = conn.CreateCommand();
            migrateIntervals.CommandText = """
                UPDATE scheduled_jobs
                SET interval_minutes = CASE
                    WHEN name = @serverAvailability THEN 1
                    WHEN name IN (@tcg, @pokemonTcg, @gundamTcg, @preorderTcg, @pokemonPreorderTcg, @gundamPreorderTcg) THEN 60
                    ELSE interval_minutes
                END
                WHERE interval_minutes IS NULL
                  AND name IN (@serverAvailability, @tcg, @pokemonTcg, @gundamTcg, @preorderTcg, @pokemonPreorderTcg, @gundamPreorderTcg)
                """;
            migrateIntervals.Parameters.AddWithValue("@serverAvailability", Constants.Jobs.ServerAvailability);
            migrateIntervals.Parameters.AddWithValue("@tcg", Constants.Jobs.Tcg);
            migrateIntervals.Parameters.AddWithValue("@pokemonTcg", Constants.Jobs.PokemonTcg);
            migrateIntervals.Parameters.AddWithValue("@gundamTcg", Constants.Jobs.GundamTcg);
            migrateIntervals.Parameters.AddWithValue("@preorderTcg", Constants.Jobs.PreorderTcg);
            migrateIntervals.Parameters.AddWithValue("@pokemonPreorderTcg", Constants.Jobs.PokemonPreorderTcg);
            migrateIntervals.Parameters.AddWithValue("@gundamPreorderTcg", Constants.Jobs.GundamPreorderTcg);
            migrateIntervals.ExecuteNonQuery();

            // Migration: FitnessWeekly was seeded as Monday (1), update to Sunday (0)
            using var migrate = conn.CreateCommand();
            migrate.CommandText = "UPDATE scheduled_jobs SET day_of_week = 0 WHERE name = @name AND day_of_week = 1";
            migrate.Parameters.AddWithValue("@name", Constants.Jobs.FitnessWeekly);
            migrate.ExecuteNonQuery();
        }

        private static void EnsureColumn(MySqlConnection conn, string columnName, string definition)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'scheduled_jobs'
                  AND COLUMN_NAME = @columnName
                """;
            cmd.Parameters.AddWithValue("@columnName", columnName);
            var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            if (exists) return;

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE scheduled_jobs ADD COLUMN {columnName} {definition}";
            alter.ExecuteNonQuery();
        }

        private static void MigrateLegacyTcgJobs(MySqlConnection conn)
        {
            using var select = conn.CreateCommand();
            select.CommandText = """
                SELECT name, enabled, day_of_week, hour, minute, interval_minutes, last_run
                FROM scheduled_jobs
                WHERE name IN (@pokemon, @gundam, @preorder, @pokemonPreorder, @gundamPreorder, @tcg)
                """;
            select.Parameters.AddWithValue("@pokemon", Constants.Jobs.PokemonTcg);
            select.Parameters.AddWithValue("@gundam", Constants.Jobs.GundamTcg);
            select.Parameters.AddWithValue("@preorder", Constants.Jobs.PreorderTcg);
            select.Parameters.AddWithValue("@pokemonPreorder", Constants.Jobs.PokemonPreorderTcg);
            select.Parameters.AddWithValue("@gundamPreorder", Constants.Jobs.GundamPreorderTcg);
            select.Parameters.AddWithValue("@tcg", Constants.Jobs.Tcg);

            var rows = new List<ScheduledJob>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new ScheduledJob
                    {
                        Name = reader.GetString("name"),
                        Enabled = reader.GetBoolean("enabled"),
                        DayOfWeek = reader.IsDBNull(reader.GetOrdinal("day_of_week")) ? null : (int?)reader.GetInt32("day_of_week"),
                        Hour = reader.GetInt32("hour"),
                        Minute = reader.GetInt32("minute"),
                        IntervalMinutes = reader.IsDBNull(reader.GetOrdinal("interval_minutes")) ? null : (int?)reader.GetInt32("interval_minutes"),
                        LastRun = reader.IsDBNull(reader.GetOrdinal("last_run")) ? null : reader.GetDateTime("last_run"),
                    });
                }
            }

            var legacyTcg = rows.FirstOrDefault(r => r.Name == Constants.Jobs.Tcg);
            if (legacyTcg != null)
            {
                InsertTcgJobFromLegacy(conn, Constants.Jobs.PokemonTcg, legacyTcg);
                InsertTcgJobFromLegacy(conn, Constants.Jobs.GundamTcg, legacyTcg);
                InsertTcgJobFromLegacy(conn, Constants.Jobs.PokemonPreorderTcg, legacyTcg);
                InsertTcgJobFromLegacy(conn, Constants.Jobs.GundamPreorderTcg, legacyTcg);
            }

            var legacyPreorder = rows.FirstOrDefault(r => r.Name == Constants.Jobs.PreorderTcg);
            if (legacyPreorder != null)
            {
                InsertTcgJobFromLegacy(conn, Constants.Jobs.PokemonPreorderTcg, legacyPreorder);
                InsertTcgJobFromLegacy(conn, Constants.Jobs.GundamPreorderTcg, legacyPreorder);
            }

            using var delete = conn.CreateCommand();
            delete.CommandText = "DELETE FROM scheduled_jobs WHERE name IN (@tcg, @preorder)";
            delete.Parameters.AddWithValue("@tcg", Constants.Jobs.Tcg);
            delete.Parameters.AddWithValue("@preorder", Constants.Jobs.PreorderTcg);
            delete.ExecuteNonQuery();
        }

        private static void InsertTcgJobFromLegacy(MySqlConnection conn, string name, ScheduledJob legacy)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT IGNORE INTO scheduled_jobs (name, enabled, day_of_week, hour, minute, interval_minutes, last_run)
                VALUES (@name, @enabled, @dow, @hour, @minute, @intervalMinutes, @lastRun)
                """;
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@enabled", legacy.Enabled);
            insert.Parameters.AddWithValue("@dow", (object?)legacy.DayOfWeek ?? DBNull.Value);
            insert.Parameters.AddWithValue("@hour", legacy.Hour);
            insert.Parameters.AddWithValue("@minute", legacy.Minute);
            insert.Parameters.AddWithValue("@intervalMinutes", (object?)legacy.IntervalMinutes ?? DBNull.Value);
            insert.Parameters.AddWithValue("@lastRun", (object?)legacy.LastRun ?? DBNull.Value);
            insert.ExecuteNonQuery();
        }

        public List<ScheduledJob> GetAll()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, enabled, day_of_week, hour, minute, interval_minutes, last_run FROM scheduled_jobs";
            using var reader = cmd.ExecuteReader();
            var result = new List<ScheduledJob>();
            while (reader.Read())
                result.Add(new ScheduledJob
                {
                    Name      = reader.GetString("name"),
                    Enabled   = reader.GetBoolean("enabled"),
                    DayOfWeek = reader.IsDBNull(reader.GetOrdinal("day_of_week")) ? null : (int?)reader.GetInt32("day_of_week"),
                    Hour      = reader.GetInt32("hour"),
                    Minute    = reader.GetInt32("minute"),
                    IntervalMinutes = reader.IsDBNull(reader.GetOrdinal("interval_minutes")) ? null : (int?)reader.GetInt32("interval_minutes"),
                    LastRun   = reader.IsDBNull(reader.GetOrdinal("last_run")) ? null : reader.GetDateTime("last_run"),
                });
            return result;
        }

        public void MarkRan(string name)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE scheduled_jobs SET last_run = @now WHERE name = @name";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }

        public void ResetLastRun(string name)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE scheduled_jobs SET last_run = NULL WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }

        public void Update(ScheduledJob job)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE scheduled_jobs
                SET enabled = @enabled, day_of_week = @dow, hour = @hour, minute = @minute, interval_minutes = @intervalMinutes
                WHERE name = @name
                """;
            cmd.Parameters.AddWithValue("@name",    job.Name);
            cmd.Parameters.AddWithValue("@enabled", job.Enabled);
            cmd.Parameters.AddWithValue("@dow",     (object?)job.DayOfWeek ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hour",    job.Hour);
            cmd.Parameters.AddWithValue("@minute",  job.Minute);
            cmd.Parameters.AddWithValue("@intervalMinutes", (object?)job.IntervalMinutes ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        // now = local/zoned time used for day-of-week / hour / minute matching
        public bool ShouldRun(ScheduledJob job, DateTime now)
        {
            if (!job.Enabled) return false;
            if (job.DayOfWeek != null && job.DayOfWeek != (int)now.DayOfWeek) return false;

            if (job.IntervalMinutes is > 0)
            {
                if (now.TimeOfDay < new TimeSpan(job.Hour, job.Minute, 0)) return false;
                return job.LastRun == null ||
                    (DateTime.UtcNow - job.LastRun.Value).TotalMinutes >= job.IntervalMinutes.Value;
            }

            return job.Hour == now.Hour &&
                job.Minute == now.Minute &&
                (job.LastRun == null || (DateTime.UtcNow - job.LastRun.Value).TotalMinutes >= 1);
        }

        // True if the job is enabled, the configured hour:minute matches now, and has not yet run today (Eastern date).
        public bool ShouldRunToday(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz)
        {
            if (job.IntervalMinutes is > 0) return ShouldRun(job, nowEastern);
            if (!job.Enabled) return false;
            if (job.Hour != nowEastern.Hour || job.Minute != nowEastern.Minute) return false;
            if (job.LastRun == null) return true;
            var lastRunEastern = TimeZoneInfo.ConvertTime(
                DateTime.SpecifyKind(job.LastRun.Value, DateTimeKind.Utc), tz);
            return lastRunEastern.Date < nowEastern.Date;
        }

        // True if the job is enabled, configured hour:minute matches, today matches the configured day-of-week, and has not yet run this week.
        public bool ShouldRunThisWeek(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz)
        {
            if (job.IntervalMinutes is > 0) return ShouldRun(job, nowEastern);
            if (!job.Enabled) return false;

            // Only fire on the configured day (default Sunday)
            var weekDay = job.DayOfWeek ?? (int)DayOfWeek.Sunday;
            if ((int)nowEastern.DayOfWeek != weekDay) return false;

            // If never run, fire on the next tick on the correct day (no time check needed)
            if (job.LastRun == null) return true;

            if (job.Hour != nowEastern.Hour || job.Minute != nowEastern.Minute) return false;
            var lastRunEastern = TimeZoneInfo.ConvertTime(
                DateTime.SpecifyKind(job.LastRun.Value, DateTimeKind.Utc), tz);
            // Week boundary = most recent occurrence of the configured day
            var daysToWeekStart = ((int)nowEastern.DayOfWeek - weekDay + 7) % 7;
            var weekStart = nowEastern.Date.AddDays(-daysToWeekStart);
            return lastRunEastern.Date < weekStart;
        }
    }
}
