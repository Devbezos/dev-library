using dev_library.Data.Discord;
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

    public static class JobRepository
    {
        // Seed rows — INSERT IGNORE, so existing rows (user-edited) are preserved
        private static readonly (string Name, int? DayOfWeek, int Hour, int Minute)[] _defaults =
        [
            (Constants.Jobs.FitnessDaily,        null, 0,  0),
            (Constants.Jobs.FitnessWeekly,       1,    0,  0),   // 1 = Monday
            (Constants.Jobs.DroptimizerReminder, 2,    17, 0),   // 2 = Tuesday
            (Constants.Jobs.ServerAvailability,  null, 0,  0),   // runs every tick; hour/minute unused
            (Constants.Jobs.KeyAudit,            null, 0,  0),   // timing controlled by Helpers.IsKeyAuditTime
        ];

        public static void EnsureTable()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
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
        }

        public static List<ScheduledJob> GetAll()
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
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

        public static void MarkRan(string name)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE scheduled_jobs SET last_run = @now WHERE name = @name";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }

        public static void Update(ScheduledJob job)
        {
            using var conn = new MySqlConnection(SqlClient.ConnectionString);
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
        public static bool ShouldRun(ScheduledJob job, DateTime now) =>
            job.Enabled &&
            (job.DayOfWeek == null || job.DayOfWeek == (int)now.DayOfWeek) &&
            job.Hour   == now.Hour &&
            job.Minute == now.Minute &&
            (job.LastRun == null || (DateTime.UtcNow - job.LastRun.Value).TotalMinutes >= 1);

        // Catch-up: true if the job should have run today but hasn't yet.
        // nowEastern must already be converted to the local/zoned time; tz is used to compute
        // the UTC equivalent of today's scheduled slot so last_run (stored as UTC) can be compared.
        public static bool ShouldRunCatchUp(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz)
        {
            if (!job.Enabled) return false;
            if (job.DayOfWeek != null && job.DayOfWeek != (int)nowEastern.DayOfWeek) return false;

            // Scheduled time hasn't arrived yet today — nothing to catch up
            if (nowEastern.Hour < job.Hour || (nowEastern.Hour == job.Hour && nowEastern.Minute < job.Minute))
                return false;

            // Compute the UTC timestamp of today's scheduled run
            var scheduledLocal = new DateTime(nowEastern.Year, nowEastern.Month, nowEastern.Day,
                                              job.Hour, job.Minute, 0, DateTimeKind.Unspecified);
            var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(scheduledLocal, tz);

            // Run if never ran, or last run was before today's scheduled time
            return job.LastRun == null || job.LastRun.Value < scheduledUtc;
        }
    }
}
