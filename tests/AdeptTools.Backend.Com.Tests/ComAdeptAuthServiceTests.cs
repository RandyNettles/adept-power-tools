using AdeptTools.Backend.Com.Auth;
using AdeptTools.Backend.Com.Infrastructure;
using Xunit;

namespace AdeptTools.Backend.Com.Tests;

public class ComAdeptAuthServiceTests : IDisposable
{
    private readonly ComOperationRunner _runner = new();

    [Fact]
    public void IsAuthenticated_WhenNotConnected_ReturnsFalse()
    {
        var session = new ComSessionManager(_runner);
        var authService = new ComAdeptAuthService(_runner, session);

        Assert.False(authService.IsAuthenticated);
    }

    [Fact]
    public void AccessToken_WhenNotConnected_ReturnsNull()
    {
        var session = new ComSessionManager(_runner);
        var authService = new ComAdeptAuthService(_runner, session);

        Assert.Null(authService.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_WhenNotConnected_ReturnsFailure()
    {
        var session = new ComSessionManager(_runner);
        var authService = new ComAdeptAuthService(_runner, session);

        var result = await authService.RefreshAsync();

        Assert.False(result.Success);
        Assert.Equal("COM session is not connected.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WithoutComSdk_ReturnsError()
    {
        var session = new ComSessionManager(_runner);
        var authService = new ComAdeptAuthService(_runner, session);

        // This will fail because the COM SDK is not installed on the build machine
        var result = await authService.LoginAsync("http://localhost", "admin", "password");

        Assert.False(result.Success);
        Assert.Contains("COM connection error", result.ErrorMessage!);
    }

    [Fact]
    public async Task LogoutAsync_WhenNotConnected_CompletesWithoutError()
    {
        var session = new ComSessionManager(_runner);
        var authService = new ComAdeptAuthService(_runner, session);

        await authService.LogoutAsync();
        // Should not throw
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
