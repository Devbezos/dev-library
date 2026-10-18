namespace DevClient.Data;

public sealed class SkillSnapshot
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public long Xp { get; set; }
    public int? Rank { get; set; }
}

public sealed class BossSnapshot
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? KillCount { get; set; }
    public int? Rank { get; set; }
}

public sealed class PlayerSnapshot
{
    public string Username { get; set; } = "Dev0001";
    public string FocusMode { get; set; } = "balanced";
    public string FetchedAt { get; set; } = string.Empty;
    public string Source { get; set; } = "cache";
    public Dictionary<string, SkillSnapshot> Skills { get; set; } = new();
    public Dictionary<string, BossSnapshot> Bosses { get; set; } = new();
    public bool IsStale { get; set; }
    public string? Error { get; set; }
}

public sealed class RunescapePrice
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public long? High { get; set; }
    public long? Low { get; set; }
    public long? Current { get; set; }
    public string Basis { get; set; } = "missing";
    public string FetchedAt { get; set; } = string.Empty;
    public bool IsStale { get; set; }
}

public sealed class RunescapePriceResponse
{
    public string FetchedAt { get; set; } = string.Empty;
    public string Source { get; set; } = "osrs-wiki";
    public bool IsStale { get; set; }
    public string? Error { get; set; }
    public Dictionary<int, RunescapePrice> Prices { get; set; } = new();
}

public sealed class RunescapeState
{
    public string Username { get; set; } = "Dev0001";
    public string FocusMode { get; set; } = "balanced";
    public List<RunescapeSkillGoal> SkillGoals { get; set; } = [];
    public List<RunescapeSkillMilestone> SkillMilestones { get; set; } = [];
    public List<RunescapeGearGoal> GearGoals { get; set; } = [];
    public List<RunescapeUnlockGoal> UnlockGoals { get; set; } = [];
    public List<RunescapeRoutineGoal> RoutineGoals { get; set; } = [];
    public List<RunescapeBossGoal> BossGoals { get; set; } = [];
    public Dictionary<string, bool> ManualMilestones { get; set; } = new();
}

public sealed class RunescapeSkillGoal
{
    public string Id { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public int TargetLevel { get; set; }
    public int Priority { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool ManuallyComplete { get; set; }
}

public sealed class RunescapeGearGoal
{
    public string Id { get; set; } = string.Empty;
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = "utility";
    public string Priority { get; set; } = "medium";
    public bool Tradeable { get; set; }
    public bool Owned { get; set; }
    public bool Archived { get; set; }
    public bool IsSeeded { get; set; } = true;
    public string Stage { get; set; } = "core mid-game";
    public string Recommendation { get; set; } = "recommended";
    public string? Requirement { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RunescapeSkillMilestone
{
    public string Id { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public int TargetLevel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<RunescapeTrainingMethod> SuggestedMethods { get; set; } = [];
    public string? SelectedMethodId { get; set; }
    public long? EstimatedGpCost { get; set; }
    public decimal? EstimatedHours { get; set; }
    public List<string> PrerequisiteMilestoneIds { get; set; } = [];
    public string Priority { get; set; } = "soon";
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
    public bool Skipped { get; set; }
    public bool IsSeeded { get; set; } = true;
}

public sealed class RunescapeTrainingMethod
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = "low cost";
    public int? XpPerHour { get; set; }
    public long? EstimatedGpCost { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class RunescapeUnlockGoal
{
    public string Id { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "quest";
    public string Prerequisites { get; set; } = string.Empty;
    public long? EstimatedGpCost { get; set; }
    public List<string> DependencyIds { get; set; } = [];
    public bool Complete { get; set; }
    public bool Archived { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RunescapeRoutineGoal
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Cadence { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? LastCompletedAt { get; set; }
    public string? NextDueAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> RelatedUnlockIds { get; set; } = [];
    public bool NotificationOptIn { get; set; }
    public int SortOrder { get; set; }
}
public sealed class RunescapeBossGoal
{
    public string Id { get; set; } = string.Empty;
    public string BossKey { get; set; } = string.Empty;
    public string BossName { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public int? TargetKillCount { get; set; }
    public int SortOrder { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Prerequisites { get; set; } = string.Empty;
    public string RecommendedStats { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? TargetDate { get; set; }
    public bool ManualComplete { get; set; }
}

public interface IRunescapeProgressRepository
{
    void EnsureTable();
    void EnsureDefault(RunescapeState state);
    RunescapeState Get();
    void Save(RunescapeState state);
    void ResetSeededDefaults();
}
