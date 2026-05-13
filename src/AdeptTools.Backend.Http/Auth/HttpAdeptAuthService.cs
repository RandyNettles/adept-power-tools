using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using AdeptTools.Backend.Http.Models;
using AdeptTools.Core.Auth;

namespace AdeptTools.Backend.Http.Auth;

public class HttpAdeptAuthService : IAdeptAuthService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string CallbackPath = "/sso-callback";
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
        _httpClient.BaseAddress = new Uri(serverUrl.TrimEnd('/'));

        // SSO flow: open browser to server's SSO endpoint, listen for callback with token
        var (listener, redirectUri) = StartCallbackListener();
        if (listener is null)
            return new AuthResult(false, "Unable to start SSO callback listener (ports in use)");

        try
        {
            var ssoUrl = BuildSsoUrl(serverUrl, redirectUri, userName);
            Process.Start(new ProcessStartInfo(ssoUrl) { UseShellExecute = true });

            // Wait for the SSO callback
            var callbackResult = await WaitForCallbackAsync(listener, ct);
            if (callbackResult is null)
                return new AuthResult(false, "SSO callback was not received");

            if (!string.IsNullOrEmpty(callbackResult.Error))
                return new AuthResult(false, $"SSO failed: {callbackResult.Error}");

            AccessToken = callbackResult.AccessToken;
            IsAuthenticated = true;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

            return new AuthResult(
                Success: true,
                AccessToken: callbackResult.AccessToken,
                UserId: callbackResult.UserId,
                UserName: callbackResult.UserName,
                DisplayName: callbackResult.DisplayName,
                EmailAddress: callbackResult.EmailAddress,
                AppVersion: callbackResult.AppVersion,
                WorkAreaId: callbackResult.WorkAreaId);
        }
        catch (OperationCanceledException)
        {
            return new AuthResult(false, "SSO login was cancelled");
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

    private static string BuildSsoUrl(string serverUrl, string redirectUri, string userName)
    {
        var baseUrl = serverUrl.TrimEnd('/');
        var encoded = Uri.EscapeDataString(redirectUri);
        var userParam = string.IsNullOrWhiteSpace(userName) ? "" : $"&login_hint={Uri.EscapeDataString(userName)}";
        return $"{baseUrl}/api/account/sso?redirect_uri={encoded}&client_id=AdeptTool&platform=AdeptTool{userParam}";
    }

    private static (HttpListener? Listener, string RedirectUri) StartCallbackListener()
    {
        for (int port = CallbackPortRangeStart; port <= CallbackPortRangeEnd; port++)
        {
            var prefix = $"http://localhost:{port}{CallbackPath}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                return (listener, $"http://localhost:{port}{CallbackPath}");
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        return (null, string.Empty);
    }

    private static async Task<SsoCallbackResult?> WaitForCallbackAsync(HttpListener listener, CancellationToken ct)
    {
        using var reg = ct.Register(() => listener.Stop());
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException) when (ct.IsCancellationRequested)
        {
            return null;
        }

        var query = context.Request.Url?.Query ?? "";
        var parameters = HttpUtility.ParseQueryString(query);

        // Respond to the browser with a success page
        var responseHtml = "<html><body><h2>Login successful</h2><p>You can close this window.</p></body></html>";
        var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, ct);
        context.Response.Close();

        var error = parameters["error"];
        if (!string.IsNullOrEmpty(error))
            return new SsoCallbackResult { Error = parameters["error_description"] ?? error };

        return new SsoCallbackResult
        {
            AccessToken = parameters["access_token"],
            UserId = parameters["user_id"],
            UserName = parameters["user_name"],
            DisplayName = parameters["display_name"],
            EmailAddress = parameters["email"],
            AppVersion = parameters["app_version"],
            WorkAreaId = parameters["work_area_id"]
        };
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

    private class SsoCallbackResult
    {
        public string? AccessToken { get; init; }
        public string? UserId { get; init; }
        public string? UserName { get; init; }
        public string? DisplayName { get; init; }
        public string? EmailAddress { get; init; }
        public string? AppVersion { get; init; }
        public string? WorkAreaId { get; init; }
        public string? Error { get; init; }
    }
}
