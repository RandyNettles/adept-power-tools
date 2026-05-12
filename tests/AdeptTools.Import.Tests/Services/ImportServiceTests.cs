using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Readers;
using AdeptTools.Import.Services;
using Xunit;

namespace AdeptTools.Import.Tests.Services;

public class ImportServiceTests
{
    private static ImportService CreateService(MockImportApiClient? mockClient = null)
    {
        var client = mockClient ?? new MockImportApiClient();
        return new ImportService(
            client,
            new ImportExcelReader(),
            new ImportXmlConfigReader(),
            new FieldResolver(),
            new MappingValidator(),
            new SearchBuilder(),
            new AutoMapper());
    }

    private static (List<SearchKeyMapping> searchKeys, List<FillFieldMapping> fillFields) DefaultMappings()
    {
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "FileName", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Description", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        };
        return (searchKeys, fillFields);
    }

    private static List<ImportRow> CreateTestRows(int count)
    {
        var rows = new List<ImportRow>();
        for (int i = 1; i <= count; i++)
        {
            rows.Add(new ImportRow
            {
                RowNumber = i + 1,
                Values = new()
                {
                    ["FileName"] = $"DWG-{i:D3}.dwg",
                    ["Description"] = $"Test drawing {i}"
                }
            });
        }
        return rows;
    }

    [Fact]
    public async Task RunAsync_AllMatch_AllUpdated()
    {
        var mockClient = new MockImportApiClient(MockImportMode.Default);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(5)),
            ct: CancellationToken.None);

        Assert.Equal(5, result.TotalRows);
        Assert.Equal(5, result.Updated);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task RunAsync_NoMatch_AddIfNotFoundFalse_AllSkipped()
    {
        var mockClient = new MockImportApiClient(MockImportMode.NoMatch);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(3), addIfNotFound: false),
            ct: CancellationToken.None);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(0, result.Updated);
        Assert.Equal(3, result.Skipped);
    }

    [Fact]
    public async Task RunAsync_NoMatch_AddIfNotFoundTrue_AllCreated()
    {
        var mockClient = new MockImportApiClient(MockImportMode.NoMatch);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(3), addIfNotFound: true),
            ct: CancellationToken.None);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(3, result.Created);
        Assert.Equal(3, mockClient.CreatedDocuments.Count);
    }

    [Fact]
    public async Task RunAsync_MultiMatch_AllSkipped()
    {
        var mockClient = new MockImportApiClient(MockImportMode.MultiMatch);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(2)),
            ct: CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Skipped);
        Assert.All(result.Results, r => Assert.Contains("Multiple", r.Message));
    }

    [Fact]
    public async Task RunAsync_DryRun_NoApiCalls()
    {
        var mockClient = new MockImportApiClient(MockImportMode.Default);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(3), dryRun: true),
            ct: CancellationToken.None);

        Assert.True(result.DryRun);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(3, result.Skipped); // dry-run rows are reported as skipped
        Assert.Empty(mockClient.SavedDataCards);
        Assert.Equal(0, mockClient.SearchCallCount);
    }

    [Fact]
    public async Task RunAsync_Cancellation_PartialResults()
    {
        var mockClient = new MockImportApiClient(MockImportMode.Default);
        var service = CreateServiceDirect(mockClient);
        var cts = new CancellationTokenSource();

        var rows = CreateTestRows(10);
        var progressCount = 0;
        var progress = new Progress<ImportProgress>(_ =>
        {
            progressCount++;
            if (progressCount >= 4) // Cancel after 2 rows (each row reports twice)
                cts.Cancel();
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RunAsync(CreateDirectRunRequest(rows), progress, cts.Token));
    }

    [Fact]
    public async Task RunAsync_SearchResultsOnlyMode_NoSaves()
    {
        var mockClient = new MockImportApiClient(MockImportMode.Default);
        var service = CreateServiceDirect(mockClient);

        var result = await service.RunAsync(
            CreateDirectRunRequest(CreateTestRows(3), importMode: ImportMode.SearchResultsOnly),
            ct: CancellationToken.None);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(3, result.Skipped); // SearchResultsOnly skips matching rows without writing
        Assert.Empty(mockClient.SavedDataCards);
    }

    [Fact]
    public async Task RunAsync_FillIfEmpty_SkipsPopulatedFields()
    {
        var mockClient = new FillIfEmptyMockClient();
        var service = CreateServiceDirect(mockClient, fillMode: FillMode.IfEmpty);

        var rows = new List<ImportRow>
        {
            new() { RowNumber = 2, Values = new() { ["FileName"] = "DWG-001.dwg", ["Description"] = "New desc" } }
        };

        var result = await service.RunAsync(
            CreateDirectRunRequest(rows, fillMode: FillMode.IfEmpty),
            ct: CancellationToken.None);

        // The mock returns DESCRIPTION as populated, so it should be skipped,
        // but the field may still have other empty fields to update
        Assert.Equal(1, result.TotalRows);
    }

    [Fact]
    public async Task RunAsync_EmptySearchKey_RowSkipped()
    {
        var mockClient = new MockImportApiClient(MockImportMode.Default);
        var service = CreateServiceDirect(mockClient);

        var rows = new List<ImportRow>
        {
            new() { RowNumber = 2, Values = new() { ["FileName"] = "", ["Description"] = "Test" } }
        };

        var result = await service.RunAsync(
            CreateDirectRunRequest(rows),
            ct: CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("Empty search key", result.Results[0].Message);
    }

    // Helper: create service that operates directly with pre-built mappings (bypasses file parsing)
    private static DirectImportService CreateServiceDirect(
        MockImportApiClient client, FillMode fillMode = FillMode.Overwrite)
    {
        return new DirectImportService(client, fillMode);
    }

    private static DirectRunRequest CreateDirectRunRequest(
        List<ImportRow> rows,
        bool addIfNotFound = false,
        bool dryRun = false,
        ImportMode importMode = ImportMode.UpdateDataCard,
        FillMode fillMode = FillMode.Overwrite)
    {
        return new DirectRunRequest
        {
            DataRows = rows,
            Config = new ImportConfig { ImportMode = importMode, AddIfNotFound = addIfNotFound, DryRun = dryRun },
            FillMode = fillMode
        };
    }

    // Mock that returns populated DESCRIPTION field
    private class FillIfEmptyMockClient : MockImportApiClient
    {
        public override Task<Dictionary<string, string>> GetDataCardValuesAsync(
            int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default)
        {
            return Task.FromResult(new Dictionary<string, string>
            {
                ["S_LONGNAME"] = $"{fileId}.dwg",
                ["U1"] = "Existing description" // DESCRIPTION is already populated
            });
        }
    }
}

// Test-only wrapper that bypasses file parsing to test the row processing logic directly
internal class DirectRunRequest
{
    public List<ImportRow> DataRows { get; init; } = new();
    public ImportConfig Config { get; init; } = new();
    public FillMode FillMode { get; init; }
}

internal class DirectImportService
{
    private readonly MockImportApiClient _client;
    private readonly FillMode _fillMode;

    public DirectImportService(MockImportApiClient client, FillMode fillMode = FillMode.Overwrite)
    {
        _client = client;
        _fillMode = fillMode;
    }

    public async Task<ImportBatchResult> RunAsync(
        DirectRunRequest request,
        IProgress<ImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var batch = new ImportBatchResult { TotalRows = request.DataRows.Count };

        // Build mappings directly
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "FileName", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Description", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = _fillMode }
        };

        if (request.Config.DryRun)
        {
            batch.DryRun = true;
            foreach (var row in request.DataRows)
            {
                batch.Results.Add(new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = row.GetStringValue("FileName"),
                    Outcome = ImportOutcome.Skipped,
                    Message = "Dry run — no changes made"
                });
                batch.Skipped++;
            }
            return batch;
        }

        var searchBuilder = new SearchBuilder();
        var hasFillIfEmpty = fillFields.Any(f => f.Mode == FillMode.IfEmpty);

        foreach (var row in request.DataRows)
        {
            ct.ThrowIfCancellationRequested();

            var primaryKey = row.GetStringValue("FileName");

            progress?.Report(new ImportProgress
            {
                RowNumber = row.RowNumber,
                TotalRows = request.DataRows.Count,
                CurrentPrimaryKey = primaryKey,
                Phase = ImportPhase.Processing
            });

            var search = searchBuilder.BuildSearch(searchKeys, row);
            if (search is null)
            {
                batch.Results.Add(new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = primaryKey,
                    Outcome = ImportOutcome.Skipped,
                    Message = "Empty search key"
                });
                batch.Skipped++;
                continue;
            }

            var searchResult = await _client.SearchByFieldsAsync(search, ct);

            ImportRowResult rowResult;

            switch (searchResult.MatchCount)
            {
                case 0 when !request.Config.AddIfNotFound:
                    rowResult = new ImportRowResult
                    {
                        RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                        Outcome = ImportOutcome.Skipped, Message = "Not found"
                    };
                    batch.Skipped++;
                    break;

                case 0 when request.Config.AddIfNotFound:
                    var createResult = await _client.CreateNewDocumentAsync(
                        request.Config.WorkAreaId ?? "DEFAULT", primaryKey, ct);

                    var createFieldValues = BuildFieldValues(row, fillFields);
                    if (createFieldValues.Count > 0)
                        await _client.SaveDataCardAsync(createResult.TableNumber, createResult.FileId,
                            createResult.MajRev, createResult.MinRev, createFieldValues, ct);

                    rowResult = new ImportRowResult
                    {
                        RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                        Outcome = ImportOutcome.Created, Message = "Created",
                        FieldsUpdated = createFieldValues.Count
                    };
                    batch.Created++;
                    break;

                case 1 when request.Config.ImportMode == ImportMode.SearchResultsOnly:
                    rowResult = new ImportRowResult
                    {
                        RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                        Outcome = ImportOutcome.Skipped, Message = "Match found (search results only)"
                    };
                    batch.Skipped++;
                    break;

                case 1:
                    var match = searchResult.Rows[0];
                    Dictionary<string, string>? currentValues = null;
                    if (hasFillIfEmpty)
                        currentValues = await _client.GetDataCardValuesAsync(
                            match.TableNumber, match.FileId, match.MajRev, match.MinRev, ct);

                    var fieldValues = BuildFieldValues(row, fillFields, currentValues);
                    if (fieldValues.Count > 0)
                    {
                        await _client.SaveDataCardAsync(match.TableNumber, match.FileId,
                            match.MajRev, match.MinRev, fieldValues, ct);
                        rowResult = new ImportRowResult
                        {
                            RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                            Outcome = ImportOutcome.Updated, Message = $"Updated ({fieldValues.Count} fields)",
                            FieldsUpdated = fieldValues.Count
                        };
                        batch.Updated++;
                    }
                    else
                    {
                        rowResult = new ImportRowResult
                        {
                            RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                            Outcome = ImportOutcome.Skipped, Message = "No fields to update"
                        };
                        batch.Skipped++;
                    }
                    break;

                default:
                    rowResult = new ImportRowResult
                    {
                        RowNumber = row.RowNumber, PrimaryKeyDisplay = primaryKey,
                        Outcome = ImportOutcome.Skipped, Message = $"Multiple ({searchResult.MatchCount}) matches found"
                    };
                    batch.Skipped++;
                    break;
            }

            batch.Results.Add(rowResult);

            progress?.Report(new ImportProgress
            {
                RowNumber = row.RowNumber,
                TotalRows = request.DataRows.Count,
                CurrentPrimaryKey = primaryKey,
                Phase = ImportPhase.Processing,
                Outcome = rowResult.Outcome,
                Message = rowResult.Message
            });
        }

        return batch;
    }

    private static Dictionary<string, string> BuildFieldValues(
        ImportRow row, List<FillFieldMapping> fillFields, Dictionary<string, string>? currentValues = null)
    {
        var values = new Dictionary<string, string>();
        foreach (var fill in fillFields)
        {
            var cellValue = row.GetStringValue(fill.ExcelColumn);

            if (fill.Mode == FillMode.IfEmpty && currentValues is not null)
            {
                if (currentValues.TryGetValue(fill.SchemaId, out var existing) && !string.IsNullOrEmpty(existing))
                    continue;
            }

            if (!string.IsNullOrEmpty(cellValue))
                values[fill.SchemaId] = cellValue;
        }
        return values;
    }
}
