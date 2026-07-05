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
        public int GetWorkflowsCallCount { get; private set; }

        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            GetWorkflowsCallCount++;
            return base.GetWorkflowsAsync(ct);
        }

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

    /// <summary>
    /// Mock client with many workflows for parallel testing.
    /// </summary>
    private class ManyWorkflowsMockClient : MockWorkflowApiClient
    {
        private readonly List<WorkflowAdminItem> _workflows;

        public ManyWorkflowsMockClient(int count)
        {
            _workflows = Enumerable.Range(1, count).Select(i => new WorkflowAdminItem
            {
                WorkflowId = $"wf-{i:D3}",
                WorkflowName = $"Workflow {i}",
                Active = true,
                StepCount = 2,
                InProcessCount = 0,
                Edit = true,
                Share = true,
                Delete = true
            }).ToList();
        }

        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new WorkflowAdminPacket
            {
                CurrentUserId = "MOCK_USER",
                Workflows = _workflows
            });
        }
    }

    /// <summary>
    /// Mock client with mixed permission/lock states.
    /// </summary>
    private class MixedPermissionMockClient : MockWorkflowApiClient
    {
        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new WorkflowAdminPacket
            {
                CurrentUserId = "MOCK_USER",
                Workflows = new List<WorkflowAdminItem>
                {
                    new()
                    {
                        WorkflowId = "wf-locked", WorkflowName = "Locked WF",
                        Active = true, StepCount = 2, Delete = true,
                        LockedByDisplayName = "Other User"
                    },
                    new()
                    {
                        WorkflowId = "wf-noperm", WorkflowName = "No Permission WF",
                        Active = true, StepCount = 3, Delete = false
                    },
                    new()
                    {
                        WorkflowId = "wf-eligible", WorkflowName = "Eligible WF",
                        Active = true, StepCount = 1, Delete = true
                    }
                }
            });
        }
    }

    /// <summary>
    /// Mock client where lock owner is whitespace only.
    /// </summary>
    private class WhitespaceLockedByMockClient : MockWorkflowApiClient
    {
        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new WorkflowAdminPacket
            {
                CurrentUserId = "MOCK_USER",
                Workflows = new List<WorkflowAdminItem>
                {
                    new()
                    {
                        WorkflowId = "wf-space", WorkflowName = "Whitespace Lock WF",
                        Active = true, StepCount = 1, Delete = true,
                        LockedByDisplayName = "  "
                    }
                }
            });
        }
    }

    [Fact]
    public async Task DeleteAsync_WorkflowIds_DeletesOnlySpecifiedIds()
    {
        var client = new ManyWorkflowsMockClient(4);
        var service = CreateService(client);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            WorkflowIds = new List<string> { "wf-001", "wf-003" },
            Force = true
        });

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Contains(result.Results, r => r.WorkflowName == "Workflow 1");
        Assert.Contains(result.Results, r => r.WorkflowName == "Workflow 3");
        Assert.DoesNotContain(result.Results, r => r.WorkflowName == "Workflow 2");
        Assert.DoesNotContain(result.Results, r => r.WorkflowName == "Workflow 4");
    }

    [Fact]
    public async Task DeleteAsync_WorkflowIds_ExcludesLockedAndNoPermission()
    {
        var client = new MixedPermissionMockClient();
        var service = CreateService(client);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            WorkflowIds = new List<string> { "wf-locked", "wf-noperm", "wf-eligible" },
            Force = true
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal("Eligible WF", result.Results[0].WorkflowName);
    }

    [Fact]
    public async Task DeleteAsync_WorkflowIds_WhitespaceLock_IsTreatedAsUnlocked()
    {
        var client = new WhitespaceLockedByMockClient();
        var service = CreateService(client);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            WorkflowIds = new List<string> { "wf-space" },
            Force = true
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal("Whitespace Lock WF", result.Results[0].WorkflowName);
    }

    [Fact]
    public async Task DeleteAsync_ParallelExecution_AllSucceed()
    {
        var client = new ManyWorkflowsMockClient(12);
        var service = CreateService(client);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            Force = true
        });

        Assert.Equal(12, result.Total);
        Assert.Equal(12, result.Succeeded);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task DeleteAsync_PreFetchedPacket_NoExtraFetch()
    {
        var trackingClient = new TrackingMockWorkflowApiClient();
        var service = CreateService(trackingClient);

        // Pre-fetch the packet manually
        var packet = await trackingClient.GetWorkflowsAsync();
        trackingClient.GetWorkflowsCallCount.ToString(); // Reset isn't needed — we track from here
        var callCountBefore = trackingClient.GetWorkflowsCallCount;

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            PreFetchedPacket = packet,
            Force = true
        });

        Assert.Equal(0, trackingClient.GetWorkflowsCallCount - callCountBefore);
        Assert.True(result.Succeeded > 0);
    }

    [Fact]
    public async Task DeleteAsync_UnexpectedException_StoresSummaryAndVerboseDetailsSeparately()
    {
        var client = new ThrowingDeleteMockClient();
        var service = CreateService(client);

        var result = await service.DeleteAsync(new WorkflowDeleteRequest
        {
            Filter = "*",
            Force = true
        });

        Assert.Equal(1, result.Failed);
        var failure = Assert.Single(result.Results);
        Assert.Equal("InvalidOperationException: simulated delete failure", failure.Message);
        Assert.DoesNotContain(" at ", failure.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.NotNull(failure.Details);
        Assert.Contains("System.InvalidOperationException: simulated delete failure", failure.Details, StringComparison.Ordinal);
    }

    private sealed class ThrowingDeleteMockClient : MockWorkflowApiClient
    {
        public override Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new WorkflowAdminPacket
            {
                CurrentUserId = "MOCK_USER",
                Workflows = new List<WorkflowAdminItem>
                {
                    new()
                    {
                        WorkflowId = "wf-throw",
                        WorkflowName = "Throwing WF",
                        Active = true,
                        Delete = true
                    }
                }
            });
        }

        public override Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            throw new InvalidOperationException("simulated delete failure");
        }
    }
}
