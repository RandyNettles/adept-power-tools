using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Input;

public enum TrusteeRole
{
    Reviewer,
    EmailNotify,
    AlertNotify
}

public class WorkflowInputModel
{
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string? Memo { get; set; }
    public int? TimeoutDays { get; set; }
    public int? RecurringTimeoutDays { get; set; }
    public bool ExcludeSaturday { get; set; }
    public bool ExcludeSunday { get; set; }
    public List<WorkflowInputStep> Steps { get; set; } = new();
}

public class WorkflowInputStep
{
    public string Name { get; set; } = string.Empty;
    public int RequiredApprovalsCount { get; set; }
    public bool AutoAdvance { get; set; }
    public List<WorkflowInputTrustee> Trustees { get; set; } = new();
}

public class WorkflowInputTrustee
{
    public string TrusteeId { get; set; } = string.Empty;
    public WorkflowUserType TrusteeType { get; set; }
    public TrusteeRole Role { get; set; } = TrusteeRole.Reviewer;
}
