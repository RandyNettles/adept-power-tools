# TDN - Test Strategy and Contract-Test Coverage Map

## Purpose

Define an architecture-level test strategy for Adept Power Tools and map current automated test coverage to architecture contracts, with explicit identification of gaps and risk posture.

## Scope

In scope:
- Contract-oriented automated test strategy for:
  - `AdeptTools.Workflow.Tests`
  - `AdeptTools.Cli.Tests`
  - `AdeptTools.Core.Tests`
- Unit and component-level contract tests currently present.
- Coverage gaps and recommended next-layer tests.

Out of scope:
- UI automation strategy for launcher UX.
- Non-functional load/performance benchmarking details.
- Full CI pipeline policy (covered by separate build/release governance).

## Source Context

Primary references:
- tests/AdeptTools.Workflow.Tests
- tests/AdeptTools.Cli.Tests
- tests/AdeptTools.Core.Tests
- docs/architecture/tdn/tdn-cli-and-launcher-contract-parity.md
- docs/architecture/tdn/tdn-error-taxonomy-and-exit-code-policy.md
- docs/architecture/tdn/tdn-workflow-concurrency-locking-and-recovery-strategy.md
- docs/architecture/tdn/tdn-configuration-precedence-and-environment-model.md
- docs/architecture/tdn/tdn-workflow-save-boundary-and-canonicalization-contract.md
- docs/architecture/tdn/tdn-workflow-identity-and-serialization-contract-third-client.md

## Problem Statement

The solution has meaningful unit-test coverage, especially in workflow transformation and service behavior, but lacks an explicit architecture artifact that states:
- Which architecture contracts are currently protected by automated tests.
- Which tests are proving those contracts.
- Which contracts remain weakly covered or uncovered.

Without this map, regressions are harder to assess by architectural impact and test investment can drift from high-risk boundaries.

## Decision Summary

1. Adopt a contract-first testing model where every architectural contract maps to one or more test layers.
2. Treat existing tests as the baseline contract safety net and classify coverage strength per contract.
3. Define a gap register with targeted contract-test additions before broadening to larger integration scope.
4. Use exit-code, observability, and locking policies as first-class testable contracts, not documentation-only concerns.

## Test Strategy Model

## Test layers and intent

1. Contract Unit Tests (fast, deterministic)
- Validate pure rules, mappings, canonicalization, and guard behavior.
- Dominant current layer in Workflow and Core tests.

2. Service Component Tests (in-process with mocks/fakes)
- Validate orchestration behavior and side-effect policy at service boundaries.
- Present in workflow service tests and CLI parser/middleware flow tests.

3. Adapter Contract Tests (real protocol semantics with isolated endpoint)
- Validate HTTP/COM adapter behavior against protocol-level expectations.
- Minimal current evidence in scoped projects; targeted expansion required.

4. End-to-End Scenario Tests (operator contract)
- Validate command-to-output, exit code, and artifact behavior over real wiring.
- Limited direct coverage; requires intentional suite design.

## Contract categories

- C1: Input parsing and canonicalization contracts.
- C2: Validation contracts (hard error vs warning semantics).
- C3: Workflow transformation/persistence contracts (trustees, notifications, flags).
- C4: Runtime configuration and mode resolution contracts.
- C5: Error taxonomy and exit-code contracts.
- C6: Concurrency/locking/recovery contracts.
- C7: Observability and diagnostics contracts.
- C8: Security/session boundary contracts.

## Coverage Map

Coverage rating:
- Strong: multiple focused tests across edge cases.
- Partial: some coverage, notable missing edges.
- Gap: no direct test evidence in scoped suites.

| Contract | Current Coverage | Evidence Anchors | Rating |
|---|---|---|---|
| C1 Input parsing and canonicalization | XML/Excel parsing variants, enum converter round-trip forms | `WorkflowXmlReaderTests`, `WorkflowExcelReaderTests`, `WorkflowEnumJsonConvertersTests` | Strong |
| C2 Validation semantics | Error/warning boundaries for empty trustees, duplicates, limits, negative values | `WorkflowValidatorTests`, preflight tests in `WorkflowServiceModifyTests` | Strong |
| C3 Workflow transformation and persistence mapping | Trustee role mapping, dedupe, step-scoped notifications, active flag propagation, approver null-target semantics | `WorkflowServiceCreateTests`, `WorkflowServiceModifyTests` | Strong |
| C4 Runtime configuration and mode resolution | CLI global options parse + guard (`--server` unless `--mock`), help/version bypass behavior | `AuthCommandTests` | Partial |
| C5 Error taxonomy and exit codes | Basic success/failure command outcomes and selective verbose detail rendering | `AuthCommandTests` (`WorkflowDelete_Failure_ShowsVerboseDetailsOnlyWithVerboseFlag`) | Partial |
| C6 Concurrency/locking/recovery | Locked-workflow exclusion in delete flow; transient save retry path on modify | `WorkflowServiceDeleteTests`, `WorkflowServiceModifyTests` | Partial |
| C7 Observability and diagnostics schema | No explicit assertions on correlation IDs, structured event envelope, redaction classes | None in scoped suites | Gap |
| C8 Session persistence and security boundaries | Mock auth state lifecycle only (login/logout/refresh) | `MockAdeptAuthServiceTests`, `MockAdeptApiClientTests` | Partial |

## Existing Strengths

1. Workflow domain rules are heavily validated at the behavior boundary where regressions are likely (readers, validators, mapping, save orchestration).
2. Notification and trustee edge cases are explicitly covered, including dedupe and specialized role semantics.
3. CLI smoke/contract checks exercise root command shape, auth test flow, and core middleware guard behavior.
4. Core mock auth/api tests provide deterministic confidence for mock-mode behavior and local auth-state transitions.

## Gap Register

## G1 - Exit-code policy matrix not comprehensively tested

Risk:
- Architectural exit-code taxonomy can drift from real command behavior, affecting automation scripts.

Current signal:
- Limited success/failure assertions; no matrix validating mapped codes by failure class.

Needed tests:
- Parameterized command tests asserting exit code mappings for validation, auth, transport, cancellation, and partial-result outcomes.

## G2 - Configuration precedence contract lacks exhaustive tests

Risk:
- Runtime precedence drift between explicit options, defaults, and fallback logic.

Current signal:
- CLI has basic guard tests; broader precedence matrix not covered in scoped suites.

Needed tests:
- Contract tests for explicit-vs-default precedence across `--backend`, `--server`, `--mock`, `--user`, `--verbose`, `--log`.

## G3 - Locking and recovery policy under-covered

Risk:
- Stale lock handling and untag/cleanup warning contracts can regress silently.

Current signal:
- Locked item exclusion and one transient save retry path are covered.

Needed tests:
- Deterministic tests for lock ownership transitions, stale lock behavior, and warning surfacing for non-fatal unlock/untag failures.

## G4 - Observability schema contract untested

Risk:
- Event shape, redaction, and correlation consistency can break support diagnostics without failing functional tests.

Current signal:
- No direct test assertions in scoped suites.

Needed tests:
- Logger sink contract tests asserting required fields (`operation`, `mode`, `workflowId/name`, `status`, `durationMs`, correlation) and redaction behavior.

## G5 - Security/session boundary contract shallowly covered

Risk:
- Persistence and invalidation boundaries may drift from session-security TDNs.

Current signal:
- Mock auth lifecycle only; no secure-store boundary or invalidation scenario tests in scoped suites.

Needed tests:
- Component tests around session restore/reconnect/logout invalidation boundaries using store abstractions.

## Recommended Test Additions (Near-Term)

Priority order:
1. Exit-code contract matrix tests for top-level CLI command families.
2. Configuration precedence matrix tests for CLI middleware resolution.
3. Workflow locking/recovery contract tests (including warning/non-fatal paths).
4. Observability envelope/redaction contract tests for operation lifecycle logging.
5. Session boundary tests across persist/restore/invalidate transitions.

## Contract-to-Suite Ownership

- `AdeptTools.Workflow.Tests`
  - Owns C1, C2, C3, and primary parts of C6.
- `AdeptTools.Cli.Tests`
  - Owns C4 and C5 command-surface contracts.
- `AdeptTools.Core.Tests`
  - Owns C8 foundations for auth-state behavior in mock mode.

Cross-cutting contracts requiring collaboration:
- C7 observability (Core + command/application surfaces).
- C8 non-mock persistence boundaries (Core + Launcher/App services where applicable).

## Minimum Regression Gate (Architecture)

A change touching any of these must include/adjust contract tests:
1. Workflow trustee/notification mapping or canonicalization.
2. CLI middleware global option resolution and server requirement guard.
3. Exit-code mapping behavior.
4. Locking/recovery decision paths.
5. Logging schema/redaction implementation.
6. Session persistence/invalidation behavior.

## Traceability

This TDN operationalizes and test-enables contracts defined in:
- `tdn-error-taxonomy-and-exit-code-policy.md`
- `tdn-configuration-precedence-and-environment-model.md`
- `tdn-workflow-concurrency-locking-and-recovery-strategy.md`
- `tdn-observability-and-operational-diagnostics.md`
- `tdn-session-persistence-security-boundaries.md`
- workflow identity/save/canonicalization TDN set

## Open Questions

1. Should exit-code conformance tests run as pure unit tests only, or as a lightweight command-process integration suite?
2. Should observability contract tests enforce exact field names centrally via a shared schema object?
3. Which project should host cross-surface contract tests that span CLI middleware + workflow service + logging envelope?