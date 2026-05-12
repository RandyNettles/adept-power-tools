namespace AdeptTools.Workflow.Models;

public class WorkflowNotificationDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TrusteeId { get; set; } = string.Empty;
    public WorkflowUserType Type { get; set; }
}
