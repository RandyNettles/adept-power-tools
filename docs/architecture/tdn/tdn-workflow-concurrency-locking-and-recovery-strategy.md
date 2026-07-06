# TDN - Workflow Concurrency, Locking, and Recovery Strategy

## Purpose

Define the architecture-level concurrency and lock lifecycle policy for workflow authoring operations, including:
- Edit-lock ownership model.
- Tag/untag lifecycle boundaries.
- Stale-lock handling and recovery expectations.
- User-facing messaging and operator guidance.

## Scope

In scope:
- Workflow create, modify, and delete operational concurrency behavior.
- Tag-based edit locking in `AdeptTools.Workflow`.
- Cleanup expectations for success, failure, and cancellation paths.
- Support/operations guidance for lock contention and suspected stale locks.

Out of scope:
- Non-workflow feature locking behavior.
- Backend database internal lock implementation details.
- Launcher visual design details not affecting operational semantics.

## Source Context

Primary references:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md
- docs/runbooks/Workflow-template-runbook.md
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Api/IWorkflowApiClient.cs
- src/AdeptTools.Workflow/Models/WorkflowEditModel.cs
- src/AdeptTools.Workflow/Models/WorkflowAdminItem.cs

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

## Concurrency Model

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

## Lock Ownership Contract

### Authoritative owner

- The active lock/tag owner is the actor that obtained editability for the current workflow.
- For modify, ownership is established by successful `TagAsync` with `BEditable=true`.
- For create, ownership is established by server create/edit session semantics.

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

## Lock Lifecycle and Cleanup Policy

### Success path

1. Acquire/hold lock.
2. Perform authoring and save.
3. Run post-save verification.
4. Attempt `UntagAsync`.
5. Return success; include cleanup warning if untag fails.

### Failure path

- On any failure after lock acquisition, attempt untag before returning failure.
- Cleanup attempt is best-effort and must not mask the original failure cause.

### Exception path

- Catch unexpected exceptions and attempt best-effort untag.
- Return failure with original exception context (not untag exception context).

### Cancellation path

- Treat cancellation as failure-to-complete with cleanup attempt.
- Attempt untag when cancellation occurs after lock acquisition.

## Delete Concurrency Policy

Delete never force-breaks locks.

Eligibility rules:
- Locked workflows are excluded from deletable candidates.
- Lock state is inferred from `LockedByDisplayName` presence.
- Blank/whitespace lock owner fields are treated as non-locked for eligibility filtering.

Operational behavior:
- If all candidates are locked or otherwise non-eligible, operation completes with empty/no-op semantics and operator guidance.

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

## Future capability gate

If backend later supports safe stale-lock metadata and force-release API:
- Enable controlled recovery only behind explicit capability flag.
- Require audit logging (who forced unlock, when, why, target workflow).
- Require explicit operator confirmation before force release.

## User Messaging Contract

Messages must be actionable and class-distinct.

### Lock contention message

Required content:
- Workflow name.
- Lock owner when available.
- Next action guidance (retry later or coordinate unlock).

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

## Playbook A: Locked by active owner

1. Return skip/fail (per operation semantics) with owner info.
2. Advise retry window.
3. Do not mutate workflow.

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

### Mock

- Must emulate lock acquisition failure, cleanup warning, and contention messaging paths for regression coverage.

## Implementation Requirements

1. Keep `UntagAsync` as mandatory success-path step after post-save verification.
2. Keep failure-path cleanup as best-effort helper that does not hide primary errors.
3. Preserve explicit lock-owner message on modify lock contention.
4. Keep delete lock filtering deterministic and whitespace-safe.
5. Do not add implicit stale-lock auto-break behavior without explicit backend capability contract.

## Operational Checklist

1. Modify on locked workflow returns non-mutating result with lock owner guidance.
2. Create/modify retain lock until verification completes.
3. Any post-lock failure attempts cleanup before exit.
4. Save-success + untag-fail surfaces warning (not silent).
5. Delete excludes locked workflows and reports no-op outcomes clearly.
6. Suspected stale locks route to manual verification playbook.

## Traceability

Primary implementation anchors:
- `WorkflowService.ModifySingleWorkflowAsync` lock acquisition and lock-owner skip path.
- `WorkflowService.CreateSingleWorkflowAsync` and `ModifySingleWorkflowAsync` success-path untag warnings.
- `WorkflowService.TryUntagAsync` best-effort cleanup helper.
- `WorkflowService.ApplyFilters` delete-time locked workflow exclusion.

Related architecture context:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md
- docs/architecture/tdn/tdn-workflow-save-boundary-and-canonicalization-contract.md

## Open Questions

1. Should lock contention be modeled as `Skip` or `Fail` consistently across all workflow write operations?
2. Should runbooks include an explicit stale-lock triage flowchart for support teams?
3. If backend exposes lock-age metadata, what max-age threshold should classify stale-candidate state?
