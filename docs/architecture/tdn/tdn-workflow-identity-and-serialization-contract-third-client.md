# TDN - Workflow Identity and Serialization Contract (Third Client)

## Purpose

This TDN defines a stable, implementation-ready identity and serialization contract for a third Adept client that supports:
- Create workflow
- Modify workflow
- Delete workflow

It is derived from observed AdeptWebClient behavior and TaskPane command payload patterns.

## Scope and Non-Goals

In scope:
- Workflow admin payload identity fields (workflow, step, trustee, notification).
- Enum/value wire format expectations.
- Validation and normalization rules before save.
- Edge-case handling for persisted recipients.

Out of scope:
- Runtime workflow command execution UI (Approve/Reject/Reroute dialogs).
- User account management semantics beyond workflow recipient lookup.

## Canonical Identity Model

Use these canonical identifiers in the client domain model:
- workflowId: string identifier.
- stepId: string identifier.
- targetType: numeric enum (WORKFLOW_USER_TYPE).
- targetId: string identifier for non-email targets.
- email: string for email recipients.
- trusteeId: string identifier in trustee list rows.

Identity rules:
- Non-email recipients are identified by tuple: (targetType, targetId).
- Email recipients are identified by tuple: (targetType = WFUT_EMAIL, email).
- WFUT_APPROVERS is a synthetic target type and may persist with null targetId.

## Wire Enum Contract

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

## Recipient-to-Payload Mapping

### Trustee list rows (step reviewer permissions)

When a user selects step reviewers, map each selected target to trustee row:
- workflowId <- current step.workflowId
- stepId <- current step.stepId
- type <- selected.targetType
- trusteeId <- selected.targetId
- flags <- 0 unless future server contract requires otherwise

Constraint:
- Reviewer trustees should only persist types with ID-backed targets (KEY/GROUP/USER as supported by server data).

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

## CRUD Payload Requirements

### Create workflow

Required minimum before POST:
- Non-empty workflow name.
- At least one step.
- First-step reference valid.
- Step IDs and workflow ID present if server create-new bootstrap model requires round-trip.

Serialize full WorkflowEditModel snapshot to create endpoint.

### Modify workflow

Use full replacement/update payload style:
- Send complete WorkflowEditModel snapshot.
- Include updated step definitions, trustee rows, and notification rows.
- Include IDs for unchanged entities to avoid accidental recreation.

### Delete workflow

Delete by workflowId only.
Client should treat system/protected workflows as non-deletable when server marks them protected.

## Normalization and Validation Rules

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

## Enum/String Transformation Rules

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
