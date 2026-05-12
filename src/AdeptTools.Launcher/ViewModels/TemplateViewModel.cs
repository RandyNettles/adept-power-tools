using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Launcher.Services;

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
    private string? _resultMessage;

    [ObservableProperty]
    private bool _isSuccess;

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
            // Template generation is a placeholder for now — will be implemented
            // when the Workflow/Import libraries are available (Stages 2B/2C)
            await Task.Delay(500); // simulate work

            // Create a minimal placeholder file
            await System.IO.File.WriteAllBytesAsync(OutputPath, Array.Empty<byte>());

            IsSuccess = true;
            ResultMessage = $"Template created: {OutputPath}";
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
}
