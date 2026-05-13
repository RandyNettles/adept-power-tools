using AdeptTools.Backend.Com.Infrastructure;
using Xunit;

namespace AdeptTools.Backend.Com.Tests;

public class ComLifecycleTests
{
    [Fact]
    public void Release_WithNull_DoesNotThrow()
    {
        object? obj = null;
        ComLifecycle.Release(ref obj);
        Assert.Null(obj);
    }

    [Fact]
    public void Release_WithNonComObject_SetsToNull()
    {
        object? obj = "not a COM object";
        ComLifecycle.Release(ref obj);
        Assert.Null(obj);
    }

    [Fact]
    public void ReleaseGeneric_WithNull_DoesNotThrow()
    {
        string? obj = null;
        ComLifecycle.Release(ref obj);
        Assert.Null(obj);
    }

    [Fact]
    public void UseAndRelease_ReturnsResult()
    {
        var input = "test value";
        var result = ComLifecycle.UseAndRelease(input, s => s.Length);
        Assert.Equal(10, result);
    }

    [Fact]
    public void CreateInstance_InvalidProgId_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            ComLifecycle.CreateInstance<object>("NonExistent.ProgId.12345"));
    }

    [Fact]
    public void GetActiveInstance_InvalidProgId_ReturnsNull()
    {
        var result = ComLifecycle.GetActiveInstance<object>("NonExistent.ProgId.12345");
        Assert.Null(result);
    }
}
