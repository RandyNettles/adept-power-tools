using AdeptTools.Backend.Com.Infrastructure;
using Xunit;

namespace AdeptTools.Backend.Com.Tests;

public class ComSessionManagerTests : IDisposable
{
    private readonly ComOperationRunner _runner = new();

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        var session = new ComSessionManager(_runner);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void GetProject_WhenNotConnected_ThrowsInvalidOperation()
    {
        var session = new ComSessionManager(_runner);
        Assert.Throws<InvalidOperationException>(() => session.GetProject());
    }

    [Fact]
    public void Dispose_WhenNotConnected_DoesNotThrow()
    {
        var session = new ComSessionManager(_runner);
        session.Dispose();
        // Should not throw
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
