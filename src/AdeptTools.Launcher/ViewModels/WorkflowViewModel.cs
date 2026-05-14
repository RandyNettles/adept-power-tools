using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Launcher.Services;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;

namespace AdeptTools.Launcher.ViewModels;

public partial class WorkflowViewModel : ObservableObject
{
    private readonly Func<IWorkflowApiClient> _apiClientFactory;
    private readonly Func<IWorkflowService> _serviceFactory;
    private readonly IDialogService _dialogService;
    private readonly MockModeState _mockModeState;
    private CancellationTokenSource? _cts;

    public ObservableCollection<WorkflowListItem> Workflows { get; } = new();
    public ObservableCollection<ResultItem> ResultItems { get; } = new();

    private ICollectionView? _workflowsView;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateFromExcelCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateFromXmlCommand))]
    [NotifyCanExecuteChangedFor(nameof(ModifyFromFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
    private bool _isOperationRunning;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private int _workflowCount;

    [ObservableProperty]
    private string _filterInfo = string.Empty;

    [ObservableProperty]
    private bool _isMockMode;

    public int SelectedCount => Workflows.Count(w => w.IsSelected);

    public WorkflowViewModel(
        Func<IWorkflowApiClient> apiClientFactory,
        Func<IWorkflowService> serviceFactory,
        IDialogService dialogService,
        MockModeState mockModeState)
    {
        _apiClientFactory = apiClientFactory;
        _serviceFactory = serviceFactory;
        _dialogService = dialogService;
        _mockModeState = mockModeState;
        _isMockMode = mockModeState.IsMock;

        mockModeState.Changed += (_, isMock) => IsMockMode = isMock;
    }

    public void OnNavigatedTo()
    {
        SetupCollectionView();
        _ = RefreshAsync();
    }

    private void SetupCollectionView()
    {
        _workflowsView = CollectionViewSource.GetDefaultView(Workflows);
        _workflowsView.Filter = FilterWorkflow;
    }

    private bool FilterWorkflow(object obj)
    {
        if (obj is not WorkflowListItem item) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;

        var pattern = FilterText.Trim();
        if (pattern.Contains('*'))
        {
            // Simple glob: replace * with regex equivalent
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(item.Name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return item.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnFilterTextChanged(string value)
    {
        _workflowsView?.Refresh();
        UpdateFilterInfo();
    }

    private void UpdateFilterInfo()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            FilterInfo = $"{WorkflowCount} workflows shown";
        }
        else
        {
            var visibleCount = _workflowsView?.Cast<object>().Count() ?? 0;
            FilterInfo = $"{visibleCount} of {WorkflowCount} workflows shown (filter: \"{FilterText}\")";
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var client = _apiClientFactory();
            var packet = await client.GetWorkflowsAsync();

            Workflows.Clear();
            foreach (var item in packet.Workflows)
            {
                var listItem = new WorkflowListItem(item);
                listItem.PropertyChanged += WorkflowItem_PropertyChanged;
                Workflows.Add(listItem);
            }

            WorkflowCount = Workflows.Count;
            UpdateFilterInfo();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage("Error", $"Failed to load workflows: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void WorkflowItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowListItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    // --- Create ---

    private bool CanRunOperation() => !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task CreateFromExcelAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("Excel Workbooks (*.xlsx)|*.xlsx");
        if (path is null) return;
        await RunCreateAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task CreateFromXmlAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("XML Files (*.xml)|*.xml");
        if (path is null) return;
        await RunCreateAsync(path);
    }

    private async Task RunCreateAsync(string filePath)
    {
        await RunOperationAsync(async (service, progress, ct) =>
        {
            var request = new WorkflowCreateRequest { InputFilePath = filePath, DryRun = IsDryRun };
            return await service.CreateAsync(request, progress, ct);
        }, "Creating");
    }

    // --- Modify ---

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task ModifyFromFileAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("Excel Workbooks (*.xlsx)|*.xlsx|XML Files (*.xml)|*.xml");
        if (path is null) return;

        await RunOperationAsync(async (service, progress, ct) =>
        {
            var request = new WorkflowModifyRequest { InputFilePath = path, DryRun = IsDryRun };
            return await service.ModifyAsync(request, progress, ct);
        }, "Modifying");
    }

    // --- Delete ---

    private bool CanDeleteSelected() => SelectedCount > 0 && !IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    public async Task DeleteSelectedAsync()
    {
        try
        {
            await DeleteSelectedCoreAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Delete failed:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "Delete Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task DeleteSelectedCoreAsync()
    {
        var selected = Workflows.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0) return;

        var dialogVm = new ConfirmDeleteDialogViewModel(selected, IsDryRun);

        // Show the confirmation dialog
        var dialog = new Controls.ConfirmDeleteDialog { DataContext = dialogVm };
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();

        if (dialogVm.DialogResult != true) return;

        var deletableIds = dialogVm.DeletableWorkflows.Select(w => w.WorkflowId).ToList();
        var isDryRun = dialogVm.IsDryRun;

        // Show skip results immediately
        foreach (var skipped in dialogVm.SkippedWorkflows)
        {
            ResultItems.Add(new ResultItem(ResultStatus.Skip, $"{skipped.Name} \u2014 {skipped.SkipReason}"));
        }

        if (deletableIds.Count == 0)
        {
            ShowResults = true;
            ResultItems.Add(new ResultItem(ResultStatus.Skip, "No workflows eligible for deletion."));
            return;
        }

        // For dry run, report results locally without calling the server
        if (isDryRun)
        {
            ShowResults = true;
            foreach (var wf in dialogVm.DeletableWorkflows)
            {
                ResultItems.Add(new ResultItem(ResultStatus.Ok,
                    $"DRY RUN — Would delete: {wf.Name} ({wf.InProcessCount} in-process)"));
            }
            ResultItems.Add(new ResultItem(ResultStatus.Ok,
                $"DRY RUN — Summary: {dialogVm.DeletableWorkflows.Count} workflow(s) would be deleted."));
            return;
        }

        await RunOperationAsync(async (service, progress, ct) =>
        {
            var request = new WorkflowDeleteRequest
            {
                Filter = string.Join("|", deletableIds),
                DryRun = false
            };
            return await service.DeleteAsync(request, progress, ct);
        }, "Deleting");
    }

    // --- Cancellation ---

    private bool CanCancelOperation() => IsOperationRunning;

    [RelayCommand(CanExecute = nameof(CanCancelOperation))]
    private void CancelOperation()
    {
        _cts?.Cancel();
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

    // --- Shared operation runner ---

    private async Task RunOperationAsync(
        Func<IWorkflowService, IProgress<WorkflowProgress>, CancellationToken, Task<WorkflowBatchResult>> operation,
        string operationVerb)
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
            var progress = new Progress<WorkflowProgress>(p =>
            {
                if (p.TotalCount > 0)
                    ProgressPercent = (double)p.CurrentIndex / p.TotalCount * 100;

                ProgressText = $"{operationVerb} workflow {p.CurrentIndex} of {p.TotalCount}: {p.WorkflowName}";

                if (p.Status != WorkflowResultStatus.Success || p.Message is not null)
                {
                    var status = p.Status switch
                    {
                        WorkflowResultStatus.Success => ResultStatus.Ok,
                        WorkflowResultStatus.Fail => ResultStatus.Fail,
                        WorkflowResultStatus.Skip => ResultStatus.Skip,
                        _ => ResultStatus.Ok
                    };
                    var msg = p.Message ?? p.WorkflowName;
                    ResultItems.Add(new ResultItem(status, msg));
                }
            });

            var result = await operation(service, progress, _cts.Token);

            // Add any results not already reported via progress
            foreach (var r in result.Results)
            {
                var status = r.Status switch
                {
                    WorkflowResultStatus.Success => ResultStatus.Ok,
                    WorkflowResultStatus.Fail => ResultStatus.Fail,
                    WorkflowResultStatus.Skip => ResultStatus.Skip,
                    _ => ResultStatus.Ok
                };
                var message = r.Message ?? r.WorkflowName;
                ResultItems.Add(new ResultItem(status, message));
            }

            ProgressPercent = 100;

            // Summary
            var dryRunPrefix = result.DryRun ? "DRY RUN \u2014 " : "";
            var summary = $"{dryRunPrefix}Summary: {result.Total} total, {result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped";
            ProgressText = summary;
        }
        catch (OperationCanceledException)
        {
            ResultItems.Add(new ResultItem(ResultStatus.Skip, "Operation cancelled."));
            ProgressText = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            ResultItems.Add(new ResultItem(ResultStatus.Fail, $"Unexpected error: {ex.Message}"));
            ProgressText = "Operation failed.";
        }
        finally
        {
            IsOperationRunning = false;
            _cts?.Dispose();
            _cts = null;

            // Refresh the grid after mutating operations
            await RefreshAsync();
        }
    }
}
