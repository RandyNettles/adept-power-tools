# TDN - Workflow Identity and Serialization Contract (Third Client)

## Purpose

This TDN defines a stable, implementation-ready identity and serialization contract for a third Adept client that supports:
- Create workflow
- Modify workflow
- Delete workflow

It is derived from observed AdeptWebClient behavior and TaskPane command payload patterns.

This TDN also makes mode and surface boundaries explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Scope and Non-Goals

In scope:
- Workflow admin payload identity fields (workflow, step, trustee, notification).
- Enum/value wire format expectations.
- Validation and normalization rules before save.
- Edge-case handling for persisted recipients.

Out of scope:
- Runtime workflow command execution UI (Approve/Reject/Reroute dialogs).
- User account management semantics beyond workflow recipient lookup.

## Boundary Model

This TDN has two contract layers.

### Layer 1: Shared identity/serialization contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- Canonical workflow/step/trustee/notification identity fields.
- Allowed enum/value serialization semantics.
- Required normalization, validation, and edge-case behavior.

Does not define:
- Exact transport API shapes for every backend.
- Exact UI authoring flow.
- Mode-specific persistence mechanics beyond preserving the same semantic contract.

### Layer 2: Mode and surface realization

Applies to:
- Mode-specific persistence and serialization behavior.
- CLI and Client orchestration/presentation behavior.

Defines:
- Where HTTP/COM/Mock realization differs.
- Where CLI and Client may differ in interaction style.

Does not redefine:
- The shared identity/serialization contract itself.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode is the primary current ATP implementation surface for JSON-based workflow identity and serialization behavior.

HTTP-specific characteristics:
- Uses numeric ASCII-code enum values in JSON payloads.
- Uses full edit-model snapshot style save behavior.
- AWC/Web API-oriented identity expectations strongly influence current ATP behavior.

Boundary rule:
- HTTP-specific transport shape may differ, but it must preserve the shared identity, enum, and validation contract.

### COM mode

COM mode realizes the same contract through native/Desktop object-graph mutation and table-write behavior.

COM-specific characteristics:
- Persists enum values as character-coded fields through native write paths.
- Uses manager/list edit-session and `Update()` commit behavior rather than HTTP POST.

Boundary rule:
- COM/native realization may differ in transport and storage mechanics, but it must preserve the same semantic identity, enum, and normalization contract.

### Mock mode

Mock mode is a deterministic simulation path for authoring and persistence behavior.

Mock-specific characteristics:
- May simulate payloads, identity verification, and edge-case behavior for testing.

Boundary rule:
- Mock mode must simulate the same identity/serialization rules and must not silently weaken validation or invariant expectations.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns command-driven execution and textual reporting of workflow authoring and serialization outcomes.

CLI contract:
- Must preserve the same identity, serialization, and validation semantics.
- May surface diagnostics and warnings in script-friendly form.

### Client surface

Client owns interactive workflow authoring orchestration and presentation of identity/serialization-sensitive outcomes.

Client contract:
- Must preserve the same identity, serialization, and validation semantics.
- May use richer authoring UX without changing the underlying contract.

Boundary rule:
- Surface differences may affect interaction style, but not the meaning of persisted identity, enum values, or validation/pass-fail behavior.

## COM Path (11.4.5)

Adept 11.4.5 workflow admin CRUD is Desktop/Core-first, not WebAPI-admin-first.

Observed native route:
- Create: `CWorkFlowAdmin::OnBnClickedAddWF()` -> `CCliWorkflowDefList::Add(name)` -> editor mutates in-memory workflow graph -> `CCliWorkflowDefManager::Update()` -> child list `Write()` methods persist WF/WFSTEP/WFTR/NOTIFY state.
- Modify: `CWorkFlowAdmin::OnBnClickedEditWF()` -> `CCliWorkflowDefManager::a11_Edit(workflowId, msg)` -> editor mutates in-memory graph -> `Update()`.
- Delete: `CWorkFlowAdmin::OnBnClickedDeleteWF()` -> `CCliWorkflowDefList::Delete(workflowId)` -> `Update()`.

Interop-visible route in this codebase:
- `Project.WorkflowDefManager.Edit(ref msg)`
- mutate workflow object graph
- `Project.WorkflowDefManager.Update()`
- optional `CancelEdit()`

11.4.5-specific implications:
- There is no dedicated AdeptWeb admin workflow CRUD controller in the provided Adept 11.4.5 slice.
- Native persistence writes enum fields as character codes; JSON bridges should still use numeric ASCII-code values when introducing a third client.
- Delete side effects are part of the native write path, not a separate cleanup API.

Mode/surface implication:
- COM-path evidence qualifies native/Desktop realization; it does not replace the shared identity/serialization contract both CLI and Client must preserve.

## Canonical Identity Model

Shared-contract boundary:
- The identity model below is the shared semantic contract across modes and both surfaces.
- Mode-specific notes qualify realization details, not the identity meaning itself.

Use these canonical identifiers in the client domain model:
- workflowId: string identifier.
- stepId: string identifier.
- notificationId: string identifier when notification rows are persisted individually.
- workflowObjectId: string identifier for notification owner scope (workflow id or step id).
- targetType: numeric enum (WORKFLOW_USER_TYPE).
- targetId: string identifier for non-email targets.
- email: string for email recipients.
- ename: display name text for notification rows.
- trusteeId: string identifier in trustee list rows.

Identity rules:
- Non-email recipients are identified by tuple: (targetType, targetId).
- Email recipients are identified by tuple: (targetType = WFUT_EMAIL, email).
- WFUT_APPROVERS is a synthetic target type and may persist with null targetId.

## Wire Enum Contract

Shared-contract boundary:
- The enum contract below is shared across modes and both surfaces.
- HTTP and COM may realize it through different wire/storage mechanics while preserving the same semantic values.

Workflow enums on Web API-oriented save paths use numeric values (ASCII-code style), not symbolic letters.

WORKFLOW_USER_TYPE:
- WFUT_UNDEFINED = 32
- WFUT_KEY = 75
- WFUT_GROUP = 71
- WFUT_USER = 85
- WFUT_EMAIL = 69
- WFUT_APPROVERS = 65

ON_ACTION_NOTIFY:
- OAN_UNDEFINED = 32
- OAN_APPROVE = 65
- OAN_TIMEOUT = 84

Persistence requirement:
- Serialize enum values as numbers in JSON payloads.
- Do not serialize symbolic labels (for example, "WFUT_USER" or "U") in API payloads.

11.4.5 COM/native note:
- Native code persists the same values as character-coded enum fields (`'U'`, `'G'`, `'K'`, `'E'`, `'A'`, `'T'`) via table write paths.
- Numeric JSON values remain the correct transport bridge representation because they match the same ASCII codes used by native persistence.

Mode boundary:
- Numeric JSON values are an HTTP/bridge realization detail; the shared contract is the semantic enum identity itself.

## Recipient-to-Payload Mapping

Shared-contract boundary:
- The recipient mapping rules below are semantic contract requirements across modes and both surfaces.
- Backend modes may realize them through different payload or object-graph shapes.

### Trustee list rows (step reviewer permissions)

When a user selects step reviewers, map each selected target to trustee row:
- workflowId <- current step.workflowId
- stepId <- current step.stepId
- type <- selected.targetType
- trusteeId <- selected.targetId
- flags <- 0 unless future server contract requires otherwise

Constraint:
- Reviewer trustees should only persist types with ID-backed targets (KEY/GROUP/USER as supported by server data).

11.4.5 COM/native note:
- Native trustee add/write path is `CCliWorkflowTrusteeDefList::Add(trusteeId, targetType)` followed by `Write()` of step id, trustee id, and type.

### Notification rows (email/alerts)

Map selected recipients to notification row:
- workflowObjectId <- stepId or workflowId depending on list owner
- action <- OAN_APPROVE (transition notifications) or OAN_TIMEOUT (alerts)
- targetType <- selected.targetType
- targetId <- selected.targetId (or null for APPROVERS)
- email <- selected.email when targetType is WFUT_EMAIL; otherwise optional
- ename <- display name text for UI readability

Special synthetic default:
- If notification is enabled and list is empty, client may insert:
  - targetType = WFUT_APPROVERS
  - targetId = null
  - email = null
  - ename = localized "All Reviewers" label

11.4.5 COM/native note:
- Native notification add path is `CCliWorkflowNtfList::Add(targetType, targetId, email, ename)`.
- Native write path persists notification id, workflow object id, action, target type, target id, email, and ename.
- Native add logic deduplicates email by lowercase email, Approvers by type only, and other recipients by `(type, id)`.

Mode/surface boundary:
- Modes and surfaces may differ in how recipients are authored or displayed, but not in the semantic mapping rules for persisted recipients.

## CRUD Payload Requirements

Shared-contract boundary:
- The CRUD requirements below define semantic requirements shared across modes and both surfaces.
- HTTP and COM may express them through different mutation mechanisms.

### Create workflow

Required minimum before POST:
- Non-empty workflow name.
- At least one step.
- First-step reference valid.
- Step IDs and workflow ID present if server create-new bootstrap model requires round-trip.

Serialize full WorkflowEditModel snapshot to create endpoint.

11.4.5 COM/native note:
- Native create enters manager/list edit flow, adds workflow object graph entries, and commits with `Update()` rather than POSTing an HTTP edit model.

### Modify workflow

Use full replacement/update payload style:
- Send complete WorkflowEditModel snapshot.
- Include updated step definitions, trustee rows, and notification rows.
- Include IDs for unchanged entities to avoid accidental recreation.

11.4.5 COM/native note:
- Native modify uses in-memory object-graph mutation after `a11_Edit(workflowId, msg)` acquires workflow-level edit ownership.
- Preserve unchanged IDs in the object graph before `Update()` so native `Write()` paths update instead of recreating rows.

### Delete workflow

Delete by workflowId only.
Client should treat system/protected workflows as non-deletable when server marks them protected.

11.4.5 COM/native note:
- Native delete is `CCliWorkflowDefList::Delete(workflowId)` plus final `Update()`.
- Native write path also repoints `LIBWF`, resets `FIL.DEFWFID`, removes `NOTIFY` rows for deleted workflow objects, and reconciles active docs in workflow.

## Normalization and Validation Rules

Shared-contract boundary:
- The normalization and validation rules below are shared across modes and both surfaces.
- Mode-specific mechanics may affect how or where they are enforced, but not their semantic pass/fail meaning.

Run these rules before save:
- Trim text fields used as identifiers or emails.
- Deduplicate recipients:
  - Non-email: unique by (targetType, targetId).
  - Email: unique by lowercase(email).
- Validate email syntax for WFUT_EMAIL entries.
- Reject entries where:
  - targetType is ID-backed but targetId is missing.
  - targetType is WFUT_EMAIL and email is missing.
- Allow targetId null only when targetType is WFUT_APPROVERS.
- Keep enum values numeric in outgoing model.

Recommended integrity checks:
- Every trustee row stepId must exist in step definitions.
- Every notification.workflowObjectId must map to a valid workflow/step object depending on list type.

11.4.5 COM/native additions:
- Treat system workflow as immutable for edit/delete.
- If active documents exist in the workflow being deleted, require explicit user confirmation before commit.

Surface/mode boundary:
- CLI and Client may present validation and confirmation differently, and HTTP/COM/Mock may enforce via different mechanics, but the blocking-versus-allowed distinction is shared.

## Enum/String Transformation Rules

Shared-contract boundary:
- Transformation rules below are semantic constraints across all modes and both surfaces.

Where transformation is allowed:
- UI may display friendly labels (User, Group, All Reviewers, Approve, Timeout).
- UI may use localized strings for display-only fields (for example, ename).

Where transformation is not allowed:
- Persisted API fields for targetType and action must remain numeric enums.
- ID fields (targetId, trusteeId, workflowId, stepId) must remain raw identifier strings.

Practical rule:
- Transform for display at the view-model boundary.
- Transform back to numeric enums and raw IDs before serialization.

## Edge Cases

Shared-contract boundary:
- Edge-case handling below is shared across all modes and both surfaces.
- Mode-specific notes qualify how the case appears, not whether it matters.

1. Unknown target on reload
- If saved targetId no longer exists in group/user caches, keep row in model and flag as unresolved in UI.
- Do not silently drop unresolved rows during save without user confirmation.

2. Email cache mismatch
- Resolve WFUT_EMAIL rows by email value, not targetId.

3. APPROVERS default behavior
- Only auto-insert APPROVERS row when notifications are enabled and recipient list is empty.
- If user adds explicit recipients, keep APPROVERS only if intentionally retained.

4. Mixed recipient types
- Do not merge USER and GROUP rows that share identical display names.
- Identity is type + ID (or email for WFUT_EMAIL), not display name.

5. Locked workflow by another admin
- Native modify path honors workflow-level edit lock behavior (`a11_Edit`).
- Surface lock owner/message and do not force overwrite.

Surface/mode boundary:
- CLI and Client may surface lock contention differently, and COM/native may detect it differently from HTTP, but the semantic requirement remains the same.

## Example Outbound Snippets

### Trustee row

```json
{
  "workflowId": "wf-123",
  "stepId": "step-10",
  "type": 85,
  "trusteeId": "user-42",
  "flags": 0
}
```

### Approve notification for explicit email

```json
{
  "notificationId": "",
  "workflowObjectId": "step-10",
  "action": 65,
  "targetType": 69,
  "targetId": "",
  "email": "approver@example.com",
  "ename": "approver@example.com"
}
```

### Approve notification for all reviewers

```json
{
  "notificationId": "",
  "workflowObjectId": "step-10",
  "action": 65,
  "targetType": 65,
  "targetId": null,
  "email": null,
  "ename": "All Reviewers"
}
```

## Cross-References

- docs/architecture/tdn/tdn-workflow-identity-and-mapping-invariants.md
- docs/architecture/tdn/tdn-workflow-save-boundary-and-canonicalization-contract.md
- docs/architecture/tdn/tdn-workflow-adept-power-tools-share-and-trustee-identity.md

## Reference Implementation Anchors

AdeptWebClient references (authoritative for admin workflow save behavior):
- Enum definitions and numeric values in apitypes.ts.
- Trustee/notification contract comments in workflowmodel.ts.
- Recipient mapping in workflow-step component and selector component.
- Save call path in workflow.service modify/create methods.

TaskPane references (command payload alignment and ID transport patterns):
- SelectionListXfer serialization in clsSelectionCommand static wrappers.
- Workflow ID/comment/permanent assignment in frmSetWorkflow.
- Schema row-to-model ID population in clsUtilities.

Use these anchors to verify regressions when Web API contract changes.

11.4.5 native anchors for COM/Desktop parity:
- `nativemain/gui/WorkFlowAdmin.cpp`
- `nativemain/Core/CliWorkflowDefManager.cpp`
- `nativemain/Core/CliWorkflowDefList.cpp`
- `nativemain/Core/CliWorkflowStepDefList.cpp`
- `nativemain/Core/CliWorkflowTrusteeDefList.cpp`
- `nativemain/Core/CliWorkflowNtfList.cpp`
- `nativemain/Core/AdeptApiTypes.h`

## Surface/Mode Separation Checklist

1. Does HTTP preserve the same identity and enum semantics while using JSON edit-model transport?
2. Does COM/native preserve the same identity and normalization semantics through object-graph and native table-write behavior?
3. Does Mock simulate serialization, dedupe, and validation rules rather than bypassing them?
4. Do CLI and Client preserve the same semantic validation, edge-case, and persisted-identity outcomes even if authoring UX differs?
5. Are mode-specific transport/storage details kept distinct from the shared identity/serialization contract itself?
