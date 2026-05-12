using AdeptTools.Core.Models;

namespace AdeptTools.Workflow.Models;

public class WorkflowEditModel : ApiResult
{
    public bool BEditable { get; set; }
    public string? AlreadyTaggedByName { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = new();
    public List<WorkflowStepModel> WorkflowStepModels { get; set; } = new();
    public List<WorkflowNotificationDefinition> EmailNotificationList { get; set; } = new();
    public List<WorkflowNotificationDefinition> AlertNotificationList { get; set; } = new();
    public WorkflowStepModel? EAddStep { get; set; }
    public string? EStepId { get; set; }
}
