namespace DevClient.Data;

public sealed class ProgressState
{
    public List<ProgressHabit> Habits { get; set; } = [];
    public List<ProgressTask> Tasks { get; set; } = [];
    public List<ProgressGoal> Goals { get; set; } = [];
    public Dictionary<string, ProgressDailyNote> Notes { get; set; } = new();
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


