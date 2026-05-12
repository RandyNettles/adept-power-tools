using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Readers;

namespace AdeptTools.Import.Services;

public class ImportService : IImportService
{
    private readonly IImportApiClient _apiClient;
    private readonly ImportExcelReader _excelReader;
    private readonly ImportXmlConfigReader _xmlConfigReader;
    private readonly FieldResolver _fieldResolver;
    private readonly MappingValidator _validator;
    private readonly SearchBuilder _searchBuilder;
    private readonly AutoMapper _autoMapper;

    // Library cache for AddIfNotFound flow
    private Dictionary<string, string>? _libraryCache;
    private List<LibraryDto>? _libraryTree;

    public ImportService(
        IImportApiClient apiClient,
        ImportExcelReader excelReader,
        ImportXmlConfigReader xmlConfigReader,
        FieldResolver fieldResolver,
        MappingValidator validator,
        SearchBuilder searchBuilder,
        AutoMapper autoMapper)
    {
        _apiClient = apiClient;
        _excelReader = excelReader;
        _xmlConfigReader = xmlConfigReader;
        _fieldResolver = fieldResolver;
        _validator = validator;
        _searchBuilder = searchBuilder;
        _autoMapper = autoMapper;
    }

    public async Task<ImportBatchResult> RunAsync(
        ImportRunRequest request, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
    {
        var batch = new ImportBatchResult();

        // Phase A: Parse input
        progress?.Report(new ImportProgress { Phase = ImportPhase.Parsing });

        var (config, mappings, dataRows) = ParseInput(request.ExcelPath, request.ConfigPath);

        if (request.DryRun)
            config.DryRun = true;

        batch.TotalRows = dataRows.Count;

        // Phase B: Resolve fields
        var resolution = await _fieldResolver.ResolveFieldsAsync(mappings, _apiClient, ct);
        if (!resolution.IsValid)
        {
            batch.Errors.AddRange(resolution.Errors);
            return batch;
        }

        // Phase C: Validate
        progress?.Report(new ImportProgress { Phase = ImportPhase.Validating });

        var validation = _validator.Validate(config, resolution.SearchKeys, resolution.FillFields, dataRows, resolution.AllFields);
        if (!validation.IsValid)
        {
            batch.Errors.AddRange(validation.Errors.Select(e => e.Message));
            return batch;
        }

        // Phase D: Dry-run exit gate
        if (config.DryRun)
        {
            batch.DryRun = true;
            // Generate synthetic results for dry-run reporting
            foreach (var row in dataRows)
            {
                var primaryKey = GetPrimaryKeyDisplay(row, resolution.SearchKeys);
                batch.Results.Add(new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = primaryKey,
                    Outcome = ImportOutcome.Skipped,
                    Message = "Dry run — no changes made"
                });
                batch.Skipped++;
            }
            return batch;
        }

        // Phase E: Row-by-row processing
        var hasFillIfEmpty = resolution.FillFields.Any(f => f.Mode == FillMode.IfEmpty);

        for (var i = 0; i < dataRows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var row = dataRows[i];
            var primaryKey = GetPrimaryKeyDisplay(row, resolution.SearchKeys);

            progress?.Report(new ImportProgress
            {
                RowNumber = row.RowNumber,
                TotalRows = dataRows.Count,
                CurrentPrimaryKey = primaryKey,
                Phase = ImportPhase.Processing
            });

            try
            {
                var rowResult = await ProcessRowAsync(
                    row, config, resolution.SearchKeys, resolution.FillFields, hasFillIfEmpty, primaryKey, ct);

                batch.Results.Add(rowResult);
                switch (rowResult.Outcome)
                {
                    case ImportOutcome.Updated: batch.Updated++; break;
                    case ImportOutcome.Created: batch.Created++; break;
                    case ImportOutcome.Skipped: batch.Skipped++; break;
                    case ImportOutcome.Failed: batch.Failed++; break;
                }

                progress?.Report(new ImportProgress
                {
                    RowNumber = row.RowNumber,
                    TotalRows = dataRows.Count,
                    CurrentPrimaryKey = primaryKey,
                    Phase = ImportPhase.Processing,
                    Outcome = rowResult.Outcome,
                    Message = rowResult.Message
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failResult = new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = primaryKey,
                    Outcome = ImportOutcome.Failed,
                    Message = ex.Message
                };
                batch.Results.Add(failResult);
                batch.Failed++;
            }
        }

        progress?.Report(new ImportProgress { Phase = ImportPhase.Complete });
        return batch;
    }

    public async Task<MappingValidationResult> ValidateAsync(ImportValidateRequest request, CancellationToken ct = default)
    {
        var (config, mappings, dataRows) = ParseInput(request.ExcelPath, request.ConfigPath);

        var resolution = await _fieldResolver.ResolveFieldsAsync(mappings, _apiClient, ct);
        if (!resolution.IsValid)
        {
            var errorResult = new MappingValidationResult
            {
                RowCount = dataRows.Count,
                SearchKeyCount = 0,
                FillFieldCount = 0
            };
            foreach (var error in resolution.Errors)
                errorResult.Errors.Add(new MappingValidationError { Message = error });
            return errorResult;
        }

        return _validator.Validate(config, resolution.SearchKeys, resolution.FillFields, dataRows, resolution.AllFields);
    }

    public async Task<List<AdeptFieldDefinitionDto>> FetchFieldsAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetAvailableFieldsAsync(ct);
    }

    public async Task<List<ColumnMapping>> AutoMapAsync(string excelPath, string? sheetName = null, CancellationToken ct = default)
    {
        // Read Excel headers
        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new OfficeOpenXml.ExcelPackage(new FileInfo(excelPath));

        var sheet = sheetName is not null
            ? package.Workbook.Worksheets[sheetName]
            : package.Workbook.Worksheets.FirstOrDefault();

        if (sheet is null)
            throw new InvalidOperationException(sheetName is not null
                ? $"Sheet '{sheetName}' not found in workbook."
                : "Workbook has no worksheets.");

        var headers = new List<string>();
        if (sheet.Dimension is not null)
        {
            for (int col = 1; col <= sheet.Dimension.End.Column; col++)
            {
                var text = sheet.Cells[1, col].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    headers.Add(text);
            }
        }

        var fields = await _apiClient.GetAvailableFieldsAsync(ct);
        return _autoMapper.AutoMap(headers, fields);
    }

    private (ImportConfig config, List<ColumnMapping> mappings, List<ImportRow> dataRows) ParseInput(
        string excelPath, string? configPath)
    {
        if (configPath is not null)
        {
            // XML config + Excel data
            var (config, mappings) = _xmlConfigReader.ReadConfig(configPath);
            var excelData = _excelReader.ReadWorkbook(excelPath);
            return (config, mappings, excelData.DataRows);
        }
        else
        {
            // All-in-one Excel workbook
            var excelData = _excelReader.ReadWorkbook(excelPath);
            return (excelData.Config, excelData.Mappings, excelData.DataRows);
        }
    }

    private async Task<ImportRowResult> ProcessRowAsync(
        ImportRow row,
        ImportConfig config,
        List<SearchKeyMapping> searchKeys,
        List<FillFieldMapping> fillFields,
        bool hasFillIfEmpty,
        string primaryKey,
        CancellationToken ct)
    {
        // Build search query
        var search = _searchBuilder.BuildSearch(searchKeys, row);
        if (search is null)
        {
            return new ImportRowResult
            {
                RowNumber = row.RowNumber,
                PrimaryKeyDisplay = primaryKey,
                Outcome = ImportOutcome.Skipped,
                Message = "Empty search key"
            };
        }

        // Execute search
        var searchResult = await _apiClient.SearchByFieldsAsync(search, ct);

        // Branch on match count
        switch (searchResult.MatchCount)
        {
            case 0 when !config.AddIfNotFound:
                return new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = primaryKey,
                    Outcome = ImportOutcome.Skipped,
                    Message = "Not found"
                };

            case 0 when config.AddIfNotFound:
                return await CreateAndUpdateAsync(row, config, searchKeys, fillFields, primaryKey, ct);

            case 1:
                if (config.ImportMode == ImportMode.SearchResultsOnly)
                {
                    return new ImportRowResult
                    {
                        RowNumber = row.RowNumber,
                        PrimaryKeyDisplay = primaryKey,
                        Outcome = ImportOutcome.Skipped,
                        Message = "Match found (search results only)"
                    };
                }

                var matchedRow = searchResult.Rows[0];
                return await UpdateFieldValuesAsync(
                    row, matchedRow.TableNumber, matchedRow.FileId, matchedRow.MajRev, matchedRow.MinRev,
                    fillFields, hasFillIfEmpty, primaryKey, ct);

            default:
                return new ImportRowResult
                {
                    RowNumber = row.RowNumber,
                    PrimaryKeyDisplay = primaryKey,
                    Outcome = ImportOutcome.Skipped,
                    Message = $"Multiple ({searchResult.MatchCount}) matches found"
                };
        }
    }

    private async Task<ImportRowResult> CreateAndUpdateAsync(
        ImportRow row,
        ImportConfig config,
        List<SearchKeyMapping> searchKeys,
        List<FillFieldMapping> fillFields,
        string primaryKey,
        CancellationToken ct)
    {
        // Get S_LONGNAME value from row
        var longNameKey = searchKeys.FirstOrDefault(sk =>
            string.Equals(sk.AdeptFieldName, "S_LONGNAME", StringComparison.OrdinalIgnoreCase));

        var fileName = longNameKey is not null ? row.GetStringValue(longNameKey.ExcelColumn) : primaryKey;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new ImportRowResult
            {
                RowNumber = row.RowNumber,
                PrimaryKeyDisplay = primaryKey,
                Outcome = ImportOutcome.Failed,
                Message = "Cannot create document: S_LONGNAME value is empty"
            };
        }

        var workAreaId = config.WorkAreaId ?? "DEFAULT";
        var createResult = await _apiClient.CreateNewDocumentAsync(workAreaId, fileName, ct);

        if (!createResult.IsSuccess)
        {
            return new ImportRowResult
            {
                RowNumber = row.RowNumber,
                PrimaryKeyDisplay = primaryKey,
                Outcome = ImportOutcome.Failed,
                Message = $"Create failed: {createResult.ErrorMessage}"
            };
        }

        // Update fields on the new document
        var updateResult = await UpdateFieldValuesAsync(
            row, createResult.TableNumber, createResult.FileId, createResult.MajRev, createResult.MinRev,
            fillFields, false, primaryKey, ct);

        // Library check-in
        var libraryResult = await TryCheckInToLibraryAsync(row, searchKeys, createResult.FileId,
            createResult.MajRev, createResult.MinRev, ct);

        var libraryMessage = libraryResult is not null ? $", signed into {libraryResult}" : ", in work area";

        return new ImportRowResult
        {
            RowNumber = row.RowNumber,
            PrimaryKeyDisplay = primaryKey,
            Outcome = ImportOutcome.Created,
            FieldsUpdated = updateResult.FieldsUpdated,
            Message = $"Created{libraryMessage} ({updateResult.FieldsUpdated} fields)"
        };
    }

    private async Task<ImportRowResult> UpdateFieldValuesAsync(
        ImportRow row,
        int tableNumber,
        string fileId,
        int majRev,
        int minRev,
        List<FillFieldMapping> fillFields,
        bool hasFillIfEmpty,
        string primaryKey,
        CancellationToken ct)
    {
        Dictionary<string, string>? currentValues = null;

        // Only fetch current values if any Fill: If Empty mappings exist
        if (hasFillIfEmpty)
        {
            currentValues = await _apiClient.GetDataCardValuesAsync(tableNumber, fileId, majRev, minRev, ct);
        }

        var fieldValues = new Dictionary<string, string>();

        foreach (var fill in fillFields)
        {
            var cellValue = row.GetStringValue(fill.ExcelColumn);

            if (fill.Mode == FillMode.IfEmpty && currentValues is not null)
            {
                if (currentValues.TryGetValue(fill.SchemaId, out var currentVal)
                    && !string.IsNullOrEmpty(currentVal))
                {
                    continue; // Field already has a value, skip
                }
            }

            if (fill.IsDate)
            {
                var dateVal = row.GetDateValue(fill.ExcelColumn);
                if (dateVal.HasValue)
                    cellValue = dateVal.Value.ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrEmpty(cellValue))
                fieldValues[fill.SchemaId] = cellValue;
        }

        if (fieldValues.Count == 0)
        {
            return new ImportRowResult
            {
                RowNumber = row.RowNumber,
                PrimaryKeyDisplay = primaryKey,
                Outcome = ImportOutcome.Skipped,
                Message = "No fields to update"
            };
        }

        var saveResult = await _apiClient.SaveDataCardAsync(tableNumber, fileId, majRev, minRev, fieldValues, ct);

        if (!saveResult.IsSuccess)
        {
            return new ImportRowResult
            {
                RowNumber = row.RowNumber,
                PrimaryKeyDisplay = primaryKey,
                Outcome = ImportOutcome.Failed,
                Message = $"Save failed: {saveResult.ErrorMessage}",
                FieldsUpdated = 0
            };
        }

        return new ImportRowResult
        {
            RowNumber = row.RowNumber,
            PrimaryKeyDisplay = primaryKey,
            Outcome = ImportOutcome.Updated,
            Message = $"Updated ({fieldValues.Count} fields)",
            FieldsUpdated = fieldValues.Count
        };
    }

    private async Task<string?> TryCheckInToLibraryAsync(
        ImportRow row,
        List<SearchKeyMapping> searchKeys,
        string fileId, int majRev, int minRev,
        CancellationToken ct)
    {
        // Detect library-related search keys
        var libNameKey = searchKeys.FirstOrDefault(sk =>
            string.Equals(sk.AdeptFieldName, "S_LIBNAME", StringComparison.OrdinalIgnoreCase));
        var libPathKey = searchKeys.FirstOrDefault(sk =>
            string.Equals(sk.AdeptFieldName, "S_LIBPATH", StringComparison.OrdinalIgnoreCase));
        var libIdKey = searchKeys.FirstOrDefault(sk =>
            string.Equals(sk.AdeptFieldName, "S_LIBID", StringComparison.OrdinalIgnoreCase));

        string? libraryId = null;
        string? libraryDisplayName = null;

        if (libIdKey is not null)
        {
            libraryId = row.GetStringValue(libIdKey.ExcelColumn);
            libraryDisplayName = libraryId;
        }
        else if (libNameKey is not null)
        {
            var libName = row.GetStringValue(libNameKey.ExcelColumn);
            if (!string.IsNullOrWhiteSpace(libName))
            {
                libraryId = await ResolveLibraryByNameAsync(libName, ct);
                libraryDisplayName = libName;
            }
        }
        else if (libPathKey is not null)
        {
            var libPath = row.GetStringValue(libPathKey.ExcelColumn);
            if (!string.IsNullOrWhiteSpace(libPath))
            {
                libraryId = await ResolveLibraryByPathAsync(libPath, ct);
                libraryDisplayName = libPath;
            }
        }

        if (string.IsNullOrWhiteSpace(libraryId))
            return null;

        await _apiClient.CheckInToLibraryAsync(fileId, majRev, minRev, libraryId, ct);
        return libraryDisplayName;
    }

    private async Task<string?> ResolveLibraryByNameAsync(string name, CancellationToken ct)
    {
        _libraryCache ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_libraryCache.TryGetValue($"name:{name}", out var cached))
            return cached;

        _libraryTree ??= await _apiClient.GetLibraryTreeAsync(ct);

        var libraryId = FindLibraryByName(_libraryTree, name);
        if (libraryId is not null)
            _libraryCache[$"name:{name}"] = libraryId;

        return libraryId;
    }

    private async Task<string?> ResolveLibraryByPathAsync(string path, CancellationToken ct)
    {
        _libraryCache ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_libraryCache.TryGetValue($"path:{path}", out var cached))
            return cached;

        _libraryTree ??= await _apiClient.GetLibraryTreeAsync(ct);

        var libraryId = FindLibraryByPath(_libraryTree, path);
        if (libraryId is not null)
            _libraryCache[$"path:{path}"] = libraryId;

        return libraryId;
    }

    private static string? FindLibraryByName(List<LibraryDto> libraries, string name)
    {
        foreach (var lib in libraries)
        {
            if (string.Equals(lib.Name, name, StringComparison.OrdinalIgnoreCase))
                return lib.LibraryId;

            var childResult = FindLibraryByName(lib.Children, name);
            if (childResult is not null)
                return childResult;
        }
        return null;
    }

    private static string? FindLibraryByPath(List<LibraryDto> libraries, string path)
    {
        foreach (var lib in libraries)
        {
            if (string.Equals(lib.Path, path, StringComparison.OrdinalIgnoreCase))
                return lib.LibraryId;

            var childResult = FindLibraryByPath(lib.Children, path);
            if (childResult is not null)
                return childResult;
        }
        return null;
    }

    private static string GetPrimaryKeyDisplay(ImportRow row, List<SearchKeyMapping> searchKeys)
    {
        if (searchKeys.Count == 0) return $"Row {row.RowNumber}";
        var firstKey = searchKeys[0];
        var value = row.GetStringValue(firstKey.ExcelColumn);
        return string.IsNullOrWhiteSpace(value) ? $"Row {row.RowNumber}" : value;
    }
}
