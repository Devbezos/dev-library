using MySqlConnector;

namespace DevClient.Data;
public sealed class PokemonCenterSecurityStateRepository : IPokemonCenterSecurityStateRepository
{
    private readonly string _connectionString;

    public PokemonCenterSecurityStateRepository(string connectionString) => _connectionString = connectionString;

    public void EnsureTable()
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS pokemon_center_security_state (
                state_key   VARCHAR(100) NOT NULL PRIMARY KEY,
                fingerprint CHAR(64)     NOT NULL,
                queue_detected TINYINT(1) NOT NULL DEFAULT 0,
                summary     TEXT         NOT NULL,
                checked_at  DATETIME     NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        EnsureColumn(conn, "pokemon_center_security_state", "queue_detected", "TINYINT(1) NOT NULL DEFAULT 0");

        using var transitionCmd = conn.CreateCommand();
        transitionCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS pokemon_center_security_transitions (
                id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                state_key VARCHAR(100) NOT NULL,
                previous_queue_detected TINYINT(1) NOT NULL,
                current_queue_detected TINYINT(1) NOT NULL,
                previous_fingerprint CHAR(64) NOT NULL,
                current_fingerprint CHAR(64) NOT NULL,
                previous_summary TEXT NOT NULL,
                current_summary TEXT NOT NULL,
                changed_at DATETIME NOT NULL,
                INDEX idx_pokemon_center_security_transitions_state_time (state_key, changed_at)
            )
            """;
        transitionCmd.ExecuteNonQuery();
    }

    public PokemonCenterSecurityState? Get(string stateKey)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT state_key, fingerprint, queue_detected, summary, checked_at
            FROM pokemon_center_security_state
            WHERE state_key = @stateKey
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@stateKey", stateKey);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new PokemonCenterSecurityState(
            reader.GetString("state_key"),
            reader.GetString("fingerprint"),
            reader.GetBoolean("queue_detected"),
            reader.GetString("summary"),
            reader.GetDateTime("checked_at"));
    }

    public void Set(PokemonCenterSecurityState state)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pokemon_center_security_state (state_key, fingerprint, queue_detected, summary, checked_at)
            VALUES (@stateKey, @fingerprint, @queueDetected, @summary, @checkedAt)
            ON DUPLICATE KEY UPDATE
                fingerprint = VALUES(fingerprint),
                queue_detected = VALUES(queue_detected),
                summary = VALUES(summary),
                checked_at = VALUES(checked_at)
            """;
        cmd.Parameters.AddWithValue("@stateKey", state.StateKey);
        cmd.Parameters.AddWithValue("@fingerprint", state.Fingerprint);
        cmd.Parameters.AddWithValue("@queueDetected", state.QueueDetected);
        cmd.Parameters.AddWithValue("@summary", state.Summary);
        cmd.Parameters.AddWithValue("@checkedAt", state.CheckedAt);
        cmd.ExecuteNonQuery();
    }

    public void AddTransition(PokemonCenterSecurityTransition transition)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pokemon_center_security_transitions (
                state_key,
                previous_queue_detected,
                current_queue_detected,
                previous_fingerprint,
                current_fingerprint,
                previous_summary,
                current_summary,
                changed_at)
            VALUES (
                @stateKey,
                @previousQueueDetected,
                @currentQueueDetected,
                @previousFingerprint,
                @currentFingerprint,
                @previousSummary,
                @currentSummary,
                @changedAt)
            """;
        cmd.Parameters.AddWithValue("@stateKey", transition.StateKey);
        cmd.Parameters.AddWithValue("@previousQueueDetected", transition.PreviousQueueDetected);
        cmd.Parameters.AddWithValue("@currentQueueDetected", transition.CurrentQueueDetected);
        cmd.Parameters.AddWithValue("@previousFingerprint", transition.PreviousFingerprint);
        cmd.Parameters.AddWithValue("@currentFingerprint", transition.CurrentFingerprint);
        cmd.Parameters.AddWithValue("@previousSummary", transition.PreviousSummary);
        cmd.Parameters.AddWithValue("@currentSummary", transition.CurrentSummary);
        cmd.Parameters.AddWithValue("@changedAt", transition.ChangedAt);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureColumn(MySqlConnection conn, string tableName, string columnName, string definition)
    {
        using var exists = conn.CreateCommand();
        exists.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
              AND COLUMN_NAME = @columnName
            """;
        exists.Parameters.AddWithValue("@tableName", tableName);
        exists.Parameters.AddWithValue("@columnName", columnName);

        if (Convert.ToInt32(exists.ExecuteScalar()) > 0) return;

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
        alter.ExecuteNonQuery();
    }
}



