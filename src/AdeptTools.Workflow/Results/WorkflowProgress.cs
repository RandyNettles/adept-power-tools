namespace AdeptTools.Workflow.Results;

public class WorkflowProgress
{
    public int CurrentIndex { get; init; }
    public int TotalCount { get; init; }
    public string WorkflowName { get; init; } = string.Empty;
    public WorkflowResultStatus Status { get; init; }
    public string? Message { get; init; }
}
