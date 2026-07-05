using AdeptTools.Core.Models;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Api;

public class MockWorkflowApiClient : IWorkflowApiClient
{
    private readonly Dictionary<string, WorkflowEditModel> _savedWorkflows = new(StringComparer.OrdinalIgnoreCase);

    public virtual WorkflowApiCapabilities Capabilities => WorkflowApiCapabilities.Full;

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
        if (_savedWorkflows.TryGetValue(workflowId, out var saved))
        {
            return Task.FromResult(saved);
        }

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
        if (!string.IsNullOrWhiteSpace(model.WorkflowDefinition.WorkflowId))
        {
            _savedWorkflows[model.WorkflowDefinition.WorkflowId] = model;
        }

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

        // Replicate COM behavior: returns a fresh model re-read from the server.
        // Only structural data (step IDs, order, names) is preserved — in-memory
        // changes to trustees and workflow-level properties are discarded.
        var updatedModel = new WorkflowEditModel
        {
            BEditable = model.BEditable,
            WorkflowDefinition = new WorkflowDefinition
            {
                WorkflowId = wfId,
                Name = model.WorkflowDefinition.Name
            },
            WorkflowStepModels = model.WorkflowStepModels.Select(s => new WorkflowStepModel
            {
                WorkflowStepDefinition = new WorkflowStepDefinition
                {
                    WorkflowId = wfId,
                    StepId = s.WorkflowStepDefinition.StepId,
                    Order = s.WorkflowStepDefinition.Order,
                    Name = s.WorkflowStepDefinition.Name
                },
                WorkflowTrusteeDefinitions = new List<WorkflowTrusteeDefinition>()
            }).ToList()
        };

        var newOrder = updatedModel.WorkflowStepModels.Count + 1;
        var newStep = new WorkflowStepModel
        {
            WorkflowStepDefinition = new WorkflowStepDefinition
            {
                WorkflowId = wfId,
                StepId = newStepId,
                Order = newOrder,
                Name = $"Step {newOrder}"
            },
            WorkflowTrusteeDefinitions = new List<WorkflowTrusteeDefinition>()
        };

        updatedModel.WorkflowStepModels.Add(newStep);
        updatedModel.EAddStep = newStep;
        updatedModel.EStepId = newStepId;

        return Task.FromResult(updatedModel);
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

    public virtual Task<ApiResult> SetWorkflowSharedAsync(string workflowId, bool shared, CancellationToken ct = default)
    {
        return Task.FromResult(ApiResult.Success("Workflow share state updated (mock)."));
    }

    public virtual Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<WorkflowCommonTarget>
        {
            new() { Key = "ALL_USERS", DisplayName = "All Users" },
            new() { Key = "ENGINEERING", DisplayName = "Engineering Team" }
        });
    }

    public virtual Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<AdeptUserEntry>
        {
            new() { UserId = "jsmith", DisplayName = "Smith, John" },
            new() { UserId = "mjones", DisplayName = "Jones, Mary" },
            new() { UserId = "akhan", DisplayName = "Khan, Amir" },
            new() { UserId = "cchiboroski", DisplayName = "Chiboroski, Craig" },
            new() { UserId = "rbabu", DisplayName = "Rameshbabu, Vijay" },
            new() { UserId = "bwilson", DisplayName = "Wilson, Bob" },
            new() { UserId = "user1", DisplayName = "User, Test One" },
            new() { UserId = "reviewer1", DisplayName = "Reviewer, First" },
            new() { UserId = "notifyUser", DisplayName = "Notify, User" }
        });
    }

    public virtual async Task<AdeptUserEntry?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var users = await GetUsersAsync(ct);
        return users.FirstOrDefault(u =>
            string.Equals(u.UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public virtual Task<List<AdeptGroupEntry>> GetGroupsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<AdeptGroupEntry>
        {
            new() { GroupId = "engineering", Name = "Engineering" },
            new() { GroupId = "eng-managers", Name = "Engineering Manager" },
            new() { GroupId = "alertGroup", Name = "Alert Group" },
            new() { GroupId = "Designers", Name = "Designers" },
            new() { GroupId = "Field Engineers", Name = "Field Engineers" }
        });
    }
}
