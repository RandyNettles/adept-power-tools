using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

public class AuthenticateRequest
{
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("forceLogin")]
    public bool ForceLogin { get; set; }

    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; set; }
}
