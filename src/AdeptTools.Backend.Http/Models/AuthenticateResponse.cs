using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdeptTools.Backend.Http.Models;

public class AuthenticateResponse
{
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("workAreaId")]
    public string? WorkAreaId { get; set; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Nonce issued by the server with status-230 responses to correlate the
    /// subsequent disambiguation call back to this pending SSO session.
    /// </summary>
    [JsonPropertyName("sso_nonce")]
    public string? SsoNonce { get; set; }

    /// <summary>
    /// The server sends the user list as an escaped JSON string OR a JSON array for status 230.
    /// Captured as JsonElement so we can handle both forms.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement DataRaw { get; set; }

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public List<UserSelectionItem>? GetUserSelectionItems()
    {
        try
        {
            switch (DataRaw.ValueKind)
            {
                case JsonValueKind.Array:
                    return DataRaw.Deserialize<List<UserSelectionItem>>(CaseInsensitive);

                case JsonValueKind.String:
                    var s = DataRaw.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return JsonSerializer.Deserialize<List<UserSelectionItem>>(s, CaseInsensitive);
                    break;
            }
        }
        catch { }

        return null;
    }
}

