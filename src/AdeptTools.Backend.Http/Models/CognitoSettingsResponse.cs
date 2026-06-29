using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

/// <summary>
/// Response from GET /api/admin/options/cognito-settings (public, no auth required).
/// Used for Synergis internal IDP authentication.
/// </summary>
public class CognitoSettingsResponse
{
    [JsonPropertyName("authUrl")]
    public string? AuthUrl { get; set; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Server-registered redirect URI for this Cognito app client, if provided.
    /// When present, the Adept server acts as the OAuth relay and handles the
    /// Cognito callback; use this URI in the authorize URL instead of localhost.
    /// </summary>
    [JsonPropertyName("redirectUri")]
    public string? RedirectUri { get; set; }
}
