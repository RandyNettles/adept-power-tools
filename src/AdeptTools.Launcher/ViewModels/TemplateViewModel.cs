using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Launcher.Services;
using OfficeOpenXml;

namespace AdeptTools.Launcher.ViewModels;

public enum WorkbookType
{
    DataImport,
    Workflow
}

public partial class TemplateViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private WorkbookType _selectedType = WorkbookType.DataImport;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private string? _resultMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private bool _isSuccess;

    public bool IsError => !IsSuccess && ResultMessage is not null;

    public TemplateViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    [RelayCommand]
    private void BrowseOutputPath()
    {
        var defaultName = SelectedType == WorkbookType.DataImport
            ? "import-template.xlsx"
            : "workflow-template.xlsx";

        var path = _dialogService.ShowSaveFileDialog(
            "Excel Workbooks (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            defaultName);

        if (path is not null)
            OutputPath = path;
    }

    private bool CanGenerate() => !string.IsNullOrWhiteSpace(OutputPath) && !IsGenerating;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
        ResultMessage = null;

        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var outputPath = OutputPath;
            var selectedType = SelectedType;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() =>
            {
                using var package = new ExcelPackage();

                if (selectedType == WorkbookType.Workflow)
                    BuildWorkflowTemplate(package);
                else
                    BuildImportTemplate(package);

                package.SaveAs(new FileInfo(outputPath));
            });

            IsSuccess = true;
            ResultMessage = $"Template created: {outputPath}";
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            ResultMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private static void BuildWorkflowTemplate(ExcelPackage package)
    {
        // Config sheet — connection settings are managed by the launcher's Connect screen
        var config = package.Workbook.Worksheets.Add("Config");
        config.Cells[1, 1].Value = "Setting";
        config.Cells[1, 2].Value = "Value";
        config.Cells[2, 1].Value = "DryRun";
        config.Cells[2, 2].Value = "true";
        config.Cells[1, 1].Style.Font.Bold = true;
        config.Cells[1, 2].Style.Font.Bold = true;
        config.Column(1).Width = 16;
        config.Column(2).Width = 40;

            // Template workflow sheet — vertical sub-table layout
            var wf = package.Workbook.Worksheets.Add("WF-_Template");
            wf.Cells[1, 1].Value = "Workflow Name:";
            wf.Cells[1, 2].Value = "(rename this sheet to WF-YourWorkflowName)";
            wf.Cells[3, 1].Value = "Memo:";
            wf.Cells[4, 1].Value = "Deadline (days):";
            wf.Cells[5, 1].Value = "Active:";
            wf.Cells[5, 2].Value = "true";
            wf.Cells[6, 1].Value = "Shared:";
            wf.Cells[6, 2].Value = "false";

            // Instructions (row 7 — before the step table header, outside the data range)
            wf.Cells[7, 1].Value = "Instructions: Each group of rows is a step. The first row starts a new step (fill in Step Name). " +
                "Continuation rows (blank Step Name) add trustees to the previous step.";

            // Step table headers at row 8 — vertical layout
            wf.Cells[8, 1].Value = "Step Name";
            wf.Cells[8, 2].Value = "Approvals Required";
            wf.Cells[8, 3].Value = "Auto Advance";
            wf.Cells[8, 4].Value = "Allow Empty Trustees";
            wf.Cells[8, 5].Value = "Trustee";
            wf.Cells[8, 6].Value = "Type";
            wf.Cells[8, 7].Value = "Role";

            // Example: Step 1 with multiple trustees (vertical rows)
            wf.Cells[9, 1].Value = "Draft";
            wf.Cells[9, 2].Value = 0;
            wf.Cells[9, 3].Value = "false";
            wf.Cells[9, 4].Value = "false";
            wf.Cells[9, 5].Value = "jsmith";
            wf.Cells[9, 6].Value = "User";
            wf.Cells[9, 7].Value = "Reviewer";
            // Continuation row (same step, additional trustee)
            wf.Cells[10, 5].Value = "eng-managers";
            wf.Cells[10, 6].Value = "Group";
            wf.Cells[10, 7].Value = "Notify";

            // Example: Step 2 with trustees
            wf.Cells[11, 1].Value = "Review";
            wf.Cells[11, 2].Value = 2;
            wf.Cells[11, 3].Value = "true";
            wf.Cells[11, 4].Value = "false";
            wf.Cells[11, 5].Value = "mjones";
            wf.Cells[11, 6].Value = "User";
            wf.Cells[11, 7].Value = "Reviewer";
            wf.Cells[12, 5].Value = "akhan";
            wf.Cells[12, 6].Value = "User";
            wf.Cells[12, 7].Value = "Reviewer";
            wf.Cells[13, 5].Value = "pm-notify";
            wf.Cells[13, 6].Value = "Group";
            wf.Cells[13, 7].Value = "Alert";

            // Example: Terminal step with no trustees
            wf.Cells[14, 1].Value = "Approved";
            wf.Cells[14, 2].Value = 0;
            wf.Cells[14, 3].Value = "false";
            wf.Cells[14, 4].Value = "true";

            // Data validation: Type dropdown
            var typeValidation = wf.DataValidations.AddListValidation("F9:F100");
            typeValidation.Formula.Values.Add("User");
            typeValidation.Formula.Values.Add("Group");
            typeValidation.Formula.Values.Add("Meta");
            typeValidation.Formula.Values.Add("Email");

            // Data validation: Role dropdown
            var roleValidation = wf.DataValidations.AddListValidation("G9:G100");
            roleValidation.Formula.Values.Add("Reviewer");
            roleValidation.Formula.Values.Add("Notify");
            roleValidation.Formula.Values.Add("Alert");

            // Formatting
            using (var range = wf.Cells[8, 1, 8, 7])
            {
                range.Style.Font.Bold = true;
            }
            wf.Column(1).Width = 20;
            wf.Column(2).Width = 20;
            wf.Column(3).Width = 14;
            wf.Column(4).Width = 22;
            wf.Column(5).Width = 24;
            wf.Column(6).Width = 14;
            wf.Column(7).Width = 14;
            wf.Column(4).Width = 20;
            wf.Column(5).Width = 10;
            wf.Column(6).Width = 12;


        }

        private static void BuildImportTemplate(ExcelPackage package)
        {
            // Config sheet — connection settings are managed by the launcher's Connect screen
            var config = package.Workbook.Worksheets.Add("Config");
            config.Cells[1, 1].Value = "Setting";
            config.Cells[1, 2].Value = "Value";
            config.Cells[2, 1].Value = "ImportMode";
            config.Cells[2, 2].Value = "Update";
            config.Cells[3, 1].Value = "AddIfNotFound";
            config.Cells[3, 2].Value = "false";
            config.Cells[4, 1].Value = "WorkAreaId";
            config.Cells[5, 1].Value = "HeaderRows";
            config.Cells[5, 2].Value = "1";
            config.Cells[6, 1].Value = "SkipHiddenRows";
            config.Cells[6, 2].Value = "true";
            config.Cells[7, 1].Value = "DryRun";
            config.Cells[7, 2].Value = "true";
        config.Cells[1, 1].Style.Font.Bold = true;
        config.Cells[1, 2].Style.Font.Bold = true;
        config.Column(1).Width = 18;
        config.Column(2).Width = 40;

        // IMP-Mapping sheet
        var mapping = package.Workbook.Worksheets.Add("IMP-Mapping");
        mapping.Cells[1, 1].Value = "ExcelColumn";
        mapping.Cells[1, 2].Value = "AdeptField";
        mapping.Cells[1, 3].Value = "Action";
        mapping.Cells[2, 1].Value = "Title";
        mapping.Cells[2, 2].Value = "DESCRIPTION";
        mapping.Cells[2, 3].Value = "Set";
        using (var range = mapping.Cells[1, 1, 1, 3])
        {
            range.Style.Font.Bold = true;
        }
        mapping.Column(1).Width = 16;
        mapping.Column(2).Width = 20;
        mapping.Column(3).Width = 12;

        // IMP-Data sheet
        var data = package.Workbook.Worksheets.Add("IMP-Data");
        data.Cells[1, 1].Value = "Title";
        data.Cells[1, 1].Style.Font.Bold = true;
        data.Column(1).Width = 30;
    }

    [RelayCommand]
    private void OpenInExcel()
    {
        if (string.IsNullOrWhiteSpace(OutputPath) || !System.IO.File.Exists(OutputPath))
            return;

        Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
            return;

        var folder = System.IO.Path.GetDirectoryName(OutputPath);
        if (folder is not null && System.IO.Directory.Exists(folder))
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }
}
