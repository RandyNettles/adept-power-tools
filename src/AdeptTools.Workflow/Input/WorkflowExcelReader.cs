using System.Text.RegularExpressions;
using AdeptTools.Workflow.Models;
using OfficeOpenXml;

namespace AdeptTools.Workflow.Input;

public class WorkflowExcelReader
{
    private static readonly Regex TrusteeHeaderRegex = new(@"^Trustee\s+(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TypeHeaderRegex = new(@"^Type\s+(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public WorkflowExcelInput Read(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));

        var result = new WorkflowExcelInput();

        // Read Config sheet
        var configSheet = package.Workbook.Worksheets["Config"];
        if (configSheet is not null)
        {
            result.ServerUrl = GetCellText(configSheet, "ServerUrl");
            result.ProjectName = GetCellText(configSheet, "ProjectName");
            var dryRunText = GetCellText(configSheet, "DryRun");
            result.DryRun = string.Equals(dryRunText, "true", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(dryRunText, "yes", StringComparison.OrdinalIgnoreCase);
        }

        // Discover WF- prefixed sheets
        foreach (var sheet in package.Workbook.Worksheets)
        {
            if (!sheet.Name.StartsWith("WF-", StringComparison.OrdinalIgnoreCase))
                continue;
            if (sheet.Name.Equals("WF-_Template", StringComparison.OrdinalIgnoreCase))
                continue;

            var workflowName = sheet.Name[3..]; // Strip "WF-" prefix
            var model = ReadWorkflowSheet(sheet, workflowName);
            result.Workflows.Add(model);
        }

        return result;
    }

    private static WorkflowInputModel ReadWorkflowSheet(ExcelWorksheet sheet, string workflowName)
    {
        var model = new WorkflowInputModel
        {
            Name = workflowName,
            Memo = GetCellText(sheet, 3, 2),
            Active = ParseBool(GetCellText(sheet, 5, 2), defaultValue: true)
        };

        var deadlineDaysText = GetCellText(sheet, 4, 2);
        if (int.TryParse(deadlineDaysText, out var deadlineDays))
            model.TimeoutDays = deadlineDays;

        // Read step headers at row 7
        var headerRow = 7;
        var headers = ReadHeaders(sheet, headerRow);

        if (headers.Count == 0)
            return model;

        // Find column indices
        int stepNameCol = FindColumn(headers, "Step Name");
        int approvalsCol = FindColumn(headers, "Approvals Required");
        int autoAdvanceCol = FindColumn(headers, "Auto Advance");

        // Find trustee/type column pairs
        var trusteePairs = FindTrusteePairs(headers);

        // Read step rows starting at row 8
        var row = headerRow + 1;
        while (row <= sheet.Dimension?.End.Row)
        {
            var stepNameText = stepNameCol >= 0 ? GetCellText(sheet, row, stepNameCol) : null;
            if (string.IsNullOrWhiteSpace(stepNameText))
                break;

            var step = new WorkflowInputStep
            {
                Name = stepNameText
            };

            if (approvalsCol >= 0)
            {
                var approvalsText = GetCellText(sheet, row, approvalsCol);
                if (int.TryParse(approvalsText, out var approvals))
                    step.RequiredApprovalsCount = approvals;
            }

            if (autoAdvanceCol >= 0)
            {
                step.AutoAdvance = ParseBool(GetCellText(sheet, row, autoAdvanceCol), defaultValue: false);
            }

            // Read trustees
            foreach (var (trusteeCol, typeCol) in trusteePairs)
            {
                var trusteeId = GetCellText(sheet, row, trusteeCol);
                var typeText = GetCellText(sheet, row, typeCol);

                if (string.IsNullOrWhiteSpace(trusteeId))
                    continue;

                if (TrusteeTypeMapper.TryMap(typeText, out var trusteeType))
                {
                    step.Trustees.Add(new WorkflowInputTrustee
                    {
                        TrusteeId = trusteeId,
                        TrusteeType = trusteeType
                    });
                }
            }

            model.Steps.Add(step);
            row++;
        }

        return model;
    }

    private static Dictionary<int, string> ReadHeaders(ExcelWorksheet sheet, int row)
    {
        var headers = new Dictionary<int, string>();
        if (sheet.Dimension is null) return headers;

        for (int col = 1; col <= sheet.Dimension.End.Column; col++)
        {
            var text = GetCellText(sheet, row, col);
            if (!string.IsNullOrWhiteSpace(text))
                headers[col] = text;
        }

        return headers;
    }

    private static int FindColumn(Dictionary<int, string> headers, string name)
    {
        foreach (var (col, header) in headers)
        {
            if (string.Equals(header, name, StringComparison.OrdinalIgnoreCase))
                return col;
        }
        return -1;
    }

    private static List<(int TrusteeCol, int TypeCol)> FindTrusteePairs(Dictionary<int, string> headers)
    {
        var trusteeColumns = new Dictionary<int, int>(); // number → column
        var typeColumns = new Dictionary<int, int>();     // number → column

        foreach (var (col, header) in headers)
        {
            var trusteeMatch = TrusteeHeaderRegex.Match(header);
            if (trusteeMatch.Success)
            {
                trusteeColumns[int.Parse(trusteeMatch.Groups[1].Value)] = col;
                continue;
            }

            var typeMatch = TypeHeaderRegex.Match(header);
            if (typeMatch.Success)
            {
                typeColumns[int.Parse(typeMatch.Groups[1].Value)] = col;
            }
        }

        var pairs = new List<(int, int)>();
        foreach (var (number, trusteeCol) in trusteeColumns.OrderBy(kv => kv.Key))
        {
            if (typeColumns.TryGetValue(number, out var typeCol))
            {
                pairs.Add((trusteeCol, typeCol));
            }
        }

        return pairs;
    }

    private static string GetCellText(ExcelWorksheet sheet, int row, int col)
    {
        return sheet.Cells[row, col].Text?.Trim() ?? string.Empty;
    }

    private static string GetCellText(ExcelWorksheet sheet, string namedRange)
    {
        var name = sheet.Workbook.Names.FirstOrDefault(n =>
            string.Equals(n.Name, namedRange, StringComparison.OrdinalIgnoreCase));
        if (name is null) return string.Empty;
        return name.Value?.ToString()?.Trim() ?? string.Empty;
    }

    private static bool ParseBool(string text, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        return text.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "1" or "✓" => true,
            "FALSE" or "NO" or "0" or "✗" => false,
            _ => defaultValue
        };
    }
}
