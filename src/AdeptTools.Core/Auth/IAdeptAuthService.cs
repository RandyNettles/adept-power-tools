namespace AdeptTools.Core.Auth;

public interface IAdeptAuthService
{
    Task<AuthResult> LoginAsync(string serverUrl, string userName, string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(CancellationToken ct = default);
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
}
