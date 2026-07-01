using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Api;

/// <summary>
/// COM-based implementation of IWorkflowApiClient.
/// Uses the NxWorkflowAdmin COM object for workflow CRUD operations.
/// </summary>
public class ComWorkflowApiClient : IWorkflowApiClient
{
    private readonly ILegacyCoreApiSession _legacySession;
    private readonly LegacyComFeatureFlags _flags;

    public ComWorkflowApiClient(ILegacyCoreApiSession legacySession, LegacyComFeatureFlags flags)
    {
        _legacySession = legacySession;
        _flags = flags;
    }

    private void EnsureWorkflowEnabled()
    {
        if (!_flags.EnableLegacyWorkflow)
        {
            throw new NotSupportedException(
                "Legacy COM workflow phase is currently disabled. Set ADEPTTOOLS_LEGACYCOM_WORKFLOW=true to enable it.");
        }
    }

    public async Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        return new WorkflowSetup
        {
            MaximumLengthWorkflowName = InvokeGetInt(admin, "MaxWorkflowNameLength"),
            MaximumLengthStepName = InvokeGetInt(admin, "MaxStepNameLength"),
            MaximumWorkflowSteps = InvokeGetInt(admin, "MaxWorkflowSteps"),
            MaximumWorkflows = InvokeGetInt(admin, "MaxWorkflows")
        };
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);
        var info = await _legacySession.GetSessionInfoAsync(ct);

        var packet = new WorkflowAdminPacket
        {
            CurrentUserId = info.UserId,
            WfNameLen = InvokeGetInt(admin, "MaxWorkflowNameLength"),
            WfStepNameLen = InvokeGetInt(admin, "MaxStepNameLength"),
            Workflows = new List<WorkflowAdminItem>()
        };

        var count = InvokeGetInt(admin, "WorkflowCount");
        for (var i = 0; i < count; i++)
        {
            var wfInfo = InvokeMethod(admin, "GetWorkflowInfo", i);
            packet.Workflows.Add(new WorkflowAdminItem
            {
                WorkflowId = InvokeGetString(wfInfo, "WorkflowId"),
                WorkflowName = InvokeGetString(wfInfo, "Name"),
                Active = InvokeGetBool(wfInfo, "Active"),
                StepCount = InvokeGetInt(wfInfo, "StepCount"),
                InProcessCount = InvokeGetInt(wfInfo, "InProcessCount"),
                Edit = InvokeGetBool(wfInfo, "CanEdit"),
                Share = InvokeGetBool(wfInfo, "CanShare"),
                Delete = InvokeGetBool(wfInfo, "CanDelete"),
                LockedByDisplayName = InvokeGetString(wfInfo, "LockedByDisplayName")
            });
            ReleaseCom(ref wfInfo);
        }

        return packet;
    }

    public Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        // COM SDK doesn't have a separate "basic" endpoint — return full list
        return GetWorkflowsAsync(ct);
    }

    public async Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var nxWf = InvokeMethod(admin, "CreateNewWorkflow");
        try
        {
            return MapWorkflowToEditModel(nxWf);
        }
        finally
        {
            ReleaseCom(ref nxWf);
        }
    }

    public async Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var nxWf = InvokeMethod(admin, "OpenWorkflow", workflowId);
        try
        {
            return MapWorkflowToEditModel(nxWf);
        }
        finally
        {
            ReleaseCom(ref nxWf);
        }
    }

    public async Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var nxWf = InvokeMethod(admin, "OpenWorkflow", model.WorkflowDefinition.WorkflowId);
        try
        {
            // Apply workflow-level properties
            InvokeSet(nxWf, "Name", model.WorkflowDefinition.Name);
            InvokeSet(nxWf, "Memo", model.WorkflowDefinition.Memo ?? string.Empty);
            InvokeSet(nxWf, "Active", true);
            InvokeSet(nxWf, "DoEmailNotify", model.WorkflowDefinition.BDoEmailNotify);
            InvokeSet(nxWf, "TimeoutOn", model.WorkflowDefinition.BTimeoutOn);
            InvokeSet(nxWf, "RecurringTimeoutOn", model.WorkflowDefinition.BRecurringTimeoutOn);
            InvokeSet(nxWf, "Timeout", model.WorkflowDefinition.Timeout ?? string.Empty);
            InvokeSet(nxWf, "RecurringTimeout", model.WorkflowDefinition.RecurringTimeout ?? string.Empty);
            InvokeSet(nxWf, "TimeoutIncludeSaturday", model.WorkflowDefinition.BTimeoutIncludeSaturday);
            InvokeSet(nxWf, "TimeoutIncludeSunday", model.WorkflowDefinition.BTimeoutIncludeSunday);

            // Apply step-level properties
            var stepCount = InvokeGetInt(nxWf, "StepCount");
            for (var i = 0; i < model.WorkflowStepModels.Count && i < stepCount; i++)
            {
                var stepModel = model.WorkflowStepModels[i];
                if (stepModel.BDeleted) continue;

                var nxStep = InvokeMethod(nxWf, "GetStep", i);
                ApplyStepProperties(nxStep, stepModel);
                ReleaseCom(ref nxStep);
            }

            var saveResult = Convert.ToInt32(InvokeMethod(nxWf, "Save"));
            return saveResult == 0
                ? ApiResult.Success("Workflow saved via COM.")
                : ApiResult.Failure(saveResult, $"COM Save failed with code {saveResult}.");
        }
        finally
        {
            ReleaseCom(ref nxWf);
        }
    }

    public async Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var result = Convert.ToInt32(InvokeMethod(admin, "DeleteWorkflow", workflowId));
        return result == 0
            ? ApiResult.Success("Workflow deleted via COM.")
            : ApiResult.Failure(result, $"COM Delete failed with code {result}.");
    }

    public async Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var nxWf = InvokeMethod(admin, "OpenWorkflow", model.WorkflowDefinition.WorkflowId);
        try
        {
            var nxStep = InvokeMethod(nxWf, "AddStep", position);
            var newStepId = InvokeGetString(nxStep, "StepId");
            ReleaseCom(ref nxStep);

            var updatedModel = MapWorkflowToEditModel(nxWf);
            updatedModel.EStepId = newStepId;
            return updatedModel;
        }
        finally
        {
            ReleaseCom(ref nxWf);
        }
    }

    public async Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var nxWf = InvokeMethod(admin, "TagWorkflow", workflowId);
        try
        {
            return MapWorkflowToEditModel(nxWf);
        }
        finally
        {
            ReleaseCom(ref nxWf);
        }
    }

    public async Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var result = Convert.ToInt32(InvokeMethod(admin, "UntagWorkflow", workflowId));
        return result == 0
            ? ApiResult.Success()
            : ApiResult.Failure(result, $"COM Untag failed with code {result}.");
    }

    public async Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        var admin = await GetWorkflowAdminDispatchAsync(ct);

        var metagroups = new List<WorkflowCommonTarget>();
        var count = InvokeGetInt(admin, "MetagroupCount");

        for (var i = 0; i < count; i++)
        {
            var mg = InvokeMethod(admin, "GetMetagroup", i);
            metagroups.Add(new WorkflowCommonTarget
            {
                Key = InvokeGetString(mg, "Key"),
                DisplayName = InvokeGetString(mg, "DisplayName")
            });
            ReleaseCom(ref mg);
        }

        return metagroups;
    }

    public Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        // COM SDK does not expose user enumeration. This operation requires the HTTP backend.
        throw new NotSupportedException(
            "User list retrieval is not supported via COM. Use the HTTP backend for name resolution.");
    }

    private static WorkflowEditModel MapWorkflowToEditModel(object nxWf)
    {
        var model = new WorkflowEditModel
        {
            BEditable = InvokeGetBool(nxWf, "IsEditable"),
            AlreadyTaggedByName = InvokeGetString(nxWf, "AlreadyTaggedByName"),
            WorkflowDefinition = new WorkflowDefinition
            {
                WorkflowId = InvokeGetString(nxWf, "WorkflowId"),
                Name = InvokeGetString(nxWf, "Name"),
                Memo = InvokeGetString(nxWf, "Memo"),
                BDoEmailNotify = InvokeGetBool(nxWf, "DoEmailNotify"),
                BTimeoutOn = InvokeGetBool(nxWf, "TimeoutOn"),
                BRecurringTimeoutOn = InvokeGetBool(nxWf, "RecurringTimeoutOn"),
                Timeout = InvokeGetString(nxWf, "Timeout"),
                RecurringTimeout = InvokeGetString(nxWf, "RecurringTimeout"),
                BTimeoutIncludeSaturday = InvokeGetBool(nxWf, "TimeoutIncludeSaturday"),
                BTimeoutIncludeSunday = InvokeGetBool(nxWf, "TimeoutIncludeSunday")
            },
            WorkflowStepModels = new List<WorkflowStepModel>()
        };

        var stepCount = InvokeGetInt(nxWf, "StepCount");
        var workflowId = model.WorkflowDefinition.WorkflowId;
        for (var i = 0; i < stepCount; i++)
        {
            var nxStep = InvokeMethod(nxWf, "GetStep", i);
            var stepModel = new WorkflowStepModel
            {
                WorkflowStepDefinition = new WorkflowStepDefinition
                {
                    WorkflowId = workflowId,
                    StepId = InvokeGetString(nxStep, "StepId"),
                    Order = InvokeGetInt(nxStep, "Order"),
                    Name = InvokeGetString(nxStep, "Name"),
                    BCanChangeWorkflow = InvokeGetBool(nxStep, "CanChangeWorkflow"),
                    BCanSignOut = InvokeGetBool(nxStep, "CanSignOut"),
                    BCanExpediteReject = InvokeGetBool(nxStep, "CanExpediteReject"),
                    BCanExpediteApprove = InvokeGetBool(nxStep, "CanExpediteApprove"),
                    BDoEmailNotify = InvokeGetBool(nxStep, "DoEmailNotify"),
                    BRequireComment = InvokeGetBool(nxStep, "RequireComment"),
                    BOnlyAllowAssignToApprovers = InvokeGetBool(nxStep, "OnlyAllowAssignToApprovers"),
                    RequiredApprovalsCount = InvokeGetInt(nxStep, "RequiredApprovalsCount"),
                    AutoAdvance = InvokeGetBool(nxStep, "AutoAdvance"),
                    BTimeoutOn = InvokeGetBool(nxStep, "TimeoutOn"),
                    BRecurringTimeoutOn = InvokeGetBool(nxStep, "RecurringTimeoutOn"),
                    Timeout = InvokeGetString(nxStep, "Timeout"),
                    RecurringTimeout = InvokeGetString(nxStep, "RecurringTimeout"),
                    BTimeoutIncludeSaturday = InvokeGetBool(nxStep, "TimeoutIncludeSaturday"),
                    BTimeoutIncludeSunday = InvokeGetBool(nxStep, "TimeoutIncludeSunday")
                },
                InProcessCount = InvokeGetInt(nxStep, "InProcessCount"),
                WorkflowTrusteeDefinitions = new List<WorkflowTrusteeDefinition>()
            };

            var trusteeCount = InvokeGetInt(nxStep, "TrusteeCount");
            for (var j = 0; j < trusteeCount; j++)
            {
                var nxTrustee = InvokeMethod(nxStep, "GetTrustee", j);
                stepModel.WorkflowTrusteeDefinitions.Add(new WorkflowTrusteeDefinition
                {
                    WorkflowId = workflowId,
                    StepId = stepModel.WorkflowStepDefinition.StepId,
                    TrusteeId = InvokeGetString(nxTrustee, "TrusteeId"),
                    Type = MapTrusteeType(InvokeGetString(nxTrustee, "Type"))
                });
                ReleaseCom(ref nxTrustee);
            }

            model.WorkflowStepModels.Add(stepModel);
            ReleaseCom(ref nxStep);
        }

        return model;
    }

    private static void ApplyStepProperties(object nxStep, WorkflowStepModel stepModel)
    {
        var def = stepModel.WorkflowStepDefinition;
        InvokeSet(nxStep, "Name", def.Name);
        InvokeSet(nxStep, "CanChangeWorkflow", def.BCanChangeWorkflow);
        InvokeSet(nxStep, "CanSignOut", def.BCanSignOut);
        InvokeSet(nxStep, "CanExpediteReject", def.BCanExpediteReject);
        InvokeSet(nxStep, "CanExpediteApprove", def.BCanExpediteApprove);
        InvokeSet(nxStep, "DoEmailNotify", def.BDoEmailNotify);
        InvokeSet(nxStep, "RequireComment", def.BRequireComment);
        InvokeSet(nxStep, "OnlyAllowAssignToApprovers", def.BOnlyAllowAssignToApprovers);
        InvokeSet(nxStep, "RequiredApprovalsCount", def.RequiredApprovalsCount);
        InvokeSet(nxStep, "AutoAdvance", def.AutoAdvance);
        InvokeSet(nxStep, "TimeoutOn", def.BTimeoutOn);
        InvokeSet(nxStep, "RecurringTimeoutOn", def.BRecurringTimeoutOn);
        InvokeSet(nxStep, "Timeout", def.Timeout ?? string.Empty);
        InvokeSet(nxStep, "RecurringTimeout", def.RecurringTimeout ?? string.Empty);
        InvokeSet(nxStep, "TimeoutIncludeSaturday", def.BTimeoutIncludeSaturday);
        InvokeSet(nxStep, "TimeoutIncludeSunday", def.BTimeoutIncludeSunday);

        // Update trustees
        InvokeMethod(nxStep, "ClearTrustees");
        foreach (var trustee in stepModel.WorkflowTrusteeDefinitions)
        {
            InvokeMethod(nxStep, "AddTrustee", trustee.TrusteeId, MapTrusteeTypeToString(trustee.Type));
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

    private async Task<object> GetWorkflowAdminDispatchAsync(CancellationToken ct)
    {
        var dispatch = await _legacySession.GetConnectedDispatchAsync(ct);
        var attempted = new List<string>();

        foreach (var (candidate, label) in EnumerateCandidates(dispatch))
        {
            var workflowAdmin = TryInvoke(candidate, "GetWorkflowAdmin");
            if (workflowAdmin != null)
                return workflowAdmin;

            attempted.Add(label);
        }

        throw new NotSupportedException(
            "Legacy COM session does not expose workflow administration (GetWorkflowAdmin). " +
            $"Probed candidates: {string.Join(", ", attempted)}");
    }

    private static IEnumerable<(object candidate, string label)> EnumerateCandidates(object root)
    {
        var queue = new Queue<(object candidate, string label, int depth)>();
        var seen = new HashSet<nint>();
        queue.Enqueue((root, "root", 0));

        while (queue.Count > 0)
        {
            var (candidate, label, depth) = queue.Dequeue();
            var identity = GetIdentityKey(candidate);
            if (identity != 0 && !seen.Add(identity))
                continue;

            yield return (candidate, label);

            if (depth >= 3)
                continue;

            foreach (var methodName in new[] { "GetProject", "GetCore", "GetCoreApi", "GetLogin", "GetDomain", "GetApplication" })
            {
                var next = TryInvoke(candidate, methodName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{methodName}()", depth + 1));
            }

            foreach (var propertyName in new[] { "Project", "Core", "Login", "Domain", "Application" })
            {
                var next = TryGetProperty(candidate, propertyName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{propertyName}", depth + 1));
            }
        }
    }

    private static object? TryGetProperty(object target, string name)
    {
        try
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                target,
                null);
        }
        catch
        {
            return null;
        }
    }

    private static nint GetIdentityKey(object value)
    {
        try
        {
            if (Marshal.IsComObject(value))
            {
                var ptr = Marshal.GetIUnknownForObject(value);
                Marshal.Release(ptr);
                return ptr;
            }
        }
        catch
        {
        }

        return value.GetHashCode();
    }

    private static object InvokeMethod(object target, string name, params object[] args)
    {
        var value = target.GetType().InvokeMember(
            name,
            BindingFlags.InvokeMethod,
            null,
            target,
            args);

        return value ?? throw new InvalidOperationException(
            $"COM invocation '{name}' returned null on type {target.GetType().FullName}.");
    }

    private static object? TryInvoke(object target, string name, params object[] args)
    {
        try
        {
            return InvokeMethod(target, name, args);
        }
        catch
        {
            return null;
        }
    }

    private static string InvokeGetString(object target, string name)
    {
        var value = target.GetType().InvokeMember(
            name,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value?.ToString() ?? string.Empty;
    }

    private static int InvokeGetInt(object target, string name)
    {
        var value = target.GetType().InvokeMember(
            name,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value is int i ? i : Convert.ToInt32(value);
    }

    private static bool InvokeGetBool(object target, string name)
    {
        var value = target.GetType().InvokeMember(
            name,
            BindingFlags.GetProperty,
            null,
            target,
            null);
        return value is bool b ? b : Convert.ToBoolean(value);
    }

    private static void InvokeSet(object target, string name, object? value)
    {
        target.GetType().InvokeMember(
            name,
            BindingFlags.SetProperty,
            null,
            target,
            new[] { value });
    }

    private static void ReleaseCom(ref object? value)
    {
        ComLifecycle.Release(ref value);
    }
}
