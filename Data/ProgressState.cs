namespace DevClient.Data;

public sealed class ProgressState
{
    public List<ProgressHabit> Habits { get; set; } = [];
    public List<ProgressTask> Tasks { get; set; } = [];
    public List<ProgressGoal> Goals { get; set; } = [];
    public Dictionary<string, ProgressDailyNote> Notes { get; set; } = new();
    public ProgressCheckInSettings CheckIn { get; set; } = new();
}

/// <summary>The bot's nightly Discord DM asking whether each habit and today's tasks got done.</summary>
public sealed class ProgressCheckInSettings
{
    public bool Enabled { get; set; }
    /// <summary>Discord user to DM, as a string because snowflakes exceed JS's safe integer range.</summary>
    public string DiscordUserId { get; set; } = string.Empty;
    /// <summary>Local time of day (America/Toronto), "HH:mm".</summary>
    public string Time { get; set; } = "23:59";
}

public sealed class ProgressHabit
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Target { get; set; } = 1;
    public string Unit { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Dictionary<string, decimal> Completions { get; set; } = new();
}

public sealed class ProgressTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = "open";
}

public sealed class ProgressGoal
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public decimal Target { get; set; } = 1;
    public decimal Current { get; set; }
    public decimal? Start { get; set; }
    public List<ProgressGoalStep> Steps { get; set; } = [];
}

public sealed class ProgressGoalStep
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
}

public sealed class ProgressDailyNote
{
    public string Date { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public string Win { get; set; } = string.Empty;
    public string Reflection { get; set; } = string.Empty;
}

public interface IProgressRepository
{
    void EnsureTable();
    void EnsureDefault(ProgressState state);
    ProgressState Get();
    void Save(ProgressState state);
}


