namespace AdeptTools.Workflow.Results;

public class WorkflowBatchResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public bool DryRun { get; set; }
    public List<WorkflowOperationResult> Results { get; set; } = new();
}
