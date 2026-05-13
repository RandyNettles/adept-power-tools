using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for workflow administration.
/// Provides CRUD operations for workflows via the Adept COM SDK.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000009")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxWorkflowAdmin
{
    int MaxWorkflowNameLength { get; }
    int MaxStepNameLength { get; }
    int MaxWorkflowSteps { get; }
    int MaxWorkflows { get; }
    int WorkflowCount { get; }

    INxWorkflowInfo GetWorkflowInfo(int index);
    INxWorkflow CreateNewWorkflow();
    INxWorkflow OpenWorkflow(string workflowId);
    int DeleteWorkflow(string workflowId);
    INxWorkflow TagWorkflow(string workflowId);
    int UntagWorkflow(string workflowId);
    int MetagroupCount { get; }
    INxMetagroup GetMetagroup(int index);
}

/// <summary>
/// Summary information about a workflow (list item).
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-00000000000A")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxWorkflowInfo
{
    string WorkflowId { get; }
    string Name { get; }
    bool Active { get; }
    int StepCount { get; }
    int InProcessCount { get; }
    bool CanEdit { get; }
    bool CanShare { get; }
    bool CanDelete { get; }
    string LockedByDisplayName { get; }
}

/// <summary>
/// A workflow open for editing (tagged/locked).
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-00000000000B")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxWorkflow
{
    string WorkflowId { get; }
    bool IsEditable { get; }
    string AlreadyTaggedByName { get; }
    string Name { get; set; }
    string Memo { get; set; }
    bool Active { get; set; }
    bool DoEmailNotify { get; set; }
    bool TimeoutOn { get; set; }
    bool RecurringTimeoutOn { get; set; }
    string Timeout { get; set; }
    string RecurringTimeout { get; set; }
    bool TimeoutIncludeSaturday { get; set; }
    bool TimeoutIncludeSunday { get; set; }

    int StepCount { get; }
    INxWorkflowStep GetStep(int index);
    INxWorkflowStep AddStep(int position);
    void RemoveStep(int index);
    int Save();
}

/// <summary>
/// A single workflow step open for editing.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-00000000000C")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxWorkflowStep
{
    string StepId { get; }
    int Order { get; }
    string Name { get; set; }
    bool CanChangeWorkflow { get; set; }
    bool CanSignOut { get; set; }
    bool CanExpediteReject { get; set; }
    bool CanExpediteApprove { get; set; }
    bool DoEmailNotify { get; set; }
    bool RequireComment { get; set; }
    bool OnlyAllowAssignToApprovers { get; set; }
    int RequiredApprovalsCount { get; set; }
    bool AutoAdvance { get; set; }
    bool TimeoutOn { get; set; }
    bool RecurringTimeoutOn { get; set; }
    string Timeout { get; set; }
    string RecurringTimeout { get; set; }
    bool TimeoutIncludeSaturday { get; set; }
    bool TimeoutIncludeSunday { get; set; }
    int InProcessCount { get; }

    int TrusteeCount { get; }
    INxWorkflowTrustee GetTrustee(int index);
    void AddTrustee(string trusteeId, string type);
    void RemoveTrustee(int index);
    void ClearTrustees();
}

/// <summary>
/// A trustee assigned to a workflow step.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-00000000000D")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxWorkflowTrustee
{
    string TrusteeId { get; }
    string Type { get; }
}

/// <summary>
/// A metagroup entry available for workflow trustees.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-00000000000E")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxMetagroup
{
    string Key { get; }
    string DisplayName { get; }
}
