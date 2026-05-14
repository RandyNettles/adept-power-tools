using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Services;

public class WorkflowCreateRequest
{
    public required string InputFilePath { get; init; }
    public bool DryRun { get; init; }
}

public class WorkflowModifyRequest
{
    public required string InputFilePath { get; init; }
    public bool DryRun { get; init; }
}

public class WorkflowDeleteRequest
{
    public string? Filter { get; init; }
    public string Status { get; init; } = "all";
    public bool DryRun { get; init; }
    public bool Force { get; init; }
    public string? ManifestPath { get; init; }
    public List<string>? WorkflowIds { get; init; }
    public WorkflowAdminPacket? PreFetchedPacket { get; init; }
}

public class WorkflowListRequest
{
    public string? Filter { get; init; }
    public string Format { get; init; } = "table";
}
