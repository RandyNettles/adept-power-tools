using AdeptTools.Core.Api;
using AdeptTools.Core.Auth;
using Xunit;

namespace AdeptTools.Core.Tests;

public class MockAdeptApiClientTests
{
    [Fact]
    public async Task GetUserInfoAsync_ReturnsMockData()
    {
        var authService = new MockAdeptAuthService();
        var client = new MockAdeptApiClient(authService);

        var userInfo = await client.GetUserInfoAsync();

        Assert.NotNull(userInfo);
        Assert.Equal("MockUser", userInfo.UserName);
        Assert.Equal("Mock User", userInfo.DisplayName);
        Assert.Equal("mock-1.0.0", userInfo.AppVersion);
    }

    [Fact]
    public async Task IsLoggedInAsync_WhenNotAuthenticated_ReturnsFalse()
    {
        var authService = new MockAdeptAuthService();
        var client = new MockAdeptApiClient(authService);

        var isLoggedIn = await client.IsLoggedInAsync();

        Assert.False(isLoggedIn);
    }

    [Fact]
    public async Task IsLoggedInAsync_WhenAuthenticated_ReturnsTrue()
    {
        var authService = new MockAdeptAuthService();
        await authService.LoginAsync("mock://localhost", "user", "pass");
        var client = new MockAdeptApiClient(authService);

        var isLoggedIn = await client.IsLoggedInAsync();

        Assert.True(isLoggedIn);
    }

    [Fact]
    public async Task GetServerVersionAsync_ReturnsMockVersion()
    {
        var authService = new MockAdeptAuthService();
        var client = new MockAdeptApiClient(authService);

        var version = await client.GetServerVersionAsync();

        Assert.Equal("mock-1.0.0", version);
    }
}
