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

    public ComAdeptAuthService(ILegacyCoreApiSession legacySession)
    {
        _legacySession = legacySession;
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
