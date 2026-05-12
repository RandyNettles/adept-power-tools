namespace AdeptTools.Core.Auth;

public class MockAdeptAuthService : IAdeptAuthService
{
    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public Task<AuthResult> LoginAsync(string serverUrl, string userName, string password, CancellationToken ct = default)
    {
        AccessToken = $"mock-jwt-token-{Guid.NewGuid():N}";
        IsAuthenticated = true;

        return Task.FromResult(new AuthResult(
            Success: true,
            AccessToken: AccessToken,
            UserId: "mock-user-001",
            UserName: userName,
            DisplayName: "Mock User",
            EmailAddress: "mock@example.com",
            AppVersion: "mock-1.0.0",
            WorkAreaId: "mock-workarea-001"));
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        IsAuthenticated = false;
        AccessToken = null;
        return Task.CompletedTask;
    }

    public Task<AuthResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
            return Task.FromResult(new AuthResult(false, "Not authenticated"));

        AccessToken = $"mock-jwt-token-{Guid.NewGuid():N}";
        return Task.FromResult(new AuthResult(true, AccessToken: AccessToken));
    }
}
