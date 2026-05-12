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

            default:
                return false;
        }
    }
}
