namespace AdeptTools.Workflow.Models;

public enum WorkflowNotificationAction
{
    Undefined = ' ',
    Approve = 'A',
    Timeout = 'T'
}

public class WorkflowNotificationDefinition
{
    public string NotificationId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TrusteeId { get; set; } = string.Empty;
    public WorkflowUserType Type { get; set; }
    public int Flags { get; set; }
    public WorkflowNotificationAction Action { get; set; } = WorkflowNotificationAction.Undefined;

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

    public string WorkflowObjectId
    {
        get => StepId;
        set => StepId = value;
    }

    public string Email { get; set; } = string.Empty;
    public string EName { get; set; } = string.Empty;
}
