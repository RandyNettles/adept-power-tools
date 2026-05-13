using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

public class OAuthLoginRequest
{
    [JsonPropertyName("auth_code")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("codeVerifier")]
    public string? CodeVerifier { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }
}
