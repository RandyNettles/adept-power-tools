using CommunityToolkit.Mvvm.ComponentModel;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Launcher.ViewModels;

public partial class WorkflowListItem : ObservableObject
{
    public string WorkflowId { get; }
    public string Name { get; }
    public bool IsActive { get; }
    public int StepCount { get; }
    public int InProcessCount { get; }
    public string? LockedBy { get; }
    public bool CanDelete { get; }
    public bool CanEdit { get; }
    public bool IsLocked => !string.IsNullOrWhiteSpace(LockedBy);

    [ObservableProperty]
    private bool _isSelected;

    public WorkflowListItem(WorkflowAdminItem item)
    {
        WorkflowId = item.WorkflowId;
        Name = item.WorkflowName;
        IsActive = item.Active;
        StepCount = item.StepCount;
        InProcessCount = item.InProcessCount;
        LockedBy = string.IsNullOrWhiteSpace(item.LockedByDisplayName)
            ? null
            : item.LockedByDisplayName.Trim();
        CanDelete = item.Delete;
        CanEdit = item.Edit;
    }
}
