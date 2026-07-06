# TDN - Workflow Adept Power Tools Hardening Notes (Post-Incident)

## Purpose

This TDN captures Adept Power Tools hardening decisions that were intentionally removed from the Adept 12 AWC deep-dive to keep source-of-truth boundaries clear.

## Incident Summary

Observed behavior during Adept Power Tools workflow create flow:

- Draft step had Designers as Notify.
- Review step had reviewers plus Alert recipients only.
- After create, Review incorrectly showed Designers as Notify.

## Corrective Strategy

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

## Why This Is Correct

- AWC structural step operations are full-model replacement boundaries.
- Durable identity requires stable step identity (step order plus step id), not returned list position.
- Notify and alert semantics are safest when step-local and congruent by StepId.

## Verification Coverage Added

- Two-step create where only step 1 has Notify.
- Two-step modify where only step 1 has Notify.
- Create with out-of-order server step collection.
- Save transient retry behavior for status 139 disposed-context signature.

## Operational Expectation

- Draft retains Designers notify once.
- Review has no notify recipients unless explicitly declared.
