using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

public class SsoSettingsResponse
{
    [JsonPropertyName("oAuthEnabled")]
    public bool OAuthEnabled { get; set; }

    [JsonPropertyName("oAuthAuthority")]
    public string? OAuthAuthority { get; set; }

    [JsonPropertyName("oAuthClientId")]
    public string? OAuthClientId { get; set; }

    [JsonPropertyName("oAuthScope")]
    public string? OAuthScope { get; set; }

    [JsonPropertyName("oAuthAdditionalParameters")]
    public string? OAuthAdditionalParameters { get; set; }
}
