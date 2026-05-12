using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public interface IImportService
{
    Task<ImportBatchResult> RunAsync(ImportRunRequest request, IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
    Task<MappingValidationResult> ValidateAsync(ImportValidateRequest request, CancellationToken ct = default);
    Task<List<AdeptFieldDefinitionDto>> FetchFieldsAsync(CancellationToken ct = default);
    Task<List<ColumnMapping>> AutoMapAsync(string excelPath, string? sheetName = null, CancellationToken ct = default);
}
