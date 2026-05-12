namespace AdeptTools.Workflow.Models;

public class WorkflowStepModel
{
    public bool BDeleted { get; set; }
    public WorkflowStepDefinition WorkflowStepDefinition { get; set; } = new();
    public int InProcessCount { get; set; }
    public List<WorkflowTrusteeDefinition> WorkflowTrusteeDefinitions { get; set; } = new();
    public List<WorkflowNotificationDefinition> EmailNotificationList { get; set; } = new();
    public List<WorkflowNotificationDefinition> AlertNotificationList { get; set; } = new();
}
