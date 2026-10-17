using Newtonsoft.Json;

namespace DevClient.Data.Hevy;

/// <summary>A workout from the Hevy API (<c>GET /v1/workouts</c>). Times are ISO 8601 UTC, e.g. "2021-09-14T12:00:00Z".</summary>
public class HevyWorkout
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("routine_id")] public string? RoutineId { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("start_time")] public DateTime StartTime { get; set; }
    [JsonProperty("end_time")] public DateTime EndTime { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("exercises")] public List<HevyExercise> Exercises { get; set; } = new();
}

public class HevyExercise
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("notes")] public string? Notes { get; set; }
    [JsonProperty("exercise_template_id")] public string ExerciseTemplateId { get; set; } = string.Empty;
    [JsonProperty("superset_id")] public int? SupersetId { get; set; }
    [JsonProperty("sets")] public List<HevySet> Sets { get; set; } = new();
}

public class HevySet
{
    [JsonProperty("index")] public int Index { get; set; }
    /// <summary>"normal", "warmup", "dropset" or "failure".</summary>
    [JsonProperty("type")] public string Type { get; set; } = "normal";
    [JsonProperty("weight_kg")] public double? WeightKg { get; set; }
    [JsonProperty("reps")] public int? Reps { get; set; }
    [JsonProperty("distance_meters")] public double? DistanceMeters { get; set; }
    [JsonProperty("duration_seconds")] public int? DurationSeconds { get; set; }
    [JsonProperty("rpe")] public double? Rpe { get; set; }
    [JsonProperty("custom_metric")] public double? CustomMetric { get; set; }
}

public class HevyWorkoutsPage
{
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("page_count")] public int PageCount { get; set; }
    [JsonProperty("workouts")] public List<HevyWorkout> Workouts { get; set; } = new();
}

/// <summary>One change since a date: <c>type</c> "updated" (with the workout) or "deleted" (with its id).</summary>
public class HevyWorkoutEvent
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("workout")] public HevyWorkout? Workout { get; set; }
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
}

public class HevyWorkoutEventsPage
{
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("page_count")] public int PageCount { get; set; }
    [JsonProperty("events")] public List<HevyWorkoutEvent> Events { get; set; } = new();
}

public class HevyUserInfo
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    [JsonProperty("name")] public string? Name { get; set; }
    /// <summary>"kg" or "lbs".</summary>
    [JsonProperty("weight_unit")] public string WeightUnit { get; set; } = "kg";
    [JsonProperty("is_pro")] public bool IsPro { get; set; }
}

public class HevyUserInfoResponse
{
    [JsonProperty("data")] public HevyUserInfo Data { get; set; } = new();
}
