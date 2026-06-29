using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

/// <summary>
/// Request body for POST /api/Account/login (SSO mode).
/// Mirrors the request shape used by the Adept Web Client.
/// </summary>
public class AccountLoginRequest
{
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "SSO";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("cultureName")]
    public string CultureName { get; set; } = System.Globalization.CultureInfo.CurrentCulture.Name;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = "Adept";

    [JsonPropertyName("forceLogin")]
    public bool ForceLogin { get; set; }

    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = "";

    [JsonPropertyName("autoLogin")]
    public bool AutoLogin { get; set; }

    [JsonPropertyName("timeZoneOffset")]
    public string TimeZoneOffset { get; set; } = TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes.ToString();

    [JsonPropertyName("auth_code")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("sso_state_received")]
    public string? SsoStateReceived { get; set; }

    [JsonPropertyName("sso_state_hash")]
    public string? SsoStateHash { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    [JsonPropertyName("windowsDomain")]
    public string WindowsDomain { get; set; } = "";

    [JsonPropertyName("windowsLogin")]
    public string WindowsLogin { get; set; } = "";

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "";

    [JsonPropertyName("tokenDurationSeconds")]
    public int TokenDurationSeconds { get; set; }

    [JsonPropertyName("codeVerifier")]
    public string? CodeVerifier { get; set; }

    [JsonPropertyName("userSelection")]
    public string UserSelection { get; set; } = "";

    [JsonPropertyName("sso_nonce")]
    public string? SsoNonce { get; set; }
}
