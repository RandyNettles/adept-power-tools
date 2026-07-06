# TDN: Backend Capability Matrix and Fallback Rules

- Status: Proposed
- Date: 2026-07-06
- Owners: Architecture, Backend, Client, CLI
- Related ADR: docs/architecture/adr/ADR-0002-runtime-backend-selection-via-interfaces-and-di.md
- Related PRD: docs/prd/prd.md

## Purpose

Define one authoritative backend capability matrix for HTTP, COM, and Mock modes, including fallback rules and fail-fast behavior.

This TDN is also the reference template for future mode-aware TDNs.

## Scope

In scope:
- Runtime backend modes: HTTP, COM, Mock.
- Product surfaces: CLI and Client (Launcher).
- Capability support level and fallback semantics.
- Error-handling posture (fail-fast vs graceful fallback).

Out of scope:
- Detailed endpoint contracts for each feature.
- UX copy details and localization text.
- Feature-level deep dives (covered by feature ADR/TDN docs).

## Decision Summary

1. Capability ownership is mode-first (HTTP/COM/Mock), then surface mapping (CLI/Client).
2. Every capability must declare one of: Supported, Unsupported, Partial, or Mock-only.
3. Unsupported mode paths must fail fast with explicit guidance; silent downgrade is not allowed unless listed as an approved fallback.
4. Surface parity is required at intent level, not always at interaction style.
5. Future TDNs that discuss runtime behavior must include explicit mode sections and a surface mapping section.

## Capability Support Levels

- Supported: Production-ready and contractually expected.
- Partial: Available with documented limitations.
- Unsupported: Intentionally unavailable; must fail fast.
- Mock-only: Available only in mock execution for tests/demos.

## Backend Capability Matrix (Authoritative)

| Capability Area | HTTP | COM | Mock | Notes |
|---|---|---|---|---|
| Authentication | Supported | Supported | Mock-only | HTTP uses API/SSO; COM uses SDK/session; Mock returns deterministic test behavior. |
| Session Resume Persistence | Supported | Unsupported | Partial | HTTP persisted secure session resume is implemented; COM remains process/session bound. |
| Workflow List/Create/Modify/Delete | Supported | Supported | Partial | Core operations available in both production backends; mock may simulate subsets. |
| Workflow Share/Unshare Mutation | Supported | Unsupported | Partial | COM path is explicit fail-fast for share-state mutation. |
| Trustee/Notification Persistence | Supported | Supported | Partial | Invariants shared; implementation details differ by backend. |
| Import Pipeline (fetch/map/validate/run) | Supported | Supported | Partial | Mock should emulate deterministic outcomes for non-destructive validation/testing. |
| Lock/Tag-based Edit Concurrency | Supported | Partial | Partial | Behavior may vary by backend API exposure and environment constraints. |
| Structured Server Diagnostics | Supported | Partial | Mock-only | HTTP has richer status/payload diagnostics; COM may provide reduced detail. |

## Approved Fallback Rules

### Global Rules

1. No implicit backend switching at runtime after command/startup selection.
2. No feature-level silent fallback from COM to HTTP or HTTP to COM.
3. Fallback to Mock is never automatic for production operations.
4. On unsupported capability, fail fast and provide next action guidance.

### Per-Capability Rules

- Workflow share/unshare:
  - HTTP: execute normally.
  - COM: fail fast with guidance to use HTTP backend.
  - Mock: may simulate result only for test/demo flows.

- Session resume:
  - HTTP: attempt secure resume path, validate expiry/session, clear stale state.
  - COM: do not emulate persisted auth resume.
  - Mock: optional deterministic pseudo-resume allowed when marked as mock behavior.

- Diagnostics:
  - Prefer preserving backend-native details in logs.
  - Surface normalized error category to user-facing layers.

## Fail-Fast Contract

When a capability is Unsupported for the selected mode:
- Return explicit capability/mode mismatch error.
- Include the selected mode and requested operation.
- Include one recommended operator action (for example, rerun with HTTP backend).
- Do not execute partial side effects.

## Mode Sections (Template for Future TDNs)

This section pattern should be reused in future TDN documents.

### HTTP Mode

- Support posture: primary for 12.x WebAPI environments.
- Typical strengths: broad feature coverage, rich API diagnostics, SSO/session flows.
- Required invariants:
  - Preserve wire contracts expected by server.
  - Preserve identity fidelity for workflow trustees/notifications.
- Fail-fast triggers:
  - Contract mismatch, auth/session invalidation, unsupported endpoint semantics.

### COM Mode

- Support posture: legacy/desktop compatibility path.
- Typical strengths: 11.x SDK compatibility.
- Typical constraints:
  - Some modern API-driven operations are not available.
  - Signature and behavioral differences may require adapter/fallback code paths.
- Required invariants:
  - Preserve operation safety and explicit unsupported signaling.
- Fail-fast triggers:
  - Unsupported capability in COM path (for example share mutation).

### Mock Mode

- Support posture: deterministic test and demo path.
- Purpose:
  - Validate orchestration, UX flow, and command wiring without live environment dependencies.
- Constraints:
  - Not authoritative for production backend compatibility.
- Required invariants:
  - Must clearly label simulated behavior.
  - Must not be used as implicit fallback for production errors.

## Surface Mapping (CLI and Client)

Yes, each surface should typically have its own section in mode-aware TDNs.

Reason:
- Modes define capability availability.
- Surfaces define interaction contract and operator experience.

Recommended pattern:
1. Keep mode sections as primary architecture truth.
2. Add one section per surface to define presentation and orchestration differences.
3. Require each surface section to reference the same mode matrix and fallback rules.

### CLI Surface

- Must expose mode selection explicitly in command/runtime options.
- Must return deterministic exit behavior for unsupported mode/capability combinations.
- Should include machine-readable output where applicable.

### Client (Launcher) Surface

- Must present mode availability and unsupported operations clearly in UI.
- Must prevent ambiguous user actions when capability is unsupported for selected mode.
- Should preserve operator guidance for switching mode when needed.

## Governance for Future TDNs

Every new mode-aware TDN should include, at minimum:
1. Decision summary.
2. Capability matrix (HTTP/COM/Mock).
3. Explicit fallback rules.
4. Fail-fast contract.
5. HTTP section.
6. COM section.
7. Mock section.
8. Surface mapping section (CLI and Client).

## Open Questions

1. Should lock/tag semantics be classified as Supported or Partial for COM across all target environments?
2. Should mock mode enforce stricter parity checks for workflow share/identity scenarios?
3. Do we need a standardized capability code enum for machine-readable unsupported-mode errors?

## Consequences

Positive:
- Reduces ambiguity about backend support.
- Makes unsupported paths predictable and supportable.
- Provides reusable structure for future TDNs.

Trade-offs:
- Requires ongoing maintenance as backend capabilities evolve.
- Requires stricter review discipline to keep matrix and implementation aligned.
