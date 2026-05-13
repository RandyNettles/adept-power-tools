using System.Collections.Concurrent;

namespace AdeptTools.Backend.Com.Infrastructure;

/// <summary>
/// Schedules COM operations onto a dedicated STA (Single-Threaded Apartment) thread.
/// All COM interop calls must execute on an STA thread to maintain proper COM threading behavior.
/// This runner creates one STA thread at startup and queues all work items onto it.
/// </summary>
public sealed class ComOperationRunner : IDisposable
{
    private readonly Thread _staThread;
    private readonly BlockingCollection<ComWorkItem> _workQueue = new();
    private bool _disposed;

    public ComOperationRunner()
    {
        _staThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "AdeptTools-COM-STA"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
    }

    /// <summary>
    /// Executes an action on the STA thread and returns the result.
    /// </summary>
    public Task<T> RunAsync<T>(Func<T> operation, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        var workItem = new ComWorkItem(() =>
        {
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                var result = operation();
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                registration.Dispose();
            }
        });

        if (!_workQueue.TryAdd(workItem))
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(ComOperationRunner)));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Executes a void action on the STA thread.
    /// </summary>
    public Task RunAsync(Action operation, CancellationToken ct = default)
    {
        return RunAsync(() => { operation(); return 0; }, ct);
    }

    private void ProcessQueue()
    {
        foreach (var workItem in _workQueue.GetConsumingEnumerable())
        {
            workItem.Execute();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _workQueue.CompleteAdding();

        // Give the STA thread time to finish remaining work
        if (_staThread.IsAlive)
            _staThread.Join(TimeSpan.FromSeconds(5));

        _workQueue.Dispose();
    }

    private sealed class ComWorkItem
    {
        private readonly Action _action;

        public ComWorkItem(Action action) => _action = action;

        public void Execute() => _action();
    }
}
