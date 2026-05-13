using AdeptTools.Backend.Com.Infrastructure;
using Xunit;

namespace AdeptTools.Backend.Com.Tests;

public class ComOperationRunnerTests : IDisposable
{
    private readonly ComOperationRunner _runner = new();

    [Fact]
    public async Task RunAsync_ExecutesOnStaThread()
    {
        var apartmentState = await _runner.RunAsync(() =>
            Thread.CurrentThread.GetApartmentState());

        Assert.Equal(ApartmentState.STA, apartmentState);
    }

    [Fact]
    public async Task RunAsync_ReturnsResult()
    {
        var result = await _runner.RunAsync(() => 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_PropagatesException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _runner.RunAsync<int>(() => throw new InvalidOperationException("test error")));
    }

    [Fact]
    public async Task RunAsync_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _runner.RunAsync(() => 1, cts.Token));
    }

    [Fact]
    public async Task RunAsync_VoidAction_Executes()
    {
        var executed = false;

        await _runner.RunAsync(() => { executed = true; });

        Assert.True(executed);
    }

    [Fact]
    public async Task RunAsync_SequentialOperations_ExecuteInOrder()
    {
        var results = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            var value = i;
            await _runner.RunAsync(() => { results.Add(value); });
        }

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, results);
    }

    [Fact]
    public async Task RunAsync_AllOperationsShareSameThread()
    {
        var threadIds = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            var threadId = await _runner.RunAsync(() => Environment.CurrentManagedThreadId);
            threadIds.Add(threadId);
        }

        Assert.True(threadIds.Distinct().Count() == 1, "All operations should run on the same STA thread.");
    }

    [Fact]
    public void Dispose_CompletesGracefully()
    {
        var runner = new ComOperationRunner();
        runner.Dispose();
        // Should not throw or hang
    }

    [Fact]
    public async Task RunAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var runner = new ComOperationRunner();
        runner.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            runner.RunAsync(() => 1));
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
