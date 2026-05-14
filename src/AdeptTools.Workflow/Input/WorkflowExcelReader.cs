using AdeptTools.Workflow.Models;
using OfficeOpenXml;

namespace AdeptTools.Workflow.Input;

public class WorkflowExcelReader
{
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

        // Find column indices for the vertical layout
        int stepNameCol = FindColumn(headers, "Step Name");
        int approvalsCol = FindColumn(headers, "Approvals Required");
        int autoAdvanceCol = FindColumn(headers, "Auto Advance");
        int trusteeCol = FindColumn(headers, "Trustee");
        int typeCol = FindColumn(headers, "Type");
        int roleCol = FindColumn(headers, "Role");

        // Read rows using vertical sub-table logic:
        // A new step begins when the Step Name cell is non-empty.
        // Subsequent rows with empty Step Name belong to the preceding step (trustee rows).
        WorkflowInputStep? currentStep = null;
        var dataStart = headerRow + 1;
        var lastRow = sheet.Dimension?.End.Row ?? dataStart;

        for (int row = dataStart; row <= lastRow; row++)
        {
            var stepNameText = stepNameCol >= 0 ? GetCellText(sheet, row, stepNameCol) : null;

            if (!string.IsNullOrWhiteSpace(stepNameText))
            {
                // New step boundary
                currentStep = new WorkflowInputStep
                {
                    Name = stepNameText
                };

                if (approvalsCol >= 0)
                {
                    var approvalsText = GetCellText(sheet, row, approvalsCol);
                    if (int.TryParse(approvalsText, out var approvals))
                        currentStep.RequiredApprovalsCount = approvals;
                }

                if (autoAdvanceCol >= 0)
                {
                    currentStep.AutoAdvance = ParseBool(GetCellText(sheet, row, autoAdvanceCol), defaultValue: false);
                }

                model.Steps.Add(currentStep);
            }

            if (currentStep is null)
                continue;

            // Read trustee from this row (applies to both step rows and continuation rows)
            if (trusteeCol < 0)
                continue;

            var trusteeValue = GetCellText(sheet, row, trusteeCol);
            if (string.IsNullOrWhiteSpace(trusteeValue))
                continue;

            var typeText = typeCol >= 0 ? GetCellText(sheet, row, typeCol) : string.Empty;
            var roleText = roleCol >= 0 ? GetCellText(sheet, row, roleCol) : string.Empty;

            // Comma-delimited split
            var trusteeIds = TrusteeParser.Split(trusteeValue);
            foreach (var id in trusteeIds)
            {
                if (TrusteeTypeMapper.TryMap(typeText, out var trusteeType))
                {
                    TrusteeTypeMapper.TryMapRole(roleText, out var role);

                    currentStep.Trustees.Add(new WorkflowInputTrustee
                    {
                        TrusteeId = id,
                        TrusteeType = trusteeType,
                        Role = role
                    });
                }
            }
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
