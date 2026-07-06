# TDN - Workflow Identity and Mapping Invariants

## Purpose

This TDN defines non-negotiable identity and mapping invariants for workflow create/modify operations in Adept Power Tools.

It specifically governs:
- Step identity and step mapping behavior.
- Trustee identity fidelity.
- Notification recipient identity fidelity.
- Acceptable matching and aliasing behavior.

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

## Identity Model

### Step identity

- Durable step identity is step ID plus canonical step ordering context.
- Array position in server response is not a durable identity.
- Step names are normalized for correlation/verification comparisons.

### Trustee identity

- Reviewer trustees are identity tuples of (type, trusteeId).
- For User trustees, persisted trusteeId must remain identity-congruent with downstream AWC target identity expectations.
- Reviewer trustee types are constrained to User, Group, or Key.

### Notification identity

- Notification recipients are identity tuples of (targetType, targetId) for non-email recipients.
- Email notification recipients are identity tuples keyed by normalized email address.
- Approvers recipient is identity-by-type (no required ID payload).

## Non-Negotiable Invariants

1. Step mapping must not depend on incidental server array ordering.
2. Reviewer trustee type must be one of User, Group, Key; any other type is invalid for reviewer role.
3. Step-level notification rows must remain step-bound (StepId congruence required).
4. Workflow save success is not final until post-save identity verification passes.
5. User trustee IDs must preserve canonical persisted form expected by AWC identity rehydration.
6. Duplicate notification recipients must be collapsed by canonical identity key before save.
7. Invalid notify/alert recipient rows must not be persisted silently as successful intent.

## Step Mapping Contract

### Create/Modify correlation

- Input steps are mapped to active workflow steps using deterministic order semantics.
- Correlation and persistence checks use normalized step names for lookup.
- Deleted steps are excluded from active verification scope.

### Why

This prevents cross-step leakage when server step collections are returned out of incidental order.

## Reviewer Trustee Mapping Contract

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

### Step ownership

- Email notify recipients are owned by step EmailNotificationList.
- Alert notify recipients are owned by step AlertNotificationList.
- Notifications are filtered to exact StepId congruence before save.

### Deduplication

Recipients are deduplicated by canonical key:
- Email: E:<normalized-email>
- Approvers: type-only key
- Other recipients: <type>:<normalized-id>

### Validation

- Approvers is always valid for notification roles.
- Email requires syntactically valid non-empty email value.
- Non-email recipients require non-empty ID.

If all provided notify/alert rows on a step are invalid, operation fails with actionable validation output.

### Post-save notification verification

- Expected alert and email recipient identities are computed from input by step.
- Persisted notification identities are read from saved model by step and list type.
- Missing expected recipients cause operation failure.

## Acceptable Matching Behavior

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

Hard failure conditions include:
- Invalid reviewer trustee type.
- Duplicate step names after normalization.
- Missing step during persistence verification.
- Missing reviewer identities after save.
- Missing notification identities after save.
- Identity mapping ambiguity that cannot be resolved deterministically.

Warnings are allowed only for non-identity concerns (for example visibility context warnings), not for identity mismatch.

## Mode Notes

### HTTP

- Supports user-directory-backed identity alias resolution.
- Requires canonical persisted User IDs to remain congruent with AWC user identity expectations.

### COM

- Must enforce the same invariants even if lower-level signatures differ.
- Unsupported capabilities must fail fast without weakening identity guarantees.

### Mock

- Must emulate contract semantics for identity verification and mismatch failures.
- Mock success must not bypass invariant checks.

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
