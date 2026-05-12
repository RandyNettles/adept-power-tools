using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public class AutoMapper
{
    private static readonly Dictionary<string, string> KnownAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Library Name"] = "S_LIBNAME",
        ["Library Path"] = "S_LIBPATH",
        ["Library Id"] = "S_LIBID",
        ["File Name"] = "S_LONGNAME",
        ["Filename"] = "S_LONGNAME",
        ["Revision Date"] = "S_REVDATE",
    };

    public List<ColumnMapping> AutoMap(List<string> excelHeaders, List<AdeptFieldDefinitionDto> fieldDefs)
    {
        var mappings = new List<ColumnMapping>();

        foreach (var header in excelHeaders)
        {
            var fieldDef = FindField(header, fieldDefs);

            if (fieldDef is null)
            {
                mappings.Add(new ColumnMapping
                {
                    ExcelColumn = header,
                    AdeptField = header,
                    Action = MappingAction.DoNotImport
                });
                continue;
            }

            var isDate = fieldDef.FieldType.Equals("FDT_DATE", StringComparison.OrdinalIgnoreCase);
            var action = fieldDef.IsSystem ? MappingAction.SearchKey : MappingAction.FillOverwrite;
            var op = action == MappingAction.SearchKey ? SearchOperator.Equals : (SearchOperator?)null;

            mappings.Add(new ColumnMapping
            {
                ExcelColumn = header,
                AdeptField = fieldDef.FieldName,
                Action = action,
                Operator = op,
                FieldType = fieldDef.FieldType
            });
        }

        return mappings;
    }

    private static AdeptFieldDefinitionDto? FindField(string header, List<AdeptFieldDefinitionDto> fieldDefs)
    {
        // Try exact match on fieldName
        var match = fieldDefs.FirstOrDefault(f =>
            string.Equals(f.FieldName, header, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        // Try exact match on displayName
        match = fieldDefs.FirstOrDefault(f =>
            string.Equals(f.DisplayName, header, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        // Try known aliases
        if (KnownAliases.TryGetValue(header, out var aliasFieldName))
        {
            match = fieldDefs.FirstOrDefault(f =>
                string.Equals(f.FieldName, aliasFieldName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return null;
    }
}
