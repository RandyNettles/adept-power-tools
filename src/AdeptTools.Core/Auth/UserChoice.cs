namespace AdeptTools.Core.Auth;

/// <summary>
/// Represents one Adept user account returned when multiple users are linked to a single IdP identity (status 230).
/// </summary>
public record UserChoice(string Id, string UserName, string TypeLabel)
{
    public string DisplayLabel => $"{UserName}  ({TypeLabel})";
}
