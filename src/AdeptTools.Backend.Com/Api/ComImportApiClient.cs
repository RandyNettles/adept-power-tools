using System.Reflection;
using System.Runtime.InteropServices;
using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Core.Models;
using AdeptTools.Import.Api;
using AdeptTools.Import.Models;

namespace AdeptTools.Backend.Com.Api;

/// <summary>
/// COM-based implementation of IImportApiClient backed by legacy COM dispatch objects.
/// </summary>
public class ComImportApiClient : IImportApiClient
{
    private readonly ILegacyCoreApiSession _legacySession;
    private readonly LegacyComFeatureFlags _flags;

    public ComImportApiClient(ILegacyCoreApiSession legacySession, LegacyComFeatureFlags flags)
    {
        _legacySession = legacySession;
        _flags = flags;
    }

    private void EnsureImportEnabled()
    {
        if (!_flags.EnableLegacyImport)
        {
            throw new NotSupportedException(
                "Legacy COM import phase is currently disabled. Set ADEPTTOOLS_LEGACYCOM_IMPORT=true to enable it.");
        }
    }

    public async Task<List<AdeptFieldDefinitionDto>> GetAvailableFieldsAsync(CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var fields = new List<AdeptFieldDefinitionDto>();
        var count = InvokeInt(db, "GetFieldCount");

        for (var i = 0; i < count; i++)
        {
            var fieldDef = Invoke(db, "GetFieldDef", i);
            try
            {
                var dto = new AdeptFieldDefinitionDto
                {
                    FieldName = GetString(fieldDef, "FieldName"),
                    DisplayName = GetString(fieldDef, "DisplayName"),
                    SchemaId = GetString(fieldDef, "SchemaId"),
                    FieldType = GetString(fieldDef, "FieldType"),
                    Width = GetIntProperty(fieldDef, "Width"),
                    IsSystem = GetBool(fieldDef, "IsSystem"),
                    IsRestricted = GetBool(fieldDef, "IsRestricted"),
                    RestrictedValues = new List<string>()
                };

                if (dto.IsRestricted)
                {
                    var rvCount = GetIntProperty(fieldDef, "RestrictedValueCount");
                    for (var j = 0; j < rvCount; j++)
                    {
                        dto.RestrictedValues.Add(Convert.ToString(Invoke(fieldDef, "GetRestrictedValue", j)) ?? string.Empty);
                    }
                }

                fields.Add(dto);
            }
            finally
            {
                ReleaseCom(ref fieldDef);
            }
        }

        return fields;
    }

    public async Task<SearchResultDto> SearchByFieldsAsync(SearchRequestDto search, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);
        var criteria = CreateSearchCriteria(search);
        object? searchResult = null;

        try
        {
            searchResult = Invoke(db, "Search", criteria);
            var rowCount = GetIntProperty(searchResult, "RowCount");

            var result = new SearchResultDto
            {
                MatchCount = rowCount
            };

            for (var i = 0; i < rowCount; i++)
            {
                var row = Invoke(searchResult, "GetRow", i);
                try
                {
                    var resultRow = new SearchResultRow
                    {
                        TableNumber = GetIntProperty(row, "TableNumber"),
                        FileId = GetString(row, "FileId"),
                        MajRev = GetIntProperty(row, "MajRev"),
                        MinRev = GetIntProperty(row, "MinRev"),
                        FieldValues = new Dictionary<string, string>()
                    };

                    foreach (var term in search.SearchCriteria)
                    {
                        if (string.IsNullOrEmpty(term.FieldName))
                            continue;

                        var value = Convert.ToString(Invoke(row, "GetFieldValue", term.FieldName));
                        if (!string.IsNullOrEmpty(value))
                            resultRow.FieldValues[term.FieldName] = value;
                    }

                    result.Rows.Add(resultRow);
                }
                finally
                {
                    ReleaseCom(ref row);
                }
            }

            return result;
        }
        finally
        {
            ReleaseCom(ref searchResult);
            ReleaseCom(ref criteria);
        }
    }

    public async Task<Dictionary<string, string>> GetDataCardValuesAsync(
        int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var dataCard = Invoke(db, "GetDataCard", tableNumber, fileId, majRev, minRev);
        try
        {
            var values = new Dictionary<string, string>();
            var fieldCount = GetIntProperty(dataCard, "FieldCount");

            for (var i = 0; i < fieldCount; i++)
            {
                var fieldName = Convert.ToString(Invoke(dataCard, "GetFieldName", i)) ?? string.Empty;
                if (string.IsNullOrEmpty(fieldName)) continue;

                var fieldValue = Convert.ToString(Invoke(dataCard, "GetFieldValue", fieldName)) ?? string.Empty;
                values[fieldName] = fieldValue;
            }

            return values;
        }
        finally
        {
            ReleaseCom(ref dataCard);
        }
    }

    public async Task<ApiResult> SaveDataCardAsync(
        int tableNumber, string fileId, int majRev, int minRev,
        Dictionary<string, string> fieldValues, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var dataCard = Invoke(db, "GetDataCard", tableNumber, fileId, majRev, minRev);
        try
        {
            foreach (var (fieldName, value) in fieldValues)
            {
                Invoke(dataCard, "SetFieldValue", fieldName, value);
            }

            var result = Convert.ToInt32(Invoke(dataCard, "Save"));
            return result == 0
                ? ApiResult.Success($"Saved {fieldValues.Count} fields via COM.")
                : ApiResult.Failure(result, $"COM DataCard Save failed with code {result}.");
        }
        finally
        {
            ReleaseCom(ref dataCard);
        }
    }

    public async Task<List<string>> GetRestrictedValuesAsync(string schemaId, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var count = InvokeInt(db, "GetFieldCount");
        for (var i = 0; i < count; i++)
        {
            var fieldDef = Invoke(db, "GetFieldDef", i);
            try
            {
                if (GetString(fieldDef, "SchemaId") == schemaId && GetBool(fieldDef, "IsRestricted"))
                {
                    var values = new List<string>();
                    var rvCount = GetIntProperty(fieldDef, "RestrictedValueCount");
                    for (var j = 0; j < rvCount; j++)
                    {
                        values.Add(Convert.ToString(Invoke(fieldDef, "GetRestrictedValue", j)) ?? string.Empty);
                    }
                    return values;
                }
            }
            finally
            {
                ReleaseCom(ref fieldDef);
            }
        }

        return new List<string>();
    }

    public async Task<CreateDocResultDto> CreateNewDocumentAsync(
        string workAreaId, string fileName, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        // Keep same behavior as previous implementation for now.
        return await Task.FromResult(new CreateDocResultDto
        {
            StatusCode = 1,
            ErrorMessage = "Legacy COM import path does not currently support CreateDocument without SDK interop."
        });
    }

    public async Task<ApiResult> CheckInToLibraryAsync(
        string fileId, int majRev, int minRev, string libraryId, CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var result = InvokeInt(db, "CheckInToLibrary", fileId, majRev, minRev, libraryId);
        return result == 0
            ? ApiResult.Success("Checked in via COM.")
            : ApiResult.Failure(result, $"COM CheckIn failed with code {result}.");
    }

    public async Task<int> GetMaxFilenameLengthAsync(CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);
        return InvokeInt(db, "GetMaxFilenameLength");
    }

    public async Task<List<LibraryDto>> GetLibraryTreeAsync(CancellationToken ct = default)
    {
        EnsureImportEnabled();
        var db = await GetDatabaseDispatchAsync(ct);

        var tree = Invoke(db, "GetLibraryTree");
        try
        {
            var libraries = new List<LibraryDto>();
            var count = GetIntProperty(tree, "LibraryCount");
            for (var i = 0; i < count; i++)
            {
                var nxLib = Invoke(tree, "GetLibrary", i);
                try
                {
                    libraries.Add(MapLibrary(nxLib));
                }
                finally
                {
                    ReleaseCom(ref nxLib);
                }
            }

            return libraries;
        }
        finally
        {
            ReleaseCom(ref tree);
        }
    }

    private async Task<object> GetDatabaseDispatchAsync(CancellationToken ct)
    {
        var dispatch = await _legacySession.GetConnectedDispatchAsync(ct);
        var attempted = new List<string>();

        foreach (var (candidate, label) in EnumerateCandidates(dispatch))
        {
            var db = TryInvoke(candidate, "GetDatabase");
            if (db != null)
                return db;

            attempted.Add(label);
        }

        throw new NotSupportedException(
            "Legacy COM session does not expose database operations (GetDatabase). " +
            $"Probed candidates: {string.Join(", ", attempted)}");
    }

    private static IEnumerable<(object candidate, string label)> EnumerateCandidates(object root)
    {
        var queue = new Queue<(object candidate, string label, int depth)>();
        var seen = new HashSet<nint>();
        queue.Enqueue((root, "root", 0));

        while (queue.Count > 0)
        {
            var (candidate, label, depth) = queue.Dequeue();
            var identity = GetIdentityKey(candidate);
            if (identity != 0 && !seen.Add(identity))
                continue;

            yield return (candidate, label);

            if (depth >= 3)
                continue;

            foreach (var methodName in new[] { "GetProject", "GetCore", "GetCoreApi", "GetLogin", "GetDomain", "GetApplication" })
            {
                var next = TryInvoke(candidate, methodName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{methodName}()", depth + 1));
            }

            foreach (var propertyName in new[] { "Project", "Core", "Login", "Domain", "Application" })
            {
                var next = TryGetProperty(candidate, propertyName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{propertyName}", depth + 1));
            }
        }
    }

    private static object? TryGetProperty(object target, string name)
    {
        try
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                target,
                null);
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
        }

        return value.GetHashCode();
    }

    private static object CreateSearchCriteria(SearchRequestDto search)
    {
        var type = Type.GetTypeFromProgID("AdeptSDK.NxSearchCriteria", throwOnError: true)!;
        var criteria = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to create AdeptSDK.NxSearchCriteria COM object.");

        TryInvoke(criteria, "SetMaxResults", 100);

        foreach (var term in search.SearchCriteria)
        {
            var fieldName = term.FieldName ?? string.Empty;
            var value = term.ValueStr ?? string.Empty;
            Invoke(criteria, "AddCondition", fieldName, term.SearchOp, value);
        }

        return criteria;
    }

    private static LibraryDto MapLibrary(object nxLib)
    {
        var dto = new LibraryDto
        {
            LibraryId = GetString(nxLib, "LibraryId"),
            Name = GetString(nxLib, "Name"),
            Path = GetString(nxLib, "Path"),
            Children = new List<LibraryDto>()
        };

        var childCount = GetIntProperty(nxLib, "ChildCount");
        for (var i = 0; i < childCount; i++)
        {
            var child = Invoke(nxLib, "GetChild", i);
            try
            {
                dto.Children.Add(MapLibrary(child));
            }
            finally
            {
                ReleaseCom(ref child);
            }
        }

        return dto;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        var value = target.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            null,
            target,
            args);

        return value ?? throw new InvalidOperationException(
            $"COM invocation '{methodName}' returned null on type {target.GetType().FullName}.");
    }

    private static object? TryInvoke(object target, string methodName, params object[] args)
    {
        try
        {
            return Invoke(target, methodName, args);
        }
        catch
        {
            return null;
        }
    }

    private static int InvokeInt(object target, string methodName, params object[] args)
    {
        var value = Invoke(target, methodName, args);
        return value is int i ? i : Convert.ToInt32(value);
    }

    private static int GetIntProperty(object target, string propertyName)
    {
        var value = target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value is int i ? i : Convert.ToInt32(value);
    }

    private static bool GetBool(object target, string propertyName)
    {
        var value = target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value is bool b ? b : Convert.ToBoolean(value);
    }

    private static string GetString(object target, string propertyName)
    {
        var value = target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value?.ToString() ?? string.Empty;
    }

    private static void ReleaseCom(ref object? comObject)
    {
        ComLifecycle.Release(ref comObject);
    }
}
