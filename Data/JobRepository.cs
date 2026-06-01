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
        public DateTime? LastRun { get; set; } // stored/compared as UTC
    }

    public class JobRepository : IJobRepository
    {
        private readonly string _connectionString;

        public JobRepository(string connectionString) => _connectionString = connectionString;

        // Seed rows — INSERT IGNORE, so existing rows (user-edited) are preserved
        private static readonly (string Name, int? DayOfWeek, int Hour, int Minute)[] _defaults =
        [
            (Constants.Jobs.FitnessDaily,        null, 0,  0),
            (Constants.Jobs.FitnessWeekly,       0,    0,  0),   // 0 = Sunday
            (Constants.Jobs.DroptimizerReminder, 2,    17, 0),   // 2 = Tuesday
            (Constants.Jobs.ServerAvailability,  null, 0,  0),   // runs every tick; hour/minute unused
            (Constants.Jobs.KeyAudit,            null, 0,  0),   // timing controlled by Helpers.IsKeyAuditTime
            (Constants.Jobs.PokemonTcg,          null, 10, 0),   // every day at 10:00
            (Constants.Jobs.GundamTcg,           null, 10, 0),   // every day at 10:00
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
                    last_run    DATETIME         NULL
                )
                """;
            cmd.ExecuteNonQuery();

            foreach (var (name, dayOfWeek, hour, minute) in _defaults)
            {
                using var seed = conn.CreateCommand();
                seed.CommandText = """
                    INSERT IGNORE INTO scheduled_jobs (name, enabled, day_of_week, hour, minute)
                    VALUES (@name, 1, @dow, @hour, @minute)
                    """;
                seed.Parameters.AddWithValue("@name", name);
                seed.Parameters.AddWithValue("@dow", (object?)dayOfWeek ?? DBNull.Value);
                seed.Parameters.AddWithValue("@hour", hour);
                seed.Parameters.AddWithValue("@minute", minute);
                seed.ExecuteNonQuery();
            }

            // Migration: FitnessWeekly was seeded as Monday (1), update to Sunday (0)
            using var migrate = conn.CreateCommand();
            migrate.CommandText = "UPDATE scheduled_jobs SET day_of_week = 0 WHERE name = @name AND day_of_week = 1";
            migrate.Parameters.AddWithValue("@name", Constants.Jobs.FitnessWeekly);
            migrate.ExecuteNonQuery();
        }

        public List<ScheduledJob> GetAll()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, enabled, day_of_week, hour, minute, last_run FROM scheduled_jobs";
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
                SET enabled = @enabled, day_of_week = @dow, hour = @hour, minute = @minute
                WHERE name = @name
                """;
            cmd.Parameters.AddWithValue("@name",    job.Name);
            cmd.Parameters.AddWithValue("@enabled", job.Enabled);
            cmd.Parameters.AddWithValue("@dow",     (object?)job.DayOfWeek ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hour",    job.Hour);
            cmd.Parameters.AddWithValue("@minute",  job.Minute);
            cmd.ExecuteNonQuery();
        }

        // now = local/zoned time used for day-of-week / hour / minute matching
        public bool ShouldRun(ScheduledJob job, DateTime now) =>
            job.Enabled &&
            (job.DayOfWeek == null || job.DayOfWeek == (int)now.DayOfWeek) &&
            job.Hour   == now.Hour &&
            job.Minute == now.Minute &&
            (job.LastRun == null || (DateTime.UtcNow - job.LastRun.Value).TotalMinutes >= 1);

        // True if the job is enabled, the configured hour:minute matches now, and has not yet run today (Eastern date).
        public bool ShouldRunToday(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz)
        {
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
