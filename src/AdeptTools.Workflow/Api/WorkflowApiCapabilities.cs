namespace AdeptTools.Workflow.Api;

public sealed class WorkflowApiCapabilities
{
    public static WorkflowApiCapabilities Full { get; } = new()
    {
        SupportsShareMutation = true,
        SupportsUserDirectoryLookup = true,
        SupportsGroupDirectoryLookup = true
    };

    public bool SupportsShareMutation { get; init; }
    public bool SupportsUserDirectoryLookup { get; init; }
    public bool SupportsGroupDirectoryLookup { get; init; }
}