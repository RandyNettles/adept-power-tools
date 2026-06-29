namespace AdeptTools.Core.Auth;

public interface IAdeptAuthService
{
    Task<AuthResult> LoginAsync(string serverUrl, string userName, string password = "", CancellationToken ct = default);

    /// <summary>
    /// Completes login after the user has selected one of the multiple accounts returned in a
    /// <see cref="AuthResult.RequiresUserSelection"/> response (server status 230).
    /// </summary>
    Task<AuthResult> SelectUserAsync(string userId, string userName, CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(CancellationToken ct = default);
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
}
