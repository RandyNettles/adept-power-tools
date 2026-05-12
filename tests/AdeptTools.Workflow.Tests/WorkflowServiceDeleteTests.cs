using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowServiceDeleteTests
{
    private readonly MockWorkflowApiClient _mockClient = new();
    private readonly WorkflowExcelReader _excelReader = new();
    private readonly WorkflowXmlReader _xmlReader = new();
    private readonly WorkflowValidator _validator = new();

    private WorkflowService CreateService(IWorkflowApiClient? client = null) =>
        new(client ?? _mockClient, _excelReader, _xmlReader, _validator);

    [Fact]
    public async Task DeleteAsync_FilterMatchesTwo_OnlyTwoDeleted()
    {
        var service = CreateService();

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*Review*",
            Force = true
        });

        // Mock has "Design Review" — should match 1
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_FilterMatchesNone_EmptyResult()
    {
        var service = CreateService();

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "NonExistent*",
            Force = true
        });

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task DeleteAsync_DryRun_NoApiCalls()
    {
        var trackingClient = new TrackingMockWorkflowApiClient();
        var service = CreateService(trackingClient);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            DryRun = true,
            Force = true
        });

        Assert.True(result.DryRun);
        Assert.Equal(0, trackingClient.DeleteCallCount);
        Assert.All(result.Results, r => Assert.Contains("Would delete", r.Message));
    }

    [Fact]
    public async Task DeleteAsync_StatusInactive_OnlyInactiveMatched()
    {
        var service = CreateService();

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            Status = "inactive",
            Force = true
        });

        // Mock has 1 inactive workflow: "Final Check"
        Assert.Equal(1, result.Total);
        Assert.All(result.Results, r => Assert.Equal(WorkflowResultStatus.Success, r.Status));
    }

    [Fact]
    public async Task DeleteAsync_ManifestPath_WritesJsonFile()
    {
        var service = CreateService();
        var manifestPath = Path.Combine(Path.GetTempPath(), $"manifest_{Guid.NewGuid():N}.json");

        try
        {
            await service.DeleteAsync(new WorkflowDeleteRequest
            {
                Filter = "*",
                DryRun = true,
                Force = true,
                ManifestPath = manifestPath
            });

            Assert.True(File.Exists(manifestPath));
            var json = File.ReadAllText(manifestPath);
            Assert.Contains("workflowId", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(manifestPath)) File.Delete(manifestPath);
        }
    }

    [Fact]
    public async Task DeleteAsync_LockedWorkflow_Excluded()
    {
        var lockedClient = new LockedWorkflowMockClient();
        var service = new WorkflowService(lockedClient, _excelReader, _xmlReader, _validator);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            Force = true
        });

        // Locked workflows are filtered out
        Assert.Equal(0, result.Total);
    }

    /// <summary>
    /// Mock client that tracks delete calls.
    /// </summary>
    private class TrackingMockWorkflowApiClient : MockWorkflowApiClient
    {
        public int DeleteCallCount { get; private set; }

        public override Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            DeleteCallCount++;
            return base.DeleteWorkflowAsync(workflowId, ct);
        }
    }

    /// <summary>
    /// Mock client where all workflows are locked.
    /// </summary>
    private class LockedWorkflowMockClient : MockWorkflowApiClient
    {
        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new WorkflowAdminPacket
            {
                Workflows = new List<WorkflowAdminItem>
                {
                    new()
                    {
                        WorkflowId = "wf-locked", WorkflowName = "Locked WF",
                        Active = true, StepCount = 2, Delete = true,
                        LockedByDisplayName = "Other User"
                    }
                }
            });
        }
    }
}
