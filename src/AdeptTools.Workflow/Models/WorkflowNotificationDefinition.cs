namespace AdeptTools.Workflow.Models;

public enum WorkflowNotificationAction
{
    Undefined = ' ',
    Approve = 'A',
    Timeout = 'T'
}

public class WorkflowNotificationDefinition
{
    private string _trusteeId = string.Empty;
    private string? _targetId;

    public string NotificationId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TrusteeId
    {
        get => !string.IsNullOrWhiteSpace(_trusteeId) ? _trusteeId : (_targetId ?? string.Empty);
        set => _trusteeId = value ?? string.Empty;
    }

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

    public string? TargetId
    {
        get => _targetId ?? (!string.IsNullOrWhiteSpace(_trusteeId) ? _trusteeId : null);
        set
        {
            _targetId = value;
            // Preserve backward compatibility for paths that still read TrusteeId.
            if (string.IsNullOrWhiteSpace(_trusteeId) && !string.IsNullOrWhiteSpace(value))
                _trusteeId = value;
        }
    }

    public string WorkflowObjectId
    {
        get => StepId;
        set => StepId = value;
    }

    public string Email { get; set; } = string.Empty;
    public string EName { get; set; } = string.Empty;
}
