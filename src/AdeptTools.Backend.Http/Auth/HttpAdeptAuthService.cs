using System.Diagnostics;
using System.Globalization;
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

    // Cognito app client has exactly http://localhost:51555/callback registered.
    private const int CognitoCallbackPort = 51555;
    private const string CognitoCallbackPath = "callback";

    private string? _serverBaseUrl;
    private string? _refreshToken;

    /// <summary>Stored when the server returns status 230 — contains the access_token issued alongside the multiple-user prompt.</summary>
    private AuthenticateResponse? _pending230Response;
    /// <summary>The SSO state hash from the flow that produced the 230 — used to identify the pending session during user selection.</summary>
    private string? _pending230StateHash;

    public HttpAdeptAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken => _refreshToken;

    public DateTimeOffset? GetAccessTokenExpiryUtc(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return null;

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            if (!doc.RootElement.TryGetProperty("exp", out var expEl))
                return null;

            long unixSeconds;
            if (expEl.ValueKind == JsonValueKind.Number && expEl.TryGetInt64(out var n))
            {
                unixSeconds = n;
            }
            else if (expEl.ValueKind == JsonValueKind.String &&
                     long.TryParse(expEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            {
                unixSeconds = s;
            }
            else
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResult> TryResumeSessionAsync(
        string serverUrl,
        string accessToken,
        string? refreshToken,
        DateTimeOffset? accessTokenExpiresUtc,
        string? userId,
        string? userName,
        string? displayName,
        string? emailAddress,
        string? appVersion,
        string? workAreaId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(accessToken))
            return new AuthResult(false, "Saved session is incomplete.");

        var expiry = accessTokenExpiresUtc ?? GetAccessTokenExpiryUtc(accessToken);
        if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
            return new AuthResult(false, "Saved session has expired.");

        _serverBaseUrl = serverUrl.TrimEnd('/') + "/";
        AccessToken = accessToken;
        _refreshToken = refreshToken;
        IsAuthenticated = true;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        var refreshResult = await RefreshAsync(ct);
        if (!refreshResult.Success)
        {
            await LogoutAsync(ct);
            return new AuthResult(false, refreshResult.ErrorMessage ?? "Saved session is no longer valid.");
        }

        return new AuthResult(
            Success: true,
            AccessToken: AccessToken,
            UserId: userId,
            UserName: userName,
            DisplayName: displayName,
            EmailAddress: emailAddress,
            AppVersion: appVersion,
            WorkAreaId: workAreaId);
    }

    public async Task<AuthResult> LoginAsync(string serverUrl, string userName, string password = "", CancellationToken ct = default)
    {
        // Strip any query string or fragment from the URL — users sometimes paste browser
        // redirect URLs (e.g. https://server/?src=connect) instead of the raw API base URL.
        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsedUri))
            _serverBaseUrl = parsedUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
        else
            _serverBaseUrl = serverUrl.TrimEnd('/') + "/";

        // Step 0: Determine login mode from server bootstrap
        ClientBootstrapResponse? bootstrap = null;
        try
        {
            var bootstrapResponse = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/client-bootstrap", ct);
            if (bootstrapResponse.IsSuccessStatusCode)
                bootstrap = await bootstrapResponse.Content.ReadFromJsonAsync<ClientBootstrapResponse>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Continue — will attempt OAuth settings next
        }

        var loginMode = bootstrap?.LoginMode ??
            (bootstrap?.IsOauthEnabled == true ? "oauth" :
             bootstrap?.IsCognitoConfigured == true ? "cognito" : "local");

        // Normalise Azure AD variant spellings to "oauth"
        if (loginMode.Equals("azure", StringComparison.OrdinalIgnoreCase) ||
            loginMode.Equals("azuread", StringComparison.OrdinalIgnoreCase) ||
            loginMode.Equals("entra", StringComparison.OrdinalIgnoreCase))
            loginMode = "oauth";

        // Local login with an explicit password — go straight to password auth.
        if (loginMode.Equals("local", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(password))
            return await LoginWithPasswordAsync(userName, password, ct);

        // For every other case where the password is blank (loginMode "sso", "oauth", "cognito",
        // or "local" with no password), attempt browser SSO flows in order:
        //   1. Customer-configured OAuth IDP  (OAuthSettings endpoint — covers Azure AD / Entra)
        //   2. Cognito / Synergis internal IDP (cognito-settings endpoint)
        //   3. Windows domain credentials     (last resort, only when loginMode != "oauth"/"cognito")
        bool useCognito = false;
        string authorizeEndpoint;
        string tokenEndpoint;
        string clientId;
        string scope;
        string stateHash;
        string? additionalParams = null;

        // Try OAuth / Azure AD settings
        SsoSettingsResponse? oauthCfg = null;
        string? oauthFetchError = null;
        try
        {
            var r = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/OAuthSettings", ct);
            if (r.IsSuccessStatusCode)
            {
                var body = await r.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(body))
                    oauthCfg = JsonSerializer.Deserialize<SsoSettingsResponse>(body, JsonOptions);
            }
            else
            {
                oauthFetchError = $"OAuthSettings returned HTTP {(int)r.StatusCode}";
            }
        }
        catch (Exception ex) { oauthFetchError = ex.Message; }

        bool oauthConfigured = !string.IsNullOrWhiteSpace(oauthCfg?.AuthorizationUrl) &&
                               !string.IsNullOrWhiteSpace(oauthCfg?.ClientId);

        // When the server explicitly says OAuth (Azure AD), never fall through to Cognito.
        bool oauthOnlyMode = loginMode.Equals("oauth", StringComparison.OrdinalIgnoreCase);

        if (oauthConfigured)
        {
            authorizeEndpoint = oauthCfg!.AuthorizationUrl!;
            tokenEndpoint = oauthCfg.TokenUrl ?? authorizeEndpoint.Replace("/authorize", "/token");
            clientId = oauthCfg.ClientId!;
            scope = oauthCfg.Scope ?? "openid email profile";
            stateHash = oauthCfg.StateHash ?? GenerateState();
            additionalParams = oauthCfg.OAuthAdditionalLoginParams;
        }
        else if (!oauthOnlyMode)
        {
            // Try Cognito settings
            CognitoSettingsResponse? cognitoCfg = null;
            try
            {
                var r = await _httpClient.GetAsync($"{_serverBaseUrl}api/admin/options/cognito-settings", ct);
                if (r.IsSuccessStatusCode)
                    cognitoCfg = await r.Content.ReadFromJsonAsync<CognitoSettingsResponse>(JsonOptions, ct);
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(cognitoCfg?.AuthUrl) && !string.IsNullOrWhiteSpace(cognitoCfg?.ClientId))
            {
                useCognito = true;
                authorizeEndpoint = cognitoCfg.AuthUrl!;
                tokenEndpoint = cognitoCfg.AuthUrl!.Replace("/authorize", "/token");
                clientId = cognitoCfg.ClientId!;
                scope = cognitoCfg.Scope ?? "openid email";
                stateHash = $"cognito-{GenerateState()}";
            }
            else
            {
                // Neither OAuth nor Cognito configured — fall back to Windows domain auth.
                return await LoginWithWindowsSsoAsync(userName, ct);
            }
        }
        else
        {
            // Server says OAuth/Azure but settings were unreachable.
            var detail = oauthFetchError is not null ? $" ({oauthFetchError})" : "";
            return new AuthResult(false,
                $"Azure AD / OAuth SSO is configured on this server but the settings could not be " +
                $"retrieved from /api/admin/options/OAuthSettings{detail}. " +
                $"Verify the server URL is correct and the endpoint is accessible.");
        }

        // Step 2: Start localhost callback listener
        var (listener, redirectUri) = StartCallbackListener(useCognito);
        if (listener is null)
            return new AuthResult(false, useCognito
                ? $"Unable to start SSO callback listener on port {CognitoCallbackPort} (port in use)"
                : "Unable to start SSO callback listener (ports 49100–49110 in use)");

        try
        {
            // Step 3: Generate PKCE code verifier + challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);

            // Step 4: Build authorize URL and pre-flight check redirect URI acceptance.
            // Cognito immediately 302-redirects to its own error page when redirect_uri is not
            // registered — we can detect this without opening a browser by checking that first
            // redirect, avoiding a 3-minute listener timeout for the user.
            var authorizeUrl = BuildAuthorizeUrl(authorizeEndpoint, clientId, redirectUri,
                scope, codeChallenge, stateHash, useCognito ? null : userName, additionalParams);

            if (useCognito)
                authorizeUrl += "&prompt=login";

            // Pre-flight check: skip for Cognito because the redirect URI is now exactly matched
            // (http://localhost:51555/callback). Running the pre-flight against Cognito can
            // start a spurious OAuth session that delivers a callback to our listener before the
            // user even opens the browser, causing state-mismatch failures.
            if (!useCognito)
            {
                var preflightError = await CheckRedirectUriAcceptedAsync(authorizeUrl, ct);
                if (preflightError is not null)
                {
                    var idpName = "Azure AD / OAuth";
                    return new AuthResult(false, $"{idpName}: {preflightError}");
                }
            }

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            // Step 5: Wait for IdP to redirect back with authorization code
            var callbackResult = await WaitForCallbackAsync(listener, stateHash, ct);
            if (callbackResult.Error is not null)
                return new AuthResult(false, $"SSO failed: {callbackResult.Error}");

            var authCode = callbackResult.Code!;

            // Step 6: Call Adept server's /api/Account/login with auth code + PKCE verifier
            var loginRequest = new AccountLoginRequest
            {
                UserName = userName,   // send the typed username, not the literal "SSO"
                Password = "",
                AuthCode = authCode,
                SsoStateReceived = stateHash,
                SsoStateHash = stateHash,
                RedirectUri = redirectUri,
                CodeVerifier = codeVerifier,
                ForceLogin = false,
                ClientId = "Adept"
            };

            // Use PostLoginAsync so we read the body on 4xx responses and surface the real error.
            var (authResponse, rawBody) = await PostLoginAsync(loginRequest, ct);
            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            if (authResponse.StatusCode != 0 && IsAlreadyLoggedInError(authResponse))
            {
                loginRequest.ForceLogin = true;
                (authResponse, rawBody) = await PostLoginAsync(loginRequest, ct);
                if (authResponse is null)
                    return new AuthResult(false, "Empty response from server");
            }

            // Status 230 — multiple Adept users are linked to this IdP identity.
            if (authResponse.StatusCode == 230)
            {
                _pending230Response = authResponse;
                _pending230StateHash = stateHash;  // save the SSO session identifier
                List<UserChoice>? choices;
                try { choices = ParseUserChoices(rawBody!); }
                catch (Exception ex) { return new AuthResult(false, $"Status 230 parse error: {ex.GetType().Name}: {ex.Message}"); }
                return new AuthResult(false, RequiresUserSelection: true, UserChoices: choices);
            }

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
                WorkAreaId: authResponse.WorkAreaId);        }
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
            listener.Close(); // Close() releases the port; Stop() alone does not.
        }
    }

    /// <summary>
    /// Makes a no-redirect GET to the OAuth authorize URL and checks whether Cognito/IdP
    /// immediately rejects the redirect_uri (redirect_mismatch). Returns a human-readable
    /// error string if rejected, or null if the redirect URI appears to be accepted.
    /// </summary>
    private static async Task<string?> CheckRedirectUriAcceptedAsync(string authorizeUrl, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(authorizeUrl, ct);

            // Cognito responds with 302 → its own /error page when redirect_uri is not registered.
            if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently)
            {
                var location = response.Headers.Location?.ToString() ?? "";
                if (location.Contains("error=redirect_mismatch") || location.Contains("error=invalid_redirect_uri"))
                {
                    return "SSO failed: the OAuth/Cognito app client does not have 'http://localhost' " +
                           "in its allowed callback URLs. " +
                           "An administrator must add 'http://localhost' to the Cognito (or OAuth) " +
                           "app client's allowed callback URLs. " +
                           "Per AWS documentation, registering 'http://localhost' allows any localhost port.";
                }
            }
        }
        catch
        {
            // Pre-flight is best-effort; if it fails just proceed and let the listener handle it.
        }

        return null;
    }

    private async Task<AuthResult> LoginWithWindowsSsoAsync(string userName, CancellationToken ct)
    {
        try
        {
            var loginRequest = new AccountLoginRequest
            {
                UserName = userName,
                Password = "",
                WindowsDomain = Environment.UserDomainName,
                WindowsLogin = Environment.UserName,
                ForceLogin = false,
                ClientId = "Adept"
            };

            var (authResponse, rawBody) = await PostLoginAsync(loginRequest, ct);
            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");

            if (authResponse.StatusCode != 0 && IsAlreadyLoggedInError(authResponse))
            {
                loginRequest.ForceLogin = true;
                (authResponse, rawBody) = await PostLoginAsync(loginRequest, ct);
                if (authResponse is null)
                    return new AuthResult(false, "Empty response from server");
            }

            if (authResponse.StatusCode == 230)
            {
                _pending230Response = authResponse;
                List<UserChoice>? ch;
                try { ch = ParseUserChoices(rawBody!); } catch { ch = null; }
                return new AuthResult(false, RequiresUserSelection: true, UserChoices: ch);
            }

            if (authResponse.StatusCode != 0)
                return new AuthResult(false, GetFriendlyAuthErrorMessage(authResponse, "Windows SSO login failed"));

            return BuildAuthResult(authResponse);
        }
        catch (HttpRequestException ex)
        {
            return new AuthResult(false, $"Windows SSO login failed: {ex.Message}");
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

            var (authResponse2, rawBody2) = await PostLoginAsync(loginRequest, ct);
            if (authResponse2 is null)
                return new AuthResult(false, "Empty response from server");

            // If user is already logged in, retry with forceLogin
            if (authResponse2.StatusCode != 0 && IsAlreadyLoggedInError(authResponse2))
            {
                loginRequest.ForceLogin = true;
                (authResponse2, rawBody2) = await PostLoginAsync(loginRequest, ct);
                if (authResponse2 is null)
                    return new AuthResult(false, "Empty response from server");
            }

            if (authResponse2.StatusCode == 230)
            {
                _pending230Response = authResponse2;
                List<UserChoice>? ch;
                try { ch = ParseUserChoices(rawBody2!); } catch { ch = null; }
                return new AuthResult(false, RequiresUserSelection: true, UserChoices: ch);
            }

            if (authResponse2.StatusCode != 0)
                return new AuthResult(false, GetFriendlyAuthErrorMessage(authResponse2, "Login failed"));

            return BuildAuthResult(authResponse2);
        }
        catch (HttpRequestException ex)
        {
            return new AuthResult(false, $"Login failed: {ex.Message}");
        }
    }

    private async Task<(AuthenticateResponse? Response, string? RawBody)> PostLoginAsync(AccountLoginRequest request, CancellationToken ct)
    {
        var loginUrl = $"{_serverBaseUrl}api/Account/login";
        var response = await _httpClient.PostAsJsonAsync(loginUrl, request, JsonOptions, ct);

        // Read body regardless of status code — the server returns JSON even on 400
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            var statusText = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"})";
            var hint = response.StatusCode == HttpStatusCode.NotFound
                ? " The server URL may be missing the Adept web app path (for example '/AdeptWeb/')."
                : string.Empty;
            var synthetic = new AuthenticateResponse
            {
                StatusCode = -1,
                ErrorMessage = $"Login endpoint returned an empty response body from '{loginUrl}' with {statusText}.{hint}"
            };
            return (synthetic, string.Empty);
        }

        AuthenticateResponse? auth;
        try
        {
            auth = JsonSerializer.Deserialize<AuthenticateResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Return a synthetic error response that surfaces the raw body for diagnosis.
            var hint = response.StatusCode == HttpStatusCode.NotFound
                ? " The server URL may be missing the Adept web app path (for example '/AdeptWeb/')."
                : string.Empty;
            auth = new AuthenticateResponse
            {
                StatusCode = -1,
                ErrorMessage = $"Unexpected server response (HTTP {(int)response.StatusCode}): {ex.Message} — Body: {body[..Math.Min(300, body.Length)]}.{hint}"
            };
        }
        return (auth, body);
    }

    /// <summary>Extracts the user list from the raw status-230 response body.</summary>
    private static List<UserChoice>? ParseUserChoices(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);

        // The server may encode the user list in several places.
        // Collect candidate JSON strings to search through in order.
        var candidates = new List<(string Source, string Json)>();

        // 1. Top-level 'data' or 'resultData' arrays/strings
        foreach (var propName in new[] { "data", "resultData" })
        {
            if (doc.RootElement.TryGetProperty(propName, out var el))
            {
                string? j = el.ValueKind == JsonValueKind.String ? el.GetString()
                           : el.ValueKind == JsonValueKind.Array  ? el.GetRawText()
                           : null;
                if (!string.IsNullOrWhiteSpace(j) && j != "[]")
                    candidates.Add((propName, j));
            }
        }

        // 2. The 'errorMessage' field may itself be a JSON object string containing 'data'.
        if (doc.RootElement.TryGetProperty("errorMessage", out var emEl) &&
            emEl.ValueKind == JsonValueKind.String)
        {
            var inner = emEl.GetString();
            if (!string.IsNullOrWhiteSpace(inner))
                candidates.Add(("errorMessage(inner)", inner));
        }

        foreach (var (source, json) in candidates)
        {
            // The candidate may be the array itself, or a JSON object that contains 'data'.
            List<UserSelectionItem>? items = null;
            if (json.TrimStart().StartsWith("["))
            {
                // Direct array
                items = JsonSerializer.Deserialize<List<UserSelectionItem>>(json, JsonOptions);
            }
            else if (json.TrimStart().StartsWith("{"))
            {
                // Object — look for 'data' inside it
                using var inner = JsonDocument.Parse(json);
                foreach (var innerProp in new[] { "data", "resultData" })
                {
                    if (inner.RootElement.TryGetProperty(innerProp, out var innerEl))
                    {
                        string? arrJson = innerEl.ValueKind == JsonValueKind.String ? innerEl.GetString()
                                         : innerEl.ValueKind == JsonValueKind.Array  ? innerEl.GetRawText()
                                         : null;
                        if (!string.IsNullOrWhiteSpace(arrJson) && arrJson != "[]")
                        {
                            items = JsonSerializer.Deserialize<List<UserSelectionItem>>(arrJson, JsonOptions);
                            break;
                        }
                    }
                }
            }

            if (items is { Count: > 0 })
            {
                return items
                    .Where(u => !string.IsNullOrEmpty(u.Id) && !string.IsNullOrEmpty(u.UserName))
                    .Select(u => new UserChoice(u.Id!, u.UserName!, u.TypeLabel))
                    .ToList();
            }
        }

        var keys = string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name));
        throw new Exception($"Could not find a non-empty user list in any expected field. Keys: [{keys}]");
    }

    /// <summary>
    /// <summary>
    /// Completes login after status 230 by re-calling /api/Account/login with the access_token
    /// from the 230 response in the Authorization header plus the chosen user ID.
    /// The original auth code must NOT be re-sent — it is already consumed.
    /// </summary>
    public async Task<AuthResult> SelectUserAsync(string userId, string userName, CancellationToken ct = default)
    {
        if (_pending230Response is null)
            return new AuthResult(false, "No pending user selection — please log in again.");

        var pending = _pending230Response;
        var pendingStateHash = _pending230StateHash;
        _pending230Response = null;
        _pending230StateHash = null;

        // The 230 response already has a valid access_token — the server completed the OAuth
        // exchange and is just waiting for user-account disambiguation.
        // Try using the 230 token directly first; if the selected user matches the one in the
        // 230 response we're done. Otherwise attempt a disambiguation call.
        if (!string.IsNullOrEmpty(pending.AccessToken))
        {
            // If the 230 userId matches the selected userId, accept the 230 session as-is.
            if (string.Equals(pending.UserId, userId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pending.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                return BuildAuthResult(pending);
            }
        }

        try
        {
            // Disambiguation call: tell the server which account to use for this SSO session.
            var selectionRequest = new AccountLoginRequest
            {
                UserName = userName,
                UserSelection = userId,
                SsoStateReceived = pendingStateHash,
                SsoStateHash = pendingStateHash,
                SsoNonce = pending.SsoNonce,
                ClientId = "Adept",
                ForceLogin = false
            };

            // Include the 230 Bearer token so the server can find the pending session.
            var previousAuth = _httpClient.DefaultRequestHeaders.Authorization;
            if (!string.IsNullOrEmpty(pending.AccessToken))
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pending.AccessToken);

            var (authResponse, _) = await PostLoginAsync(selectionRequest, ct);

            if (!IsAuthenticated)
                _httpClient.DefaultRequestHeaders.Authorization = previousAuth;

            if (authResponse is null)
                return new AuthResult(false, "Empty response from server");
            if (authResponse.StatusCode != 0)
                return new AuthResult(false, GetFriendlyAuthErrorMessage(authResponse, "User selection failed"));

            return BuildAuthResult(authResponse);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"User selection error: {ex.Message}");
        }
    }

    private AuthResult BuildAuthResult(AuthenticateResponse r)
    {
        AccessToken = r.AccessToken;
        _refreshToken = r.RefreshToken;
        IsAuthenticated = true;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        return new AuthResult(
            Success: true,
            AccessToken: r.AccessToken,
            UserId: r.UserId,
            UserName: r.UserName,
            DisplayName: r.DisplayName,
            EmailAddress: r.EmailAddress,
            AppVersion: r.AppVersion,
            WorkAreaId: r.WorkAreaId);
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

    private static string GetFriendlyAuthErrorMessage(AuthenticateResponse response, string fallback)
    {
        // Adept may wrap the actual message in a JSON string inside errorMessage.
        var message = response.ErrorMessage;
        if (!string.IsNullOrWhiteSpace(message) && message.TrimStart().StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("message", out var innerMessage) &&
                    innerMessage.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(innerMessage.GetString()))
                {
                    message = innerMessage.GetString();
                }
            }
            catch
            {
                // Keep original message if nested JSON cannot be parsed.
            }
        }

        if (response.StatusCode == 176)
        {
            return "Login failed: the selected Adept account is currently in use. " +
                   "Close the existing session for that account or choose another user.";
        }

        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        IsAuthenticated = false;
        AccessToken = null;
        _refreshToken = null;
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
            _refreshToken = null;
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

    private static (HttpListener? Listener, string RedirectUri) StartCallbackListener(bool useCognito)
    {
        if (useCognito)
        {
            // Use the exact redirect URI registered in the Cognito app client.
            // Retry briefly — ephemeral ports from other processes (e.g. VMware NAT) can
            // temporarily occupy 51555 in CLOSE_WAIT and block http.sys from binding.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (attempt > 0)
                    Thread.Sleep(1000);

                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{CognitoCallbackPort}/");
                try
                {
                    listener.Start();
                    return (listener, $"http://localhost:{CognitoCallbackPort}/{CognitoCallbackPath}");
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                }
            }
            return (null, string.Empty);
        }

        // OAuth / Azure AD — try a range of high ports.
        for (int port = CallbackPortRangeStart; port <= CallbackPortRangeEnd; port++)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
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
        // Add a 3-minute safety timeout in case the browser shows an error (e.g. redirect_mismatch)
        // and the user never gets redirected back to our localhost listener.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        using var reg = linkedCts.Token.Register(() => listener.Close());

        // Loop until we receive a request that looks like an OAuth callback.
        // The browser may fire ancillary requests (favicon.ico, etc.) before the real callback.
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (linkedCts.Token.IsCancellationRequested)
            {
                if (timeoutCts.IsCancellationRequested)
                    return new CallbackResult
                    {
                        Error = "SSO timed out waiting for browser callback. " +
                                "This usually means the redirect URI 'http://localhost' is not registered " +
                                "in the server's OAuth/Cognito app client. " +
                                "An administrator must add 'http://localhost' to the allowed callback URLs."
                    };
                return new CallbackResult { Error = "Cancelled" };
            }

            var query = context.Request.Url?.Query ?? "";
            var parameters = HttpUtility.ParseQueryString(query);
            var hasCode  = !string.IsNullOrEmpty(parameters["code"]);
            var hasError = !string.IsNullOrEmpty(parameters["error"]);

            // Not a callback request (e.g. favicon) — return 204 and keep waiting.
            if (!hasCode && !hasError)
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                continue;
            }

            // This is a callback request — check state BEFORE writing any response,
            // since writing closes the response object and it cannot be reused.
            var error = parameters["error"];
            var returnedState = parameters["state"];
            var code = parameters["code"];

            // State mismatch: stale redirect from a previous session — skip and keep waiting.
            if (string.IsNullOrEmpty(error) && returnedState != expectedState)
            {
                var skipHtml = Encoding.UTF8.GetBytes("<html><body><p>Waiting for authentication...</p></body></html>");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = skipHtml.Length;
                await context.Response.OutputStream.WriteAsync(skipHtml, ct);
                context.Response.Close();
                continue;
            }

            // State matches (or an error was returned) — respond to the browser and return result.
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

            if (string.IsNullOrEmpty(code))
                return new CallbackResult { Error = "No authorization code received" };

            return new CallbackResult { Code = code };
        } // end while (true)
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
