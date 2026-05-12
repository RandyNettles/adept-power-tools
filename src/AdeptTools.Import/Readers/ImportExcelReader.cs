using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using OfficeOpenXml;

namespace AdeptTools.Import.Readers;

public class ImportExcelReader
{
    public ImportExcelData ReadWorkbook(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));

        var result = new ImportExcelData();

        // Read Config sheet
        var configSheet = package.Workbook.Worksheets["Config"];
        if (configSheet is null)
            throw new InvalidOperationException("Config sheet not found in workbook.");

        result.Config = ReadConfig(configSheet);

        // Read IMP-Mapping sheet
        var mappingSheet = package.Workbook.Worksheets["IMP-Mapping"];
        if (mappingSheet is null)
            throw new InvalidOperationException("IMP-Mapping sheet not found in workbook.");

        result.Mappings = ReadMappings(mappingSheet);

        // Read IMP-Data sheet
        var dataSheet = package.Workbook.Worksheets["IMP-Data"];
        if (dataSheet is null)
            throw new InvalidOperationException("IMP-Data sheet not found in workbook.");

        result.DataRows = ReadDataRows(dataSheet, result.Config, result.Mappings);

        return result;
    }

    private static ImportConfig ReadConfig(ExcelWorksheet sheet)
    {
        var config = new ImportConfig
        {
            ServerUrl = GetNamedRangeValue(sheet, "ServerUrl"),
            ImportMode = ParseImportMode(GetNamedRangeValue(sheet, "ImportMode")),
            AddIfNotFound = ParseBool(GetNamedRangeValue(sheet, "AddIfNotFound")),
            WorkAreaId = GetNamedRangeValue(sheet, "WorkAreaId"),
            SkipHiddenRows = ParseBool(GetNamedRangeValue(sheet, "SkipHiddenRows")),
            DryRun = ParseBool(GetNamedRangeValue(sheet, "DryRun"))
        };

        var headerRowsText = GetNamedRangeValue(sheet, "HeaderRows");
        if (int.TryParse(headerRowsText, out var headerRows) && headerRows > 0)
            config.HeaderRows = headerRows;

        return config;
    }

    private static List<ColumnMapping> ReadMappings(ExcelWorksheet sheet)
    {
        var mappings = new List<ColumnMapping>();

        // Find the mapping table - look for tblIMP_Mapping or scan for headers
        var headers = FindHeaders(sheet);
        if (headers.Count == 0)
            throw new InvalidOperationException("IMP-Mapping table not found. Expected headers: ExcelColumn, AdeptField, Action.");

        int excelColIdx = FindColumn(headers, "ExcelColumn", "Excel Column");
        int adeptFieldIdx = FindColumn(headers, "AdeptField", "Adept Field");
        int actionIdx = FindColumn(headers, "Action");
        int operatorIdx = FindColumn(headers, "Operator");
        int dateRangeIdx = FindColumn(headers, "DateRangeColumn", "Date Range Column");

        if (excelColIdx < 0 || adeptFieldIdx < 0 || actionIdx < 0)
            throw new InvalidOperationException("IMP-Mapping table missing required columns: ExcelColumn, AdeptField, Action.");

        var headerRow = headers.First().Value.Row;
        var row = headerRow + 1;
        var endRow = sheet.Dimension?.End.Row ?? 0;

        while (row <= endRow)
        {
            var excelColumn = GetCellText(sheet, row, excelColIdx);
            var adeptField = GetCellText(sheet, row, adeptFieldIdx);
            var actionText = GetCellText(sheet, row, actionIdx);

            if (string.IsNullOrWhiteSpace(excelColumn) && string.IsNullOrWhiteSpace(adeptField))
            {
                row++;
                continue;
            }

            var action = ParseMappingAction(actionText);
            if (action == MappingAction.DoNotImport)
            {
                row++;
                continue;
            }

            var mapping = new ColumnMapping
            {
                ExcelColumn = excelColumn,
                AdeptField = adeptField,
                Action = action
            };

            if (operatorIdx >= 0)
            {
                var operatorText = GetCellText(sheet, row, operatorIdx);
                mapping.Operator = ParseSearchOperator(operatorText);
            }

            if (dateRangeIdx >= 0)
            {
                mapping.DateRangeColumn = GetCellText(sheet, row, dateRangeIdx);
                if (string.IsNullOrWhiteSpace(mapping.DateRangeColumn))
                    mapping.DateRangeColumn = null;
            }

            mappings.Add(mapping);
            row++;
        }

        return mappings;
    }

    private static List<ImportRow> ReadDataRows(ExcelWorksheet sheet, ImportConfig config, List<ColumnMapping> mappings)
    {
        var rows = new List<ImportRow>();
        if (sheet.Dimension is null) return rows;

        // Build header index from first row (or configurable via HeaderRows)
        var headerRow = config.HeaderRows;
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int col = 1; col <= sheet.Dimension.End.Column; col++)
        {
            var headerText = GetCellText(sheet, headerRow, col);
            if (!string.IsNullOrWhiteSpace(headerText) && !columnIndex.ContainsKey(headerText))
                columnIndex[headerText] = col;
        }

        // Validate mapping columns exist in data headers
        foreach (var mapping in mappings)
        {
            if (!columnIndex.ContainsKey(mapping.ExcelColumn))
                throw new InvalidOperationException(
                    $"Mapping references Excel column '{mapping.ExcelColumn}' which was not found in the IMP-Data sheet headers.");
        }

        // Read data rows
        var startRow = headerRow + 1;
        for (int row = startRow; row <= sheet.Dimension.End.Row; row++)
        {
            if (config.SkipHiddenRows && sheet.Row(row).Hidden)
                continue;

            var importRow = new ImportRow { RowNumber = row };
            var hasData = false;

            foreach (var (header, col) in columnIndex)
            {
                var cellValue = sheet.Cells[row, col].Value;
                importRow.Values[header] = cellValue;
                if (cellValue is not null && cellValue.ToString()?.Trim().Length > 0)
                    hasData = true;
            }

            if (hasData)
                rows.Add(importRow);
        }

        return rows;
    }

    private static Dictionary<int, (string Header, int Row)> FindHeaders(ExcelWorksheet sheet)
    {
        if (sheet.Dimension is null) return new();

        // Scan first 10 rows for header row
        for (int row = 1; row <= Math.Min(10, sheet.Dimension.End.Row); row++)
        {
            var headers = new Dictionary<int, (string Header, int Row)>();
            for (int col = 1; col <= sheet.Dimension.End.Column; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (!string.IsNullOrWhiteSpace(text))
                    headers[col] = (text, row);
            }

            // Check if this row has the expected header columns
            if (headers.Values.Any(h => h.Header.Equals("ExcelColumn", StringComparison.OrdinalIgnoreCase)
                                    || h.Header.Equals("Excel Column", StringComparison.OrdinalIgnoreCase))
                && headers.Values.Any(h => h.Header.Equals("AdeptField", StringComparison.OrdinalIgnoreCase)
                                       || h.Header.Equals("Adept Field", StringComparison.OrdinalIgnoreCase)))
            {
                return headers;
            }
        }

        return new();
    }

    private static int FindColumn(Dictionary<int, (string Header, int Row)> headers, params string[] names)
    {
        foreach (var (col, info) in headers)
        {
            foreach (var name in names)
            {
                if (string.Equals(info.Header, name, StringComparison.OrdinalIgnoreCase))
                    return col;
            }
        }
        return -1;
    }

    private static string GetCellText(ExcelWorksheet sheet, int row, int col)
    {
        return sheet.Cells[row, col].Text?.Trim() ?? string.Empty;
    }

    private static string GetNamedRangeValue(ExcelWorksheet sheet, string rangeName)
    {
        var name = sheet.Workbook.Names.FirstOrDefault(n =>
            string.Equals(n.Name, rangeName, StringComparison.OrdinalIgnoreCase));
        if (name is null) return string.Empty;
        return name.Value?.ToString()?.Trim() ?? string.Empty;
    }

    private static bool ParseBool(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "1" or "✓" => true,
            _ => false
        };
    }

    private static ImportMode ParseImportMode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ImportMode.UpdateDataCard;
        return text.Trim().ToLowerInvariant() switch
        {
            "searchresultsonly" or "search results only" or "search" => ImportMode.SearchResultsOnly,
            _ => ImportMode.UpdateDataCard
        };
    }

    private static MappingAction ParseMappingAction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return MappingAction.DoNotImport;
        return text.Trim().ToLowerInvariant() switch
        {
            "searchkey" or "search key" or "search" or "0" or "1" or "2" or "3" => MappingAction.SearchKey,
            "fillifempty" or "fill if empty" or "ifempty" or "4" => MappingAction.FillIfEmpty,
            "filloverwrite" or "fill overwrite" or "overwrite" or "fill" or "5" => MappingAction.FillOverwrite,
            "donotimport" or "do not import" or "skip" or "ignore" or "6" => MappingAction.DoNotImport,
            _ => MappingAction.DoNotImport
        };
    }

    private static SearchOperator? ParseSearchOperator(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchOperator.Equals;
        return text.Trim().ToLowerInvariant() switch
        {
            "equals" or "eq" or "=" or "0" => SearchOperator.Equals,
            "dateafter" or "date after" or "after" or "1" => SearchOperator.DateAfter,
            "datebefore" or "date before" or "before" or "2" => SearchOperator.DateBefore,
            "datebetween" or "date between" or "between" or "3" => SearchOperator.DateBetween,
            _ => SearchOperator.Equals
        };
    }
}
