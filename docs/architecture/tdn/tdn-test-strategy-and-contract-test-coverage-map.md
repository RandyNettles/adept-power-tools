# TDN - Test Strategy and Contract-Test Coverage Map

## Purpose

Define an architecture-level test strategy for Adept Power Tools and map current automated test coverage to architecture contracts, with explicit identification of gaps and risk posture.

This TDN also makes mode and surface test-boundary ownership explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Scope

In scope:
- Contract-oriented automated test strategy for:
  - `AdeptTools.Workflow.Tests`
  - `AdeptTools.Cli.Tests`
  - `AdeptTools.Core.Tests`
- Shared contract coverage expectations across runtime modes and surfaces.
- Unit and component-level contract tests currently present.
- Coverage gaps and recommended next-layer tests.

Out of scope:
- UI automation strategy for launcher UX.
- Non-functional load/performance benchmarking details.
- Full CI pipeline policy (covered by separate build/release governance).

## Boundary Model

This TDN has two coverage layers.

### Layer 1: Shared contract coverage model

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- Which architectural contracts require automated protection.
- Coverage strength language (`Strong`, `Partial`, `Gap`).
- Which test layers are appropriate for a contract class.

Does not define:
- Exact test framework mechanics.
- Surface-specific UI automation requirements.
- Backend-specific implementation details beyond what is needed to assign contract coverage.

### Layer 2: Mode and surface coverage realization

Applies to:
- Mode-specific adapter and capability behavior.
- CLI and Client surface ownership boundaries.

Defines:
- Which tests are shared across modes and surfaces.
- Which tests must be mode-specific.
- Which tests are primarily CLI-facing versus Client-facing.

Does not redefine:
- The underlying architecture contracts themselves.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode typically has the richest current contract evidence in this repository.

Coverage expectations:
- Shared service/orchestration tests may use HTTP-oriented mocks/fakes as their dominant baseline.
- HTTP-specific adapter behavior, richer diagnostics, and session-resume semantics require dedicated coverage where they materially affect contracts.

Boundary rule:
- HTTP-oriented evidence does not automatically prove COM or Mock parity when mode semantics differ.

### COM mode

COM mode requires explicit coverage where behavior is not contract-identical to HTTP.

Coverage expectations:
- COM-specific unsupported/partial capability paths need targeted tests.
- COM/native edit/session/locking or auth/session constraints need dedicated coverage when shared tests cannot exercise them.
- Native/Desktop parity-sensitive workflow CRUD semantics are not fully proven by HTTP-mode service tests.

Boundary rule:
- When COM behavior differs in capability, diagnostics, lifecycle, or safety boundary, dedicated COM-facing contract tests are required rather than inferred parity.

### Mock mode

Mock mode is a deterministic simulation path and requires explicit contract coverage for simulation boundaries.

Coverage expectations:
- Mock should prove orchestration safety, deterministic outcomes, and non-production behavior boundaries.
- Mock tests do not by themselves prove production HTTP or COM compatibility.

Boundary rule:
- Mock coverage is evidence for simulation-path correctness, not a substitute for production-mode adapter coverage.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns process-facing contract areas.

Coverage expectations:
- Exit-code mapping.
- Global option/configuration guard behavior.
- Command-surface argument and summary behavior.
- Script/automation-facing failure semantics.

Boundary rule:
- CLI tests do not by themselves prove Client interaction parity, even when they cover shared service semantics.

### Client surface

Client owns interactive orchestration and presentation-adjacent semantic boundaries.

Coverage expectations:
- Resume/connect security boundaries where Client owns secure store behavior.
- UI-driven operation orchestration semantics where dialogs/progress/cancel flows matter to safety.
- Cross-surface parity checks where Client is supposed to preserve shared semantics.

Boundary rule:
- Client surface semantic coverage may be provided by service/component tests and parity-oriented tests even if full UI automation is out of scope.

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

## COM Path (11.4.5)

The provided Adept 11.4.5 workflow documents add native/Desktop evidence that should inform contract coverage expectations:
- Active backend/runtime tests in Adept 11 include `SelectionCommand_Workflow_Tests.cs` for runtime workflow command behavior.
- Admin workflow CRUD is Desktop/Core-first, so parity-sensitive contract tests for a third client should also consider native `Edit`/`Update` object-graph semantics, not just HTTP-mode service orchestration.

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
5. Shared contract coverage and mode/surface-specific coverage must be distinguished explicitly so “tested” does not overstate HTTP-only, COM-only, CLI-only, or Client-only evidence.

## Test Strategy Model

Boundary note:
- The layers below are shared test-layer concepts across modes and surfaces.
- They do not imply equal current evidence for HTTP, COM, Mock, CLI, and Client.

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

Shared-contract boundary:
- The categories below are architecture contracts shared across modes and surfaces.
- Coverage evidence for a category may still be mode-specific or surface-specific.

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

Boundary note:
- Ratings describe current evidence in this repository scope, not automatic parity across all modes and both surfaces.

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

11.4.5-specific testing note:
- The supplied Adept 11 runtime evidence shows strong runtime command coverage, but not equivalent admin CRUD contract coverage for third-client authoring semantics.

Mode/surface implication:
- Current “Strong” workflow coverage is primarily proof of shared rules plus HTTP-oriented/mockable orchestration behavior, not full COM/native admin parity.

## Existing Strengths

1. Workflow domain rules are heavily validated at the behavior boundary where regressions are likely (readers, validators, mapping, save orchestration).
2. Notification and trustee edge cases are explicitly covered, including dedupe and specialized role semantics.
3. CLI smoke/contract checks exercise root command shape, auth test flow, and core middleware guard behavior.
4. Core mock auth/api tests provide deterministic confidence for mock-mode behavior and local auth-state transitions.

Boundary note:
- These strengths are unevenly distributed across modes and surfaces; current evidence is strongest in shared service logic, CLI behavior, and mockable paths.

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
6. COM/native parity tests for workflow admin graph semantics where adapter coverage is added (edit lock, `Update()` boundary, delete side effects, recipient dedupe rules).
7. Client-surface semantic tests for secure session persistence, resume invalidation, and parity-critical long-running operation behaviors where Client owns the boundary.

## Contract-to-Suite Ownership

Surface/mode boundary:
- Ownership below is about current repository suite responsibility, not proof that a contract is fully covered in every mode and on both surfaces.

- `AdeptTools.Workflow.Tests`
  - Owns C1, C2, C3, and primary parts of C6.
- `AdeptTools.Cli.Tests`
  - Owns C4 and C5 command-surface contracts.
- `AdeptTools.Core.Tests`
  - Owns C8 foundations for auth-state behavior in mock mode.

Cross-cutting contracts requiring collaboration:
- C7 observability (Core + command/application surfaces).
- C8 non-mock persistence boundaries (Core + Launcher/App services where applicable).

Mode-specific ownership notes:
- HTTP-heavy shared service behavior is often exercised indirectly through Workflow/Core test doubles.
- COM-specific parity evidence needs dedicated adapter/native-facing coverage where behavior diverges.
- Mock-specific behavior should remain explicitly tested as simulation, not counted as production parity.

Surface-specific ownership notes:
- CLI owns process-facing contract verification.
- Client owns secure-store/session and interactive semantic-boundary verification where those are not reducible to CLI tests.

## Minimum Regression Gate (Architecture)

A change touching any of these must include/adjust contract tests:
1. Workflow trustee/notification mapping or canonicalization.
2. CLI middleware global option resolution and server requirement guard.
3. Exit-code mapping behavior.
4. Locking/recovery decision paths.
5. Logging schema/redaction implementation.
6. Session persistence/invalidation behavior.
7. Any change that alters mode-specific behavior or CLI/Client semantic parity must add or update tests at the affected boundary.

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