using AdeptTools.Backend.Com.Interop;

namespace AdeptTools.Backend.Com.Infrastructure;

/// <summary>
/// Manages the lifetime of the COM NxProject session.
/// Shared across all COM backend services as a singleton.
/// </summary>
public sealed class ComSessionManager : IDisposable
{
    private readonly ComOperationRunner _runner;
    private INxProject? _project;
    private INxDb? _database;
    private INxWorkflowAdmin? _workflowAdmin;
    private bool _disposed;

    private const string ProjectProgId = "AdeptSDK.NxProject";

    public ComSessionManager(ComOperationRunner runner)
    {
        _runner = runner;
    }

    public bool IsConnected => _project?.IsConnected ?? false;

    /// <summary>
    /// Connects to an Adept server via COM. Must be called before other operations.
    /// </summary>
    public async Task<int> ConnectAsync(string serverUrl, string userId, string password, CancellationToken ct = default)
    {
        return await _runner.RunAsync(() =>
        {
            _project = ComLifecycle.CreateInstance<INxProject>(ProjectProgId);
            return _project.Connect(serverUrl, userId, password);
        }, ct);
    }

    /// <summary>
    /// Gets the active NxProject COM object. Throws if not connected.
    /// </summary>
    public INxProject GetProject()
    {
        return _project ?? throw new InvalidOperationException(
            "COM session is not connected. Call ConnectAsync first.");
    }

    /// <summary>
    /// Gets or creates the NxDb COM object from the current project.
    /// </summary>
    public async Task<INxDb> GetDatabaseAsync(CancellationToken ct = default)
    {
        if (_database != null) return _database;

        _database = await _runner.RunAsync(() => GetProject().GetDatabase(), ct);
        return _database;
    }

    /// <summary>
    /// Gets or creates the NxWorkflowAdmin COM object from the current project.
    /// </summary>
    public async Task<INxWorkflowAdmin> GetWorkflowAdminAsync(CancellationToken ct = default)
    {
        if (_workflowAdmin != null) return _workflowAdmin;

        _workflowAdmin = await _runner.RunAsync(() => GetProject().GetWorkflowAdmin(), ct);
        return _workflowAdmin;
    }

    /// <summary>
    /// Disconnects the COM session and releases all COM objects.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _runner.RunAsync(() =>
        {
            ComLifecycle.Release(ref _workflowAdmin);
            ComLifecycle.Release(ref _database);

            if (_project != null)
            {
                try { _project.Disconnect(); }
                catch { /* Best-effort disconnect */ }
                ComLifecycle.Release(ref _project);
            }
        }, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Synchronous cleanup — best effort
        ComLifecycle.Release(ref _workflowAdmin);
        ComLifecycle.Release(ref _database);

        if (_project != null)
        {
            try { _project.Disconnect(); }
            catch { /* Best-effort */ }
            ComLifecycle.Release(ref _project);
        }
    }
}
