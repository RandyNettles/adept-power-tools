using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Backend.Com.Interop;
using AdeptTools.Core.Models;
using AdeptTools.Import.Api;
using AdeptTools.Import.Models;

namespace AdeptTools.Backend.Com.Api;

/// <summary>
/// COM-based implementation of IImportApiClient.
/// Uses NxDb/NxDataCard COM objects for field definitions, search, and data card operations.
/// </summary>
public class ComImportApiClient : IImportApiClient
{
    private readonly ComOperationRunner _runner;
    private readonly ComSessionManager _session;

    public ComImportApiClient(ComOperationRunner runner, ComSessionManager session)
    {
        _runner = runner;
        _session = session;
    }

    public async Task<List<AdeptFieldDefinitionDto>> GetAvailableFieldsAsync(CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var fields = new List<AdeptFieldDefinitionDto>();
            var count = db.GetFieldCount();

            for (var i = 0; i < count; i++)
            {
                var fieldDef = db.GetFieldDef(i);
                try
                {
                    var dto = new AdeptFieldDefinitionDto
                    {
                        FieldName = fieldDef.FieldName,
                        DisplayName = fieldDef.DisplayName,
                        SchemaId = fieldDef.SchemaId,
                        FieldType = fieldDef.FieldType,
                        Width = fieldDef.Width,
                        IsSystem = fieldDef.IsSystem,
                        IsRestricted = fieldDef.IsRestricted,
                        RestrictedValues = new List<string>()
                    };

                    if (fieldDef.IsRestricted)
                    {
                        for (var j = 0; j < fieldDef.RestrictedValueCount; j++)
                        {
                            dto.RestrictedValues.Add(fieldDef.GetRestrictedValue(j));
                        }
                    }

                    fields.Add(dto);
                }
                finally
                {
                    ComLifecycle.Release(ref fieldDef);
                }
            }

            return fields;
        }, ct);
    }

    public async Task<SearchResultDto> SearchByFieldsAsync(SearchRequestDto search, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            // Create search criteria from the request
            var criteria = CreateSearchCriteria(db, search);
            INxSearchResult? searchResult = null;

            try
            {
                searchResult = db.Search(criteria);

                var result = new SearchResultDto
                {
                    MatchCount = searchResult.RowCount
                };

                for (var i = 0; i < searchResult.RowCount; i++)
                {
                    var row = searchResult.GetRow(i);
                    try
                    {
                        var resultRow = new SearchResultRow
                        {
                            TableNumber = row.TableNumber,
                            FileId = row.FileId,
                            MajRev = row.MajRev,
                            MinRev = row.MinRev,
                            FieldValues = new Dictionary<string, string>()
                        };

                        // Populate field values from the search criteria fields
                        foreach (var term in search.SearchCriteria)
                        {
                            if (!string.IsNullOrEmpty(term.FieldName))
                            {
                                var value = row.GetFieldValue(term.FieldName);
                                if (value != null)
                                    resultRow.FieldValues[term.FieldName] = value;
                            }
                        }

                        result.Rows.Add(resultRow);
                    }
                    finally
                    {
                        ComLifecycle.Release(ref row);
                    }
                }

                return result;
            }
            finally
            {
                ComLifecycle.Release(ref searchResult);
                ComLifecycle.Release(ref criteria);
            }
        }, ct);
    }

    public async Task<Dictionary<string, string>> GetDataCardValuesAsync(
        int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var dataCard = db.GetDataCard(tableNumber, fileId, majRev, minRev);
            try
            {
                var values = new Dictionary<string, string>();
                for (var i = 0; i < dataCard.FieldCount; i++)
                {
                    var fieldName = dataCard.GetFieldName(i);
                    var fieldValue = dataCard.GetFieldValue(fieldName);
                    values[fieldName] = fieldValue ?? string.Empty;
                }
                return values;
            }
            finally
            {
                ComLifecycle.Release(ref dataCard);
            }
        }, ct);
    }

    public async Task<ApiResult> SaveDataCardAsync(
        int tableNumber, string fileId, int majRev, int minRev,
        Dictionary<string, string> fieldValues, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var dataCard = db.GetDataCard(tableNumber, fileId, majRev, minRev);
            try
            {
                foreach (var (fieldName, value) in fieldValues)
                {
                    dataCard.SetFieldValue(fieldName, value);
                }

                var result = dataCard.Save();
                return result == 0
                    ? ApiResult.Success($"Saved {fieldValues.Count} fields via COM.")
                    : ApiResult.Failure(result, $"COM DataCard Save failed with code {result}.");
            }
            finally
            {
                ComLifecycle.Release(ref dataCard);
            }
        }, ct);
    }

    public async Task<List<string>> GetRestrictedValuesAsync(string schemaId, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var count = db.GetFieldCount();
            for (var i = 0; i < count; i++)
            {
                var fieldDef = db.GetFieldDef(i);
                try
                {
                    if (fieldDef.SchemaId == schemaId && fieldDef.IsRestricted)
                    {
                        var values = new List<string>();
                        for (var j = 0; j < fieldDef.RestrictedValueCount; j++)
                        {
                            values.Add(fieldDef.GetRestrictedValue(j));
                        }
                        return values;
                    }
                }
                finally
                {
                    ComLifecycle.Release(ref fieldDef);
                }
            }

            return new List<string>();
        }, ct);
    }

    public async Task<CreateDocResultDto> CreateNewDocumentAsync(
        string workAreaId, string fileName, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var result = db.CreateDocument(workAreaId, fileName, out var fileId, out var majRev, out var minRev);

            if (result != 0)
            {
                return new CreateDocResultDto
                {
                    StatusCode = result,
                    ErrorMessage = $"COM CreateDocument failed with code {result}."
                };
            }

            return new CreateDocResultDto
            {
                StatusCode = 0,
                TableNumber = 1,
                FileId = fileId,
                MajRev = majRev,
                MinRev = minRev
            };
        }, ct);
    }

    public async Task<ApiResult> CheckInToLibraryAsync(
        string fileId, int majRev, int minRev, string libraryId, CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var result = db.CheckInToLibrary(fileId, majRev, minRev, libraryId);
            return result == 0
                ? ApiResult.Success("Checked in via COM.")
                : ApiResult.Failure(result, $"COM CheckIn failed with code {result}.");
        }, ct);
    }

    public async Task<int> GetMaxFilenameLengthAsync(CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);
        return await _runner.RunAsync(() => db.GetMaxFilenameLength(), ct);
    }

    public async Task<List<LibraryDto>> GetLibraryTreeAsync(CancellationToken ct = default)
    {
        var db = await _session.GetDatabaseAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var tree = db.GetLibraryTree();
            try
            {
                var libraries = new List<LibraryDto>();
                for (var i = 0; i < tree.LibraryCount; i++)
                {
                    var nxLib = tree.GetLibrary(i);
                    try
                    {
                        libraries.Add(MapLibrary(nxLib));
                    }
                    finally
                    {
                        ComLifecycle.Release(ref nxLib);
                    }
                }
                return libraries;
            }
            finally
            {
                ComLifecycle.Release(ref tree);
            }
        }, ct);
    }

    private static INxSearchCriteria CreateSearchCriteria(INxDb db, SearchRequestDto search)
    {
        // Create criteria via late-binding (COM SDK creates it internally)
        // The INxDb.Search method accepts the criteria object
        var criteria = (INxSearchCriteria)Activator.CreateInstance(
            Type.GetTypeFromProgID("AdeptSDK.NxSearchCriteria", throwOnError: true)!)!;

        criteria.SetMaxResults(100);

        foreach (var term in search.SearchCriteria)
        {
            var fieldName = term.FieldName ?? string.Empty;
            var value = term.ValueStr ?? string.Empty;
            criteria.AddCondition(fieldName, term.SearchOp, value);
        }

        return criteria;
    }

    private static LibraryDto MapLibrary(INxLibrary nxLib)
    {
        var dto = new LibraryDto
        {
            LibraryId = nxLib.LibraryId,
            Name = nxLib.Name,
            Path = nxLib.Path,
            Children = new List<LibraryDto>()
        };

        for (var i = 0; i < nxLib.ChildCount; i++)
        {
            var child = nxLib.GetChild(i);
            try
            {
                dto.Children.Add(MapLibrary(child));
            }
            finally
            {
                ComLifecycle.Release(ref child);
            }
        }

        return dto;
    }
}
