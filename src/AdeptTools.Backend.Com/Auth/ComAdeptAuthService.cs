using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Core.Auth;

namespace AdeptTools.Backend.Com.Auth;

/// <summary>
/// COM-based implementation of IAdeptAuthService.
/// Connects to an Adept server via the NxProject COM object.
/// </summary>
public class ComAdeptAuthService : IAdeptAuthService
{
    private readonly ILegacyCoreApiSession _legacySession;
    private readonly ComSessionManager? _sessionManager;

    public ComAdeptAuthService(ILegacyCoreApiSession legacySession, ComSessionManager? sessionManager = null)
    {
        _legacySession = legacySession;
        _sessionManager = sessionManager;
    }

    public bool IsAuthenticated => _legacySession.IsConnected;

    /// <summary>
    /// COM sessions don't use tokens — returns a sentinel value when connected.
    /// </summary>
    public string? AccessToken => IsAuthenticated ? "com-session-active" : null;

    public async Task<AuthResult> LoginAsync(string serverUrl, string userName, string password = "", CancellationToken ct = default)
    {
        try
        {
            var result = await _legacySession.ConnectAsync(serverUrl, userName, password, ct);

            if (result != 0)
            {
                return new AuthResult(
                    Success: false,
                    ErrorMessage: $"COM connection failed with error code {result}.");
            }

            // Best-effort: also connect the NxProject SDK session so that
            // GetWorkflowAdmin() is available via the typed SDK fast path.
            // CoreApi (legacy) does not expose GetWorkflowAdmin; NxProject does.
            if (_sessionManager is not null && !_sessionManager.IsConnected)
            {
                try
                {
                    await _sessionManager.ConnectAsync(serverUrl, userName, password, ct);
                }
                catch
                {
                    // NxProject connection failure is non-fatal; fall back to BFS.
                }
            }

            var info = await _legacySession.GetSessionInfoAsync(ct);

            return new AuthResult(
                Success: true,
                AccessToken: "com-session-active",
                UserId: info.UserId,
                UserName: info.UserName,
                DisplayName: info.DisplayName,
                EmailAddress: info.EmailAddress,
                AppVersion: info.AppVersion,
                WorkAreaId: info.WorkAreaId);
        }
        catch (Exception ex)
        {
            return new AuthResult(
                Success: false,
                ErrorMessage: BuildErrorMessage(ex));
        }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current is not null)
        {
            parts.Add(current.Message);
            current = current.InnerException;
        }
        return string.Join(" → ", parts);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await _legacySession.DisconnectAsync(ct);

        if (_sessionManager is not null && _sessionManager.IsConnected)
        {
            try
            {
                await _sessionManager.DisconnectAsync(ct);
            }
            catch
            {
                // Best-effort disconnect.
            }
        }
    }

    public Task<AuthResult> RefreshAsync(CancellationToken ct = default)
    {
        // COM sessions don't expire — if still connected, report success
        if (IsAuthenticated)
        {
            return Task.FromResult(new AuthResult(
                Success: true,
                AccessToken: "com-session-active"));
        }

        return Task.FromResult(new AuthResult(
            Success: false,
            ErrorMessage: "COM session is not connected."));
    }

    public Task<AuthResult> SelectUserAsync(string userId, string userName, CancellationToken ct = default)
        => Task.FromResult(new AuthResult(false, "User selection is not applicable for COM connections."));
}
