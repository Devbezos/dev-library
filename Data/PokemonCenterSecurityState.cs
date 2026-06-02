using MySqlConnector;

namespace dev_library.Data;

public sealed record PokemonCenterSecurityState(
    string StateKey,
    string Fingerprint,
    string Summary,
    DateTime CheckedAt);

public interface IPokemonCenterSecurityStateRepository
{
    void EnsureTable();
    PokemonCenterSecurityState? Get(string stateKey);
    void Set(PokemonCenterSecurityState state);
}

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
                summary     TEXT         NOT NULL,
                checked_at  DATETIME     NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public PokemonCenterSecurityState? Get(string stateKey)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT state_key, fingerprint, summary, checked_at
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
            reader.GetString("summary"),
            reader.GetDateTime("checked_at"));
    }

    public void Set(PokemonCenterSecurityState state)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pokemon_center_security_state (state_key, fingerprint, summary, checked_at)
            VALUES (@stateKey, @fingerprint, @summary, @checkedAt)
            ON DUPLICATE KEY UPDATE
                fingerprint = VALUES(fingerprint),
                summary = VALUES(summary),
                checked_at = VALUES(checked_at)
            """;
        cmd.Parameters.AddWithValue("@stateKey", state.StateKey);
        cmd.Parameters.AddWithValue("@fingerprint", state.Fingerprint);
        cmd.Parameters.AddWithValue("@summary", state.Summary);
        cmd.Parameters.AddWithValue("@checkedAt", state.CheckedAt);
        cmd.ExecuteNonQuery();
    }
}
