using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

/// <summary>
/// Response from GET /api/admin/options/OAuthSettings (public, no auth required).
/// </summary>
public class SsoSettingsResponse
{
    [JsonPropertyName("AuthorizationURL")]
    public string? AuthorizationUrl { get; set; }

    [JsonPropertyName("ClientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("Scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("State")]
    public string? State { get; set; }

    [JsonPropertyName("StateHash")]
    public string? StateHash { get; set; }

    [JsonPropertyName("TokenURL")]
    public string? TokenUrl { get; set; }

    [JsonPropertyName("OAuthAdditionalLoginParams")]
    public string? OAuthAdditionalLoginParams { get; set; }

    [JsonPropertyName("DeviceAuthorization")]
    public string? DeviceAuthorization { get; set; }

    [JsonPropertyName("Issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("EndSessionURL")]
    public string? EndSessionUrl { get; set; }
}
