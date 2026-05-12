using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public class FieldResolutionResult
{
    public List<SearchKeyMapping> SearchKeys { get; set; } = new();
    public List<FillFieldMapping> FillFields { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<AdeptFieldDefinitionDto> AllFields { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}

public class FieldResolver
{
    public async Task<FieldResolutionResult> ResolveFieldsAsync(
        List<ColumnMapping> mappings, IImportApiClient client, CancellationToken ct = default)
    {
        var result = new FieldResolutionResult();
        var fields = await client.GetAvailableFieldsAsync(ct);
        result.AllFields = fields;

        foreach (var mapping in mappings)
        {
            // Look up field by name (case-insensitive)
            var fieldDef = fields.FirstOrDefault(f =>
                string.Equals(f.FieldName, mapping.AdeptField, StringComparison.OrdinalIgnoreCase));

            // Fallback: try displayName match
            if (fieldDef is null)
            {
                fieldDef = fields.FirstOrDefault(f =>
                    string.Equals(f.DisplayName, mapping.AdeptField, StringComparison.OrdinalIgnoreCase));
            }

            if (fieldDef is null)
            {
                result.Errors.Add($"Field '{mapping.AdeptField}' (mapped from Excel column '{mapping.ExcelColumn}') was not found in server field definitions.");
                continue;
            }

            // Update the mapping's field type from the resolved definition
            mapping.FieldType = fieldDef.FieldType;

            var isDate = fieldDef.FieldType.Equals("FDT_DATE", StringComparison.OrdinalIgnoreCase);

            switch (mapping.Action)
            {
                case MappingAction.SearchKey:
                    result.SearchKeys.Add(new SearchKeyMapping
                    {
                        ExcelColumn = mapping.ExcelColumn,
                        AdeptFieldName = fieldDef.FieldName,
                        SchemaId = fieldDef.SchemaId,
                        Operator = mapping.Operator ?? SearchOperator.Equals,
                        DateRangeColumn = mapping.DateRangeColumn,
                        IsDate = isDate
                    });
                    break;

                case MappingAction.FillIfEmpty:
                    result.FillFields.Add(new FillFieldMapping
                    {
                        ExcelColumn = mapping.ExcelColumn,
                        AdeptFieldName = fieldDef.FieldName,
                        SchemaId = fieldDef.SchemaId,
                        Mode = FillMode.IfEmpty,
                        IsDate = isDate
                    });
                    break;

                case MappingAction.FillOverwrite:
                    result.FillFields.Add(new FillFieldMapping
                    {
                        ExcelColumn = mapping.ExcelColumn,
                        AdeptFieldName = fieldDef.FieldName,
                        SchemaId = fieldDef.SchemaId,
                        Mode = FillMode.Overwrite,
                        IsDate = isDate
                    });
                    break;
            }
        }

        return result;
    }
}
