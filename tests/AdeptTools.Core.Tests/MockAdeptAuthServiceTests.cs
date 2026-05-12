using AdeptTools.Core.Auth;
using Xunit;

namespace AdeptTools.Core.Tests;

public class MockAdeptAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsSuccess()
    {
        var service = new MockAdeptAuthService();

        var result = await service.LoginAsync("mock://localhost", "testuser", "testpass");

        Assert.True(result.Success);
        Assert.NotNull(result.AccessToken);
        Assert.Equal("testuser", result.UserName);
        Assert.Equal("Mock User", result.DisplayName);
    }

    [Fact]
    public async Task LoginAsync_SetsIsAuthenticated()
    {
        var service = new MockAdeptAuthService();
        Assert.False(service.IsAuthenticated);

        await service.LoginAsync("mock://localhost", "testuser", "testpass");

        Assert.True(service.IsAuthenticated);
        Assert.NotNull(service.AccessToken);
    }

    [Fact]
    public async Task LogoutAsync_ClearsAuthState()
    {
        var service = new MockAdeptAuthService();
        await service.LoginAsync("mock://localhost", "testuser", "testpass");

        await service.LogoutAsync();

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_WhenNotAuthenticated_ReturnsFalse()
    {
        var service = new MockAdeptAuthService();

        var result = await service.RefreshAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_WhenAuthenticated_ReturnsNewToken()
    {
        var service = new MockAdeptAuthService();
        await service.LoginAsync("mock://localhost", "testuser", "testpass");
        var originalToken = service.AccessToken;

        var result = await service.RefreshAsync();

        Assert.True(result.Success);
        Assert.NotEqual(originalToken, service.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_AcceptsAnyCredentials()
    {
        var service = new MockAdeptAuthService();

        var result1 = await service.LoginAsync("mock://any", "user1", "pass1");
        var result2 = await service.LoginAsync("mock://any", "user2", "");
        var result3 = await service.LoginAsync("mock://any", "", "pass3");

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
    }
}
