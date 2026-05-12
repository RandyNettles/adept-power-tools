using AdeptTools.Core.Models;

namespace AdeptTools.Workflow.Models;

public class WorkflowAdminPacket : ApiResult
{
    public string? CurrentUserId { get; set; }
    public int WfNameLen { get; set; }
    public int WfStepNameLen { get; set; }
    public List<WorkflowAdminItem> Workflows { get; set; } = new();
}
