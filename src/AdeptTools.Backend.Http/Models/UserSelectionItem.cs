using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

/// <summary>
/// One Adept user account returned in the status-230 "multiple users linked" response.
/// </summary>
public class UserSelectionItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("loginName")]
    public string? LoginName { get; set; }

    [JsonPropertyName("powerUser")]
    public bool PowerUser { get; set; }

    [JsonPropertyName("liteUser")]
    public bool LiteUser { get; set; }

    [JsonPropertyName("admin")]
    public bool Admin { get; set; }

    public string TypeLabel => Admin ? "Admin" : LiteUser ? "Lite" : "Standard";
}
