using AdeptTools.Core.Models;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Api;

public class MockWorkflowApiClient : IWorkflowApiClient
{
    public virtual Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new WorkflowSetup
        {
            MaximumLengthWorkflowName = 128,
            MaximumLengthStepName = 128,
            MaximumWorkflowSteps = 50,
            MaximumWorkflows = 100
        });
    }

    public virtual Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new WorkflowAdminPacket
        {
            CurrentUserId = "MOCK_USER",
            WfNameLen = 128,
            WfStepNameLen = 128,
            Workflows = new List<WorkflowAdminItem>
            {
                new()
                {
                    WorkflowId = "wf-001", WorkflowName = "Design Review",
                    Active = true, StepCount = 3, InProcessCount = 5,
                    Edit = true, Share = true, Delete = true
                },
                new()
                {
                    WorkflowId = "wf-002", WorkflowName = "Piping Approval",
                    Active = true, StepCount = 4, InProcessCount = 12,
                    Edit = true, Share = true, Delete = true
                },
                new()
                {
                    WorkflowId = "wf-003", WorkflowName = "Final Check",
                    Active = false, StepCount = 2, InProcessCount = 0,
                    Edit = true, Share = true, Delete = true
                }
            }
        });
    }

    public virtual Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        return GetWorkflowsAsync(ct);
    }

    public virtual Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        var wfId = Guid.NewGuid().ToString("N")[..8];
        var stepId = Guid.NewGuid().ToString("N")[..8];

        return Task.FromResult(new WorkflowEditModel
        {
            BEditable = true,
            WorkflowDefinition = new WorkflowDefinition
            {
                WorkflowId = wfId,
                Name = string.Empty
            },
            WorkflowStepModels = new List<WorkflowStepModel>
            {
                new()
                {
                    WorkflowStepDefinition = new WorkflowStepDefinition
                    {
                        WorkflowId = wfId,
                        StepId = stepId,
                        Order = 1,
                        Name = "Step 1"
                    }
                }
            }
        });
    }

    public virtual Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        return Task.FromResult(new WorkflowEditModel
        {
            BEditable = true,
            WorkflowDefinition = new WorkflowDefinition
            {
                WorkflowId = workflowId,
                Name = "Mock Workflow"
            },
            WorkflowStepModels = new List<WorkflowStepModel>
            {
                new()
                {
                    WorkflowStepDefinition = new WorkflowStepDefinition
                    {
                        WorkflowId = workflowId,
                        StepId = "step-001",
                        Order = 1,
                        Name = "Step 1"
                    }
                }
            }
        });
    }

    public virtual Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        return Task.FromResult(ApiResult.Success("Workflow saved (mock)."));
    }

    public virtual Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        return Task.FromResult(ApiResult.Success("Workflow deleted (mock)."));
    }

    public virtual Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        var newStepId = Guid.NewGuid().ToString("N")[..8];
        var wfId = model.WorkflowDefinition.WorkflowId;
        var newOrder = model.WorkflowStepModels.Count + 1;

        var newStep = new WorkflowStepModel
        {
            WorkflowStepDefinition = new WorkflowStepDefinition
            {
                WorkflowId = wfId,
                StepId = newStepId,
                Order = newOrder,
                Name = $"Step {newOrder}"
            }
        };

        model.WorkflowStepModels.Add(newStep);
        model.EAddStep = newStep;
        model.EStepId = newStepId;

        return Task.FromResult(model);
    }

    public virtual Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        return Task.FromResult(new WorkflowEditModel
        {
            BEditable = true,
            WorkflowDefinition = new WorkflowDefinition { WorkflowId = workflowId, Name = "Mock Workflow" },
            WorkflowStepModels = new List<WorkflowStepModel>
            {
                new()
                {
                    WorkflowStepDefinition = new WorkflowStepDefinition
                    {
                        WorkflowId = workflowId, StepId = "step-001", Order = 1, Name = "Step 1"
                    }
                }
            }
        });
    }

    public virtual Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        return Task.FromResult(ApiResult.Success());
    }

    public virtual Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<WorkflowCommonTarget>
        {
            new() { Key = "ALL_USERS", DisplayName = "All Users" },
            new() { Key = "ENGINEERING", DisplayName = "Engineering Team" }
        });
    }
}
