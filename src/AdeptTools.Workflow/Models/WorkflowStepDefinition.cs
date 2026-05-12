namespace AdeptTools.Workflow.Models;

public class WorkflowStepDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool BCanChangeWorkflow { get; set; }
    public bool BCanSignOut { get; set; }
    public bool BCanExpediteReject { get; set; }
    public bool BCanExpediteApprove { get; set; }
    public bool BDoEmailNotify { get; set; }
    public bool BRequireComment { get; set; }
    public bool BOnlyAllowAssignToApprovers { get; set; }
    public int RequiredApprovalsCount { get; set; }
    public bool AutoAdvance { get; set; }
    public bool BTimeoutOn { get; set; }
    public bool BRecurringTimeoutOn { get; set; }
    public string? Timeout { get; set; }
    public string? RecurringTimeout { get; set; }
    public bool BTimeoutIncludeSaturday { get; set; }
    public bool BTimeoutIncludeSunday { get; set; }
}
