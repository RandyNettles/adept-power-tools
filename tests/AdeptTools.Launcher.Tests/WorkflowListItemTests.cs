using AdeptTools.Launcher.ViewModels;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Launcher.Tests;

public class WorkflowListItemTests
{
    [Fact]
    public void MapsPropertiesFromAdminItem()
    {
        var adminItem = new WorkflowAdminItem
        {
            WorkflowId = "wf-123",
            WorkflowName = "Design Review",
            Active = true,
            StepCount = 4,
            InProcessCount = 7,
            Delete = true,
            Edit = true,
            Share = false,
            LockedByDisplayName = null
        };

        var item = new WorkflowListItem(adminItem);

        Assert.Equal("wf-123", item.WorkflowId);
        Assert.Equal("Design Review", item.Name);
        Assert.True(item.IsActive);
        Assert.Equal(4, item.StepCount);
        Assert.Equal(7, item.InProcessCount);
        Assert.True(item.CanDelete);
        Assert.True(item.CanEdit);
        Assert.Null(item.LockedBy);
        Assert.False(item.IsLocked);
    }

    [Fact]
    public void IsLocked_WhenLockedByIsNotNull()
    {
        var adminItem = new WorkflowAdminItem
        {
            WorkflowId = "wf-456",
            WorkflowName = "Locked WF",
            Active = true,
            StepCount = 2,
            InProcessCount = 0,
            Delete = true,
            Edit = true,
            Share = true,
            LockedByDisplayName = "Admin User"
        };

        var item = new WorkflowListItem(adminItem);

        Assert.True(item.IsLocked);
        Assert.Equal("Admin User", item.LockedBy);
    }

    [Fact]
    public void IsSelected_DefaultsFalse_CanToggle()
    {
        var adminItem = new WorkflowAdminItem
        {
            WorkflowId = "wf-789",
            WorkflowName = "Test",
            Active = true,
            StepCount = 1,
            InProcessCount = 0,
            Delete = true,
            Edit = true,
            Share = true
        };

        var item = new WorkflowListItem(adminItem);

        Assert.False(item.IsSelected);
        item.IsSelected = true;
        Assert.True(item.IsSelected);
    }
}
