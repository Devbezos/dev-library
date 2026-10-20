namespace DevClient.Data.Fitness;

/// <summary>
/// Sorts activities by name (Google's display name or exercise type, or a Hevy workout's title) for the automatic
/// Progress habits: stretching sessions and walks don't count as a workout.
/// </summary>
public static class FitnessActivityKinds
{
    /// <summary>Google's "STRETCHING" type or anything named like a stretch.</summary>
    public static bool IsStretching(string name) => name.Contains("stretch", StringComparison.OrdinalIgnoreCase);

    public static bool IsWalking(string name) => name.Contains("walk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lifting and other training: anything that isn't a stretch or a walk.</summary>
    public static bool IsWorkout(string name) => !IsStretching(name) && !IsWalking(name);
}
