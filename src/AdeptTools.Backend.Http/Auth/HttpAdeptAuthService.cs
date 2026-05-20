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

    private string? _serverBaseUrl;

    public HttpAdeptAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<AuthResult> LoginAsync(string serverUrl, string userName, string password = "", CancellationToken ct = default)
    {
        _serverBaseUrl = serverUrl.TrimEnd('/') + "/";

        // Step 0: Determine login mode from server bootstrap
        ClientBootstrapResponse? bootstrap = null;
        try
        {
            var bootstrapResponse = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/client-bootstrap", ct);
            if (bootstrapResponse.IsSuccessStatusCode)
                bootstrap = await bootstrapResponse.Content.ReadFromJsonAsync<ClientBootstrapResponse>(JsonOptions, ct);
        }
        catch (HttpRequestException)
        {
            // Continue — will attempt OAuth settings next
        }

        var loginMode = bootstrap?.LoginMode ??
            (bootstrap?.IsOauthEnabled == true ? "oauth" :
             bootstrap?.IsCognitoConfigured == true ? "cognito" : "local");

        // Local login mode: username/password directly against the server
        if (loginMode.Equals("local", StringComparison.OrdinalIgnoreCase))
            return await LoginWithPasswordAsync(userName, password, ct);

        // Determine which SSO path to use
        bool useCognito = bootstrap is { IsCognitoConfigured: true, IsOauthEnabled: false };
        bool useOAuth = bootstrap is null or { IsOauthEnabled: true };

        if (bootstrap is not null && !bootstrap.IsOauthEnabled && !bootstrap.IsCognitoConfigured)
            return new AuthResult(false, "This server does not have SSO or Cognito configured. Please provide a password for local login.");

        // Step 1: Fetch SSO settings (OAuth or Cognito)
        string authorizeEndpoint;
        string tokenEndpoint;
        string clientId;
        string scope;
        string stateHash;
        string? additionalParams = null;

        if (useCognito)
        {
            // Cognito path (Synergis internal IDP)
            try
            {
                var cognitoResponse = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/cognito-settings", ct);
                cognitoResponse.EnsureSuccessStatusCode();
                var cognito = await cognitoResponse.Content.ReadFromJsonAsync<CognitoSettingsResponse>(JsonOptions, ct)
                    ?? throw new InvalidOperationException("Empty Cognito settings response");

                if (string.IsNullOrWhiteSpace(cognito.AuthUrl) || string.IsNullOrWhiteSpace(cognito.ClientId))
                    return new AuthResult(false, "Cognito SSO is not properly configured on this server");

                authorizeEndpoint = cognito.AuthUrl!;
                tokenEndpoint = cognito.AuthUrl!.Replace("/authorize", "/token");
                clientId = cognito.ClientId!;
                scope = cognito.Scope ?? "openid email";
                stateHash = $"cognito-{GenerateState()}";
            }
            catch (HttpRequestException ex)
            {
                return new AuthResult(false, $"Failed to fetch Cognito settings: {ex.Message}");
            }
        }
        else
        {
            // Customer-configured OAuth IDP
            try
            {
                var settingsResponse = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/OAuthSettings", ct);
                settingsResponse.EnsureSuccessStatusCode();
                var oauthSettings = await settingsResponse.Content.ReadFromJsonAsync<SsoSettingsResponse>(JsonOptions, ct)
                    ?? throw new InvalidOperationException("Empty OAuth settings response");

                if (string.IsNullOrWhiteSpace(oauthSettings.AuthorizationUrl) || string.IsNullOrWhiteSpace(oauthSettings.ClientId))
                    return new AuthResult(false, "SSO is not configured on this server (missing authorization URL or client ID)");

                authorizeEndpoint = oauthSettings.AuthorizationUrl!;
                tokenEndpoint = oauthSettings.TokenUrl ?? authorizeEndpoint.Replace("/authorize", "/token");
                clientId = oauthSettings.ClientId!;
                scope = oauthSettings.Scope ?? "openid email profile";
                stateHash = oauthSettings.StateHash ?? GenerateState();
                additionalParams = oauthSettings.OAuthAdditionalLoginParams;
            }
            catch (HttpRequestException ex)
            {
                return new AuthResult(false, $"Failed to fetch OAuth settings: {ex.Message}");
            }
        }

        // Step 2: Start localhost callback listener
        var (listener, redirectUri) = StartCallbackListener();
        if (listener is null)
            return new AuthResult(false, "Unable to start SSO callback listener (ports 49100–49110 in use)");

        try
        {
            // Step 3: Generate PKCE code verifier + challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);

            // Step 4: Open browser to IdP authorize endpoint
            var authorizeUrl = BuildAuthorizeUrl(authorizeEndpoint, clientId, redirectUri,
                scope, codeChallenge, stateHash, useCognito ? null : userName, additionalParams);

            if (useCognito)
                authorizeUrl += "&prompt=login";

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            // Step 5: Wait for IdP to redirect back with authorization code
            var callbackResult = await WaitForCallbackAsync(listener, stateHash, ct);
            if (callbackResult.Error is not null)
                return new AuthResult(false, $"SSO failed: {callbackResult.Error}");

            var authCode = callbackResult.Code!;

            // Step 6: Call Adept server's /api/Account/login with auth code + PKCE verifier
            var loginRequest = new AccountLoginRequest
            {
                UserName = "SSO",
                Password = "",
                AuthCode = authCode,
                SsoStateReceived = stateHash,
                SsoStateHash = stateHash,
                RedirectUri = redirectUri,
                CodeVerifier = codeVerifier,
                ForceLogin = false,
                ClientId = "Adept"
            };

            var loginResponse = await _httpClient.PostAsJsonAsync($"{_serverBaseUrl}api/Account/login", loginRequest, JsonOptions, ct);
            loginResponse.EnsureSuccessStatusCode();

            var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthenticateResponse>(JsonOptions, ct);
            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            if (authResponse.StatusCode != 0)
                return new AuthResult(false, authResponse.ErrorMessage ?? "SSO login failed");

            // Step 7: Store Adept JWT and set for subsequent API calls
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
            return new AuthResult(false, $"SSO login failed: {ex.Message}");
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

    private async Task<AuthResult> LoginWithPasswordAsync(string userName, string password, CancellationToken ct)
    {
        try
        {
            var loginRequest = new AccountLoginRequest
            {
                UserName = userName,
                Password = password,
                ForceLogin = false,
                ClientId = "Adept"
            };

            var authResponse = await PostLoginAsync(loginRequest, ct);
            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            // If user is already logged in, retry with forceLogin
            if (authResponse.StatusCode != 0 && IsAlreadyLoggedInError(authResponse))
            {
                loginRequest.ForceLogin = true;
                authResponse = await PostLoginAsync(loginRequest, ct);
                if (authResponse is null)
                    return new AuthResult(false, "Empty response from server");
            }

            if (authResponse.StatusCode != 0)
                return new AuthResult(false, authResponse.ErrorMessage ?? "Login failed");

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
            return new AuthResult(false, $"Login failed: {ex.Message}");
        }
    }

    private async Task<AuthenticateResponse?> PostLoginAsync(AccountLoginRequest request, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_serverBaseUrl}api/Account/login", request, JsonOptions, ct);

        // Read body regardless of status code — the server returns JSON even on 400
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return JsonSerializer.Deserialize<AuthenticateResponse>(body, JsonOptions);
    }

    private static bool IsAlreadyLoggedInError(AuthenticateResponse response)
    {
        // Common status codes for "user already logged in" scenarios
        var msg = response.ErrorMessage ?? "";
        return msg.Contains("already logged in", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("already connected", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("active session", StringComparison.OrdinalIgnoreCase)
            || response.StatusCode == 160; // EC_USER_ALREADY_LOGGED_IN
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
            var response = await _httpClient.GetAsync($"{_serverBaseUrl}api/account/isLoggedIn", ct);
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

    private class CallbackResult
    {
        public string? Code { get; init; }
        public string? Error { get; init; }
    }

    #endregion
}
