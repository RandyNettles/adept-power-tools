using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;

namespace AdeptTools.Core.Api;

public class MockAdeptApiClient : IAdeptApiClient
{
    private readonly IAdeptAuthService _authService;

    public MockAdeptApiClient(IAdeptAuthService authService)
    {
        _authService = authService;
    }

    public Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new UserInfo
        {
            UserId = "mock-user-001",
            UserName = "MockUser",
            DisplayName = "Mock User",
            EmailAddress = "mock@example.com",
            AppVersion = "mock-1.0.0",
            WorkAreaId = "mock-workarea-001"
        });
    }

    public Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_authService.IsAuthenticated);
    }

    public Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        return Task.FromResult("mock-1.0.0");
    }
}
