namespace AdeptTools.Workflow.Models;

public class WorkflowNotificationDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TrusteeId { get; set; } = string.Empty;
    public WorkflowUserType Type { get; set; }
    public int Flags { get; set; }

    // HTTP Web API workflow notifications use targetType/targetId/email.
    // Keep these aliases so the same model can be used by both COM and HTTP paths.
    public WorkflowUserType TargetType
    {
        get => Type;
        set => Type = value;
    }

    public string TargetId
    {
        get => TrusteeId;
        set => TrusteeId = value;
    }

    public string Email { get; set; } = string.Empty;
}
