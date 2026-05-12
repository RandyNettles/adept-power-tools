namespace AdeptTools.Workflow.Models;

public class WorkflowAdminItem
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int StepCount { get; set; }
    public int InProcessCount { get; set; }
    public bool Edit { get; set; }
    public bool Share { get; set; }
    public bool Delete { get; set; }
    public string? LockedByDisplayName { get; set; }
}
