namespace DevClient.Data.Fitness;

/// <summary>One workout from an outside app (e.g. Hevy), for fitness posts and history.</summary>
public sealed record WorkoutSession(string Name, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Where a fitness user's workouts come from when it isn't Google Health's exercise records. Days are the
/// owner's local (America/Toronto) calendar days.
/// </summary>
public interface IWorkoutSource
{
    /// <summary>Workouts that started from <paramref name="from"/> up to (not including) <paramref name="toExclusive"/>.</summary>
    Task<List<WorkoutSession>> GetWorkouts(DateOnly from, DateOnly toExclusive);
}
