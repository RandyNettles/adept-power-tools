# TDN - Workflow Concurrency, Locking, and Recovery Strategy

## Purpose

Define the architecture-level concurrency and lock lifecycle policy for workflow authoring operations, including:
- Edit-lock ownership model.
- Tag/untag lifecycle boundaries.
- Stale-lock handling and recovery expectations.
- User-facing messaging and operator guidance.

This TDN also makes mode and surface boundaries explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Scope

In scope:
- Workflow create, modify, and delete operational concurrency behavior.
- Shared locking/recovery contract semantics across runtime modes and surfaces.
- Tag-based edit locking in `AdeptTools.Workflow`.
- Cleanup expectations for success, failure, and cancellation paths.
- Support/operations guidance for lock contention and suspected stale locks.

Out of scope:
- Non-workflow feature locking behavior.
- Backend database internal lock implementation details.
- Launcher visual design details not affecting operational semantics.

## Boundary Model

This TDN has two contract layers.

### Layer 1: Shared locking and recovery contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- The semantic meaning of lock ownership.
- Cleanup and fail-safe expectations.
- Stale-lock posture and recovery rules.
- User-facing distinction between contention, cleanup uncertainty, and failure.

Does not define:
- Exact tag API signatures.
- Exact dialog/prompt UX.
- Backend-native lock-table implementation details beyond what is needed to preserve the contract.

### Layer 2: Mode and surface realization

Applies to:
- Backend-mode-specific lock mechanisms.
- CLI and Client interaction/orchestration differences.

Defines:
- Where HTTP/COM/Mock behavior differs.
- Which surface owns which presentation and control responsibilities.

Does not redefine:
- The shared concurrency and recovery contract.

## Source Context

Primary references:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md
- docs/runbooks/Workflow-template-runbook.md
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Api/IWorkflowApiClient.cs
- src/AdeptTools.Workflow/Models/WorkflowEditModel.cs
- src/AdeptTools.Workflow/Models/WorkflowAdminItem.cs

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode is the primary current ATP implementation surface for tag-based workflow locking.

HTTP-specific characteristics:
- Lock ownership is exposed through HTTP workflow edit/tag operations.
- Save flows can include HTTP-specific transient failure handling while lock is held.

Boundary rule:
- HTTP-specific tag mechanics may differ, but they must preserve the shared ownership, cleanup, and stale-lock contract.

### COM mode

COM mode realizes the same lock contract through native/Desktop edit sessions and multiple native lock scopes.

COM-specific characteristics:
- Native admin-session critical section exists.
- Per-workflow tag, single-admin tag, and save/copy serialization tag are distinct native mechanisms.

Boundary rule:
- COM/native mechanisms may be richer than HTTP, but they must preserve the same semantic lock ownership and recovery guarantees.

### Mock mode

Mock mode is a deterministic simulation path for lock and recovery behavior.

Mock-specific characteristics:
- May emulate lock acquisition failure, cleanup warnings, and stale-lock scenarios.

Boundary rule:
- Mock must simulate the same locking semantics and must not silently weaken contention, cleanup, or stale-lock behavior.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns command-driven orchestration and textual reporting of workflow locking outcomes.

CLI contract:
- Must preserve the same semantic lock-ownership, skip/fail, warning, and stale-lock guidance rules.
- May present contention and cleanup outcomes in script-friendly form.

### Client surface

Client owns interactive workflow authoring orchestration and presentation of lock/recovery outcomes.

Client contract:
- Must preserve the same semantic ownership, cleanup, and stale-lock behavior.
- May use dialogs, owner displays, or disablement UX without changing the underlying contract.

Boundary rule:
- Surface differences may affect interaction style, but not the meaning of lock ownership, recovery, or cleanup outcomes.

## COM Path (11.4.5)

Observed Adept 11.4.5 native admin locking path:
- Modify lock/edit ownership is acquired through `CCliWorkflowDefManager::a11_Edit(workflowId, msg)`.
- Native admin UI routes (`CWorkFlowAdmin`) respect that edit session before mutation and `Update()`.
- Interop-visible surface is `Project.WorkflowDefManager.Edit(ref msg)` / `Update()` / `CancelEdit()`.

11.4.5-specific implication:
- COM mode should model lock ownership around native edit-session semantics, not around HTTP tag endpoints.
- Adept 11.4.5 has three distinct native lock concepts in this slice:
  - per-workflow edit lock using workflow id as tag identity,
  - single-admin lock (`SINGLE_ADMIN_MGR_TAG_ID`) for destructive admin serialization,
  - workflow-save lock (`WF_SAVE_LOCK_ID`) for save/copy serialization.

Mode/surface implication:
- COM-path evidence qualifies native realization; it does not replace the shared locking/recovery contract both CLI and Client must preserve.

## 11.4.5 Native Lock Model

### Admin dialog session critical section

Observed native session setup:
- `CWorkFlowAdmin::OnInitDialog` calls `CCliWorkflowDefManager::a11_ReadyToEdit()`.
- Admin tables are refreshed via `UpdateTGroups()`.
- `LockCriticalSection(TRUE)` enters the admin editing critical section.

Observed session close:
- `CWorkFlowAdmin` destructor calls `a11_CloseEditSession()`.
- `a11_CloseEditSession()` releases the critical section and reloads local admin tables.

Policy implication:
- COM/Desktop parity work should treat admin-session setup/teardown as a first-class lock lifecycle boundary, not just per-workflow tag acquisition.

### Per-workflow edit lock

Observed native modify route:
- `CWorkFlowAdmin::OnBnClickedEditWF`
- `CCliWorkflowDefManager::a11_Edit(workflowId, msg)`
- rights validation
- `TagWfUsingID(workflowId, msg)`
- `ManagerTag(project, workflowId, ...)`

Observed native outcomes:
- Success marks the workflow editable and tracks ownership in `m_ListOfTaggedWF`.
- Failure returns a lock-owner message using native lock-failure formatting and keeps the workflow non-editable.

Observed native release paths:
- `CancelWFTag(workflowId)` untags that workflow explicitly.
- `Update()` untags the current system-tagged workflow id after successful write.
- `Clear()` untags tracked workflow tags during manager teardown/reset.

### Single-admin lock

Observed native destructive-operation gate:
- `ManagerTag(..., SINGLE_ADMIN_MGR_TAG_ID, ...)` is used to serialize destructive admin operations such as delete flows.
- If denied, UI shows admin-in-progress warning and aborts the operation.
- Release is via `ManagerUntag(..., SINGLE_ADMIN_MGR_TAG_ID)`.

Policy implication:
- Delete/reconcile paths in COM parity should not be modeled only as “locked workflow exclusion”; they also require a higher-scope destructive-operation lock concept.

### Save/copy serialization lock

Observed native save/copy gate:
- `WF_SAVE_LOCK_ID = "WFMAN_SAVE_WF_LOCK"`.
- Native flows acquire `ManagerTag(..., WF_SAVE_LOCK_ID, ...)` before save/copy naming windows.
- If denied, UI warns that another admin is saving/copying a workflow.
- Release occurs via `ReleaseGetWFLock()` calling `ManagerUntag(..., WF_SAVE_LOCK_ID)`.

Policy implication:
- COM/native parity may require a save-level serialization lock distinct from the workflow edit lock whenever naming/copy windows can race.

### Lock state refresh and owner display

Observed native refresh path:
- `FillListBoxWithWF()` calls `a11_FindAllWFCurrentlyLocked()`.
- `a11_FindAllWFCurrentlyLocked()` queries `LOCKS_DBN` and cross-checks IDs against workflow definitions.
- Matches are stored in `m_ListOfTaggedWF`.
- `PruneListOfLockedWF()` removes stale/zombie lock entries.

Observed owner-display helpers:
- `a11_GetTaggedByText(workflowId)` returns owner display text.
- `IsWfLockedByAnother(workflowId)` and `IsWfLocked(workflowId)` drive UI enable/disable behavior.

Policy implication:
- User-facing lock state should be refreshable from authoritative lock-table state and stale local lock caches must be pruned.

### Native lock utility layer

Observed shared utility APIs:
- `ManagerTagCheck(...)`
- `ManagerTag(...)`
- `ManagerUntag(...)`

Observed backing behavior:
- `ManagerTag` delegates to `RequestManager::TagRecord(...)`.
- `ManagerUntag` delegates to `RequestManager::UnTagRecord(...)`.
- Lock state is stored in `LOCKS_DBN`.

Policy implication:
- Lock identity should be treated as explicit tag ids with authoritative lock-table backing, not inferred solely from workflow records.

## Problem Statement

Workflow authoring relies on tag/edit-lock semantics, but lock ownership, release guarantees, stale-lock recovery posture, and operator messaging need one explicit policy document.

Without a unified policy, concurrency outcomes can become inconsistent across CLI and Launcher flows, especially when:
- a save succeeds but untag fails,
- a workflow is already tagged by another user,
- lock owner appears empty/ambiguous,
- an operation is interrupted mid-flight.

## Decision Summary

1. Tag ownership is the authoritative write gate for workflow modify operations.
2. Create/modify flows must hold lock ownership through post-save verification, then release lock as final step.
3. Untag is mandatory on success and best-effort on failure paths; cleanup failure must be surfaced as warning.
4. Delete operations must not attempt force-delete against locked workflows; locked items are non-eligible.
5. Stale-lock recovery is operator-mediated unless backend exposes explicit safe-force-unlock capability.
6. User messaging must clearly distinguish lock contention, save failure, and cleanup uncertainty.
7. COM/native mode may require multiple lock scopes for a single user operation: edit-session, per-workflow edit lock, destructive-operation lock, and save/copy serialization lock.
8. Mode-specific lock mechanisms and surface-specific UX may differ, but the shared semantic contract must remain stable.

## Concurrency Model

Shared-contract boundary:
- The model below defines semantic ownership and release behavior shared across modes and both surfaces.
- HTTP/COM/Mock differences change the mechanism, not the contract.

Workflow operations use optimistic orchestration with explicit edit ownership:

- Modify path:
  - `TagAsync` acquires edit intent.
  - If `BEditable == false`, operation is skipped with lock-owner message.
- Create path:
  - `CreateNewAsync` returns a newly authored model that is treated as edit-owned for the create session.
- Save path:
  - Full model save, persistence verification, and visibility checks are performed while lock is still held.
- Release path:
  - `UntagAsync` is invoked after full create/modify completion.

Design intent:
- Avoid lost-update windows by not releasing lock before validation/readback checks complete.

11.4.5 native qualification:
- Native orchestration distinguishes per-workflow edit ownership from broader admin-session and destructive/save serialization locks.

## Lock Ownership Contract

Shared-contract boundary:
- The rules below define ownership semantics shared across modes and both surfaces.

### Authoritative owner

- The active lock/tag owner is the actor that obtained editability for the current workflow.
- For modify, ownership is established by successful `TagAsync` with `BEditable=true`.
- For create, ownership is established by server create/edit session semantics.

11.4.5 native qualification:
- For modify, native authoritative ownership is the successful `a11_Edit`/`Edit` session.

### Non-owner behavior

If lock acquisition fails:
- Operation must not mutate workflow state.
- Operation returns non-success status with explicit message:
  - `Locked by <owner>` when available.
  - `Locked by another user` fallback when owner is unavailable.

### Ownership duration

Ownership must extend through:
1. Step configuration.
2. Save operation (including transient retry guard where applicable).
3. Post-save persistence verification (reviewer + notification checks).
4. Visibility check and final status composition.

Only then may lock release occur.

11.4.5 native qualification:
- Native save paths also release the current system-tagged workflow id after successful `Update()`.

## Lock Lifecycle and Cleanup Policy

Shared-contract boundary:
- Cleanup rules below are semantic guarantees shared across modes and both surfaces.
- Mode-specific cleanup primitives may differ.

### Success path

1. Acquire/hold lock.
2. Perform authoring and save.
3. Run post-save verification.
4. Attempt `UntagAsync`.
5. Return success; include cleanup warning if untag fails.

11.4.5 native qualification:
- Equivalent cleanup may involve releasing per-workflow tag, save/copy tag, and eventually closing the edit session critical section.

### Failure path

- On any failure after lock acquisition, attempt untag before returning failure.
- Cleanup attempt is best-effort and must not mask the original failure cause.

11.4.5 native qualification:
- Native cancel/error paths should include `CancelWFTag(workflowId)` or broader manager/session cleanup as appropriate for the active lock scope.

### Exception path

- Catch unexpected exceptions and attempt best-effort untag.
- Return failure with original exception context (not untag exception context).

### Cancellation path

- Treat cancellation as failure-to-complete with cleanup attempt.
- Attempt untag when cancellation occurs after lock acquisition.

## Delete Concurrency Policy

Shared-contract boundary:
- Delete locking semantics below define the shared safety posture.
- Native COM mode may add broader destructive-operation locks without changing the underlying contract.

Delete never force-breaks locks.

Eligibility rules:
- Locked workflows are excluded from deletable candidates.
- Lock state is inferred from `LockedByDisplayName` presence.
- Blank/whitespace lock owner fields are treated as non-locked for eligibility filtering.

Operational behavior:
- If all candidates are locked or otherwise non-eligible, operation completes with empty/no-op semantics and operator guidance.

11.4.5 native qualification:
- Native delete/admin paths additionally acquire `SINGLE_ADMIN_MGR_TAG_ID` so destructive operations are serialized even beyond per-workflow ownership checks.

## Timeout and Stale-Lock Recovery Strategy

## Baseline reality

Current lock contract does not expose explicit stale-lock timestamps, lease-expiry metadata, or force-unlock primitives in `IWorkflowApiClient`.

Therefore:
- The application cannot safely infer stale ownership solely from lock presence.
- Automatic lock breaking is prohibited by policy.

## Stale-lock policy

When lock appears persistent and blocking:
1. Classify as `SuspectedStaleLock` only (not definitive stale).
2. Do not auto-force unlock.
3. Advise operator to verify owner/session in authoritative admin surface.
4. Retry operation after manual cleanup or owner confirmation.

11.4.5 native qualification:
- The provided Adept 11.4.5 evidence exposes edit-session ownership behavior but no safe automatic force-unlock primitive; operator-mediated recovery remains the correct default.

## Future capability gate

If backend later supports safe stale-lock metadata and force-release API:
- Enable controlled recovery only behind explicit capability flag.
- Require audit logging (who forced unlock, when, why, target workflow).
- Require explicit operator confirmation before force release.

## User Messaging Contract

Surface boundary:
- The semantic message classes below are shared across CLI and Client.
- Presentation style may differ by surface, but the distinctions must remain intact.

Messages must be actionable and class-distinct.

### Lock contention message

Required content:
- Workflow name.
- Lock owner when available.
- Next action guidance (retry later or coordinate unlock).

11.4.5 native qualification:
- Native UI uses owner-aware lock-denied text for per-workflow edit failures and distinct warnings for save/copy contention and single-admin contention.

### Cleanup uncertainty message

If save succeeds but untag fails, return success with warning:
- "workflow was saved but the edit lock may not have been released — verify in the web client."

### Save/transient failure messaging

Required behavior:
- Preserve save failure cause.
- Include retry context where transient retry guard was applied.

### Stale-lock suspicion guidance

Required message elements:
- Lock has not been confirmed stale automatically.
- Manual verification/release required via authoritative admin surface.

## Recovery Playbooks

Shared-contract boundary:
- The playbooks below define semantic operator guidance shared across modes and surfaces.
- Mode- or surface-specific instructions may elaborate but must not contradict these branches.

## Playbook A: Locked by active owner

1. Return skip/fail (per operation semantics) with owner info.
2. Advise retry window.
3. Do not mutate workflow.

11.4.5 native variant:
- Differentiate whether contention came from per-workflow tag, save/copy tag, or single-admin lock because the operator action differs.

## Playbook B: Save succeeded, untag failed

1. Return success payload including lock-release warning.
2. Instruct operator to verify lock state before next modify.
3. If next modify reports locked, perform manual unlock verification.

## Playbook C: Suspected stale lock

1. Confirm lock persists across retry interval.
2. Verify owner/session status externally.
3. Clear lock administratively if appropriate.
4. Re-run operation.

## Playbook D: Interrupted operation

1. Assume lock may still be held.
2. On next run, attempt normal tag path.
3. If locked, follow stale-lock playbook.

## Mode Notes

### HTTP

- Uses save transient retry guard for known status 139/disposed-context signature.
- Must still preserve lock lifecycle guarantees and post-save checks.

### COM

- Must preserve same lock ownership semantics even if transport signatures differ.
- Lock-owner value normalization (blank/whitespace) is required before eligibility decisions.
- Should map `Edit(ref msg)` / `a11_Edit` failures into the same user-facing lock contention contract used by HTTP mode.
- Should expose or emulate native lock-scope distinctions where the operation semantics differ materially.

### Mock

- Must emulate lock acquisition failure, cleanup warning, and contention messaging paths for regression coverage.
- Must preserve the same semantic contract even though the lock implementation is simulated.

## 11.4.5 Native-Specific Recovery Notes

- `CancelEdit()` should be treated as the native cleanup path when an edit session is abandoned before `Update()`.
- Delete/create/modify must never bypass native system-workflow guard behavior during lock-recovery handling.
- Native clients should periodically refresh lock-table state and prune stale local lock-owner caches.
- Save/copy contention should be surfaced separately from per-workflow edit contention.

## Surface/Mode Separation Checklist

1. Does HTTP preserve the same ownership, cleanup, and stale-lock semantics through its tag-based workflow API?
2. Does COM/native preserve the same semantic contract across edit-session, per-workflow, single-admin, and save locks?
3. Does Mock simulate contention, cleanup uncertainty, and stale-lock guidance rather than bypassing them?
4. Do CLI and Client surface the same semantic distinction between contention, cleanup warning, and hard failure?
5. Are native/HTTP mechanism differences kept distinct from the shared lock/recovery contract?

## Implementation Requirements

1. Keep `UntagAsync` as mandatory success-path step after post-save verification.
2. Keep failure-path cleanup as best-effort helper that does not hide primary errors.
3. Preserve explicit lock-owner message on modify lock contention.
4. Keep delete lock filtering deterministic and whitespace-safe.
5. Do not add implicit stale-lock auto-break behavior without explicit backend capability contract.
6. Preserve distinct lock scopes when COM/native semantics require them.
7. Refresh authoritative lock state and prune stale local lock caches before presenting ownership state.

## Operational Checklist

1. Modify on locked workflow returns non-mutating result with lock owner guidance.
2. Create/modify retain lock until verification completes.
3. Any post-lock failure attempts cleanup before exit.
4. Save-success + untag-fail surfaces warning (not silent).
5. Delete excludes locked workflows and reports no-op outcomes clearly.
6. Suspected stale locks route to manual verification playbook.
7. COM/native mode distinguishes workflow-edit contention from save/copy contention and single-admin contention.
8. Lock owner display is refreshed from authoritative lock-table state.

## Traceability

Primary implementation anchors:
- `WorkflowService.ModifySingleWorkflowAsync` lock acquisition and lock-owner skip path.
- `WorkflowService.CreateSingleWorkflowAsync` and `ModifySingleWorkflowAsync` success-path untag warnings.
- `WorkflowService.TryUntagAsync` best-effort cleanup helper.
- `WorkflowService.ApplyFilters` delete-time locked workflow exclusion.

Related architecture context:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md
- docs/architecture/tdn/tdn-workflow-save-boundary-and-canonicalization-contract.md

11.4.5 native evidence anchors:
- `nativemain/gui/WorkFlowAdmin.cpp`
- `nativemain/gui/WorkFlowAdmin.h`
- `nativemain/Core/CliWorkflowDefManager.cpp`
- `nativemain/Core/CliWorkflowDefManager.h`
- `nativemain/Core/ManagerUtils.cpp`
- `nativemain/Core/ManagerUtils.h`
- `nativemain/Core/nfmglobals.h`
- `nativemain/Core/AdeptErrorCodes.h`

## Open Questions

1. Should lock contention be modeled as `Skip` or `Fail` consistently across all workflow write operations?
2. Should runbooks include an explicit stale-lock triage flowchart for support teams?
3. If backend exposes lock-age metadata, what max-age threshold should classify stale-candidate state?
