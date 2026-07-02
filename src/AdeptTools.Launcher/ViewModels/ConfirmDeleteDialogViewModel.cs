using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdeptTools.Launcher.ViewModels;

public partial class ConfirmDeleteDialogViewModel : ObservableObject
{
    public ObservableCollection<DeleteDialogItem> AllWorkflows { get; }
    public IReadOnlyList<DeleteDialogItem> DeletableWorkflows { get; }
    public IReadOnlyList<DeleteDialogItem> SkippedWorkflows { get; }

    public int TotalInProcessCount { get; }
    public bool ShowInProcessWarning => TotalInProcessCount > 0;
    public bool ShowSkipInfo => SkippedWorkflows.Count > 0;

    public string DeleteButtonText => DeletableWorkflows.Count == 1
        ? "Delete 1 Workflow"
        : $"Delete {DeletableWorkflows.Count} Workflows";

    public bool CanConfirm => DeletableWorkflows.Count > 0;

    public string TitleText => DeletableWorkflows.Count == 1
        ? "Delete Workflow"
        : "Delete Workflows";

    [ObservableProperty]
    private bool _isDryRun;

    private bool? _dialogResult;
    public bool? DialogResult
    {
        get => _dialogResult;
        private set => SetProperty(ref _dialogResult, value);
    }

    public ConfirmDeleteDialogViewModel(IReadOnlyList<WorkflowListItem> selectedWorkflows, bool isDryRun)
    {
        _isDryRun = isDryRun;

        var allItems = selectedWorkflows.Select(w => new DeleteDialogItem(w)).ToList();
        AllWorkflows = new ObservableCollection<DeleteDialogItem>(allItems);
        DeletableWorkflows = allItems.Where(w => w.CanDelete).ToList();
        SkippedWorkflows = allItems.Where(w => !w.CanDelete).ToList();
        TotalInProcessCount = DeletableWorkflows.Sum(w => w.InProcessCount);
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}

public class DeleteDialogItem
{
    public string Name { get; }
    public bool IsActive { get; }
    public int InProcessCount { get; }
    public string WorkflowId { get; }
    public bool CanDelete { get; }
    public string StatusText { get; }
    public string? SkipReason { get; }

    public DeleteDialogItem(WorkflowListItem item)
    {
        Name = item.Name;
        IsActive = item.IsActive;
        InProcessCount = item.InProcessCount;
        WorkflowId = item.WorkflowId;

        if (item.IsLocked)
        {
            CanDelete = false;
            StatusText = "\u2298 Locked";
            SkipReason = $"locked by {item.LockedBy}";
        }
        else if (!item.CanDelete)
        {
            CanDelete = false;
            StatusText = "\u2298 No permission";
            SkipReason = "no delete permission";
        }
        else
        {
            CanDelete = true;
            StatusText = "Will delete";
            SkipReason = null;
        }
    }
}
