namespace AdeptTools.Workflow.Results;

public enum WorkflowResultStatus
{
    Success,
    Fail,
    Skip
}

public class WorkflowOperationResult
{
    public required string WorkflowName { get; init; }
    public WorkflowResultStatus Status { get; init; }
    public string? Message { get; init; }
    public int StepCount { get; init; }
    public int TrusteeCount { get; init; }
}
