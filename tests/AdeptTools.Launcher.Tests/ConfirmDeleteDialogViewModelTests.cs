using AdeptTools.Launcher.ViewModels;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Launcher.Tests;

public class ConfirmDeleteDialogViewModelTests
{
    private static WorkflowListItem CreateItem(string id, string name, bool active = true, int inProcess = 0,
        bool canDelete = true, string? lockedBy = null)
    {
        return new WorkflowListItem(new WorkflowAdminItem
        {
            WorkflowId = id,
            WorkflowName = name,
            Active = active,
            StepCount = 3,
            InProcessCount = inProcess,
            Delete = canDelete,
            Edit = true,
            Share = true,
            LockedByDisplayName = lockedBy
        });
    }

    [Fact]
    public void TwoDelectable_OneLocked_SplitsCorrectly()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "Piping Review", inProcess: 12),
            CreateItem("wf-2", "Structural Review", inProcess: 8),
            CreateItem("wf-3", "Civil Approval", lockedBy: "J. Smith")
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.Equal(2, vm.DeletableWorkflows.Count);
        Assert.Single(vm.SkippedWorkflows);
        Assert.True(vm.CanConfirm);
        Assert.Equal("Delete 2 Workflows", vm.DeleteButtonText);
    }

    [Fact]
    public void AllLocked_CannotConfirm()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "Workflow A", lockedBy: "User1"),
            CreateItem("wf-2", "Workflow B", lockedBy: "User2")
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.Empty(vm.DeletableWorkflows);
        Assert.Equal(2, vm.SkippedWorkflows.Count);
        Assert.False(vm.CanConfirm);
        Assert.Equal("Delete 0 Workflows", vm.DeleteButtonText);
    }

    [Fact]
    public void NoPermission_MarkedAsSkipped()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "Workflow A", canDelete: false),
            CreateItem("wf-2", "Workflow B", canDelete: true)
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.Single(vm.DeletableWorkflows);
        Assert.Single(vm.SkippedWorkflows);
        Assert.Contains("No permission", vm.SkippedWorkflows[0].StatusText);
    }

    [Fact]
    public void InProcessWarning_ShownWhenDeletableHasInProcess()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "WF1", inProcess: 5),
            CreateItem("wf-2", "WF2", inProcess: 3)
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.True(vm.ShowInProcessWarning);
        Assert.Equal(8, vm.TotalInProcessCount);
    }

    [Fact]
    public void InProcessWarning_HiddenWhenAllZero()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "WF1", inProcess: 0),
            CreateItem("wf-2", "WF2", inProcess: 0)
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.False(vm.ShowInProcessWarning);
        Assert.Equal(0, vm.TotalInProcessCount);
    }

    [Fact]
    public void SkipInfo_HiddenWhenNoneSkipped()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "WF1"),
            CreateItem("wf-2", "WF2")
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.False(vm.ShowSkipInfo);
    }

    [Fact]
    public void DryRun_PreservedFromInput()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "WF1")
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: true);

        Assert.True(vm.IsDryRun);
    }

    [Fact]
    public void SingleWorkflow_UsesSingularTitle()
    {
        var items = new List<WorkflowListItem>
        {
            CreateItem("wf-1", "WF1")
        };

        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        Assert.Equal("Delete Workflow", vm.TitleText);
        Assert.Equal("Delete 1 Workflow", vm.DeleteButtonText);
    }

    [Fact]
    public void Confirm_SetsDialogResult()
    {
        var items = new List<WorkflowListItem> { CreateItem("wf-1", "WF1") };
        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.DialogResult);
    }

    [Fact]
    public void Cancel_SetsDialogResultFalse()
    {
        var items = new List<WorkflowListItem> { CreateItem("wf-1", "WF1") };
        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);

        vm.CancelCommand.Execute(null);

        Assert.False(vm.DialogResult);
    }

    [Fact]
    public void Cancel_RaisesPropertyChangedForDialogResult()
    {
        var items = new List<WorkflowListItem> { CreateItem("wf-1", "WF1") };
        var vm = new ConfirmDeleteDialogViewModel(items, isDryRun: false);
        var raised = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfirmDeleteDialogViewModel.DialogResult))
                raised = true;
        };

        vm.CancelCommand.Execute(null);

        Assert.True(raised);
    }
}
