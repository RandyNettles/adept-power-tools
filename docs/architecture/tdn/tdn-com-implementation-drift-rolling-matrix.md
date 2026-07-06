# TDN COM Implementation Drift Rolling Matrix

- Status: In Progress
- Date Started: 2026-07-06
- Scope: COM mode only, CLI + Launcher surfaces in AdeptPowerTools
- Baseline Comparison: Internal 11.4.5 COM behavior reference docs
- Evidence Standard: Code + tests + internal 11.4.5 reference docs
- Increment Size: 1 TDN per pass

## Rubric

Status values:
- `Implemented`: behavior exists and aligns with TDN contract intent.
- `Partial`: behavior exists but has explicit limitations, uneven surface realization, or insufficient hard proof.
- `Gap`: behavior absent or contradicted by implementation.

Drift values vs 11.4.5:
- `None`: materially aligned behavior.
- `Minor`: equivalent intent, different mechanism/shape.
- `Material`: semantic mismatch, unsupported invariant, or unsafe fallback.

Risk values:
- `Low`, `Med`, `High`

## Rolling Matrix

| TDN | Contract Intent Summary | COM CLI Status | COM Launcher Status | Drift vs 11.4.5 | Risk | Recommended Action | Evidence |
|---|---|---|---|---|---|---|---|
| [tdn-backend-capability-matrix-and-fallback-rules.md](tdn-backend-capability-matrix-and-fallback-rules.md) | Mode-first capability matrix with explicit support levels, no silent backend switching, and fail-fast guidance for unsupported COM operations. | Partial | Partial | Minor | Med | 1) Add targeted COM contract tests for unsupported matrix rows and fail-fast message shape. 2) Clarify COM partial rows (concurrency, diagnostics) with explicit invariant tests. 3) Keep share/unshare as explicit COM unsupported unless product decision changes scope. | CLI COM binding: [src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs](../../../src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs#L73), [src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs](../../../src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs#L81), [src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs](../../../src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs#L82). Launcher COM selection/routing: [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L38), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L176), [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L136), [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L164). COM capability/fail-fast behavior: [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L20), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L520), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L549), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L563), [src/AdeptTools.Backend.Com/Api/ComImportApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComImportApiClient.cs#L21), [src/AdeptTools.Backend.Com/Api/ComImportApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComImportApiClient.cs#L257). Test evidence/gaps: [tests/AdeptTools.Backend.Com.Tests/ComAdeptAuthServiceTests.cs](../../../tests/AdeptTools.Backend.Com.Tests/ComAdeptAuthServiceTests.cs#L34), [tests/AdeptTools.Backend.Com.Tests/ComOperationRunnerTests.cs](../../../tests/AdeptTools.Backend.Com.Tests/ComOperationRunnerTests.cs#L8), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L63), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L206). 11.4.5 refs: [internal/architecture/features/workflow/11.4.5-feature-workflow-locking-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-locking-native-api.md), [internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md). |
| [tdn-cli-and-launcher-contract-parity.md](tdn-cli-and-launcher-contract-parity.md) | Shared operations across CLI and Launcher must preserve equivalent semantics for validation, dry-run, destructive safety, cancellation, and unsupported-capability handling. | Partial | Partial | Minor | Med | 1) Add explicit cross-surface COM parity tests for dry-run, delete safety, and unsupported capability guidance. 2) Add launcher coverage for cancellation and partial-success result semantics under COM mode. 3) Add governance checklist enforcement in PR template for parity-impacting changes. | Parity contract source: [docs/architecture/tdn/tdn-cli-and-launcher-contract-parity.md](tdn-cli-and-launcher-contract-parity.md#L1). CLI parity behavior and safety gates: [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L16), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L107), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L150), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L257), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L325). Launcher parity behavior and staged/dry-run semantics: [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L37), [src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs#L180), [src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs#L238), [src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs#L276), [src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs#L344). Existing test evidence and gaps: [tests/AdeptTools.Cli.Tests/AuthCommandTests.cs](../../../tests/AdeptTools.Cli.Tests/AuthCommandTests.cs#L14), [tests/AdeptTools.Cli.Tests/AuthCommandTests.cs](../../../tests/AdeptTools.Cli.Tests/AuthCommandTests.cs#L137), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L178), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L203). |
| [tdn-configuration-precedence-and-environment-model.md](tdn-configuration-precedence-and-environment-model.md) | Deterministic source precedence and effective runtime tuple resolution for CLI and Launcher, with mode-aware validation/normalization before service binding. | Partial | Partial | Minor | Med | 1) Add automated precedence-matrix tests for CLI options and launcher runtime resolution. 2) Centralize endpoint normalization to reduce per-surface drift risk. 3) Add explicit launcher COM-precedence tests (selected profile/user edit/mock toggle interactions). | TDN contract: [docs/architecture/tdn/tdn-configuration-precedence-and-environment-model.md](tdn-configuration-precedence-and-environment-model.md#L1). CLI precedence and guards: [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L16), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L81), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L92), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L110), [src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs](../../../src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs#L34). Launcher precedence and persistence layers: [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L69), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L76), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L200), [src/AdeptTools.Launcher/Services/ServerHistoryService.cs](../../../src/AdeptTools.Launcher/Services/ServerHistoryService.cs#L33), [src/AdeptTools.Launcher/Services/ComProfileService.cs](../../../src/AdeptTools.Launcher/Services/ComProfileService.cs#L15), [src/AdeptTools.Launcher/Models/ComConnectionProfile.cs](../../../src/AdeptTools.Launcher/Models/ComConnectionProfile.cs#L15), [src/AdeptTools.Launcher/Services/MockModeState.cs](../../../src/AdeptTools.Launcher/Services/MockModeState.cs#L9), [src/AdeptTools.Launcher/Services/HttpClientConfig.cs](../../../src/AdeptTools.Launcher/Services/HttpClientConfig.cs#L9). Settings tuple model anchor: [src/AdeptTools.Core/Configuration/AdeptToolSettings.cs](../../../src/AdeptTools.Core/Configuration/AdeptToolSettings.cs#L5). Existing tests and known coverage gap context: [tests/AdeptTools.Cli.Tests/AuthCommandTests.cs](../../../tests/AdeptTools.Cli.Tests/AuthCommandTests.cs#L84), [tests/AdeptTools.Cli.Tests/AuthCommandTests.cs](../../../tests/AdeptTools.Cli.Tests/AuthCommandTests.cs#L118), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L198). |
| [tdn-error-taxonomy-and-exit-code-policy.md](tdn-error-taxonomy-and-exit-code-policy.md) | Shared ET taxonomy across modes/surfaces plus stable CLI exit-code mapping by class with deterministic priority order. | Gap | Gap | Material | High | 1) Implement centralized CLI exit-code mapper aligned to ET classes (0/1/2/3/4/5/6/9). 2) Add ET-class tagging in non-zero CLI outputs and ensure global exception path maps to 9. 3) Introduce taxonomy classification model for Launcher (semantic ET classes without process codes). 4) Add command-family tests asserting ET-class-to-exit-code mappings, including cancellation and usage/config/auth distinctions. | TDN policy source: [docs/architecture/tdn/tdn-error-taxonomy-and-exit-code-policy.md](tdn-error-taxonomy-and-exit-code-policy.md#L1), [docs/architecture/tdn/tdn-error-taxonomy-and-exit-code-policy.md](tdn-error-taxonomy-and-exit-code-policy.md#L226). Current CLI behavior shows non-policy mapping: [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L57), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L81), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L101), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L176), [src/AdeptTools.Cli/Commands/AuthCommands.cs](../../../src/AdeptTools.Cli/Commands/AuthCommands.cs#L79), [src/AdeptTools.Cli/Commands/AuthCommands.cs](../../../src/AdeptTools.Cli/Commands/AuthCommands.cs#L111), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L43), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L183), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L335), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L363), [src/AdeptTools.Cli/Commands/ImportCommands.cs](../../../src/AdeptTools.Cli/Commands/ImportCommands.cs#L163), [src/AdeptTools.Cli/Commands/ImportCommands.cs](../../../src/AdeptTools.Cli/Commands/ImportCommands.cs#L233), [src/AdeptTools.Cli/Commands/ImportCommands.cs](../../../src/AdeptTools.Cli/Commands/ImportCommands.cs#L238). Launcher taxonomy evidence absent: [src/AdeptTools.Launcher](../../../src/AdeptTools.Launcher). Test evidence indicates partial exit behavior checks only: [tests/AdeptTools.Cli.Tests/AuthCommandTests.cs](../../../tests/AdeptTools.Cli.Tests/AuthCommandTests.cs#L14), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L201). |
| [tdn-observability-and-operational-diagnostics.md](tdn-observability-and-operational-diagnostics.md) | Shared observability contract requiring operation correlation IDs, structured diagnostic envelope, and redaction-safe telemetry semantics across CLI and Launcher. | Gap | Gap | Material | High | 1) Introduce Core `DiagnosticContext` with operationId/surface/backendMode and propagate per operation. 2) Add structured event envelope emission (at least start/progress/summary/error/cancelled) for CLI and Launcher. 3) Add centralized redaction helper and enforce it in all diagnostic emitters. 4) Add correlation propagation for HTTP requests (`X-Correlation-Id`) and support-reference display in Launcher error dialogs. 5) Add observability contract tests for envelope fields/redaction. | TDN source and requirements: [docs/architecture/tdn/tdn-observability-and-operational-diagnostics.md](tdn-observability-and-operational-diagnostics.md#L1), [docs/architecture/tdn/tdn-observability-and-operational-diagnostics.md](tdn-observability-and-operational-diagnostics.md#L140), [docs/architecture/tdn/tdn-observability-and-operational-diagnostics.md](tdn-observability-and-operational-diagnostics.md#L263). Current CLI diagnostics are mostly plain text/result logs without operationId/envelope: [src/AdeptTools.Core/Logging/ResultLogger.cs](../../../src/AdeptTools.Core/Logging/ResultLogger.cs#L11), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L57), [src/AdeptTools.Cli/Commands/WorkflowCommands.cs](../../../src/AdeptTools.Cli/Commands/WorkflowCommands.cs#L198), [src/AdeptTools.Cli/Commands/ImportCommands.cs](../../../src/AdeptTools.Cli/Commands/ImportCommands.cs#L205). Launcher diagnostics currently use message boxes for exceptions without structured correlation context: [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L31), [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L43), [src/AdeptTools.Launcher/App.xaml.cs](../../../src/AdeptTools.Launcher/App.xaml.cs#L53). Correlation/redaction primitives absent in current source search (`operationId`, `DiagnosticContext`, `X-Correlation-Id`, redaction helper). Coverage map flags observability as gap: [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L212), [docs/architecture/tdn/tdn-test-strategy-and-contract-test-coverage-map.md](tdn-test-strategy-and-contract-test-coverage-map.md#L264). |
| [tdn-session-persistence-security-boundaries.md](tdn-session-persistence-security-boundaries.md) | Secure persisted HTTP session boundaries with fail-closed resume/invalidation, explicit HTTP-vs-COM separation, and convenience-vs-auth state isolation. | Partial | Partial | Minor | Med | 1) Add explicit remote viability check (`RefreshAsync`/`IsLoggedInRemote`) in launcher resume before setting connected state. 2) Harden CLI store with protected-at-rest strategy or explicitly document accepted risk scope. 3) Add tests for resume failure clearing, mode-switch clearing, and corruption handling across CLI + Launcher stores. | TDN requirements and gap note: [docs/architecture/tdn/tdn-session-persistence-security-boundaries.md](tdn-session-persistence-security-boundaries.md#L1), [docs/architecture/tdn/tdn-session-persistence-security-boundaries.md](tdn-session-persistence-security-boundaries.md#L348). Launcher secure store + fail-closed behavior: [src/AdeptTools.Launcher/Services/AuthSessionStore.cs](../../../src/AdeptTools.Launcher/Services/AuthSessionStore.cs#L27), [src/AdeptTools.Launcher/Services/AuthSessionStore.cs](../../../src/AdeptTools.Launcher/Services/AuthSessionStore.cs#L36), [src/AdeptTools.Launcher/Services/AuthSessionStore.cs](../../../src/AdeptTools.Launcher/Services/AuthSessionStore.cs#L52), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L103), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L138), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L269), [src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs](../../../src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs#L303). HTTP auth runtime invalidation helpers: [src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs](../../../src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs#L92), [src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs](../../../src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs#L712), [src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs](../../../src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs#L721). CLI session store contrast (plaintext JSON): [src/AdeptTools.Cli/Infrastructure/CliAuthSessionStore.cs](../../../src/AdeptTools.Cli/Infrastructure/CliAuthSessionStore.cs#L20), [src/AdeptTools.Cli/Infrastructure/CliAuthSessionStore.cs](../../../src/AdeptTools.Cli/Infrastructure/CliAuthSessionStore.cs#L25), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L115), [src/AdeptTools.Cli/Program.cs](../../../src/AdeptTools.Cli/Program.cs#L176). |
| [tdn-workflow-identity-and-mapping-invariants.md](tdn-workflow-identity-and-mapping-invariants.md) | Non-negotiable invariants for step mapping, trustee identity fidelity, notification identity fidelity, and post-save verification semantics across modes/surfaces. | Partial | Partial | Minor | Med | 1) Add COM-backed contract tests proving invariant preservation on real COM persistence paths (not only mock/com-like harnesses). 2) Add explicit tests for alias-aware reviewer matching boundaries under COM capability limits. 3) Add cross-surface invariant conformance checks so Launcher orchestration proves same hard-failure semantics as CLI/service tests. | TDN invariant source: [docs/architecture/tdn/tdn-workflow-identity-and-mapping-invariants.md](tdn-workflow-identity-and-mapping-invariants.md#L1). Shared invariant enforcement in service: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L489), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L915), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1090), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1194), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1250), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1418). Alias-resolution mechanics: [src/AdeptTools.Workflow/Input/UserMatcher.cs](../../../src/AdeptTools.Workflow/Input/UserMatcher.cs#L12). COM adapter identity/persistence behavior and capability boundaries: [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L20), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L549), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L563), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L685). Test evidence of invariant guards (mostly mock/com-like): [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L967), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1009), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1175), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L715), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L759). 11.4.5 reference baseline: [internal/architecture/features/workflow/11.4.5-workflow-identity-serialization-contract-third-client.md](../../../internal/architecture/features/workflow/11.4.5-workflow-identity-serialization-contract-third-client.md). |
| [tdn-workflow-save-boundary-and-canonicalization-contract.md](tdn-workflow-save-boundary-and-canonicalization-contract.md) | Full-snapshot save boundary with canonical post-save readback, step-owned notification semantics, and hard-fail persistence mismatch checks. | Partial | Partial | Minor | Med | 1) Add real COM integration tests to validate canonicalization and post-save mismatch handling on native persistence paths. 2) Add launcher-surface orchestration tests that prove same warning/failure semantics as service/CLI flows. 3) Add targeted tests for warning-path diagnostics (visibility mismatch and untag warning) under COM runtime conditions. | TDN contract source: [docs/architecture/tdn/tdn-workflow-save-boundary-and-canonicalization-contract.md](tdn-workflow-save-boundary-and-canonicalization-contract.md#L1). Save-boundary implementation in shared service: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L227), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L235), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L292), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L462), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L585), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1114), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1156). Canonical post-save verification semantics: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1194), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1250), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1332). COM constraints/capabilities at adapter boundary: [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L20), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L520). Test evidence for save-boundary behavior (mostly service-level/mock-com-like): [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L181), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L263), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L527), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L620), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1175). |
| [tdn-workflow-identity-and-serialization-contract-third-client.md](tdn-workflow-identity-and-serialization-contract-third-client.md) | Stable workflow/step/trustee/notification identity model and numeric enum serialization semantics aligned with native COM char-code behavior. | Partial | Partial | Minor | Med | 1) Add COM integration tests that verify persisted trustee/notification type codes and target IDs round-trip exactly (including `Approvers` synthetic semantics). 2) Add explicit Launcher parity tests for unresolved/unknown recipient retention and user-facing diagnostics. 3) Expand serializer and API boundary tests to assert no symbolic enum labels are emitted on save payloads. | TDN contract source: [docs/architecture/tdn/tdn-workflow-identity-and-serialization-contract-third-client.md](tdn-workflow-identity-and-serialization-contract-third-client.md#L1). Native 11.4.5 baseline contract: [internal/architecture/features/workflow/11.4.5-workflow-identity-serialization-contract-third-client.md](../../../internal/architecture/features/workflow/11.4.5-workflow-identity-serialization-contract-third-client.md#L1). Numeric enum model and converters in ATP: [src/AdeptTools.Workflow/Models/WorkflowUserType.cs](../../../src/AdeptTools.Workflow/Models/WorkflowUserType.cs#L3), [src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs](../../../src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs#L6), [src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs](../../../src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs#L30), [src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs](../../../src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs#L47), [src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs](../../../src/AdeptTools.Workflow/Models/WorkflowEnumJsonConverters.cs#L74). Identity/normalization mapping at save boundary: [src/AdeptTools.Workflow/Input/WorkflowInputModel.cs](../../../src/AdeptTools.Workflow/Input/WorkflowInputModel.cs#L23), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L939), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1004), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1030), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1208). COM trustee type interop mapping and capability limits: [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L637), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L699), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L712), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L740). Tests proving numeric serialization contract: [tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs#L19), [tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs#L26), [tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowEnumJsonConvertersTests.cs#L33). |
| [tdn-workflow-adept-power-tools-share-and-trustee-identity.md](tdn-workflow-adept-power-tools-share-and-trustee-identity.md) | Preserve separation of workflow-definition identity vs share-list/container identity and enforce trustee IDs congruent with effective backend user identity model. | Partial | Partial | Minor | Med | 1) Add COM runtime integration tests for native share-list/container visibility invariants (including private-by-default and in-use visibility exception behavior where applicable). 2) Add parity tests ensuring trustee identity resolution/persistence outcomes are equivalent across CLI and Launcher in COM mode when directory lookup is unavailable. 3) Add contract tests for hard-fail share mutation on COM and container-metadata failures on HTTP share path. | TDN source: [docs/architecture/tdn/tdn-workflow-adept-power-tools-share-and-trustee-identity.md](tdn-workflow-adept-power-tools-share-and-trustee-identity.md#L1). Native 11.4.5 share-list/container semantics reference: [internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md#L12), [internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md#L30), [internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-sharing-native-api.md#L59). HTTP share mutation and containerId hard-fail behavior: [src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs#L111), [src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs#L122), [src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Http/Api/HttpWorkflowApiClient.cs#L134). COM capability boundary (share mutation unsupported, user/group lookup unsupported): [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L20), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L549), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L563), [src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs](../../../src/AdeptTools.Backend.Com/Api/ComWorkflowApiClient.cs#L571). Service-level fail-fast and trustee identity resolution behavior: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L552), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L631), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1514), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1643). Existing COM-like evidence for share fail-fast: [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L715). |
| [tdn-workflow-adept-power-tools-congruence-and-validation.md](tdn-workflow-adept-power-tools-congruence-and-validation.md) | Shared preflight/post-save congruence contract with explicit parsing normalization, validation gates, and hard-error vs warning outcomes across modes/surfaces. | Partial | Partial | Minor | Med | 1) Add COM runtime contract tests asserting parity for validation boundaries and persistence mismatch failure semantics after native `Update()` paths. 2) Add Launcher-facing parity tests for warning/error presentation equivalence (especially trustee-resolution and persistence-mismatch paths). 3) Expand regression tests for Excel-vs-XML parity boundaries and document intended non-parity areas as explicit policy assertions. | TDN source: [docs/architecture/tdn/tdn-workflow-adept-power-tools-congruence-and-validation.md](tdn-workflow-adept-power-tools-congruence-and-validation.md#L1). XML parsing/nullable weekend semantics and role/default handling: [src/AdeptTools.Workflow/Input/WorkflowXmlReader.cs](../../../src/AdeptTools.Workflow/Input/WorkflowXmlReader.cs#L46), [src/AdeptTools.Workflow/Input/WorkflowXmlReader.cs](../../../src/AdeptTools.Workflow/Input/WorkflowXmlReader.cs#L137), [tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs#L147), [tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs#L175), [tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs#L203), [tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowXmlReaderTests.cs#L231). Preflight validation and warning/error policy: [src/AdeptTools.Workflow/Validation/WorkflowValidator.cs](../../../src/AdeptTools.Workflow/Validation/WorkflowValidator.cs#L8), [src/AdeptTools.Workflow/Validation/WorkflowValidator.cs](../../../src/AdeptTools.Workflow/Validation/WorkflowValidator.cs#L112), [tests/AdeptTools.Workflow.Tests/WorkflowValidatorTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowValidatorTests.cs#L8), [tests/AdeptTools.Workflow.Tests/WorkflowValidatorTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowValidatorTests.cs#L110). Service-level congruence enforcement and post-save hard-fail verification: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1090), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1194), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1250), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1480). Existing tests for congruence/validation failures and COM-like capability boundaries: [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L938), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1009), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1062), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L1112). |
| [tdn-workflow-adept-power-tools-hardening-notes.md](tdn-workflow-adept-power-tools-hardening-notes.md) | Post-incident hardening for deterministic step targeting, strict step-scoped notification ownership, StepId congruence, and bounded transient save retry safety. | Partial | Partial | Minor | Med | 1) Add COM runtime integration tests proving leakage-prevention invariant (no cross-step notify bleed) under native write-back paths. 2) Add Launcher parity tests to verify same hardening outcomes and warnings across interactive orchestration flows. 3) Add targeted regression tests for untag/cleanup warning behavior when hardening checks fail after save in COM-like scenarios. | TDN source: [docs/architecture/tdn/tdn-workflow-adept-power-tools-hardening-notes.md](tdn-workflow-adept-power-tools-hardening-notes.md#L1). Service hardening implementation for step-scoped ownership and congruence guards: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L227), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L945), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L954), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1065). Bounded transient retry guard for known disposed-context signature: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1068), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1078). Cleanup and post-save verification flow: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L263), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L275), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L292), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1142). Test evidence for hardening scenarios: [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L608), [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L748), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L212), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L507), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L640). COM-like capability fail-fast evidence: [tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceCreateTests.cs#L967), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L715). |
| [tdn-workflow-concurrency-locking-and-recovery-strategy.md](tdn-workflow-concurrency-locking-and-recovery-strategy.md) | Tag/edit-lock ownership contract with mandatory post-verification release, lock contention messaging, locked-delete exclusion, and operator-mediated stale-lock recovery posture. | Partial | Partial | Minor | Med | 1) Add COM integration tests for native multi-scope lock semantics (workflow edit vs save/copy vs single-admin contention) and map outcomes into shared contract classes. 2) Add launcher parity tests for contention/warning classification and stale-lock guidance messaging. 3) Add explicit cancellation-path tests ensuring cleanup attempts and consistent user-facing outcomes after partial progress. | TDN source: [docs/architecture/tdn/tdn-workflow-concurrency-locking-and-recovery-strategy.md](tdn-workflow-concurrency-locking-and-recovery-strategy.md#L1). Service lock lifecycle and ownership behavior: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L462), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L469), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L659), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L665), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L1142). Delete concurrency filtering and bounded parallelism: [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L744), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L748), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L785), [src/AdeptTools.Workflow/Services/WorkflowService.cs](../../../src/AdeptTools.Workflow/Services/WorkflowService.cs#L824). Existing test evidence for ordering/cleanup and contention outcomes: [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L606), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L640), [tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceModifyTests.cs#L715), [tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs#L115), [tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs](../../../tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs#L299). 11.4.5 native lock-model references: [internal/architecture/features/workflow/11.4.5-feature-workflow-locking-native-api.md](../../../internal/architecture/features/workflow/11.4.5-feature-workflow-locking-native-api.md#L1). |

## Increment Notes

## Increment 1 - tdn-backend-capability-matrix-and-fallback-rules

Summary:
- The mode-first matrix concept is implemented in architecture and wiring: CLI and Launcher both select COM explicitly and resolve COM adapters by design.
- Explicit fail-fast for unsupported COM capabilities is present for workflow share mutation and user/group directory lookups; import feature-gating also fails fast when disabled.
- Contract alignment is strong for "no silent backend switching" at startup/runtime wiring level.

Confirmed Alignments:
- Explicit COM mode selection and dedicated DI bindings in both surfaces.
- Explicit COM unsupported signaling for share/unshare and directory lookups with guidance to use HTTP.
- COM import/workflow feature flags fail fast when disabled (no implicit fallback).

Gaps / Partials:
- Capability rows marked `Partial` in TDN (lock/tag concurrency, structured diagnostics) do not yet have a complete COM-specific contract test matrix proving expected boundaries.
- Session resume row indicates COM `Unsupported`; implementation appears aligned by HTTP-only persisted resume behavior in Launcher, but direct COM non-persistence proof tests are limited.
- Cross-surface unsupported-capability UX parity is only partially evidenced by automated tests.

Drift Assessment:
- No material drift found in this increment.
- Minor drift risk remains where 11.4.5-native lock/edit semantics can vary by COM exposure and adapter probing strategy, and current tests do not comprehensively prove all matrix claims.

Open Follow-ups (for backlog triage):
1. Add COM adapter contract tests for unsupported operations and fail-fast payload/message consistency.
2. Add concurrency/lock-path contract tests for COM tag/untag/save/delete edge behavior.
3. Add launcher-oriented parity tests for unsupported COM actions to confirm deterministic operator guidance.

## Increment 2 - tdn-cli-and-launcher-contract-parity

Summary:
- CLI and Launcher both route shared operations through the same service interfaces and preserve core intent-level controls (dry-run, confirmation/cancel, and mode selection).
- Surface-specific interaction differences align with TDN intent (CLI prompts/flags vs Launcher dialogs/staged apply).
- Automated parity proof is currently stronger for CLI than Launcher, especially for COM-specific unsupported-capability and cancellation edge semantics.

Confirmed Alignments:
- Shared backend contracts are used by both surfaces (`IAdeptAuthService`, `IWorkflowService`, `IImportService` and mode-selected API clients).
- Both surfaces implement non-mutating review/dry-run paths before apply/delete operations.
- Both surfaces maintain explicit user-intent gating for destructive actions.

Gaps / Partials:
- No dedicated cross-surface conformance suite asserting equivalent semantic outcomes per scenario in COM mode.
- Launcher-specific automated coverage for parity-critical delete/cancel/partial-success behavior is limited.
- Unsupported capability guidance consistency across surfaces is implemented but not comprehensively verified via tests.

Drift Assessment:
- No material semantic drift identified in this increment.
- Minor drift risk remains due to uneven test evidence across surfaces rather than observed behavior contradiction.

Open Follow-ups (for backlog triage):
1. Introduce scenario-paired parity tests (CLI vs Launcher) for workflow create/modify/delete in COM mode.
2. Add launcher component tests for cancel and mixed-outcome semantics.
3. Add explicit unsupported-capability parity checks for COM across both surfaces.

## Increment 3 - tdn-configuration-precedence-and-environment-model

Summary:
- CLI and Launcher both implement deterministic configuration layering that is broadly aligned with the TDN precedence model.
- CLI remains invocation-authoritative and enforces non-mock server requirements before command execution.
- Launcher seeds from persisted convenience state (history/profiles), then resolves runtime values from current user edits and selected mode/profile.

Confirmed Alignments:
- CLI option defaults and explicit inputs feed a single `AdeptToolSettings` object before service registration.
- CLI enforces non-mock `--server` requirement and preserves mock override behavior.
- Launcher persists convenience state (`server-history`, `com-profiles`) and uses selected COM profile address for COM backend connections.
- Mock mode state is centralized and propagated to runtime service selection.

Gaps / Partials:
- No dedicated automated precedence matrix verifies all source-order combinations across CLI and Launcher.
- Endpoint normalization logic is distributed across surfaces (risk of future drift from tuple contract intent).
- Launcher precedence behavior is implemented but under-tested for edge combinations (profile invalidation, mode toggle timing, user-edit override persistence interactions).

Drift Assessment:
- No material semantic drift identified in this increment.
- Minor drift risk is primarily test-evidence and normalization-centralization debt, not an observed contract contradiction.

Open Follow-ups (for backlog triage):
1. Add CLI precedence matrix tests covering `--backend`, `--mock`, `--server`, and auth fallback interactions.
2. Add launcher precedence tests for startup seed vs runtime edits vs selected COM profile vs mock override.
3. Introduce shared endpoint normalization helper in Core and adopt it in both CLI and Launcher.

## Increment 4 - tdn-error-taxonomy-and-exit-code-policy

Summary:
- Current implementation does not yet realize the TDN's canonical ET taxonomy and stable exit-code mapping policy.
- CLI uses command-local numeric codes (primarily `0/1/2`) with overlapping meanings, and global exception handling currently maps to `1` instead of policy code `9`.
- Launcher does not currently expose a formal ET-class semantic model, so cross-surface taxonomy parity is not yet implemented.

Confirmed Alignments:
- Non-zero generally signals failure for CLI automation, which aligns with broad policy intent.
- Some no-op/policy pathways are already distinguished (`workflow delete` empty match path returns `2`).

Gaps / Partials:
- ET class set (`ET-001`..`ET-008`) is not codified in runtime classes or output tags.
- CLI exit-code mapping does not implement policy table (`3/4/5/6/9` are not consistently surfaced; cancellation in import currently returns `0`).
- Unhandled exception boundary currently maps to exit code `1`, conflicting with policy `9`.
- Launcher has no explicit ET-class classification layer, creating surface-level taxonomy drift.

Drift Assessment:
- Material drift confirmed for this increment due to explicit mismatch between TDN-defined exit-code contract and implemented CLI/Launcher behavior.

Open Follow-ups (for backlog triage):
1. Introduce `ErrorClass` + central `ExitCodePolicy` in Core/Cli and refactor command handlers to use it.
2. Map cancellation to policy code `6` and global unhandled exceptions to `9`.
3. Add ET-class output tagging for all non-zero CLI exits.
4. Add launcher semantic ET classification model and component tests for parity with CLI taxonomy.

## Increment 5 - tdn-observability-and-operational-diagnostics

Summary:
- Current implementation provides useful human-readable diagnostics but does not implement the TDN's required structured observability contract.
- Operation-level correlation identifiers (`operationId`) and canonical event envelope fields are not systematically emitted by CLI or Launcher.
- Redaction and data-classification policy is not implemented as a centralized enforcement layer.

Confirmed Alignments:
- CLI has optional file logging and per-operation progress logging pathways.
- Launcher has global exception handling and operation-status messaging for user awareness.

Gaps / Partials:
- No shared `DiagnosticContext` carrying `operationId`, `surface`, and `backendMode`.
- No canonical start/progress/summary/error/cancelled structured event lifecycle.
- No explicit correlation propagation to HTTP headers and no support-facing operation reference in launcher dialogs.
- No centralized redaction helper with enforced secret/sensitive data classes.
- Automated tests do not assert observability envelope fields or redaction guarantees.

Drift Assessment:
- Material drift confirmed because core mandatory contract elements (correlation semantics, structured envelope, redaction enforcement) are not present in current implementation.

Open Follow-ups (for backlog triage):
1. Implement shared structured diagnostics primitives in Core (`DiagnosticContext`, envelope model, redactor).
2. Instrument CLI and Launcher operation lifecycles with required envelope events and operation IDs.
3. Add tests for required telemetry fields, correlation propagation, and redaction rules.
4. Update runbooks to include operationId-based support triage workflow.

## Increment 6 - tdn-session-persistence-security-boundaries

Summary:
- Launcher HTTP session persistence largely aligns with the TDN's secure-store and fail-closed principles (DPAPI protection, corruption-safe load, clear-on-failure paths).
- HTTP-vs-COM/Mock separation is implemented in launcher connection logic: non-HTTP or mock flows clear persisted HTTP session state.
- The primary contract gap remains remote viability verification during startup resume before declaring connected state.

Confirmed Alignments:
- Launcher `AuthSessionStore` uses DPAPI `CurrentUser` encryption for persisted HTTP auth session material.
- Resume flow checks expiry and clears persisted state on failed/exceptional resume paths.
- Logout and non-HTTP/mode-switch paths clear persisted HTTP session state.
- Convenience state (`ServerHistoryService`, COM profiles) remains separate from persisted auth session storage.

Gaps / Partials:
- Launcher resume currently trusts local `TryResumeSessionAsync` success without immediate remote viability check.
- CLI session store persists access/refresh token state as plaintext JSON in local app data, which is weaker than launcher secure-store posture.
- Test coverage for session boundary behaviors (resume invalidation, corruption fallback, mode transition clearing) is limited.

Drift Assessment:
- No material semantic drift found for launcher HTTP session boundaries.
- Minor drift risk remains due to missing immediate remote viability check and inconsistent storage-hardening posture between surfaces.

Open Follow-ups (for backlog triage):
1. Add a post-resume remote viability check in launcher before setting `Connected`.
2. Decide and document CLI session-storage security posture (harden to DPAPI or explicitly accept surface-scoped risk).
3. Add regression tests for resume-fail clear, logout clear, and HTTP->COM/mock transition clear invariants.

## Increment 7 - tdn-workflow-identity-and-mapping-invariants

Summary:
- Shared workflow service logic strongly enforces the core invariant set (step-name-based correlation, reviewer/notification identity verification, and hard-fail on missing persisted identities).
- COM adapter capabilities and constraints are explicit, and service behavior adapts by failing fast when identity-resolution capabilities are unavailable.
- Evidence for COM-path invariants is strong in service-level and COM-like test harnesses, but weaker in true COM runtime contract tests.

Confirmed Alignments:
- Step mapping avoids array-index-only matching and uses normalized step-name correlation for modify/create flows.
- Reviewer trustee and notification identity persistence are verified post-save; missing identities hard-fail operations.
- Share mutation/capability constraints are enforced explicitly for COM-like backends without silently weakening identity semantics.

Gaps / Partials:
- Most invariant tests run through mock/com-like clients rather than real COM adapter persistence in target environments.
- Alias-aware matching behavior is capability-dependent; COM no-directory-lookup scenarios rely on exact IDs and need stronger end-to-end evidence.
- Launcher surface-level tests for identity-failure semantics parity are limited compared to service/CLI-centric coverage.

Drift Assessment:
- No material drift found; implementation intent is largely congruent with TDN invariants.
- Minor drift risk remains from evidence depth on real COM runtime and cross-surface parity validation.

Open Follow-ups (for backlog triage):
1. Add COM integration tests validating identity invariants through actual COM persistence/readback.
2. Add explicit tests for user/group lookup disabled paths to verify deterministic hard-failure semantics.
3. Add launcher-facing parity tests asserting same invariant failure outcomes as CLI/service workflows.

## Increment 8 - tdn-workflow-save-boundary-and-canonicalization-contract

Summary:
- Shared workflow service implementation strongly follows the TDN save-boundary contract: full model application, one authoritative save boundary, canonical readback verification, and strict failure semantics for persistence mismatches.
- Notification ownership and duplication controls (step-level authoritative lists plus workflow-level clear) are explicitly implemented.
- Evidence depth is strongest in service-level tests and COM-like harnesses; direct COM runtime parity evidence remains thinner.

Confirmed Alignments:
- Full-snapshot authoring flow with save boundary and canonical post-save verification is implemented for create/modify.
- Reviewer and notification persistence checks are blocking and run before final success.
- Tag/untag cleanup behavior and warning semantics are preserved without masking primary failures.
- Share mutation capability is fail-fast when unsupported, preserving boundary semantics across modes.

Gaps / Partials:
- Most canonicalization tests execute against mock/com-like clients rather than real COM backend persistence.
- Launcher-specific orchestration tests for warning/failure surface parity are limited.
- Mode-specific COM side-effect coverage (native update/delete side effects) is represented mainly through documentation references, not direct automated runtime assertions.

Drift Assessment:
- No material drift found; implementation intent is largely aligned with the save-boundary and canonicalization contract.
- Minor drift risk remains due to evidence concentration in service-layer tests vs full COM runtime validation.

Open Follow-ups (for backlog triage):
1. Add COM integration tests for canonical readback mismatch and warning-path scenarios.
2. Add launcher parity tests for save warning/failure semantics and operator guidance.
3. Expand regression tests for COM-specific commit-side-effect visibility where feasible.

## Increment 9 - tdn-workflow-identity-and-serialization-contract-third-client

Summary:
- Core identity and serialization semantics are implemented with strong alignment to the TDN: enum values are represented as native-aligned char-code numerics, and step/trustee/notification mapping is explicit in shared workflow service logic.
- COM adapter mapping preserves type-code fidelity (`U/G/K/E/A`) and uses compatible trustee-add fallbacks for interop variation.
- Remaining partials are primarily evidence-depth and cross-surface behavior coverage, not a direct contract contradiction.

Confirmed Alignments:
- Workflow user types and notification actions serialize as numeric ASCII-aligned values via explicit JSON converters.
- Input-to-model mapping enforces trustee and notification identity semantics, including `Approvers` synthetic behavior and dedupe by identity tuple.
- COM adapter maps trustee type codes bidirectionally and preserves target type semantics at interop boundary.

Gaps / Partials:
- Limited end-to-end COM runtime tests asserting persisted identity rows/target IDs survive round-trip exactly as authored.
- Launcher-specific automated coverage for unresolved/unknown recipient retention and diagnostics is light.
- Serializer tests validate converter behavior well, but boundary tests on full outbound save payloads across all call paths can be expanded.

Drift Assessment:
- No material drift found; identity and enum semantics are largely congruent with the third-client contract and 11.4.5 baseline.
- Minor drift risk remains around edge-case parity evidence depth under real COM runtime and Launcher orchestration.

Open Follow-ups (for backlog triage):
1. Add COM integration tests for trustee/notification row round-trip with mixed recipient types.
2. Add launcher parity tests for unresolved identity handling and operator messaging.
3. Add payload-shape assertions ensuring no symbolic enum labels are emitted in save operations.

## Increment 10 - tdn-workflow-adept-power-tools-share-and-trustee-identity

Summary:
- Implementation largely preserves the intended separation between workflow definition persistence and share mutation capability boundaries: HTTP performs container-centric share mutation, while COM explicitly rejects share mutation in this client path.
- Trustee identity handling includes explicit canonicalization/resolution logic in service layer and preserves fail-fast behavior when backend directory lookup is unavailable.
- Remaining partials are mostly around depth of COM-native visibility/share-list parity evidence and launcher-surface parity tests.

Confirmed Alignments:
- HTTP share mutation is containerId-driven with explicit hard failures for missing workflow/container metadata.
- COM backend advertises and enforces unsupported share mutation and unsupported user/group directory lookup.
- Service validates share mutation capability before save completion and fails fast on unsupported state-change attempts.
- Trustee resolution/persistence logic emphasizes canonical IDs and hard-failure semantics when identity cannot be safely resolved.

Gaps / Partials:
- Limited direct COM-native runtime tests in ATP proving 11.4.5 share-list/container visibility nuances (private-by-default, in-use visibility exception).
- Cross-surface (CLI vs Launcher) automated parity coverage for trustee identity edge cases in COM mode is still light.
- Native share-list semantics are strongly documented in internal references but not comprehensively asserted by executable contract tests in this repo.

Drift Assessment:
- No material drift found; current implementation intent is broadly consistent with the TDN and 11.4.5 semantics.
- Minor drift risk remains due to evidence depth on COM-native visibility/share-list behavior and launcher parity automation.

Open Follow-ups (for backlog triage):
1. Add COM integration/contract tests for share-list visibility invariants and assignment-related visibility exceptions.
2. Add launcher parity tests for COM trustee identity resolution failures and operator guidance consistency.
3. Add explicit end-to-end tests for HTTP container-metadata share failures and COM share-mutation fail-fast behavior.

## Increment 11 - tdn-workflow-adept-power-tools-congruence-and-validation

Summary:
- Current implementation strongly enforces the intended congruence and validation contract in shared service logic, with clear preflight gates, post-save persistence verification, and explicit hard-error/warning distinctions.
- XML parsing and timeout/weekend-field handling align with documented congruence intent (nullable weekend semantics and explicit overwrite-only behavior).
- Remaining partials are primarily COM-native and launcher-surface parity evidence depth, not clear semantic contradiction.

Confirmed Alignments:
- Preflight validation covers workflow/step structural constraints, naming limits, trustee presence policy, and warning behavior for `AllowEmptyTrustees`.
- Trustee resolution logic enforces backend-capability-aware behavior and fails safely when lookup-dependent IDs cannot be resolved.
- Post-save verification hard-fails on reviewer/notification persistence mismatch, preserving congruence with contract intent.
- XML reader behavior for optional weekend fields and trustee role defaults is explicitly tested and aligned with TDN guidance.

Gaps / Partials:
- Limited direct COM runtime tests proving identical validation/congruence outcomes after native `Update()` write-back paths.
- Launcher-focused parity automation for validation and warning semantics is still limited compared to service/CLI coverage.
- Excel-vs-XML non-parity areas are recognized but not fully codified as executable policy assertions.

Drift Assessment:
- No material drift found; implementation intent is largely congruent with the TDN validation model.
- Minor drift risk remains due to evidence concentration in shared service tests vs full COM-native and launcher parity runs.

Open Follow-ups (for backlog triage):
1. Add COM integration tests validating preflight/post-save congruence boundaries on native persistence paths.
2. Add launcher parity tests for validation warnings/errors and persistence-mismatch messaging.
3. Add explicit tests/documented assertions for expected Excel-vs-XML capability differences.

## Increment 12 - tdn-workflow-adept-power-tools-hardening-notes

Summary:
- Hardening controls for the incident class are strongly present in shared workflow service logic: deterministic step targeting, step-scoped notification ownership, and congruence guards that prevent cross-step leakage.
- Bounded transient save retry for known disposed-context failures is implemented and covered in create/modify tests.
- Remaining partials are primarily around direct COM-native and launcher-surface parity evidence depth, not observed semantic contradiction.

Confirmed Alignments:
- Step notification ownership remains local to each step, with workflow-level notification lists cleared to prevent dual-source duplication.
- StepId congruence filtering and dedupe behavior are enforced during step configuration.
- Transient save retry guard is explicit and constrained to known status/signature conditions.
- Post-save verification plus cleanup sequencing (`untag`) is exercised by tests, including failure-path untag behavior.

Gaps / Partials:
- Most leakage-prevention evidence is service-level and COM-like mocks rather than real COM backend persistence.
- Launcher-oriented automated parity for hardening-specific outcomes (warnings/diagnostics) remains limited.
- Native 11.4.5 side-effect parity is documented but only partially asserted via executable contract coverage.

Drift Assessment:
- No material drift found; hardening intent is substantially aligned with TDN requirements.
- Minor drift risk remains from evidence concentration outside full COM-native and launcher end-to-end paths.

Open Follow-ups (for backlog triage):
1. Add COM integration tests for cross-step notification leakage prevention and final-state congruence.
2. Add launcher parity tests for hardening-path warnings/failures and cleanup messaging.
3. Add targeted regression tests around post-save failure + untag warning combinations in COM-like flows.

## Increment 13 - tdn-workflow-concurrency-locking-and-recovery-strategy

Summary:
- Shared service logic substantially implements the lock lifecycle contract: modify acquires explicit edit ownership, holds through save/verification, then releases with warning if cleanup fails.
- Delete path enforces lock exclusion semantics and uses bounded parallelism for operational safety.
- Remaining partials are mainly COM-native multi-lock-scope parity and Launcher message-parity depth, not observed contract contradiction.

Confirmed Alignments:
- Modify lock contention returns non-mutating skip with owner-aware message fallback (`Locked by ...`).
- Success and failure paths both include cleanup attempts; tests confirm untag happens after verification and on failure exits.
- Save-success + untag-fail message class exists as warning (not silent).
- Delete excludes locked workflows and applies deterministic filtering before execution.

Gaps / Partials:
- No direct runtime proof yet for COM-native distinct lock scopes (workflow edit vs single-admin vs save/copy serialization) mapped to shared outcome classes.
- Launcher-specific automated checks for contention/stale-lock guidance messaging are limited.
- Cancellation-path-specific coverage is lighter than success/failure coverage for cleanup semantics.

Drift Assessment:
- No material drift found; implementation intent is broadly aligned with TDN locking/recovery strategy.
- Minor drift risk remains due to evidence concentration in shared-service and COM-like tests rather than full COM-native + launcher parity suites.

Open Follow-ups (for backlog triage):
1. Add COM integration tests for native multi-lock-scope contention and recovery mapping.
2. Add launcher parity tests for contention/cleanup/stale-lock guidance consistency.
3. Add targeted cancellation-path cleanup and message-classification regression tests.

## Increment 14 - Deepening: tdn-workflow-identity-and-mapping-invariants

Deepening Objective:
- Convert service-heavy invariant confidence into COM-native and Launcher parity evidence.

Deepening Actions:
1. Add COM integration scenarios that assert reviewer and notification identity persistence after native write/readback, including mixed recipient kinds and alias/non-alias forms.
2. Add capability-boundary tests for lookup-disabled COM paths proving deterministic hard-failure classification and message shape.
3. Add Launcher parity tests that assert identical failure class and guidance text for invariant violations already covered in CLI/service tests.

Acceptance Signals:
- At least one COM-native round-trip invariant suite passes for create and modify.
- Launcher and CLI produce matching invariant-failure class outcomes for equivalent inputs.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (pending COM-native run stability)
- COM Launcher: Partial -> Implemented (pending parity test breadth)

## Increment 15 - Deepening: tdn-workflow-save-boundary-and-canonicalization-contract

Deepening Objective:
- Prove canonical post-save verification behavior on real COM persistence paths and cross-surface warning parity.

Deepening Actions:
1. Add COM integration tests for canonical readback mismatch handling, including explicit mismatch-on-reviewer and mismatch-on-notification cases.
2. Add Launcher operation tests for save success with cleanup warning, and save failure with cleanup warning, preserving primary-failure precedence.
3. Add COM runtime visibility-warning tests to ensure warning class and operator guidance remain deterministic.

Acceptance Signals:
- Canonical mismatch always hard-fails before success result emission.
- Cleanup warnings never mask primary save/verification failures.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if COM mismatch suite is green)
- COM Launcher: Partial -> Implemented (if warning/failure parity is green)

## Increment 16 - Deepening: tdn-workflow-identity-and-serialization-contract-third-client

Deepening Objective:
- Prove full fidelity of numeric enum/type-code identity semantics through native COM round-trip and surface parity.

Deepening Actions:
1. Add COM integration tests asserting persisted trustee/notification type codes and target IDs round-trip exactly, including Approvers synthetic mapping.
2. Add outbound payload-shape assertions across save paths confirming numeric codes only (no symbolic labels).
3. Add Launcher parity tests for unresolved/unknown recipient retention plus consistent diagnostics.

Acceptance Signals:
- No symbolic enum serialization appears in any save payload path.
- Native readback preserves authored type-code semantics.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if round-trip fidelity passes)
- COM Launcher: Partial -> Implemented (if unresolved-recipient parity coverage passes)

## Increment 17 - Deepening: tdn-workflow-adept-power-tools-share-and-trustee-identity

Deepening Objective:
- Close evidence gaps on native share visibility semantics and trustee identity parity in COM-constrained lookup conditions.

Deepening Actions:
1. Add COM integration tests for native visibility expectations (private-by-default plus assignment-related visibility conditions where exposed).
2. Add cross-surface parity tests for trustee resolution/persistence outcomes when COM directory lookup is unavailable.
3. Add explicit contract tests for COM share-mutation hard-fail and HTTP container-metadata hard-fail paths.

Acceptance Signals:
- COM share mutation remains explicit fail-fast with stable guidance.
- CLI and Launcher trustee outcomes match for equivalent COM-constrained inputs.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if native visibility suite passes)
- COM Launcher: Partial -> Implemented (if trustee parity suite passes)

## Increment 18 - Deepening: tdn-workflow-adept-power-tools-congruence-and-validation

Deepening Objective:
- Move congruence confidence from shared-service proof to COM-native + Launcher parity proof.

Deepening Actions:
1. Add COM integration tests for preflight validation boundaries and post-save mismatch failure semantics after native update.
2. Add Launcher parity tests for warning/error class consistency on trustee-resolution, validation, and persistence mismatch scenarios.
3. Add explicit executable assertions for intended Excel-vs-XML non-parity boundaries.

Acceptance Signals:
- Native COM update paths preserve same validation and mismatch semantics as shared contract.
- Launcher message class aligns with CLI/service for equivalent fault categories.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if native congruence suite passes)
- COM Launcher: Partial -> Implemented (if parity suite passes)

## Increment 19 - Deepening: tdn-workflow-adept-power-tools-hardening-notes

Deepening Objective:
- Prove incident-hardening controls under native COM persistence and surface-level orchestration.

Deepening Actions:
1. Add COM integration tests for cross-step notification leakage prevention under native write-back.
2. Add Launcher parity tests validating same hardening outcomes and warnings for interactive flows.
3. Add targeted regression tests for post-save failure plus untag warning combinations in COM-like and COM-native contexts.

Acceptance Signals:
- No cross-step notification bleed in native save/modify cycles.
- Warning/failure ordering remains stable across CLI and Launcher.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if native leakage-prevention suite passes)
- COM Launcher: Partial -> Implemented (if hardening parity suite passes)

## Increment 20 - Deepening: tdn-workflow-concurrency-locking-and-recovery-strategy

Deepening Objective:
- Validate lock/recovery strategy against native COM lock scopes and strengthen cancellation/recovery parity.

Deepening Actions:
1. Add COM integration tests for lock-scope contention matrix (workflow edit, save/copy serialization, single-admin contention where exposed) mapped to shared outcome classes.
2. Add Launcher parity tests for contention/warning class and stale-lock guidance consistency.
3. Add cancellation-path tests that verify cleanup attempt semantics, lock release behavior, and deterministic result classification.

Acceptance Signals:
- Native lock-scope contention outcomes map deterministically to shared status/message classes.
- Cancellation never leaves ambiguous cleanup state in surfaced outcomes.

Expected Rating Impact:
- COM CLI: Partial -> Implemented (if native lock matrix passes)
- COM Launcher: Partial -> Implemented (if contention/cancellation parity passes)

## Workflow Deepening Batch Summary

Execution Mode:
- Continuous, no-approval-per-increment flow authorized by request.

Planned Sequencing:
1. Implement COM-native integration harness additions first (highest confidence uplift).
2. Implement Launcher parity suites second (cross-surface congruence uplift).
3. Implement cancellation/recovery and warning-order regressions third (operational resilience uplift).

Re-rating Rule:
- Upgrade a workflow TDN from Partial only after both conditions are met:
	1) Native COM evidence confirms contract semantics for the TDN.
	2) Launcher parity evidence confirms matching class-level outcomes with CLI/service behavior.
