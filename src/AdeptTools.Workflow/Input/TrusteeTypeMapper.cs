using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Input;

public static class TrusteeTypeMapper
{
    public static bool TryMap(string value, out WorkflowUserType type)
    {
        type = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToUpperInvariant())
        {
            case "USER":
            case "U":
            case "REVIEWER":
            case "APPROVER":
                type = WorkflowUserType.User;
                return true;

            case "GROUP":
            case "GRP":
            case "G":
                type = WorkflowUserType.Group;
                return true;

            case "META":
            case "KEY":
            case "K":
                type = WorkflowUserType.Key;
                return true;

            case "EMAIL":
            case "E":
                type = WorkflowUserType.Email;
                return true;

            case "APPROVERS":
            case "A":
                type = WorkflowUserType.Approvers;
                return true;

            default:
                return false;
        }
    }

    public static bool TryMapRole(string value, out TrusteeRole role)
    {
        role = TrusteeRole.Reviewer;

        if (string.IsNullOrWhiteSpace(value))
            return true; // Default to Reviewer

        switch (value.Trim().ToUpperInvariant())
        {
            case "REVIEWER":
            case "REVIEW":
            case "R":
            case "APPROVE":
            case "APPROVER":
                role = TrusteeRole.Reviewer;
                return true;

            case "NOTIFY":
            case "NOTIFICATION":
            case "EMAIL":
            case "EMAILNOTIFY":
            case "N":
                role = TrusteeRole.EmailNotify;
                return true;

            case "ALERT":
            case "ALERTNOTIFY":
                role = TrusteeRole.AlertNotify;
                return true;

            default:
                return false;
        }
    }
}
