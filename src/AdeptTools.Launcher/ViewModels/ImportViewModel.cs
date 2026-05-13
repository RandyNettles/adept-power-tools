using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Readers;
using AdeptTools.Import.Services;
using AdeptTools.Launcher.Services;

namespace AdeptTools.Launcher.ViewModels;

public partial class ImportViewModel : ObservableObject
{
    private readonly Func<IImportService> _serviceFactory;
    private readonly Func<IImportApiClient> _apiClientFactory;
    private readonly IDialogService _dialogService;
    private readonly MockModeState _mockModeState;
    private readonly ImportExcelReader _excelReader;
    private CancellationTokenSource? _cts;

    // Loaded workbook data (cached for re-validation and import)
    private ImportExcelData? _loadedData;

    public ObservableCollection<MappingRowItem> MappingRows { get; } = new();
    public ObservableCollection<ResultItem> ResultItems { get; } = new();

    [ObservableProperty]
    private string _workbookPath = string.Empty;

    [ObservableProperty]
    private string _configPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunImportCommand))]
    private bool _isWorkbookLoaded;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseWorkbookCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseConfigCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(FetchFieldsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
    private bool _isOperationRunning;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunImportCommand))]
    private bool _isValidated;

    [ObservableProperty]
    private ImportWorkbookSummary? _workbookSummary;

    [ObservableProperty]
    private string _validationStatusText = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isMockMode;

    [ObservableProperty]
    private bool _isFileLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _mappingFooter = string.Empty;

    public ImportViewModel(
        Func<IImportService> serviceFactory,
        Func<IImportApiClient> apiClientFactory,
        IDialogService dialogService,
        MockModeState mockModeState,
        ImportExcelReader excelReader)
    {
        _serviceFactory = serviceFactory;
        _apiClientFactory = apiClientFactory;
        _dialogService = dialogService;
        _mockModeState = mockModeState;
        _excelReader = excelReader;
        _isMockMode = mockModeState.IsMock;

        mockModeState.Changed += (_, isMock) => IsMockMode = isMock;
    }

    // --- Browse Workbook ---

    private bool CanBrowse() => !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseWorkbookAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("Excel Workbooks (*.xlsx)|*.xlsx");
        if (path is null) return;

        WorkbookPath = path;
        await LoadWorkbookAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseConfigAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("XML Config (*.xml)|*.xml");
        if (path is null) return;

        ConfigPath = path;

        // Re-load if workbook is already loaded
        if (!string.IsNullOrWhiteSpace(WorkbookPath) && IsWorkbookLoaded)
            await LoadWorkbookAsync(WorkbookPath);
    }

    public async Task LoadWorkbookAsync(string path)
    {
        IsWorkbookLoaded = false;
        IsValidated = false;
        IsFileLoading = true;
        ErrorMessage = null;
        MappingRows.Clear();
        WorkbookSummary = null;

        try
        {
            var data = _excelReader.ReadWorkbook(path);

            // Detect if this is a workflow workbook (has WF- sheets but no IMP- sheets)
            if (data.Mappings.Count == 0 && data.DataRows.Count == 0)
            {
                ErrorMessage = "No import data found. This may be a Workflow workbook — switch to the Workflows page.";
                return;
            }

            // Apply XML config overlay if specified
            if (!string.IsNullOrWhiteSpace(ConfigPath))
            {
                var xmlReader = new ImportXmlConfigReader();
                var (xmlConfig, xmlMappings) = xmlReader.ReadConfig(ConfigPath);
                data.Config = xmlConfig;
                if (xmlMappings.Count > 0)
                    data.Mappings = xmlMappings;
            }

            _loadedData = data;

            // Build summary
            var searchKeys = data.Mappings.Where(m => m.Action == MappingAction.SearchKey).ToList();
            var fillFields = data.Mappings.Where(m => m.Action is MappingAction.FillOverwrite or MappingAction.FillIfEmpty).ToList();
            var skipped = data.Mappings.Where(m => m.Action == MappingAction.DoNotImport).ToList();

            WorkbookSummary = new ImportWorkbookSummary
            {
                ImportModeDisplay = data.Config.ImportMode == ImportMode.UpdateDataCard ? "Update Data Card" : "Search Results Only",
                AddIfNotFound = data.Config.AddIfNotFound,
                WorkArea = string.IsNullOrWhiteSpace(data.Config.WorkAreaId) ? "(default)" : data.Config.WorkAreaId,
                DataRowCount = data.DataRows.Count,
                SearchKeyCount = searchKeys.Count,
                FillFieldCount = fillFields.Count,
                SkippedCount = skipped.Count,
                SearchKeyNames = string.Join(", ", searchKeys.Select(s => s.AdeptField)),
                FillFieldNames = string.Join(", ", fillFields.Select(f => f.AdeptField))
            };

            // Build mapping rows sorted: SearchKey → Fill → DoNotImport
            var firstRow = data.DataRows.FirstOrDefault();
            var mappingItems = data.Mappings
                .Select(m => new MappingRowItem
                {
                    ExcelColumn = m.ExcelColumn,
                    AdeptField = m.AdeptField,
                    ActionDisplay = MappingRowItem.FormatAction(m.Action),
                    OperatorDisplay = MappingRowItem.FormatOperator(m.Operator),
                    DateRangeColumn = m.DateRangeColumn,
                    FieldType = m.FieldType,
                    PreviewValue = firstRow?.GetStringValue(m.ExcelColumn),
                    Action = m.Action
                })
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.ExcelColumn);

            foreach (var item in mappingItems)
                MappingRows.Add(item);

            MappingFooter = $"{data.Mappings.Count} columns mapped ({searchKeys.Count} search keys, {fillFields.Count} fill, {skipped.Count} skipped)";

            // Apply dry-run default from config
            IsDryRun = data.Config.DryRun;

            IsWorkbookLoaded = true;

            // Auto-validate
            await ValidateInternalAsync();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load workbook: {ex.Message}";
        }
        finally
        {
            IsFileLoading = false;
        }
    }

    // --- Validate ---

    private bool CanValidate() => IsWorkbookLoaded && !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task ValidateAsync()
    {
        await ValidateInternalAsync();
    }

    private async Task ValidateInternalAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkbookPath)) return;

        IsValidating = true;
        IsValidated = false;
        ValidationStatusText = "Validating...";

        try
        {
            var service = _serviceFactory();
            var request = new ImportValidateRequest
            {
                ExcelPath = WorkbookPath,
                ConfigPath = string.IsNullOrWhiteSpace(ConfigPath) ? null : ConfigPath
            };

            var result = await service.ValidateAsync(request);

            if (result.IsValid)
            {
                IsValidated = true;
                if (result.Warnings.Count > 0)
                {
                    ValidationStatusText = $"\u26A0 {result.Warnings.Count} warnings \u2014 review before import";
                    ShowResults = true;
                    foreach (var w in result.Warnings)
                        ResultItems.Add(new ResultItem(ResultStatus.Skip, $"Warning: {w.Message}"));
                }
                else
                {
                    ValidationStatusText = "\u2713 Valid \u2014 ready to import";
                }
            }
            else
            {
                IsValidated = false;
                ValidationStatusText = $"\u2717 {result.Errors.Count} errors found";
                ShowResults = true;
                ResultItems.Clear();
                foreach (var e in result.Errors)
                    ResultItems.Add(new ResultItem(ResultStatus.Fail, e.Message));
            }
        }
        catch (Exception ex)
        {
            IsValidated = false;
            ValidationStatusText = $"\u2717 Validation failed: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    // --- Run Import ---

    private bool CanRunImport() => IsWorkbookLoaded && IsValidated && !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanRunImport))]
    private async Task RunImportAsync()
    {
        IsOperationRunning = true;
        ShowResults = true;
        ResultItems.Clear();
        ProgressPercent = 0;
        ProgressText = string.Empty;
        _cts = new CancellationTokenSource();

        try
        {
            var service = _serviceFactory();
            var request = new ImportRunRequest
            {
                ExcelPath = WorkbookPath,
                ConfigPath = string.IsNullOrWhiteSpace(ConfigPath) ? null : ConfigPath,
                DryRun = IsDryRun
            };

            var progress = new Progress<ImportProgress>(p =>
            {
                if (p.TotalRows > 0)
                    ProgressPercent = (double)p.RowNumber / p.TotalRows * 100;

                ProgressText = p.Phase switch
                {
                    ImportPhase.Parsing => "Parsing workbook...",
                    ImportPhase.Validating => "Validating mappings...",
                    ImportPhase.Processing => $"Processing row {p.RowNumber} of {p.TotalRows}: {p.CurrentPrimaryKey}...",
                    ImportPhase.Complete => "Complete",
                    _ => string.Empty
                };

                if (p.Phase == ImportPhase.Processing && p.Outcome is not null)
                {
                    var status = MapOutcomeToStatus(p.Outcome.Value);
                    var suffix = IsDryRun ? " (dry run)" : "";
                    var msg = $"Row {p.RowNumber}: {p.CurrentPrimaryKey} \u2014 {p.Message}{suffix}";
                    ResultItems.Add(new ResultItem(status, msg));
                }
            });

            var result = await service.RunAsync(request, progress, _cts.Token);

            // If results not streamed via progress, add them now
            if (ResultItems.Count == 0)
            {
                foreach (var r in result.Results)
                {
                    var status = MapOutcomeToStatus(r.Outcome);
                    var suffix = result.DryRun ? " (dry run)" : "";
                    var msg = $"Row {r.RowNumber}: {r.PrimaryKeyDisplay} \u2014 {r.Message}{suffix}";
                    ResultItems.Add(new ResultItem(status, msg));
                }
            }

            ProgressPercent = 100;

            // Summary
            var dryRunPrefix = result.DryRun ? "DRY RUN \u2014 " : "";
            ProgressText = $"{dryRunPrefix}Summary: {result.TotalRows} rows, {result.Updated} updated, {result.Created} created, {result.Skipped} skipped, {result.Failed} failed";
        }
        catch (OperationCanceledException)
        {
            ResultItems.Add(new ResultItem(ResultStatus.Skip, "Operation cancelled."));
            ProgressText = "Import cancelled.";
        }
        catch (Exception ex)
        {
            ResultItems.Add(new ResultItem(ResultStatus.Fail, $"Unexpected error: {ex.Message}"));
            ProgressText = "Import failed.";
        }
        finally
        {
            IsOperationRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // --- Cancellation ---

    private bool CanCancelOperation() => IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanCancelOperation))]
    private void CancelOperation()
    {
        _cts?.Cancel();
    }

    // --- Fetch Fields ---

    private bool CanFetchFields() => !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanFetchFields))]
    private async Task FetchFieldsAsync()
    {
        var savePath = _dialogService.ShowSaveFileDialog("CSV (*.csv)|*.csv|JSON (*.json)|*.json", "field-definitions");
        if (savePath is null) return;

        IsOperationRunning = true;
        try
        {
            var service = _serviceFactory();
            var fields = await service.FetchFieldsAsync();

            if (savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(fields, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(savePath, json);
            }
            else
            {
                var lines = new List<string> { "FieldName,DisplayName,SchemaId,FieldType,Width,System,Protected,Restricted" };
                foreach (var f in fields)
                    lines.Add($"{f.FieldName},{f.DisplayName},{f.SchemaId},{f.FieldType},{f.Width},{f.IsSystem},{f.IsProtected},{f.IsRestricted}");
                await System.IO.File.WriteAllLinesAsync(savePath, lines);
            }

            _dialogService.ShowMessage("Success", $"Field definitions saved to {savePath}");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage("Error", $"Failed to fetch fields: {ex.Message}");
        }
        finally
        {
            IsOperationRunning = false;
        }
    }

    // --- Results ---

    [RelayCommand]
    private void ClearResults()
    {
        ResultItems.Clear();
        ShowResults = false;
        ProgressPercent = 0;
        ProgressText = string.Empty;
    }

    [RelayCommand]
    private void CopyResults()
    {
        var lines = ResultItems.Select(r => $"{r.StatusPrefix} {r.Message}");
        var text = string.Join(Environment.NewLine, lines);
        System.Windows.Clipboard.SetText(text);
    }

    // --- Drag-and-drop ---

    public async Task HandleFileDropAsync(string filePath)
    {
        if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _dialogService.ShowMessage("Unsupported File", "Only Excel workbooks (.xlsx) are supported.");
            return;
        }

        WorkbookPath = filePath;
        await LoadWorkbookAsync(filePath);
    }

    // --- Helpers ---

    private static ResultStatus MapOutcomeToStatus(ImportOutcome outcome) => outcome switch
    {
        ImportOutcome.Updated => ResultStatus.Ok,
        ImportOutcome.Created => ResultStatus.Add,
        ImportOutcome.Skipped => ResultStatus.Skip,
        ImportOutcome.Failed => ResultStatus.Fail,
        _ => ResultStatus.Skip
    };
}
