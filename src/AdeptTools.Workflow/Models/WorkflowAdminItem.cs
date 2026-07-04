namespace AdeptTools.Workflow.Models;

public class WorkflowAdminItem
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public bool Active { get; set; }
    public int StepCount { get; set; }
    public int TrusteeCount { get; set; }
    public int ReviewerCount { get; set; }
    public int NotifyCount { get; set; }
    public int AlertCount { get; set; }
    public int InProcessCount { get; set; }
    public bool Edit { get; set; }
    public bool Share { get; set; }
    public bool Delete { get; set; }
    public string? ShareStatus { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? LockedByDisplayName { get; set; }
}
