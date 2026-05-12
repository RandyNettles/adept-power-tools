namespace AdeptTools.Workflow.Models;

public class WorkflowTrusteeDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TrusteeId { get; set; } = string.Empty;
    public WorkflowUserType Type { get; set; }
}
