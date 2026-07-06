# TDN - Workflow Adept Power Tools Hardening Notes (Post-Incident)

## Purpose

This TDN captures Adept Power Tools hardening decisions that were intentionally removed from the Adept 12 AWC deep-dive to keep source-of-truth boundaries clear.

This TDN also makes mode and surface hardening boundaries explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Boundary Model

This TDN has two contract layers.

### Layer 1: Shared hardening contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- The semantic defect being prevented.
- The required hardening invariants.
- The expected safe end state after create/modify flows.

Does not define:
- Exact transport shape.
- Exact UI presentation.
- Mode-specific adapter mechanics beyond what is needed to preserve the invariant.

### Layer 2: Mode and surface realization

Applies to:
- Mode-specific persistence/write-path behavior.
- CLI and Client orchestration/presentation behavior.

Defines:
- Where HTTP/COM/Mock realization differs.
- Which surface owns which user interaction or result-shaping responsibilities.

Does not redefine:
- The hardening invariant itself.
- The safe expected final workflow state.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode is the primary current implementation surface for the incident and fix described here.

HTTP-specific characteristics:
- Step mapping and notification ownership are realized through `WorkflowEditModel` orchestration.
- Save behavior may involve transient HTTP failure signatures that require bounded retry.

Boundary rule:
- HTTP-specific retry or response-order handling may differ, but it must preserve the same step-local notification ownership and no-cross-step-leakage invariant.

### COM mode

COM mode realizes the same hardening invariant through native/Desktop object-graph write-back behavior.

COM-specific characteristics:
- Persistence occurs through `CCliWorkflowDefManager::Update()` and child-list `Write()` methods.
- Native notification/trustee add lists already embody some dedupe/ownership behavior.

Boundary rule:
- COM/native paths must preserve the same safe final state even when the mechanism is object-graph commit rather than HTTP full-snapshot save.

### Mock mode

Mock mode is a simulation/test path for hardening behavior, not a production backend.

Mock-specific characteristics:
- Should emulate step-order, notification-scope, and persistence-mismatch scenarios deterministically.

Boundary rule:
- Mock must simulate the hardening contract accurately and must not silently hide cross-step leakage scenarios.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns command-driven orchestration and textual reporting of workflow hardening outcomes.

CLI contract:
- Must preserve the shared hardening semantics when invoking create/modify flows.
- May expose warnings, summaries, and diagnostics in script-friendly form.

### Client surface

Client owns interactive workflow authoring orchestration and presentation of hardening-relevant outcomes.

Client contract:
- Must preserve the same step-local notification ownership and safe final-state semantics.
- May present richer progress or error UX without changing the hardening behavior.

Boundary rule:
- Surface differences may affect interaction style, but not the semantic hardening result.

## COM Path (11.4.5)

The provided Adept 11.4.5 evidence adds a second hardening reference path beyond HTTP/AWC save behavior:
- Native admin CRUD persists through `CCliWorkflowDefManager::Update()` and child-list `Write()` methods.
- Notification and trustee dedupe/ownership rules are enforced in native add/write list layers as well as HTTP-mode mapping code.

11.4.5-specific implication:
- Hardening changes in Adept Power Tools should be evaluated against both HTTP full-snapshot persistence and native object-graph write-back semantics.

Mode/surface implication:
- COM path evidence qualifies mode-specific realization; it does not replace the shared hardening contract that both CLI and Client must preserve.

## Incident Summary

Observed behavior during Adept Power Tools workflow create flow:

- Draft step had Designers as Notify.
- Review step had reviewers plus Alert recipients only.
- After create, Review incorrectly showed Designers as Notify.

## Corrective Strategy

Shared-contract boundary:
- The corrective strategies below describe semantic hardening requirements that apply across all modes and both surfaces.
- Mode-specific realization differences are allowed only if they preserve these same outcomes.

### 1. Deterministic Step Targeting

Adept Power Tools correlates input steps to server step models by active step order (WorkflowStepDefinition.Order), not incidental array order from server responses.

### 2. Step-Scoped Notification Persistence

Adept Power Tools treats per-step notification lists as the only source for step notify and alert recipients.

- Step notify: EmailNotificationList
- Step alert: AlertNotificationList

Top-level workflow notification lists are cleared in Adept Power Tools create/modify paths to avoid cross-scope duplication in mixed-processing environments.

### 3. StepId Congruence Guard

Adept Power Tools normalizes notification rows so only entries with StepId matching the owning step are retained.

### 4. Transient Save Retry Guard

Adept Power Tools retries save once for known transient HTTP failure signatures:

- StatusCode = 139
- Error contains Cannot access a disposed context instance
- Error contains MsSqlDatabaseContext

Mode boundary:
- This retry guard is HTTP-specific implementation behavior, not a shared hardening invariant required of COM or Mock.

## Why This Is Correct

- AWC structural step operations are full-model replacement boundaries.
- Durable identity requires stable step identity (step order plus step id), not returned list position.
- Notify and alert semantics are safest when step-local and congruent by StepId.

Shared-boundary note:
- These rationale statements describe the invariant the system must preserve regardless of HTTP, COM, Mock, CLI, or Client realization details.

## Verification Coverage Added

- Two-step create where only step 1 has Notify.
- Two-step modify where only step 1 has Notify.
- Create with out-of-order server step collection.
- Save transient retry behavior for status 139 disposed-context signature.

Mode/surface note:
- Current listed coverage is strongest for HTTP-oriented service behavior and shared domain invariants; it does not by itself prove equivalent COM/native or Client-surface parity.

## Operational Expectation

- Draft retains Designers notify once.
- Review has no notify recipients unless explicitly declared.

Surface/mode boundary:
- This expected end state is shared across modes and both surfaces even when the workflow is authored through different adapters or interaction models.

## 11.4.5 Native-Specific Hardening Notes

- Native notification add paths dedupe email by lowercase email, Approvers by type, and other recipients by `(type, id)`.
- Native delete/update commit paths carry side effects beyond row replacement, so hardening around canonical read-back and scope ownership remains necessary when aligning ATP with 11.4.5 behavior.

## Surface/Mode Separation Checklist

1. Does HTTP preserve step-local notification ownership even when server step ordering is incidental?
2. Does COM/native preserve the same final recipient ownership after `Update()` write-back?
3. Does Mock simulate the same leakage-prevention invariant rather than bypassing it?
4. Do both CLI and Client surface the same semantic success/failure outcome for this hardening rule?
5. Are HTTP-specific behaviors, such as transient save retry, kept distinct from shared hardening invariants?
