
namespace DevClient.Data;

public sealed record PokemonCenterSecurityState(
    string StateKey,
    string Fingerprint,
    bool QueueDetected,
    string Summary,
    DateTime CheckedAt);

public sealed record PokemonCenterSecurityTransition(
    string StateKey,
    bool PreviousQueueDetected,
    bool CurrentQueueDetected,
    string PreviousFingerprint,
    string CurrentFingerprint,
    string PreviousSummary,
    string CurrentSummary,
    DateTime ChangedAt);

public interface IPokemonCenterSecurityStateRepository
{
    void EnsureTable();
    PokemonCenterSecurityState? Get(string stateKey);
    void Set(PokemonCenterSecurityState state);
    void AddTransition(PokemonCenterSecurityTransition transition);
}



