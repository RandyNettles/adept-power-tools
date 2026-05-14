namespace AdeptTools.Launcher.Services;

/// <summary>
/// Holds the active server base URL and access token for HTTP API clients.
/// Set after successful authentication.
/// </summary>
public class HttpClientConfig
{
    public string? BaseUrl { get; set; }
    public string? AccessToken { get; set; }
}
