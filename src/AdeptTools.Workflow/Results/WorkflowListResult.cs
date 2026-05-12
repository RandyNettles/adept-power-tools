using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Results;

public class WorkflowListResult
{
    public List<WorkflowAdminItem> Workflows { get; set; } = new();
    public int TotalCount { get; set; }
    public string? AppliedFilter { get; set; }
}
