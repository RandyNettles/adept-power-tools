using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public class MappingValidationResult
{
    public List<MappingValidationError> Errors { get; } = new();
    public List<MappingValidationWarning> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public int RowCount { get; set; }
    public int SearchKeyCount { get; set; }
    public int FillFieldCount { get; set; }
}

public class MappingValidationError
{
    public required string Message { get; init; }
    public string? Field { get; init; }
}

public class MappingValidationWarning
{
    public required string Message { get; init; }
    public string? Field { get; init; }
    public int? RowNumber { get; init; }
}

public class MappingValidator
{
    public MappingValidationResult Validate(
        ImportConfig config,
        List<SearchKeyMapping> searchKeys,
        List<FillFieldMapping> fillFields,
        List<ImportRow> dataRows,
        List<AdeptFieldDefinitionDto> fieldDefs)
    {
        var result = new MappingValidationResult
        {
            RowCount = dataRows.Count,
            SearchKeyCount = searchKeys.Count,
            FillFieldCount = fillFields.Count
        };

        // Must have at least one search key
        if (searchKeys.Count == 0)
        {
            result.Errors.Add(new MappingValidationError
            {
                Message = "At least one search key field is required."
            });
        }

        // Must have at least one fill field (unless SearchResultsOnly)
        if (fillFields.Count == 0 && config.ImportMode != ImportMode.SearchResultsOnly)
        {
            result.Errors.Add(new MappingValidationError
            {
                Message = "At least one fill field is required (unless using SearchResultsOnly mode)."
            });
        }

        // No duplicate search key fields
        var dupSearchKeys = searchKeys
            .GroupBy(sk => sk.AdeptFieldName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var dup in dupSearchKeys)
        {
            result.Errors.Add(new MappingValidationError
            {
                Message = $"Duplicate search key field: '{dup.Key}'.",
                Field = dup.Key
            });
        }

        // No duplicate fill fields
        var dupFillFields = fillFields
            .GroupBy(ff => ff.AdeptFieldName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var dup in dupFillFields)
        {
            result.Errors.Add(new MappingValidationError
            {
                Message = $"Duplicate fill field: '{dup.Key}'.",
                Field = dup.Key
            });
        }

        // DateBetween requires DateRangeColumn
        foreach (var sk in searchKeys.Where(sk => sk.Operator == SearchOperator.DateBetween))
        {
            if (string.IsNullOrWhiteSpace(sk.DateRangeColumn))
            {
                result.Errors.Add(new MappingValidationError
                {
                    Message = $"Search key '{sk.AdeptFieldName}' uses DateBetween operator but has no DateRangeColumn specified.",
                    Field = sk.AdeptFieldName
                });
            }
        }

        // S_LONGNAME width check for AddIfNotFound
        if (config.AddIfNotFound)
        {
            var longNameField = fieldDefs.FirstOrDefault(f =>
                string.Equals(f.FieldName, "S_LONGNAME", StringComparison.OrdinalIgnoreCase));

            if (longNameField is not null)
            {
                // Check if any fill field or search key maps to S_LONGNAME and scan data rows
                var longNameMapping = searchKeys.FirstOrDefault(sk =>
                    string.Equals(sk.AdeptFieldName, "S_LONGNAME", StringComparison.OrdinalIgnoreCase));

                if (longNameMapping is not null)
                {
                    foreach (var row in dataRows)
                    {
                        var value = row.GetStringValue(longNameMapping.ExcelColumn);
                        if (value.Length > longNameField.Width)
                        {
                            result.Errors.Add(new MappingValidationError
                            {
                                Message = $"Row {row.RowNumber}: S_LONGNAME value '{value}' exceeds maximum width of {longNameField.Width}.",
                                Field = "S_LONGNAME"
                            });
                        }
                    }
                }
            }
        }

        // Date parse warnings for search key date fields
        foreach (var sk in searchKeys.Where(sk => sk.IsDate))
        {
            foreach (var row in dataRows)
            {
                var value = row.GetStringValue(sk.ExcelColumn);
                if (!string.IsNullOrWhiteSpace(value) && row.GetDateValue(sk.ExcelColumn) is null)
                {
                    result.Warnings.Add(new MappingValidationWarning
                    {
                        Message = $"Row {row.RowNumber}: Cannot parse date value '{value}' for search key '{sk.AdeptFieldName}'.",
                        Field = sk.AdeptFieldName,
                        RowNumber = row.RowNumber
                    });
                }
            }
        }

        return result;
    }
}
