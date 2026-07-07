namespace AdeptTools.Workflow.Api;

public sealed class WorkflowApiCapabilities
{
    public static WorkflowApiCapabilities Full { get; } = new()
    {
        SupportsShareMutation = true,
        SupportsUserDirectoryLookup = true,
        SupportsGroupDirectoryLookup = true,
        SupportsListEnrichment = true
    };

    public bool SupportsShareMutation { get; init; }
    public bool SupportsUserDirectoryLookup { get; init; }
    public bool SupportsGroupDirectoryLookup { get; init; }

    /// <summary>
    /// When true, ListAsync enriches each row with per-workflow trustee/notify/alert counts
    /// by calling GetWorkflowAsync for every item. COM does not support this efficiently —
    /// OpenWorkflow is a blocking per-workflow call that hangs under load.
    /// </summary>
    public bool SupportsListEnrichment { get; init; }
}