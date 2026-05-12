namespace AdeptTools.Workflow.Validation;

public class ValidationResult
{
    public List<ValidationError> Errors { get; } = new();
    public List<ValidationWarning> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;
}

public class ValidationError
{
    public string? WorkflowName { get; init; }
    public string? StepName { get; init; }
    public string? Field { get; init; }
    public required string Message { get; init; }
}

public class ValidationWarning
{
    public string? WorkflowName { get; init; }
    public string? StepName { get; init; }
    public string? Field { get; init; }
    public required string Message { get; init; }
}
