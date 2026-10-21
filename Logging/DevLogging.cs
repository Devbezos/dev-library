namespace DevClient.Logging;

/// <summary>Shared log setup for the API and the bot.</summary>
public static class DevLogging
{
    /// <summary>
    /// Whether to also write logs/*.log. Off in a container (the .NET images set DOTNET_RUNNING_IN_CONTAINER), where
    /// Railway keeps the console output; on for local dev. Config <c>logFiles</c> (true/false) overrides either way.
    /// </summary>
    public static bool FileLoggingEnabled(string? configured) =>
        bool.TryParse(configured, out var on)
            ? on
            : !string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
}
