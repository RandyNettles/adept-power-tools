using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Backend.Com.Interop;
using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Backend.Com.Api;

/// <summary>
/// COM-based implementation of IWorkflowApiClient.
/// Uses the NxWorkflowAdmin COM object for workflow CRUD operations.
/// </summary>
public class ComWorkflowApiClient : IWorkflowApiClient
{
    private readonly ComOperationRunner _runner;
    private readonly ComSessionManager _session;

    public ComWorkflowApiClient(ComOperationRunner runner, ComSessionManager session)
    {
        _runner = runner;
        _session = session;
    }

    public async Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() => new WorkflowSetup
        {
            MaximumLengthWorkflowName = admin.MaxWorkflowNameLength,
            MaximumLengthStepName = admin.MaxStepNameLength,
            MaximumWorkflowSteps = admin.MaxWorkflowSteps,
            MaximumWorkflows = admin.MaxWorkflows
        }, ct);
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);
        var project = _session.GetProject();

        return await _runner.RunAsync(() =>
        {
            var packet = new WorkflowAdminPacket
            {
                CurrentUserId = project.UserId,
                WfNameLen = admin.MaxWorkflowNameLength,
                WfStepNameLen = admin.MaxStepNameLength,
                Workflows = new List<WorkflowAdminItem>()
            };

            for (var i = 0; i < admin.WorkflowCount; i++)
            {
                var info = admin.GetWorkflowInfo(i);
                try
                {
                    packet.Workflows.Add(new WorkflowAdminItem
                    {
                        WorkflowId = info.WorkflowId,
                        WorkflowName = info.Name,
                        Active = info.Active,
                        StepCount = info.StepCount,
                        InProcessCount = info.InProcessCount,
                        Edit = info.CanEdit,
                        Share = info.CanShare,
                        Delete = info.CanDelete,
                        LockedByDisplayName = info.LockedByDisplayName
                    });
                }
                finally
                {
                    ComLifecycle.Release(ref info);
                }
            }

            return packet;
        }, ct);
    }

    public Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        // COM SDK doesn't have a separate "basic" endpoint — return full list
        return GetWorkflowsAsync(ct);
    }

    public async Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var nxWf = admin.CreateNewWorkflow();
            try
            {
                return MapWorkflowToEditModel(nxWf);
            }
            finally
            {
                ComLifecycle.Release(ref nxWf);
            }
        }, ct);
    }

    public async Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var nxWf = admin.OpenWorkflow(workflowId);
            try
            {
                return MapWorkflowToEditModel(nxWf);
            }
            finally
            {
                ComLifecycle.Release(ref nxWf);
            }
        }, ct);
    }

    public async Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var nxWf = admin.OpenWorkflow(model.WorkflowDefinition.WorkflowId);
            try
            {
                // Apply workflow-level properties
                nxWf.Name = model.WorkflowDefinition.Name;
                nxWf.Memo = model.WorkflowDefinition.Memo ?? string.Empty;
                nxWf.Active = true; // Controlled at definition level
                nxWf.DoEmailNotify = model.WorkflowDefinition.BDoEmailNotify;
                nxWf.TimeoutOn = model.WorkflowDefinition.BTimeoutOn;
                nxWf.RecurringTimeoutOn = model.WorkflowDefinition.BRecurringTimeoutOn;
                nxWf.Timeout = model.WorkflowDefinition.Timeout ?? string.Empty;
                nxWf.RecurringTimeout = model.WorkflowDefinition.RecurringTimeout ?? string.Empty;
                nxWf.TimeoutIncludeSaturday = model.WorkflowDefinition.BTimeoutIncludeSaturday;
                nxWf.TimeoutIncludeSunday = model.WorkflowDefinition.BTimeoutIncludeSunday;

                // Apply step-level properties
                for (var i = 0; i < model.WorkflowStepModels.Count && i < nxWf.StepCount; i++)
                {
                    var stepModel = model.WorkflowStepModels[i];
                    if (stepModel.BDeleted) continue;

                    var nxStep = nxWf.GetStep(i);
                    try
                    {
                        ApplyStepProperties(nxStep, stepModel);
                    }
                    finally
                    {
                        ComLifecycle.Release(ref nxStep);
                    }
                }

                var saveResult = nxWf.Save();
                return saveResult == 0
                    ? ApiResult.Success("Workflow saved via COM.")
                    : ApiResult.Failure(saveResult, $"COM Save failed with code {saveResult}.");
            }
            finally
            {
                ComLifecycle.Release(ref nxWf);
            }
        }, ct);
    }

    public async Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var result = admin.DeleteWorkflow(workflowId);
            return result == 0
                ? ApiResult.Success("Workflow deleted via COM.")
                : ApiResult.Failure(result, $"COM Delete failed with code {result}.");
        }, ct);
    }

    public async Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var nxWf = admin.OpenWorkflow(model.WorkflowDefinition.WorkflowId);
            try
            {
                var nxStep = nxWf.AddStep(position);
                var newStepId = nxStep.StepId;
                ComLifecycle.Release(ref nxStep);

                // Re-read the full model after mutation
                var updatedModel = MapWorkflowToEditModel(nxWf);
                updatedModel.EStepId = newStepId;
                return updatedModel;
            }
            finally
            {
                ComLifecycle.Release(ref nxWf);
            }
        }, ct);
    }

    public async Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var nxWf = admin.TagWorkflow(workflowId);
            try
            {
                return MapWorkflowToEditModel(nxWf);
            }
            finally
            {
                ComLifecycle.Release(ref nxWf);
            }
        }, ct);
    }

    public async Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var result = admin.UntagWorkflow(workflowId);
            return result == 0
                ? ApiResult.Success()
                : ApiResult.Failure(result, $"COM Untag failed with code {result}.");
        }, ct);
    }

    public async Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        var admin = await _session.GetWorkflowAdminAsync(ct);

        return await _runner.RunAsync(() =>
        {
            var metagroups = new List<WorkflowCommonTarget>();

            for (var i = 0; i < admin.MetagroupCount; i++)
            {
                var mg = admin.GetMetagroup(i);
                try
                {
                    metagroups.Add(new WorkflowCommonTarget
                    {
                        Key = mg.Key,
                        DisplayName = mg.DisplayName
                    });
                }
                finally
                {
                    ComLifecycle.Release(ref mg);
                }
            }

            return metagroups;
        }, ct);
    }

    private static WorkflowEditModel MapWorkflowToEditModel(INxWorkflow nxWf)
    {
        var model = new WorkflowEditModel
        {
            BEditable = nxWf.IsEditable,
            AlreadyTaggedByName = nxWf.AlreadyTaggedByName,
            WorkflowDefinition = new WorkflowDefinition
            {
                WorkflowId = nxWf.WorkflowId,
                Name = nxWf.Name,
                Memo = nxWf.Memo,
                BDoEmailNotify = nxWf.DoEmailNotify,
                BTimeoutOn = nxWf.TimeoutOn,
                BRecurringTimeoutOn = nxWf.RecurringTimeoutOn,
                Timeout = nxWf.Timeout,
                RecurringTimeout = nxWf.RecurringTimeout,
                BTimeoutIncludeSaturday = nxWf.TimeoutIncludeSaturday,
                BTimeoutIncludeSunday = nxWf.TimeoutIncludeSunday
            },
            WorkflowStepModels = new List<WorkflowStepModel>()
        };

        for (var i = 0; i < nxWf.StepCount; i++)
        {
            var nxStep = nxWf.GetStep(i);
            try
            {
                var stepModel = new WorkflowStepModel
                {
                    WorkflowStepDefinition = new WorkflowStepDefinition
                    {
                        WorkflowId = nxWf.WorkflowId,
                        StepId = nxStep.StepId,
                        Order = nxStep.Order,
                        Name = nxStep.Name,
                        BCanChangeWorkflow = nxStep.CanChangeWorkflow,
                        BCanSignOut = nxStep.CanSignOut,
                        BCanExpediteReject = nxStep.CanExpediteReject,
                        BCanExpediteApprove = nxStep.CanExpediteApprove,
                        BDoEmailNotify = nxStep.DoEmailNotify,
                        BRequireComment = nxStep.RequireComment,
                        BOnlyAllowAssignToApprovers = nxStep.OnlyAllowAssignToApprovers,
                        RequiredApprovalsCount = nxStep.RequiredApprovalsCount,
                        AutoAdvance = nxStep.AutoAdvance,
                        BTimeoutOn = nxStep.TimeoutOn,
                        BRecurringTimeoutOn = nxStep.RecurringTimeoutOn,
                        Timeout = nxStep.Timeout,
                        RecurringTimeout = nxStep.RecurringTimeout,
                        BTimeoutIncludeSaturday = nxStep.TimeoutIncludeSaturday,
                        BTimeoutIncludeSunday = nxStep.TimeoutIncludeSunday
                    },
                    InProcessCount = nxStep.InProcessCount,
                    WorkflowTrusteeDefinitions = new List<WorkflowTrusteeDefinition>()
                };

                for (var j = 0; j < nxStep.TrusteeCount; j++)
                {
                    var nxTrustee = nxStep.GetTrustee(j);
                    try
                    {
                        stepModel.WorkflowTrusteeDefinitions.Add(new WorkflowTrusteeDefinition
                        {
                            WorkflowId = nxWf.WorkflowId,
                            StepId = nxStep.StepId,
                            TrusteeId = nxTrustee.TrusteeId,
                            Type = MapTrusteeType(nxTrustee.Type)
                        });
                    }
                    finally
                    {
                        ComLifecycle.Release(ref nxTrustee);
                    }
                }

                model.WorkflowStepModels.Add(stepModel);
            }
            finally
            {
                ComLifecycle.Release(ref nxStep);
            }
        }

        return model;
    }

    private static void ApplyStepProperties(INxWorkflowStep nxStep, WorkflowStepModel stepModel)
    {
        var def = stepModel.WorkflowStepDefinition;
        nxStep.Name = def.Name;
        nxStep.CanChangeWorkflow = def.BCanChangeWorkflow;
        nxStep.CanSignOut = def.BCanSignOut;
        nxStep.CanExpediteReject = def.BCanExpediteReject;
        nxStep.CanExpediteApprove = def.BCanExpediteApprove;
        nxStep.DoEmailNotify = def.BDoEmailNotify;
        nxStep.RequireComment = def.BRequireComment;
        nxStep.OnlyAllowAssignToApprovers = def.BOnlyAllowAssignToApprovers;
        nxStep.RequiredApprovalsCount = def.RequiredApprovalsCount;
        nxStep.AutoAdvance = def.AutoAdvance;
        nxStep.TimeoutOn = def.BTimeoutOn;
        nxStep.RecurringTimeoutOn = def.BRecurringTimeoutOn;
        nxStep.Timeout = def.Timeout ?? string.Empty;
        nxStep.RecurringTimeout = def.RecurringTimeout ?? string.Empty;
        nxStep.TimeoutIncludeSaturday = def.BTimeoutIncludeSaturday;
        nxStep.TimeoutIncludeSunday = def.BTimeoutIncludeSunday;

        // Update trustees
        nxStep.ClearTrustees();
        foreach (var trustee in stepModel.WorkflowTrusteeDefinitions)
        {
            nxStep.AddTrustee(trustee.TrusteeId, MapTrusteeTypeToString(trustee.Type));
        }
    }

    private static WorkflowUserType MapTrusteeType(string comType)
    {
        return comType?.ToUpperInvariant() switch
        {
            "U" => WorkflowUserType.User,
            "G" => WorkflowUserType.Group,
            "K" => WorkflowUserType.Key,
            "E" => WorkflowUserType.Email,
            "A" => WorkflowUserType.Approvers,
            _ => WorkflowUserType.User
        };
    }

    private static string MapTrusteeTypeToString(WorkflowUserType type)
    {
        return type switch
        {
            WorkflowUserType.User => "U",
            WorkflowUserType.Group => "G",
            WorkflowUserType.Key => "K",
            WorkflowUserType.Email => "E",
            WorkflowUserType.Approvers => "A",
            _ => "U"
        };
    }
}
