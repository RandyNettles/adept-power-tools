using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace AdeptTools.Backend.Com.Infrastructure;

/// <summary>
/// Auth/session operations backed by legacy AdeptCoreDll.CoreApi COM automation.
/// This path does not depend on Adept SDK COM ProgIDs.
/// </summary>
public sealed class LegacyCoreApiSession : ILegacyCoreApiSession, IDisposable
{
    private readonly ComOperationRunner _runner;

    private object? _coreApi;
    private SessionMode _sessionMode = SessionMode.Unknown;
    private bool _initialized;
    private bool _loggedIn;
    private bool _databaseOpen;
    private string? _activeDomainName;
    private string? _activeDatabaseName;
    private object? _guiApi;
    private readonly List<string> _connectDiagnostics = new();

    private const string CoreApiProgId = "AdeptCoreDll.CoreApi";
    private const string CoreApiProgIdV1 = "AdeptCoreDll.CoreApi.1";
    private const string GuiApiProgId = "AdeptGui.GuiApi";

    public LegacyCoreApiSession(ComOperationRunner runner)
    {
        _runner = runner;
    }

    public bool IsConnected => _loggedIn && _databaseOpen;

    public async Task<int> ConnectAsync(string serverUrl, string userName, string? password, CancellationToken ct = default)
    {
        return await _runner.RunAsync(() =>
        {
            _connectDiagnostics.Clear();
            EnsureCoreApiCreated();

            var domain = ParseDomainName(serverUrl);
            var loginPassword = password ?? string.Empty;

            // Prefer legacy CoreApi flow first.
            var coreInit = TryInvokeInt("InitializeCore");
            AddDiagnostic($"Try InitializeCore => {(coreInit.HasValue ? coreInit.Value.ToString() : "missing")}");
            if (coreInit.HasValue)
            {
                _sessionMode = SessionMode.CoreApi;
                _initialized = coreInit.Value == 0;
                if (!_initialized) return coreInit.Value;

                var login = InvokeInt("LogIntoDomain", domain, userName, loginPassword);
                AddDiagnostic($"Invoke LogIntoDomain => {login}");
                _loggedIn = login == 0;
                if (!_loggedIn) return login;

                // 1 is the default database number used by legacy flows in many environments.
                var openDb = InvokeInt("OpenDatabase", 1);
                AddDiagnostic($"Invoke OpenDatabase(1) => {openDb}");
                _databaseOpen = openDb == 0;
                if (!_databaseOpen) return openDb;

                _activeDomainName = SafeInvokeStringByNames("GetActiveDomainName") ?? domain;
                _activeDatabaseName = SafeInvokeStringByNames("GetActiveDatabaseName");
                return 0;
            }

            // Fallback for NxProject/NxCore shape: Connect(server,user,password)
            var nxConnect = TryInvokeInt("Connect", serverUrl, userName, string.IsNullOrEmpty(loginPassword) ? DBNull.Value : (object)loginPassword);
            AddDiagnostic($"Try Connect => {(nxConnect.HasValue ? nxConnect.Value.ToString() : "missing")}");
            if (nxConnect.HasValue)
            {
                _sessionMode = SessionMode.NxProject;
                _initialized = true;
                _loggedIn = nxConnect.Value == 0;
                _databaseOpen = _loggedIn;
                if (!_loggedIn) return nxConnect.Value;

                _activeDomainName = SafeInvokeStringByNames("ServerUrl") ?? domain;
                _activeDatabaseName = SafeInvokeStringByNames("WorkAreaId");
                return 0;
            }

            // Attach-mode fallback for GuiApi/GetProject paths where login is done in Adept.exe.
            if (TryAttachToExistingProject(domain))
            {
                AddDiagnostic("Attach to existing project => success");
                return 0;
            }
            AddDiagnostic("Attach to existing project => no active session");

            // AdeptGui can return project/core objects lazily while Adept.exe starts.
            if (TryRefreshProjectFromGuiApi(maxWaitMs: 5000))
            {
                AddDiagnostic("Refresh from GuiApi => success");
                coreInit = TryInvokeInt("InitializeCore");
                AddDiagnostic($"Retry InitializeCore => {(coreInit.HasValue ? coreInit.Value.ToString() : "missing")}");
                if (coreInit.HasValue)
                {
                    _sessionMode = SessionMode.CoreApi;
                    _initialized = coreInit.Value == 0;
                    if (!_initialized) return coreInit.Value;

                    var login = InvokeInt("LogIntoDomain", domain, userName, loginPassword);
                    AddDiagnostic($"Retry LogIntoDomain => {login}");
                    _loggedIn = login == 0;
                    if (!_loggedIn) return login;

                    var openDb = InvokeInt("OpenDatabase", 1);
                    AddDiagnostic($"Retry OpenDatabase(1) => {openDb}");
                    _databaseOpen = openDb == 0;
                    if (!_databaseOpen) return openDb;

                    _activeDomainName = SafeInvokeStringByNames("GetActiveDomainName") ?? domain;
                    _activeDatabaseName = SafeInvokeStringByNames("GetActiveDatabaseName");
                    return 0;
                }

                nxConnect = TryInvokeInt("Connect", serverUrl, userName, string.IsNullOrEmpty(loginPassword) ? DBNull.Value : (object)loginPassword);
                AddDiagnostic($"Retry Connect => {(nxConnect.HasValue ? nxConnect.Value.ToString() : "missing")}");
                if (nxConnect.HasValue)
                {
                    _sessionMode = SessionMode.NxProject;
                    _initialized = true;
                    _loggedIn = nxConnect.Value == 0;
                    _databaseOpen = _loggedIn;
                    if (!_loggedIn) return nxConnect.Value;

                    _activeDomainName = SafeInvokeStringByNames("ServerUrl") ?? domain;
                    _activeDatabaseName = SafeInvokeStringByNames("WorkAreaId");
                    return 0;
                }

                if (TryAttachToExistingProject(domain))
                {
                    AddDiagnostic("Retry attach to existing project => success");
                    return 0;
                }

                AddDiagnostic("Retry attach to existing project => no active session");
            }
            else
            {
                AddDiagnostic("Refresh from GuiApi => no project/core object");
            }

            if (TryConnectViaNestedCandidates(serverUrl, domain, userName, loginPassword, out var nestedResult))
            {
                AddDiagnostic($"Nested candidate connect => {nestedResult}");
                return nestedResult;
            }
            AddDiagnostic("Nested candidate connect => no recognized method shape");

            var isConnected = SafeInvokeBoolByNames("IsConnected");
            AddDiagnostic($"Read IsConnected => {(isConnected.HasValue ? isConnected.Value.ToString() : "missing")}");
            if (isConnected.HasValue && !isConnected.Value)
            {
                throw new InvalidOperationException(
                    "Adept COM is available, but there is no active logged-in Adept session. " +
                    "Open Adept Desktop Client, log in, then retry COM mode.");
            }

            throw new InvalidOperationException(
                "Connected COM object does not expose recognized login methods. " +
                "Expected CoreApi methods (InitializeCore/LogIntoDomain/OpenDatabase) or NxProject method (Connect). " +
                BuildMemberDiagnostics());
        }, ct);
    }

    public async Task<LegacySessionInfo> GetSessionInfoAsync(CancellationToken ct = default)
    {
        return await _runner.RunAsync(() =>
        {
            if (!IsConnected)
                throw new InvalidOperationException("Legacy COM session is not connected.");

            var userName = SafeInvokeStringByNames("GetCurrentUserName", "UserName", "UserId");
            var domain = SafeInvokeStringByNames("GetActiveDomainName", "ServerUrl") ?? _activeDomainName;
            var database = SafeInvokeStringByNames("GetActiveDatabaseName", "WorkAreaId") ?? _activeDatabaseName;
            var appVersion = SafeInvokeStringByNames("GetAppVersion", "AppVersion") ?? "LegacyCom";
            var userId = SafeInvokeStringByNames("UserId") ?? userName;
            var displayName = SafeInvokeStringByNames("DisplayName") ?? userName;
            var email = SafeInvokeStringByNames("EmailAddress");

            // Legacy CoreApi surface does not expose the same rich identity fields as SDK NxProject.
            // Fill what we can and derive stable display values for UX.
            return new LegacySessionInfo
            {
                UserId = userId,
                UserName = userName,
                DisplayName = displayName,
                EmailAddress = email,
                AppVersion = appVersion,
                WorkAreaId = database,
                ActiveDomainName = domain,
                ActiveDatabaseName = database
            };
        }, ct);
    }

    public async Task<object> GetConnectedDispatchAsync(CancellationToken ct = default)
    {
        return await _runner.RunAsync(() =>
        {
            if (!IsConnected || _coreApi == null)
                throw new InvalidOperationException("Legacy COM session is not connected.");

            return _coreApi;
        }, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _runner.RunAsync(() =>
        {
            if (_coreApi != null)
            {
                if (_databaseOpen)
                {
                    if (_sessionMode == SessionMode.CoreApi)
                        TryInvoke("CloseDatabase");
                    _databaseOpen = false;
                }

                if (_loggedIn)
                {
                    if (_sessionMode == SessionMode.CoreApi)
                        TryInvoke("LogOutOfDomain");
                    else if (_sessionMode == SessionMode.NxProject)
                        TryInvoke("Disconnect");
                    _loggedIn = false;
                }

                if (_initialized)
                {
                    if (_sessionMode == SessionMode.CoreApi)
                        TryInvoke("UnInitializeCore");
                    _initialized = false;
                }

                ComLifecycle.Release(ref _coreApi);
            }

            if (_guiApi != null)
            {
                ComLifecycle.Release(ref _guiApi);
            }

            _activeDomainName = null;
            _activeDatabaseName = null;
            _sessionMode = SessionMode.Unknown;
        }, ct);
    }

    public void Dispose()
    {
        // Best-effort synchronous cleanup; callers generally use async DisconnectAsync.
        if (_coreApi != null)
        {
            try { if (_sessionMode == SessionMode.CoreApi) TryInvoke("CloseDatabase"); } catch { }
            try
            {
                if (_sessionMode == SessionMode.CoreApi) TryInvoke("LogOutOfDomain");
                if (_sessionMode == SessionMode.NxProject) TryInvoke("Disconnect");
            }
            catch { }
            try { if (_sessionMode == SessionMode.CoreApi) TryInvoke("UnInitializeCore"); } catch { }
            ComLifecycle.Release(ref _coreApi);
        }

        if (_guiApi != null)
        {
            ComLifecycle.Release(ref _guiApi);
        }
    }

    private void EnsureCoreApiCreated()
    {
        if (_coreApi != null) return;

        // 1) Preferred direct CoreApi ProgID.
        if (TryCreateCoreByProgId(CoreApiProgId) || TryCreateCoreByProgId(CoreApiProgIdV1))
            return;

        // 2) Fallback through AdeptGui.GuiApi.GetCore() for installations that only register GuiApi.
        if (TryCreateCoreFromGuiApi())
            return;

        throw new InvalidOperationException(
            "Legacy Adept COM components are not registered on this workstation. " +
            "Expected one of: AdeptCoreDll.CoreApi, AdeptCoreDll.CoreApi.1, or AdeptGui.GuiApi.");
    }

    private bool TryCreateCoreByProgId(string progId)
    {
        try
        {
            _coreApi = ComLifecycle.CreateInstance<object>(progId);
            return _coreApi != null;
        }
        catch
        {
            return false;
        }
    }

    private bool TryCreateCoreFromGuiApi()
    {
        try
        {
            _guiApi = ComLifecycle.CreateInstance<object>(GuiApiProgId);
            if (_guiApi == null) return false;

            object? coreDispatch = null;
            foreach (var method in new[] { "GetCore", "GetCoreApi", "GetProject" })
            {
                coreDispatch = TryInvokeRaw(_guiApi, method);
                if (coreDispatch != null)
                    break;
            }

            if (coreDispatch is null) return false;

            // Some GuiApi routes return a core object that then exposes GetProject.
            var projectDispatch = TryInvokeRaw(coreDispatch, "GetProject");
            if (projectDispatch != null)
                coreDispatch = projectDispatch;

            _coreApi = coreDispatch;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int InvokeInt(string methodName, params object[] args)
    {
        if (_coreApi == null)
            throw new InvalidOperationException("Core API is not initialized.");

        object? result;
        try
        {
            result = _coreApi.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                _coreApi,
                args);
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            if (!TryInvokeComMethodDynamic(_coreApi, methodName, args, out result))
                throw;
        }

        return result is int i ? i : Convert.ToInt32(result);
    }

    private int? TryInvokeInt(string methodName, params object[] args)
    {
        try
        {
            return InvokeInt(methodName, args);
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            return null;
        }
    }

    private string? SafeInvokeStringByNames(params string[] methodOrPropertyNames)
    {
        if (_coreApi == null) return null;

        foreach (var name in methodOrPropertyNames)
        {
            try
            {
                // First attempt method-style call.
                object? methodResult;
                try
                {
                    methodResult = _coreApi.GetType().InvokeMember(
                        name,
                        BindingFlags.InvokeMethod,
                        null,
                        _coreApi,
                        Array.Empty<object>());
                }
                catch (Exception ex) when (IsUnknownName(ex))
                {
                    if (!TryInvokeComMethodDynamic(_coreApi, name, Array.Empty<object>(), out methodResult))
                        throw;
                }

                if (methodResult != null)
                    return methodResult.ToString();
            }
            catch (Exception ex) when (IsUnknownName(ex))
            {
                // fall through to property-style access
            }
            catch
            {
                // ignore and continue
            }

            try
            {
                object? propertyResult;
                try
                {
                    propertyResult = _coreApi.GetType().InvokeMember(
                        name,
                        BindingFlags.GetProperty,
                        null,
                        _coreApi,
                        null);
                }
                catch (Exception ex) when (IsUnknownName(ex))
                {
                    if (!TryGetComPropertyDynamic(_coreApi, name, out propertyResult))
                        throw;
                }

                if (propertyResult != null)
                    return propertyResult.ToString();
            }
            catch
            {
                // ignore and continue
            }
        }

        return null;
    }

    private bool? SafeInvokeBoolByNames(params string[] methodOrPropertyNames)
    {
        if (_coreApi == null) return null;

        foreach (var name in methodOrPropertyNames)
        {
            try
            {
                object? methodResult;
                try
                {
                    methodResult = _coreApi.GetType().InvokeMember(
                        name,
                        BindingFlags.InvokeMethod,
                        null,
                        _coreApi,
                        Array.Empty<object>());
                }
                catch (Exception ex) when (IsUnknownName(ex))
                {
                    if (!TryInvokeComMethodDynamic(_coreApi, name, Array.Empty<object>(), out methodResult))
                        throw;
                }

                if (methodResult is bool b) return b;
                if (methodResult is int i) return i != 0;
                if (methodResult != null && bool.TryParse(methodResult.ToString(), out var parsed)) return parsed;
            }
            catch (Exception ex) when (IsUnknownName(ex))
            {
                // fall through to property
            }
            catch
            {
                // ignore and continue
            }

            try
            {
                object? propertyResult;
                try
                {
                    propertyResult = _coreApi.GetType().InvokeMember(
                        name,
                        BindingFlags.GetProperty,
                        null,
                        _coreApi,
                        null);
                }
                catch (Exception ex) when (IsUnknownName(ex))
                {
                    if (!TryGetComPropertyDynamic(_coreApi, name, out propertyResult))
                        throw;
                }

                if (propertyResult is bool b) return b;
                if (propertyResult is int i) return i != 0;
                if (propertyResult != null && bool.TryParse(propertyResult.ToString(), out var parsed)) return parsed;
            }
            catch
            {
                // ignore and continue
            }
        }

        return null;
    }

    private bool TryAttachToExistingProject(string fallbackDomain)
    {
        // If current object is a core wrapper, try to drill into project object.
        var projectDispatch = TryInvokeRaw(_coreApi!, "GetProject");
        if (projectDispatch != null)
            _coreApi = projectDispatch;

        var connected = SafeInvokeBoolByNames("IsConnected");
        if (connected != true)
            return false;

        _sessionMode = SessionMode.AttachedProject;
        _initialized = true;
        _loggedIn = true;
        _databaseOpen = true;

        _activeDomainName = SafeInvokeStringByNames("ServerUrl", "GetActiveDomainName") ?? fallbackDomain;
        _activeDatabaseName = SafeInvokeStringByNames("WorkAreaId", "GetActiveDatabaseName");
        return true;
    }

    private bool TryConnectViaNestedCandidates(string serverUrl, string domain, string userName, string loginPassword, out int result)
    {
        result = 0;
        var originalCore = _coreApi;

        try
        {
            var candidates = EnumerateConnectionCandidates().ToList();
            foreach (var (candidate, label) in candidates)
            {
                if (candidate == null) continue;

                _coreApi = candidate;
                AddDiagnostic($"Probe candidate: {label}");

                if (TryConnectOnCurrentObject(serverUrl, domain, userName, loginPassword, label, out result))
                    return true;
            }

            return false;
        }
        finally
        {
            if (!IsConnected)
                _coreApi = originalCore;
        }
    }

    private bool TryConnectOnCurrentObject(string serverUrl, string domain, string userName, string loginPassword, string label, out int result)
    {
        result = 0;

        var coreInit = TryInvokeInt("InitializeCore");
        AddDiagnostic($"{label}: InitializeCore => {(coreInit.HasValue ? coreInit.Value.ToString() : "missing")}");
        if (coreInit.HasValue)
        {
            _sessionMode = SessionMode.CoreApi;
            _initialized = coreInit.Value == 0;
            if (!_initialized)
            {
                result = coreInit.Value;
                return true;
            }

            var login = InvokeInt("LogIntoDomain", domain, userName, loginPassword);
            AddDiagnostic($"{label}: LogIntoDomain => {login}");
            _loggedIn = login == 0;
            if (!_loggedIn)
            {
                result = login;
                return true;
            }

            var openDb = TryInvokeInt("OpenDatabase", 1) ?? TryInvokeInt("OpenDatabase");
            AddDiagnostic($"{label}: OpenDatabase => {(openDb.HasValue ? openDb.Value.ToString() : "missing")}");
            if (openDb.HasValue)
            {
                _databaseOpen = openDb.Value == 0;
                result = openDb.Value;
                if (_databaseOpen)
                {
                    _activeDomainName = SafeInvokeStringByNames("GetActiveDomainName", "ServerUrl") ?? domain;
                    _activeDatabaseName = SafeInvokeStringByNames("GetActiveDatabaseName", "WorkAreaId");
                }
                return true;
            }

            // Recognized core shape with no open database function surfaced.
            result = 0;
            return true;
        }

        var nxConnect = TryInvokeInt("Connect", serverUrl, userName, string.IsNullOrEmpty(loginPassword) ? DBNull.Value : (object)loginPassword);
        AddDiagnostic($"{label}: Connect => {(nxConnect.HasValue ? nxConnect.Value.ToString() : "missing")}");
        if (nxConnect.HasValue)
        {
            _sessionMode = SessionMode.NxProject;
            _initialized = true;
            _loggedIn = nxConnect.Value == 0;
            _databaseOpen = _loggedIn;
            result = nxConnect.Value;
            if (_databaseOpen)
            {
                _activeDomainName = SafeInvokeStringByNames("ServerUrl") ?? domain;
                _activeDatabaseName = SafeInvokeStringByNames("WorkAreaId");
            }
            return true;
        }

        var openDbOnly = TryInvokeInt("OpenDatabase", 1) ?? TryInvokeInt("OpenDatabase");
        AddDiagnostic($"{label}: OpenDatabase-only => {(openDbOnly.HasValue ? openDbOnly.Value.ToString() : "missing")}");
        if (openDbOnly.HasValue)
        {
            _sessionMode = SessionMode.AttachedProject;
            _initialized = true;
            _loggedIn = openDbOnly.Value == 0;
            _databaseOpen = _loggedIn;
            result = openDbOnly.Value;
            if (_databaseOpen)
            {
                _activeDomainName = SafeInvokeStringByNames("GetActiveDomainName", "ServerUrl") ?? domain;
                _activeDatabaseName = SafeInvokeStringByNames("GetActiveDatabaseName", "WorkAreaId");
            }
            return true;
        }

        if (TryAttachToExistingProject(domain))
        {
            result = 0;
            AddDiagnostic($"{label}: Attach existing project => success");
            return true;
        }

        AddDiagnostic($"{label}: Attach existing project => no active session");
        return false;
    }

    private IEnumerable<(object candidate, string label)> EnumerateConnectionCandidates()
    {
        var queue = new Queue<(object candidate, string label, int depth)>();
        var seen = new HashSet<nint>();

        if (_coreApi != null) queue.Enqueue((_coreApi, "root.coreApi", 0));
        if (_guiApi != null) queue.Enqueue((_guiApi, "root.guiApi", 0));

        while (queue.Count > 0)
        {
            var (candidate, label, depth) = queue.Dequeue();
            if (candidate == null) continue;

            var key = GetIdentityKey(candidate);
            if (key != 0 && !seen.Add(key))
                continue;

            yield return (candidate, label);

            if (depth >= 2)
                continue;

            foreach (var nextName in new[] { "GetProject", "GetCore", "GetCoreApi", "GetLogin", "GetDomain" })
            {
                var next = TryInvokeRaw(candidate, nextName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{nextName}()", depth + 1));
            }

            foreach (var nextName in new[] { "Project", "Core", "Login", "Domain" })
            {
                var next = TryGetRawProperty(candidate, nextName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{nextName}", depth + 1));
            }
        }
    }

    private static object? TryGetRawProperty(object target, string propertyName)
    {
        try
        {
            return target.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty,
                null,
                target,
                null);
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static nint GetIdentityKey(object value)
    {
        try
        {
            if (Marshal.IsComObject(value))
            {
                var ptr = Marshal.GetIUnknownForObject(value);
                Marshal.Release(ptr);
                return ptr;
            }
        }
        catch
        {
            // fall through to managed identity hash
        }

        return value.GetHashCode();
    }

    private bool TryRefreshProjectFromGuiApi(int maxWaitMs)
    {
        if (_guiApi == null) return false;

        var end = Environment.TickCount64 + maxWaitMs;
        while (Environment.TickCount64 < end)
        {
            var project = TryInvokeRaw(_guiApi, "GetProject")
                          ?? TryInvokeRaw(_guiApi, "GetCore")
                          ?? TryInvokeRaw(_guiApi, "GetCoreApi");

            if (project != null)
            {
                _coreApi = project;
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private void TryInvoke(string methodName)
    {
        if (_coreApi == null) return;

        try
        {
            _coreApi.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                _coreApi,
                Array.Empty<object>());
        }
        catch
        {
            // Best effort only.
        }
    }

    private static object? TryInvokeRaw(object target, string methodName)
    {
        try
        {
            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                target,
                Array.Empty<object>());
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            return TryInvokeComMethodDynamic(target, methodName, Array.Empty<object>(), out var dynamicResult)
                ? dynamicResult
                : null;
        }
    }

    private static bool TryInvokeComMethodDynamic(object target, string methodName, object[] args, out object? result)
    {
        result = null;
        if (target == null) return false;

        static bool HasArgs(object[] values, int count) => values != null && values.Length >= count;

        try
        {
            dynamic d = target;
            result = methodName switch
            {
                "InitializeCore" => d.InitializeCore(),
                "LogIntoDomain" when HasArgs(args, 3) => d.LogIntoDomain(args[0], args[1], args[2]),
                "OpenDatabase" when HasArgs(args, 1) => d.OpenDatabase(args[0]),
                "CloseDatabase" => d.CloseDatabase(),
                "LogOutOfDomain" => d.LogOutOfDomain(),
                "UnInitializeCore" => d.UnInitializeCore(),
                "Connect" when HasArgs(args, 3) => d.Connect(args[0], args[1], args[2]),
                "Disconnect" => d.Disconnect(),
                "GetProject" => d.GetProject(),
                "GetCore" => d.GetCore(),
                "GetCoreApi" => d.GetCoreApi(),
                "GetActiveDomainName" => d.GetActiveDomainName(),
                "GetActiveDatabaseName" => d.GetActiveDatabaseName(),
                "GetCurrentUserName" => d.GetCurrentUserName(),
                "GetAppVersion" => d.GetAppVersion(),
                "IsConnected" => d.IsConnected(),
                _ => null
            };

            return result != null || methodName is "InitializeCore" or "OpenDatabase" or "CloseDatabase" or "LogOutOfDomain" or "UnInitializeCore" or "Disconnect";
        }
        catch (RuntimeBinderException)
        {
            return false;
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
        catch (COMException comEx) when (comEx.HResult == unchecked((int)0x80020006))
        {
            return false;
        }
    }

    private static bool TryGetComPropertyDynamic(object target, string propertyName, out object? result)
    {
        result = null;
        if (target == null) return false;

        try
        {
            dynamic d = target;
            result = propertyName switch
            {
                "ServerUrl" => d.ServerUrl,
                "WorkAreaId" => d.WorkAreaId,
                "UserName" => d.UserName,
                "UserId" => d.UserId,
                "DisplayName" => d.DisplayName,
                "EmailAddress" => d.EmailAddress,
                "AppVersion" => d.AppVersion,
                "IsConnected" => d.IsConnected,
                _ => null
            };

            return result != null || propertyName == "IsConnected";
        }
        catch (RuntimeBinderException)
        {
            return false;
        }
        catch (COMException comEx) when (comEx.HResult == unchecked((int)0x80020006))
        {
            return false;
        }
    }

    private static bool IsUnknownName(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is COMException comEx && comEx.HResult == unchecked((int)0x80020006))
                return true;

            if (current.HResult == unchecked((int)0x80020006))
                return true;

            current = current.InnerException!;
        }

        return false;
    }

    private static bool IsBadParamCount(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is COMException comEx && comEx.HResult == unchecked((int)0x8002000E))
                return true;

            if (current.HResult == unchecked((int)0x8002000E))
                return true;

            current = current.InnerException!;
        }

        return false;
    }

    private bool SupportsMember(string name)
    {
        if (_coreApi == null) return false;

        try
        {
            _coreApi.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod,
                null,
                _coreApi,
                Array.Empty<object>());
            return true;
        }
        catch (Exception ex) when (IsBadParamCount(ex))
        {
            return true;
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            // fall through to property check
        }
        catch
        {
            return true;
        }

        try
        {
            _coreApi.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                _coreApi,
                null);
            return true;
        }
        catch (Exception ex) when (IsUnknownName(ex))
        {
            if (TryInvokeComMethodDynamic(_coreApi, name, Array.Empty<object>(), out _))
                return true;

            if (TryGetComPropertyDynamic(_coreApi, name, out _))
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    private string BuildMemberDiagnostics()
    {
        var objectType = _coreApi?.GetType().FullName ?? "<null>";
        var guiType = _guiApi?.GetType().FullName ?? "<null>";
        var checks = new[]
        {
            "InitializeCore",
            "LogIntoDomain",
            "OpenDatabase",
            "Connect",
            "Disconnect",
            "IsConnected",
            "GetProject",
            "GetCore",
            "GetCoreApi"
        };

        var results = string.Join(", ",
            checks.Select(name => $"{name}={(SupportsMember(name) ? "Y" : "N")}"));

        var attempts = _connectDiagnostics.Count > 0
            ? $"; attempts: {string.Join(" | ", _connectDiagnostics.TakeLast(10))}"
            : string.Empty;

        return $"COM type={objectType}; gui type={guiType}; members: {results}{attempts}";
    }

    private void AddDiagnostic(string message)
    {
        _connectDiagnostics.Add(message);
        if (_connectDiagnostics.Count > 30)
            _connectDiagnostics.RemoveAt(0);
    }

    private static string ParseDomainName(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return string.Empty;

        // Support existing COM profile format host:port and URL values.
        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            return uri.Host;

        var trimmed = serverUrl.Trim();
        var colon = trimmed.IndexOf(':');
        return colon > 0 ? trimmed[..colon] : trimmed;
    }

    private enum SessionMode
    {
        Unknown,
        CoreApi,
        NxProject,
        AttachedProject
    }
}
