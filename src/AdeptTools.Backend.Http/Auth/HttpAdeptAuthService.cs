using System.Net.Http.Json;
using System.Text.Json;
using AdeptTools.Backend.Http.Models;
using AdeptTools.Core.Auth;

namespace AdeptTools.Backend.Http.Auth;

public class HttpAdeptAuthService : IAdeptAuthService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpAdeptAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<AuthResult> LoginAsync(string serverUrl, string userName, string password, CancellationToken ct = default)
    {
        var request = new AuthenticateRequest
        {
            UserName = userName,
            Password = password,
            ClientId = "AdeptWeb",
            ForceLogin = true,
            Platform = "AdeptTool",
            AppVersion = typeof(HttpAdeptAuthService).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/account/login", request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticateResponse>(JsonOptions, ct);

            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            if (authResponse.StatusCode != 0)
                return new AuthResult(false, authResponse.ErrorMessage ?? "Authentication failed");

            AccessToken = authResponse.AccessToken;
            IsAuthenticated = true;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

            return new AuthResult(
                Success: true,
                AccessToken: authResponse.AccessToken,
                UserId: authResponse.UserId,
                UserName: authResponse.UserName,
                DisplayName: authResponse.DisplayName,
                EmailAddress: authResponse.EmailAddress,
                AppVersion: authResponse.AppVersion,
                WorkAreaId: authResponse.WorkAreaId);
        }
        catch (HttpRequestException ex)
        {
            return new AuthResult(false, $"Connection failed: {ex.Message}");
        }
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        IsAuthenticated = false;
        AccessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        return Task.CompletedTask;
    }

    public async Task<AuthResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated || AccessToken is null)
            return new AuthResult(false, "Not authenticated");

        // The Adept API doesn't have a dedicated refresh endpoint in the same way;
        // re-authentication would be needed. For now, report current state.
        var isLoggedIn = await IsLoggedInRemoteAsync(ct);
        if (!isLoggedIn)
        {
            IsAuthenticated = false;
            AccessToken = null;
            return new AuthResult(false, "Session expired");
        }

        return new AuthResult(true, AccessToken: AccessToken);
    }

    private async Task<bool> IsLoggedInRemoteAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/account/isLoggedIn", ct);
            if (!response.IsSuccessStatusCode) return false;
            var content = await response.Content.ReadAsStringAsync(ct);
            return bool.TryParse(content, out var result) && result;
        }
        catch
        {
            return false;
        }
    }
}
