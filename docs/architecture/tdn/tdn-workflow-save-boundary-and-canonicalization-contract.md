# TDN - Workflow Save Boundary and Canonicalization Contract

## Purpose

This TDN defines the authoritative save boundary and canonicalization contract for Workflow create and modify operations in Adept Power Tools.

It consolidates the behavior that is currently spread across workflow congruence and hardening notes into one implementation contract.

## Scope

In scope:
- Full-snapshot save behavior for create and modify.
- Server-canonical reload and post-save verification behavior.
- Ownership rules for workflow-level versus step-level notification lists.
- Failure handling and fail-fast contract for persistence mismatches.

Out of scope:
- UI authoring details from AdeptWebClient.
- Import pipeline contracts.
- Delete/list contracts not tied to save boundary semantics.

## COM Path (11.4.5)

Adept 11.4.5 admin workflow persistence uses native/Desktop edit-session and object-graph write-back semantics:
- Create: native add/editor flow -> `CCliWorkflowDefManager::Update()`.
- Modify: `CCliWorkflowDefManager::a11_Edit(workflowId, msg)` -> mutate graph -> `Update()`.
- Delete: `CCliWorkflowDefList::Delete(workflowId)` -> `Update()`.

11.4.5-specific implication:
- The save boundary is still a full object-graph commit boundary even though it is not expressed as one HTTP `WorkflowEditModel` POST.

## Authoritative Save Boundary

Workflow create and modify are full-snapshot authoring operations.

Contract:
1. The service constructs or loads a WorkflowEditModel.
2. The service applies input intent to that model (definition, steps, trustees, notifications, timeout fields).
3. The service performs one save boundary call (with bounded retry for known transient HTTP failure signature).
4. Post-save verification re-reads server state and treats persisted server data as canonical truth.

Implication:
- Save intent is snapshot-based.
- Canonical state after save is server-returned, not client-assumed.

11.4.5 native qualification:
- In COM/Desktop mode, the equivalent snapshot is the in-memory workflow graph owned by `CCliWorkflowDefManager` and child lists at the moment `Update()` is called.

## Canonicalization Rules

### Rule 1: Server response is canonical after save

After save succeeds, persistence checks use a fresh GetWorkflow read.
Any mismatch between expected and persisted trustees/notifications is treated as contract failure.

11.4.5 native qualification:
- Native parity checks should treat post-`Update()` persisted rows in WF/WFSTEP/WFTR/NOTIFY as canonical truth, not the pre-write editor graph.

### Rule 2: Step identity is stable by step order plus step identity, not incidental array order

Step configuration maps input steps to active server steps using deterministic ordering semantics, not incidental response ordering.

### Rule 3: Full-replace semantics for authored fields

Authored fields are actively driven to input intent, including both setting and clearing paths.
No implicit preserve-on-omit behavior is assumed for authored lists/flags inside the save boundary.

## List Ownership Contract

### Step-level notification lists are authoritative for step notifications

- Step notify recipients are owned by step EmailNotificationList.
- Step alert recipients are owned by step AlertNotificationList.

### Workflow-level notification lists are intentionally cleared in create/modify save paths

Workflow-level EmailNotificationList and AlertNotificationList are cleared before save in these paths to prevent cross-scope duplication and ambiguity.

Rationale:
- Server save behavior applies notifications from step models.
- Dual population can cause duplicated or cross-scope notification outcomes.

## Timeout and Email Flag Congruence Contract

1. Timeout fields are only overwritten when corresponding input values are explicitly present.
2. Weekend include flags are only overwritten when explicit weekend intent is present.
3. BDoEmailNotify is explicitly recomputed from step notification presence so full-replace authoring does not retain stale server state.

## Create/Modify Persistence Verification Contract

After save, verification runs in this order:
1. Reviewer trustee persistence verification by step.
2. Notification trustee persistence verification by step (alert and email/notify).
3. Visibility/share-state check for operational messaging.

If reviewer or notification verification fails:
- Operation is returned as Fail.
- Message includes missing persisted recipients context.
- Tag release is attempted best-effort before returning.

## Share Mutation Contract at Save Boundary

1. If backend supports share mutation, share intent is applied as part of create/modify flow.
2. If backend does not support share mutation and share change is requested, operation fails fast with explicit capability guidance.
3. Missing container metadata required for share mutation is treated as hard failure.

## Tag/Untag Contract

1. Save path assumes tagged edit ownership for the target workflow.
2. On all failure exits after tag acquisition, untag is attempted.
3. On success, untag is attempted and warning is included if untag fails.
4. Untag behavior is best-effort for cleanup and must not hide primary failure cause.

## Error Policy

Hard errors:
- Validation failures.
- Trustee resolution failures.
- Invalid reviewer trustee type.
- Save API failure after retry policy.
- Post-save reviewer/notification persistence mismatch.
- Unsupported share mutation request for current backend mode.

Warnings:
- Visibility mismatch after save in current list context.
- Untag warning when save succeeded but lock release could not be confirmed.

## Mode Notes (HTTP, COM, Mock)

### HTTP

- Uses save-with-transient-retry guard for known status 139 disposed-context signature.
- Supports share mutation path when capability is enabled.
- Supports richer diagnostics for FormatApiFailure messaging.

### COM

- Must honor same snapshot and canonicalization contract.
- Share mutation is unsupported and must fail fast when requested.
- Trustee persistence remains identity-congruent with downstream AWC expectations.
- Native delete/update side effects are part of the same commit boundary and must be considered part of canonical post-save state.

## 11.4.5 Native-Specific Save Semantics

Observed native write path specifics to preserve in third-client parity work:
- `Update()` is the persistence boundary for create/modify/delete.
- Delete commit applies side effects including `LIBWF` repointing, `FIL.DEFWFID` reset, `NOTIFY` cleanup, and active-doc reconciliation.
- System workflow mutability guards live in native/Desktop guard paths and must remain fail-fast invariants.

### Mock

- Must preserve the same contract shape for verification order and fail/success semantics.
- May simulate persistence mismatch paths for test coverage of contract failures.
- Must not silently convert contract failures into success.

## Invariants

1. Step names used for post-save correlation are normalized consistently.
2. Reviewer trustees persist by exact identity and expected type.
3. Notification recipients persist by expected scope (step-local) and expected identity semantics.
4. Canonical persisted model is always re-read before final success is returned.

## Non-Goals

1. This document does not redefine AWC UI behavior.
2. This document does not define delete/list contracts.
3. This document does not replace lower-level parser/validator implementation notes.

## Traceability

Primary source context:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-congruence-and-validation.md
- docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md
- src/AdeptTools.Workflow/Services/WorkflowService.cs

## Operational Checklist

Use this checklist for regression verification of the contract:
1. Create modifies only intended timeout/weekend fields when values are explicit.
2. Modify preserves step notification ownership without workflow-level duplication.
3. Out-of-order server step collections do not misapply step notifications.
4. Reviewer and notification persistence mismatches fail the operation with actionable diagnostics.
5. Save success still reports warning when untag cannot be confirmed.
