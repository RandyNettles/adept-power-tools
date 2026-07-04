namespace AdeptTools.Workflow.Input;

public class AdeptUserEntry
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? NotificationTargetId { get; set; }
}

public enum MatchConfidence
{
    Exact,
    Strong,
    Weak,
    None
}

public class UserMatchResult
{
    public string InputValue { get; set; } = string.Empty;
    public string? ResolvedUserId { get; set; }
    public string? MatchedDisplayName { get; set; }
    public MatchConfidence Confidence { get; set; }
}
