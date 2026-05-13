using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

/// <summary>
/// Response from GET /api/admin/options/client-bootstrap (public, no auth required).
/// </summary>
public class ClientBootstrapResponse
{
    [JsonPropertyName("IsAwsHosted")]
    public bool IsAwsHosted { get; set; }

    [JsonPropertyName("IsOauthEnabled")]
    public bool IsOauthEnabled { get; set; }

    [JsonPropertyName("IsCognitoConfigured")]
    public bool IsCognitoConfigured { get; set; }

    [JsonPropertyName("LoginMode")]
    public string? LoginMode { get; set; }
}
