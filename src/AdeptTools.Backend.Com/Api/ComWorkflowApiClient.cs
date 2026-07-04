using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using System.Collections;
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
    private readonly ComSessionManager? _sessionManager;
    private readonly AdeptToolSettings? _settings;
    private readonly LegacyComFeatureFlags _flags;

    public ComWorkflowApiClient(
        ILegacyCoreApiSession legacySession,
        LegacyComFeatureFlags flags,
        ComSessionManager? sessionManager = null,
        AdeptToolSettings? settings = null)
    {
        _legacySession = legacySession;
        _flags = flags;
        _sessionManager = sessionManager;
        _settings = settings;
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
        var info = await _legacySession.GetSessionInfoAsync(ct);

        object? admin = null;
        try
        {
            admin = await GetWorkflowAdminDispatchAsync(ct);
        }
        catch (NotSupportedException)
        {
            // Some legacy environments expose workflow definitions via
            // Project.WorkflowDefManager.WorkflowDefList instead of GetWorkflowAdmin.
            var fallback = await TryGetWorkflowsFromDefinitionManagerAsync(ct);
            if (fallback is not null)
            {
                fallback.CurrentUserId = info.UserId;
                return fallback;
            }

            throw;
        }

        var packet = new WorkflowAdminPacket
        {
            CurrentUserId = info.UserId,
            WfNameLen = TryGetInt(admin, "MaxWorkflowNameLength") ?? 64,
            WfStepNameLen = TryGetInt(admin, "MaxStepNameLength") ?? 64,
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
                ShareStatus = NormalizeOptionalString(TryGetString(wfInfo, "ShareStatus")),
                OwnerUserId = NormalizeOptionalString(TryGetString(wfInfo, "OwnerUserId")),
                OwnerDisplayName = NormalizeOptionalString(TryGetString(wfInfo, "OwnerDisplayName")),
                LockedByDisplayName = NormalizeOptionalString(InvokeGetString(wfInfo, "LockedByDisplayName"))
            });
            ReleaseCom(ref wfInfo);
        }

        return packet;
    }

    private async Task<WorkflowAdminPacket?> TryGetWorkflowsFromDefinitionManagerAsync(CancellationToken ct)
    {
        var dispatch = await _legacySession.GetConnectedDispatchAsync(ct);

        foreach (var (candidate, _) in EnumerateCandidates(dispatch))
        {
            var list = TryGetWorkflowDefinitionList(candidate);
            if (list is null)
                continue;

            var packet = new WorkflowAdminPacket
            {
                WfNameLen = 64,
                WfStepNameLen = 64,
                Workflows = new List<WorkflowAdminItem>()
            };

            var count = TryInvokeIntByNames(list, "GetCount", "Count", "WorkflowCount", "GetWorkflowCount")
                        ?? TryGetInt(list, "Count")
                        ?? TryGetInt(list, "WorkflowCount")
                        ?? 0;

            var anyItems = false;

            for (var i = 0; i < count; i++)
            {
                var wf = TryInvoke(list, "GetItem", i)
                         ?? TryInvoke(list, "GetItem", i + 1);
                if (wf is null)
                    continue;

                try
                {
                    anyItems = true;
                    var name = TryGetString(wf, "Name") ?? string.Empty;
                    var id = TryGetString(wf, "Id") ?? TryGetString(wf, "WorkflowId") ?? name;
                    var stepCount = TryGetStepCountFromDefinition(wf) ?? 0;
                    var isActive = TryGetBool(wf, "Active")
                                   ?? TryGetBool(wf, "bActive")
                                   ?? true;

                    packet.Workflows.Add(new WorkflowAdminItem
                    {
                        WorkflowId = id,
                        WorkflowName = name,
                        Active = isActive,
                        StepCount = stepCount,
                        InProcessCount = 0,
                        Edit = true,
                        Share = true,
                        Delete = true,
                        LockedByDisplayName = null
                    });

                    if (!string.IsNullOrEmpty(name))
                        packet.WfNameLen = Math.Max(packet.WfNameLen, name.Length);
                }
                finally
                {
                    ReleaseCom(ref wf);
                }
            }

            // Some legacy COM lists are enumerable but do not expose GetCount/GetItem.
            if (!anyItems)
            {
                foreach (var wf in EnumerateComItems(list))
                {
                    if (wf is null)
                        continue;

                    try
                    {
                        anyItems = true;
                        var name = TryGetString(wf, "Name") ?? string.Empty;
                        var id = TryGetString(wf, "Id") ?? TryGetString(wf, "WorkflowId") ?? name;
                        var stepCount = TryGetStepCountFromDefinition(wf) ?? 0;
                        var isActive = TryGetBool(wf, "Active")
                                       ?? TryGetBool(wf, "bActive")
                                       ?? true;

                        packet.Workflows.Add(new WorkflowAdminItem
                        {
                            WorkflowId = id,
                            WorkflowName = name,
                            Active = isActive,
                            StepCount = stepCount,
                            InProcessCount = 0,
                            Edit = true,
                            Share = true,
                            Delete = true,
                            LockedByDisplayName = null
                        });

                        if (!string.IsNullOrEmpty(name))
                            packet.WfNameLen = Math.Max(packet.WfNameLen, name.Length);
                    }
                    finally
                    {
                        var tmp = wf;
                        ReleaseCom(ref tmp);
                    }
                }
            }

            return packet;
        }

        return null;
    }

    private static object? TryGetWorkflowDefinitionList(object candidate)
    {
        // Candidate itself may already be the list.
        if (LooksLikeWorkflowDefinitionList(candidate))
            return candidate;

        var project = TryGetProperty(candidate, "Project")
                      ?? TryInvoke(candidate, "GetProject")
                      ?? TryInvoke(candidate, "Project");

        var manager = TryGetProperty(candidate, "WorkflowDefManager")
                      ?? TryGetProperty(candidate, "WorkFlowDefManager")
                      ?? TryInvoke(candidate, "GetWorkflowDefManager")
                      ?? TryInvoke(candidate, "GetWorkFlowDefManager")
                      ?? TryInvoke(candidate, "WorkflowDefManager")
                      ?? TryInvoke(candidate, "WorkFlowDefManager")
                      ?? (project is null ? null :
                          TryGetProperty(project, "WorkflowDefManager")
                          ?? TryGetProperty(project, "WorkFlowDefManager")
                          ?? TryInvoke(project, "GetWorkflowDefManager")
                          ?? TryInvoke(project, "GetWorkFlowDefManager")
                          ?? TryInvoke(project, "WorkflowDefManager")
                          ?? TryInvoke(project, "WorkFlowDefManager"));

        var list = TryGetProperty(candidate, "WorkflowDefList")
                   ?? TryGetProperty(candidate, "WorkFlowDefList")
                   ?? TryInvoke(candidate, "GetWorkflowDefList")
                   ?? TryInvoke(candidate, "GetWorkFlowDefList")
                   ?? TryInvoke(candidate, "WorkflowDefList")
                   ?? TryInvoke(candidate, "WorkFlowDefList")
                   ?? (manager is null ? null :
                       TryGetProperty(manager, "WorkflowDefList")
                       ?? TryGetProperty(manager, "WorkFlowDefList")
                       ?? TryInvoke(manager, "GetWorkflowDefList")
                       ?? TryInvoke(manager, "GetWorkFlowDefList")
                       ?? TryInvoke(manager, "WorkflowDefList")
                       ?? TryInvoke(manager, "WorkFlowDefList"));

        return list is not null && LooksLikeWorkflowDefinitionList(list)
            ? list
            : null;
    }

    private static bool LooksLikeWorkflowDefinitionList(object candidate)
    {
        var hasCount = TryInvokeIntByNames(candidate, "GetCount")
                       ?? TryGetInt(candidate, "Count")
                       ?? TryGetInt(candidate, "WorkflowCount");
        if (!hasCount.HasValue)
        {
            // Still treat as a likely list if it is enumerable and yields workflow-like items.
            var firstEnumerableItem = EnumerateComItems(candidate).FirstOrDefault();
            if (firstEnumerableItem is null)
                return false;

            try
            {
                return TryGetString(firstEnumerableItem, "Name") is not null
                       && (TryGetString(firstEnumerableItem, "Id") is not null
                           || TryGetString(firstEnumerableItem, "WorkflowId") is not null);
            }
            finally
            {
                var tmp = firstEnumerableItem;
                ReleaseCom(ref tmp);
            }
        }

        if (hasCount.Value == 0)
            return true;

        var first = TryInvoke(candidate, "GetItem", 0)
                    ?? TryInvoke(candidate, "FindName", string.Empty)
                    ?? EnumerateComItems(candidate).FirstOrDefault();
        if (first is null)
            return false;

        try
        {
            var looksLikeWorkflow = TryGetString(first, "Name") is not null
                                    && (TryGetString(first, "Id") is not null
                                        || TryGetString(first, "WorkflowId") is not null);
            return looksLikeWorkflow;
        }
        finally
        {
            ReleaseCom(ref first);
        }
    }

    private static IEnumerable<object?> EnumerateComItems(object source)
    {
        IEnumerable? enumerable = source as IEnumerable;

        if (enumerable is null)
        {
            var fromEnumProperty = TryGetProperty(source, "_NewEnum");
            enumerable = fromEnumProperty as IEnumerable;
        }

        if (enumerable is null)
            yield break;

        foreach (var item in enumerable)
            yield return item;
    }

    private static int? TryInvokeIntByNames(object target, params string[] names)
    {
        foreach (var name in names)
        {
            var value = TryInvoke(target, name);
            if (value is null)
                continue;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                // Continue probing.
            }
        }

        return null;
    }

    private static int? TryGetStepCountFromDefinition(object workflowDef)
    {
        var direct = TryGetInt(workflowDef, "StepCount")
                     ?? TryGetInt(workflowDef, "WorkflowStepCount");
        if (direct.HasValue)
            return direct;

        var stepList = TryGetProperty(workflowDef, "WorkflowStepDefList")
                       ?? TryInvoke(workflowDef, "GetWorkflowStepDefList");
        if (stepList is null)
            return null;

        return TryInvokeIntByNames(stepList, "GetCount")
               ?? TryGetInt(stepList, "Count");
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
            InvokeSet(nxWf, "Active", model.WorkflowDefinition.Active);
            InvokeSet(nxWf, "DoEmailNotify", model.WorkflowDefinition.BDoEmailNotify);
            InvokeSet(nxWf, "TimeoutOn", model.WorkflowDefinition.BTimeoutOn);
            InvokeSet(nxWf, "RecurringTimeoutOn", model.WorkflowDefinition.BRecurringTimeoutOn);
            InvokeSet(nxWf, "Timeout", model.WorkflowDefinition.Timeout ?? string.Empty);
            InvokeSet(nxWf, "RecurringTimeout", model.WorkflowDefinition.RecurringTimeout ?? string.Empty);
            InvokeSet(nxWf, "TimeoutIncludeSaturday", model.WorkflowDefinition.BTimeoutIncludeSaturday);
            InvokeSet(nxWf, "TimeoutIncludeSunday", model.WorkflowDefinition.BTimeoutIncludeSunday);
            TrySetProperty(nxWf, "Shared", model.WorkflowDefinition.Shared);

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

    public Task<ApiResult> SetWorkflowSharedAsync(string workflowId, bool shared, CancellationToken ct = default)
    {
        EnsureWorkflowEnabled();
        return Task.FromResult(ApiResult.Failure(
            -1,
            "Setting workflow share state is not supported via COM backend in this client. Use HTTP backend for Shared workflow operations."));
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

    public Task<AdeptUserEntry?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        // COM SDK does not expose user lookup by user ID. This operation requires the HTTP backend.
        throw new NotSupportedException(
            "User lookup by ID is not supported via COM. Use the HTTP backend for trustee validation.");
    }

    public Task<List<AdeptGroupEntry>> GetGroupsAsync(CancellationToken ct = default)
    {
        // COM SDK does not expose group enumeration in this client path.
        throw new NotSupportedException(
            "Group list retrieval is not supported via COM. Use the HTTP backend for group trustee validation.");
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
                Active = InvokeGetBool(nxWf, "Active"),
                Shared = TryGetBool(nxWf, "Shared") ?? false,
                OwnerUserId = NormalizeOptionalString(TryGetString(nxWf, "OwnerUserId")),
                OwnerDisplayName = NormalizeOptionalString(TryGetString(nxWf, "OwnerDisplayName")),
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
            AddStepTrustee(nxStep, trustee);
        }
    }

    private static void AddStepTrustee(object nxStep, WorkflowTrusteeDefinition trustee)
    {
        var typeCode = MapTrusteeTypeToString(trustee.Type);
        var typeChar = typeCode[0];
        var typeInt = (int)typeChar;

        // Legacy COM variants differ across versions (string/char/int trustee type).
        if (TryInvokeMethod(nxStep, "AddTrustee", trustee.TrusteeId, typeCode)) return;
        if (TryInvokeMethod(nxStep, "AddTrustee", trustee.TrusteeId, typeChar)) return;
        if (TryInvokeMethod(nxStep, "AddTrustee", trustee.TrusteeId, typeInt)) return;

        if (TryAddTrusteeViaList(nxStep, trustee.TrusteeId, typeCode, typeChar, typeInt)) return;

        throw new InvalidOperationException(
            $"Unable to add trustee '{trustee.TrusteeId}' with type '{typeCode}' via COM.");
    }

    private static bool TryAddTrusteeViaList(
        object nxStep,
        string trusteeId,
        string typeCode,
        char typeChar,
        int typeInt)
    {
        object? trusteeList = null;
        object? trusteeDef = null;

        try
        {
            trusteeList = TryGetProperty(nxStep, "WorkflowTrusteeDefList")
                          ?? TryGetProperty(nxStep, "TrusteeDefList")
                          ?? TryGetProperty(nxStep, "TrusteeList")
                          ?? TryInvoke(nxStep, "GetWorkflowTrusteeDefList")
                          ?? TryInvoke(nxStep, "GetTrusteeList");

            if (trusteeList is null)
                return false;

            if (TryInvokeMethod(trusteeList, "Add", trusteeId, typeCode)) return true;
            if (TryInvokeMethod(trusteeList, "Add", trusteeId, typeChar)) return true;
            if (TryInvokeMethod(trusteeList, "Add", trusteeId, typeInt)) return true;

            trusteeDef = TryInvoke(trusteeList, "FindId", trusteeId);
            if (trusteeDef is null)
                return false;

            if (TrySetProperty(trusteeDef, "IdType", typeInt)) return true;
            if (TrySetProperty(trusteeDef, "Type", typeCode)) return true;

            return false;
        }
        finally
        {
            ReleaseCom(ref trusteeDef);
            ReleaseCom(ref trusteeList);
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
            if (LooksLikeWorkflowAdmin(candidate))
                return candidate;

            foreach (var methodName in new[]
                     {
                         "GetWorkflowAdmin",
                         "GetWorkFlowAdmin",
                         "GetWfAdmin",
                         "GetWFAdmin",
                         "GetWorkflowAdministrator",
                         "GetWorkflowManager"
                     })
            {
                var workflowAdmin = TryInvoke(candidate, methodName);
                if (workflowAdmin != null)
                    return workflowAdmin;
            }

            foreach (var propertyName in new[]
                     {
                         "WorkflowAdmin",
                         "WorkFlowAdmin",
                         "WfAdmin",
                         "WFAdmin",
                         "WorkflowAdministrator",
                         "WorkflowManager"
                     })
            {
                var workflowAdmin = TryGetProperty(candidate, propertyName);
                if (workflowAdmin != null)
                    return workflowAdmin;
            }

            attempted.Add(label);
        }

        var sdkFallback = await TryGetWorkflowAdminFromSdkAsync(ct);
        if (sdkFallback is not null)
            return sdkFallback;

        throw new NotSupportedException(
            "Legacy COM session does not expose workflow administration (GetWorkflowAdmin/WorkflowAdmin). " +
            $"Probed candidates: {string.Join(", ", attempted)}");
    }

    private async Task<object?> TryGetWorkflowAdminFromSdkAsync(CancellationToken ct)
    {
        if (_sessionManager is null || _settings is null || string.IsNullOrWhiteSpace(_settings.ServerUrl))
            return null;

        try
        {
            if (!_sessionManager.IsConnected)
            {
                var userName = string.IsNullOrWhiteSpace(_settings.UserName) ? "ADM" : _settings.UserName;
                var password = Environment.GetEnvironmentVariable("ADEPTTOOLS_PASSWORD") ?? string.Empty;
                var connectResult = await _sessionManager.ConnectAsync(_settings.ServerUrl!, userName, password, ct);
                if (connectResult != 0)
                    return null;
            }

            return await _sessionManager.GetWorkflowAdminAsync(ct);
        }
        catch
        {
            // SDK fallback is optional. Legacy COM-only environments should continue
            // and report the legacy probing result instead of hard failing here.
            return null;
        }
    }

    private static bool LooksLikeWorkflowAdmin(object candidate)
    {
        var score = 0;
        if (HasProperty(candidate, "WorkflowCount")) score++;
        if (HasProperty(candidate, "MaxWorkflowNameLength")) score++;
        if (HasProperty(candidate, "MaxStepNameLength")) score++;
        if (HasProperty(candidate, "MaxWorkflowSteps")) score++;
        if (HasProperty(candidate, "MaxWorkflows")) score++;

        // Require at least two distinctive members to avoid false positives.
        return score >= 2;
    }

    private static bool HasProperty(object target, string name)
    {
        return TryGetProperty(target, name) is not null;
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

            if (depth >= 5)
                continue;

            foreach (var methodName in new[]
                     {
                         "GetProject",
                         "Project",
                         "GetCore",
                         "Core",
                         "GetCoreApi",
                         "GetLogin",
                         "Login",
                         "GetDomain",
                         "Domain",
                         "GetApplication",
                         "Application",
                         "GetDatabase",
                         "Database",
                         "GetWorkflowDefManager",
                         "GetWorkFlowDefManager",
                         "WorkflowDefManager",
                         "WorkFlowDefManager",
                         "GetWorkflowDefList",
                         "GetWorkFlowDefList",
                         "WorkflowDefList",
                         "WorkFlowDefList"
                     })
            {
                var next = TryInvoke(candidate, methodName);
                if (next != null)
                    queue.Enqueue((next, $"{label}.{methodName}()", depth + 1));
            }

            foreach (var propertyName in new[]
                     {
                         "Project",
                         "Core",
                         "Login",
                         "Domain",
                         "Application",
                         "Database",
                         "WorkflowDefManager",
                         "WorkFlowDefManager",
                         "WorkflowDefList"
                         ,"WorkFlowDefList"
                     })
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
                BindingFlags.GetProperty | BindingFlags.IgnoreCase,
                null,
                target,
                null);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetProperty(object target, string name, object? value)
    {
        try
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.SetProperty | BindingFlags.IgnoreCase,
                null,
                target,
                new[] { value });
            return true;
        }
        catch
        {
            return false;
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
            BindingFlags.InvokeMethod | BindingFlags.IgnoreCase,
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

    private static bool TryInvokeMethod(object target, string name, params object[] args)
    {
        try
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod | BindingFlags.IgnoreCase,
                null,
                target,
                args);
            return true;
        }
        catch
        {
            return false;
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

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static int? TryGetInt(object target, string name)
    {
        try
        {
            var value = target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty | BindingFlags.IgnoreCase,
                null,
                target,
                null);
            if (value is null)
                return null;

            return value is int i ? i : Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(object target, string name)
    {
        try
        {
            var value = target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty | BindingFlags.IgnoreCase,
                null,
                target,
                null);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool? TryGetBool(object target, string name)
    {
        try
        {
            var value = target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty | BindingFlags.IgnoreCase,
                null,
                target,
                null);

            return value switch
            {
                null => null,
                bool b => b,
                int i => i != 0,
                _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
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
