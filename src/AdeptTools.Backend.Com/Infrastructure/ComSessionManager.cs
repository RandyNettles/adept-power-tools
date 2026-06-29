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
    public async Task<int> ConnectAsync(string serverUrl, string userId, string? password, CancellationToken ct = default)
    {
        // Use DBNull.Value for empty/null password to send VT_NULL to the COM SDK,
        // which signals a direct Adept account login with no password.
        // An empty string (VT_BSTR "") triggers Windows SSO in the Adept SDK.
        object? sdkPassword = string.IsNullOrEmpty(password) ? DBNull.Value : (object)password;
        return await _runner.RunAsync(() =>
        {
            try
            {
                _project = ComLifecycle.CreateInstance<INxProject>(ProjectProgId);
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x800401F3))
            {
                throw new InvalidOperationException(
                    $"The Adept 11.X COM SDK is not installed on this machine. " +
                    $"Install the Adept desktop client before using the COM backend. " +
                    $"(ProgID '{ProjectProgId}' not found in registry)", ex);
            }
            return _project.Connect(serverUrl, userId, sdkPassword);
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
