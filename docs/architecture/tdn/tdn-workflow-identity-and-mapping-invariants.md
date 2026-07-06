# TDN - Workflow Identity and Mapping Invariants

## Purpose

This TDN defines non-negotiable identity and mapping invariants for workflow create/modify operations in Adept Power Tools.

It specifically governs:
- Step identity and step mapping behavior.
- Trustee identity fidelity.
- Notification recipient identity fidelity.
- Acceptable matching and aliasing behavior.

This TDN also makes mode and surface boundaries explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Scope

In scope:
- Workflow step correlation during create/modify.
- Trustee and notification persistence verification semantics.
- User identity alias handling where capability allows directory lookup.
- Failure behavior when invariants are violated.

Out of scope:
- UI picker behavior in external clients.
- Share/container mutation details (covered elsewhere).
- Delete/list filtering behavior.

## Boundary Model

This TDN has two contract layers.

### Layer 1: Shared identity and mapping invariant contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- The non-negotiable identity and mapping invariants.
- Acceptable and unacceptable matching behavior.
- Failure conditions when those invariants are violated.

Does not define:
- Exact adapter mechanics.
- Exact UI picker behavior.
- Mode-specific transport or storage details beyond preserving the same invariants.

### Layer 2: Mode and surface realization

Applies to:
- Mode-specific persistence and identity-resolution behavior.
- CLI and Client orchestration/presentation behavior.

Defines:
- Where HTTP/COM/Mock realization differs.
- Where CLI and Client may differ in interaction style.

Does not redefine:
- The invariant set itself.
- The failure meaning when invariants are violated.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode is the primary current ATP implementation surface for persisted identity congruence with AWC-facing behavior.

HTTP-specific characteristics:
- User-directory-backed alias resolution may be available.
- HTTP/AWC identity expectations strongly influence canonical persisted user-id form.

Boundary rule:
- HTTP-specific identity lookup or save mechanics may differ, but they must preserve the same non-negotiable identity invariants defined in this TDN.

### COM mode

COM mode must preserve the same invariants through native/Desktop object-graph and write-back behavior.

COM-specific characteristics:
- Native persistence writes trustee and notification identities through WF/WFTR/NOTIFY object-graph commit paths.
- Some HTTP-specific helper capabilities may not exist.

Boundary rule:
- COM/native realization may differ in mechanism, but it must not weaken the invariant set or silently downgrade identity failure conditions.

### Mock mode

Mock mode is a deterministic simulation path for identity and mapping behavior.

Mock-specific characteristics:
- May emulate persistence verification and mismatch scenarios for testability.

Boundary rule:
- Mock success must still respect the same identity invariants and must not simulate away invariant violations as success.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns command-driven execution and textual reporting of identity/mapping failures.

CLI contract:
- Must preserve the same invariant set and failure semantics.
- May surface diagnostics and missing-identity detail in script-friendly form.

### Client surface

Client owns interactive workflow authoring orchestration and presentation of identity-sensitive outcomes.

Client contract:
- Must preserve the same invariant set and failure semantics.
- May present richer picker/review UX without changing the meaning of identity success or failure.

Boundary rule:
- Surface differences may affect interaction style, but not the meaning of identity congruence or invariant violation.

## COM Path (11.4.5)

For Adept 11.4.5, these invariants must also hold on the native Desktop/Core admin route:
- `CWorkFlowAdmin` entry points open editor flows.
- `CCliWorkflowDefManager::a11_Edit(workflowId, msg)` establishes edit ownership for modify.
- `CCliWorkflowDefList::Add/Delete` and child list `Write()` methods persist WF/WFSTEP/WFTR/NOTIFY state.

11.4.5-specific implication:
- Identity guarantees cannot rely on HTTP controller semantics because the provided Adept 11.4.5 admin path is object-graph mutation plus native write-back.

Mode/surface implication:
- COM-path evidence qualifies native/Desktop realization; it does not replace the shared identity/mapping invariant contract both CLI and Client must preserve.

## Identity Model

Shared-contract boundary:
- The identity model below is the shared semantic contract across modes and both surfaces.
- Mode-specific notes qualify realization details, not the invariant meaning.

### Step identity

- Durable step identity is step ID plus canonical step ordering context.
- Array position in server response is not a durable identity.
- Step names are normalized for correlation/verification comparisons.

### Trustee identity

- Reviewer trustees are identity tuples of (type, trusteeId).
- For User trustees, persisted trusteeId must remain identity-congruent with downstream AWC target identity expectations.
- Reviewer trustee types are constrained to User, Group, or Key.

11.4.5 native note:
- Native trustee persistence writes `WFTR` trustee id plus type; the same `(type, trusteeId)` invariant must survive COM write-back.

### Notification identity

- Notification recipients are identity tuples of (targetType, targetId) for non-email recipients.
- Email notification recipients are identity tuples keyed by normalized email address.
- Approvers recipient is identity-by-type (no required ID payload).

11.4.5 native note:
- Native notification rows also carry durable notification id, workflow object id, email, and ename fields in `NOTIFY`.

## Non-Negotiable Invariants

Shared-contract boundary:
- All invariants below are shared across HTTP, COM, Mock, CLI, and Client.
- Mode or surface differences must not weaken these rules.

1. Step mapping must not depend on incidental server array ordering.
2. Reviewer trustee type must be one of User, Group, Key; any other type is invalid for reviewer role.
3. Step-level notification rows must remain step-bound (StepId congruence required).
4. Workflow save success is not final until post-save identity verification passes.
5. User trustee IDs must preserve canonical persisted form expected by AWC identity rehydration.
6. Duplicate notification recipients must be collapsed by canonical identity key before save.
7. Invalid notify/alert recipient rows must not be persisted silently as successful intent.
8. Native and HTTP save paths must preserve the same recipient dedupe semantics.

## Step Mapping Contract

### Create/Modify correlation

- Input steps are mapped to active workflow steps using deterministic order semantics.
- Correlation and persistence checks use normalized step names for lookup.
- Deleted steps are excluded from active verification scope.

### Why

This prevents cross-step leakage when server step collections are returned out of incidental order.

11.4.5 native qualification:
- Native persistence is object-graph-driven, but step ownership invariants still apply because trustee/notification rows are written under step/workflow owner IDs.

Mode boundary:
- The explanation above describes the shared invariant; native qualification explains one realization context, not a different rule.

## Reviewer Trustee Mapping Contract

Shared-contract boundary:
- The reviewer mapping rules below are invariant requirements across all modes and both surfaces.

### Allowed reviewer types

- User
- Group
- Key

### Disallowed reviewer types

- Email
- Approvers

Any disallowed reviewer type is a hard validation error.

### Post-save reviewer verification

- Expected reviewer identities are computed from input by step.
- Persisted reviewer identities are read from saved step trustee definitions.
- Missing expected reviewer identities cause operation failure.

## Notification Mapping Contract

Shared-contract boundary:
- The notification mapping rules below are invariant requirements across all modes and both surfaces.

### Step ownership

- Email notify recipients are owned by step EmailNotificationList.
- Alert notify recipients are owned by step AlertNotificationList.
- Notifications are filtered to exact StepId congruence before save.

### Deduplication

Recipients are deduplicated by canonical key:
- Email: E:<normalized-email>
- Approvers: type-only key
- Other recipients: <type>:<normalized-id>

11.4.5 native note:
- This matches observed native add-list behavior: email dedupe by lowercase email, Approvers by type only, other recipients by `(type, id)`.

### Validation

- Approvers is always valid for notification roles.
- Email requires syntactically valid non-empty email value.
- Non-email recipients require non-empty ID.

If all provided notify/alert rows on a step are invalid, operation fails with actionable validation output.

Mode/surface boundary:
- Modes and surfaces may differ in where or how validation is surfaced, but not in whether these rows are considered valid or blocking.

### Post-save notification verification

- Expected alert and email recipient identities are computed from input by step.
- Persisted notification identities are read from saved model by step and list type.
- Missing expected recipients cause operation failure.

## Acceptable Matching Behavior

Shared-contract boundary:
- Matching rules below are semantic allowances shared across all modes and both surfaces.

### Case sensitivity

- ID matching is case-insensitive for persistence verification.

### User alias matching

Alias-aware matching is acceptable for User identities only when user directory lookup capability is available.

Acceptable User alias set members include:
- UserId
- NotificationTargetId

Matching rule:
- A User identity is considered matched when expected and actual alias sets overlap.

### Type matching

- Reviewer verification requires type match.
- Notification verification enforces type semantics; Approvers matches by type alone.

## Unacceptable Matching Behavior

1. Matching reviewer identities by display name only.
2. Matching steps by array index alone across save boundaries.
3. Converting User trustee IDs between canonical formats without explicit verified mapping.
4. Treating unresolved User identities as successful persistence.

## Canonical User ID Resolution Contract

When User trustee input requires normalization:
1. Resolve against user directory/matcher capability.
2. Prefer canonical persisted ID form used by server/user directory for notification targets.
3. Preserve canonical ID on merge paths; do not overwrite canonical IDs with legacy aliases when a canonical value already exists.

## Failure Policy

Shared-contract boundary:
- Failure semantics below are invariant across all modes and both surfaces.

Hard failure conditions include:
- Invalid reviewer trustee type.
- Duplicate step names after normalization.
- Missing step during persistence verification.
- Missing reviewer identities after save.
- Missing notification identities after save.
- Identity mapping ambiguity that cannot be resolved deterministically.

Warnings are allowed only for non-identity concerns (for example visibility context warnings), not for identity mismatch.

Surface/mode boundary:
- CLI and Client may present identity failures differently, and HTTP/COM/Mock may discover them through different mechanisms, but identity mismatch remains a hard failure in all cases.

## Mode Notes

### HTTP

- Supports user-directory-backed identity alias resolution.
- Requires canonical persisted User IDs to remain congruent with AWC user identity expectations.

### COM

- Must enforce the same invariants even if lower-level signatures differ.
- Unsupported capabilities must fail fast without weakening identity guarantees.
- Native `Edit`/`Update` flows must preserve unresolved rows and not silently discard recipient identities on reload.

### Mock

- Must emulate contract semantics for identity verification and mismatch failures.
- Mock success must not bypass invariant checks.

## Surface/Mode Separation Checklist

1. Does HTTP preserve the same identity invariants while using AWC-congruent user-id expectations and directory lookup behavior?
2. Does COM/native preserve the same invariants through object-graph write-back and native persistence structures?
3. Does Mock simulate identity mismatch and verification failure semantics rather than bypassing them?
4. Do CLI and Client surface the same semantic identity failures even if diagnostics and UX differ?
5. Are mode-specific lookup/write mechanics kept distinct from the shared invariant set itself?

## Implementation Traceability

Primary source context:
- docs/architecture/tdn/tdn-workflow-adept-power-tools-share-and-trustee-identity.md
- src/AdeptTools.Workflow/Services/WorkflowService.cs

Representative implementation anchors:
- deterministic step mapping via workflow step order handling
- ConfigureStep reviewer/notification role mapping and StepId congruence filtering
- ValidateReviewerTrusteePersistenceAsync
- ValidateNotificationTrusteePersistenceAsync
- FindMissingTrusteesAsync alias-aware matching

## Regression Checklist

1. Out-of-order server step collections do not produce cross-step notification leakage.
2. Reviewer trustees with invalid types fail before save.
3. WFUT_USER trustees persist in canonical IDs expected by AWC rehydration.
4. Notification rows remain bound to owning step IDs after save.
5. Missing persisted reviewers fail operation with explicit missing identity list.
6. Missing persisted notify/alert recipients fail operation with explicit missing identity list.
7. Alias-aware user matching works only when directory lookup capability is enabled.
