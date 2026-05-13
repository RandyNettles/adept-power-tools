using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using AdeptTools.Backend.Http.Models;
using AdeptTools.Core.Auth;

namespace AdeptTools.Backend.Http.Auth;

public class HttpAdeptAuthService : IAdeptAuthService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const int CallbackPortRangeStart = 49100;
    private const int CallbackPortRangeEnd = 49110;

    public HttpAdeptAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<AuthResult> LoginAsync(string serverUrl, string userName, string password = "", CancellationToken ct = default)
    {
        var baseUrl = serverUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress ??= new Uri(baseUrl);

        // Step 1: Fetch SSO settings from the Adept server
        SsoSettingsResponse ssoSettings;
        try
        {
            var settingsResponse = await _httpClient.GetAsync("api/single-sign-on/settings", ct);
            settingsResponse.EnsureSuccessStatusCode();
            ssoSettings = await settingsResponse.Content.ReadFromJsonAsync<SsoSettingsResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Empty SSO settings response");

            if (!ssoSettings.OAuthEnabled)
                return new AuthResult(false, "SSO is not enabled on this server");

            if (string.IsNullOrWhiteSpace(ssoSettings.OAuthAuthority) || string.IsNullOrWhiteSpace(ssoSettings.OAuthClientId))
                return new AuthResult(false, "SSO is misconfigured on the server (missing authority or client ID)");
        }
        catch (HttpRequestException ex)
        {
            return new AuthResult(false, $"Failed to fetch SSO settings: {ex.Message}");
        }

        // Step 2: Discover IdP endpoints via OIDC discovery
        string authorizeEndpoint;
        string tokenEndpoint;
        try
        {
            var discovery = await DiscoverOidcEndpointsAsync(ssoSettings.OAuthAuthority, ct);
            authorizeEndpoint = discovery.AuthorizationEndpoint
                ?? throw new InvalidOperationException("IdP discovery missing authorization_endpoint");
            tokenEndpoint = discovery.TokenEndpoint
                ?? throw new InvalidOperationException("IdP discovery missing token_endpoint");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AuthResult(false, $"OIDC discovery failed: {ex.Message}");
        }

        // Step 3: Start localhost callback listener
        var (listener, redirectUri) = StartCallbackListener();
        if (listener is null)
            return new AuthResult(false, "Unable to start SSO callback listener (ports 49100–49110 in use)");

        try
        {
            // Step 4: Generate PKCE code verifier + challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);
            var state = GenerateState();

            // Step 5: Open browser to IdP authorize endpoint
            var scope = ssoSettings.OAuthScope ?? "openid email profile";
            var authorizeUrl = BuildAuthorizeUrl(authorizeEndpoint, ssoSettings.OAuthClientId, redirectUri,
                scope, codeChallenge, state, userName, ssoSettings.OAuthAdditionalParameters);
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            // Step 6: Wait for IdP to redirect back with authorization code
            var callbackResult = await WaitForCallbackAsync(listener, state, ct);
            if (callbackResult.Error is not null)
                return new AuthResult(false, $"SSO failed: {callbackResult.Error}");

            var authCode = callbackResult.Code!;

            // Step 7: Exchange auth code for IdP tokens (client-side)
            var idpTokens = await ExchangeCodeForTokensAsync(tokenEndpoint, authCode,
                redirectUri, ssoSettings.OAuthClientId, codeVerifier, ct);
            if (idpTokens.Error is not null)
                return new AuthResult(false, $"Token exchange failed: {idpTokens.Error}");

            // Step 8: Call Adept server's oauth-login with IdP token + code details
            var oauthLoginRequest = new OAuthLoginRequest
            {
                AuthCode = authCode,
                CodeVerifier = codeVerifier,
                RedirectUri = redirectUri
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idpTokens.AccessToken);

            var loginResponse = await _httpClient.PostAsJsonAsync("api/account/oauth-login", oauthLoginRequest, JsonOptions, ct);
            loginResponse.EnsureSuccessStatusCode();

            var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthenticateResponse>(JsonOptions, ct);
            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            if (authResponse.StatusCode != 0)
                return new AuthResult(false, authResponse.ErrorMessage ?? "OAuth login failed");

            // Step 9: Store Adept JWT and set for subsequent API calls
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
        catch (OperationCanceledException)
        {
            return new AuthResult(false, "SSO login was cancelled");
        }
        catch (HttpRequestException ex)
        {
            return new AuthResult(false, $"OAuth login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"SSO error: {ex.Message}");
        }
        finally
        {
            listener.Stop();
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

        var isLoggedIn = await IsLoggedInRemoteAsync(ct);
        if (!isLoggedIn)
        {
            IsAuthenticated = false;
            AccessToken = null;
            return new AuthResult(false, "Session expired");
        }

        return new AuthResult(true, AccessToken: AccessToken);
    }

    #region OIDC Discovery

    private async Task<OidcDiscovery> DiscoverOidcEndpointsAsync(string authority, CancellationToken ct)
    {
        var authorityUrl = authority.TrimEnd('/');
        // Handle Azure AD v2.0 paths
        var discoveryUrl = authorityUrl.Contains("/v2.0", StringComparison.OrdinalIgnoreCase)
            ? $"{authorityUrl}/.well-known/openid-configuration"
            : $"{authorityUrl}/.well-known/openid-configuration";

        using var discoveryClient = new HttpClient();
        var response = await discoveryClient.GetAsync(discoveryUrl, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OidcDiscovery>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty OIDC discovery document");
    }

    #endregion

    #region Authorization URL

    private static string BuildAuthorizeUrl(string authorizeEndpoint, string clientId, string redirectUri,
        string scope, string codeChallenge, string state, string? loginHint, string? additionalParams)
    {
        var sb = new StringBuilder(authorizeEndpoint);
        sb.Append('?');
        sb.Append($"client_id={Uri.EscapeDataString(clientId)}");
        sb.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
        sb.Append($"&response_type=code");
        sb.Append($"&scope={Uri.EscapeDataString(scope)}");
        sb.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        sb.Append($"&code_challenge_method=S256");
        sb.Append($"&state={Uri.EscapeDataString(state)}");

        if (!string.IsNullOrWhiteSpace(loginHint))
            sb.Append($"&login_hint={Uri.EscapeDataString(loginHint)}");

        // Append server-configured additional parameters (semicolon-delimited key=value)
        if (!string.IsNullOrWhiteSpace(additionalParams))
        {
            foreach (var param in additionalParams.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = param.Split('=', 2);
                if (parts.Length == 2)
                    sb.Append($"&{Uri.EscapeDataString(parts[0])}={Uri.EscapeDataString(parts[1])}");
            }
        }

        return sb.ToString();
    }

    #endregion

    #region Callback Listener

    private static (HttpListener? Listener, string RedirectUri) StartCallbackListener()
    {
        for (int port = CallbackPortRangeStart; port <= CallbackPortRangeEnd; port++)
        {
            var prefix = $"http://localhost:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                return (listener, $"http://localhost:{port}/");
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        return (null, string.Empty);
    }

    private static async Task<CallbackResult> WaitForCallbackAsync(HttpListener listener, string expectedState, CancellationToken ct)
    {
        using var reg = ct.Register(() => listener.Stop());
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException) when (ct.IsCancellationRequested)
        {
            return new CallbackResult { Error = "Cancelled" };
        }

        var query = context.Request.Url?.Query ?? "";
        var parameters = HttpUtility.ParseQueryString(query);

        // Respond to the browser
        var error = parameters["error"];
        string responseHtml;
        if (!string.IsNullOrEmpty(error))
        {
            responseHtml = $"<html><body><h2>Login failed</h2><p>{HttpUtility.HtmlEncode(parameters["error_description"] ?? error)}</p></body></html>";
        }
        else
        {
            responseHtml = "<html><body><h2>Login successful</h2><p>You can close this window and return to the application.</p></body></html>";
        }

        var buffer = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, ct);
        context.Response.Close();

        if (!string.IsNullOrEmpty(error))
            return new CallbackResult { Error = parameters["error_description"] ?? error };

        // Validate state to prevent CSRF
        var returnedState = parameters["state"];
        if (returnedState != expectedState)
            return new CallbackResult { Error = "Invalid state parameter — possible CSRF attack" };

        var code = parameters["code"];
        if (string.IsNullOrEmpty(code))
            return new CallbackResult { Error = "No authorization code received" };

        return new CallbackResult { Code = code };
    }

    #endregion

    #region Token Exchange

    private async Task<TokenResult> ExchangeCodeForTokensAsync(string tokenEndpoint, string code,
        string redirectUri, string clientId, string codeVerifier, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier
        });

        using var tokenClient = new HttpClient();
        var response = await tokenClient.PostAsync(tokenEndpoint, content, ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp))
        {
            var errorDesc = root.TryGetProperty("error_description", out var desc) ? desc.GetString() : errorProp.GetString();
            return new TokenResult { Error = errorDesc };
        }

        return new TokenResult
        {
            AccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null,
            IdToken = root.TryGetProperty("id_token", out var idt) ? idt.GetString() : null
        };
    }

    #endregion

    #region PKCE

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string ComputeCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string GenerateState()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    #endregion

    #region Session Check

    private async Task<bool> IsLoggedInRemoteAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/account/isLoggedIn", ct);
            if (!response.IsSuccessStatusCode) return false;
            var content = await response.Content.ReadAsStringAsync(ct);
            return bool.TryParse(content, out var result) && result;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Internal Types

    private class OidcDiscovery
    {
        [System.Text.Json.Serialization.JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; set; }
    }

    private class CallbackResult
    {
        public string? Code { get; init; }
        public string? Error { get; init; }
    }

    private class TokenResult
    {
        public string? AccessToken { get; init; }
        public string? IdToken { get; init; }
        public string? Error { get; init; }
    }

    #endregion
}
