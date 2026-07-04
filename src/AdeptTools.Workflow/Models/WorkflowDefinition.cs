namespace AdeptTools.Workflow.Models;

public class WorkflowDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public bool Shared { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? Memo { get; set; }
    public bool BDoEmailNotify { get; set; }
    public bool BTimeoutOn { get; set; }
    public bool BRecurringTimeoutOn { get; set; }
    public string? Timeout { get; set; }
    public string? RecurringTimeout { get; set; }
    public bool BTimeoutIncludeSaturday { get; set; }
    public bool BTimeoutIncludeSunday { get; set; }
}
