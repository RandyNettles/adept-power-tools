# TDN - CLI and Launcher Contract Parity

## Purpose

Define the behavioral parity contract between CLI and WPF Launcher surfaces for shared Adept Power Tools capabilities.

This TDN establishes:
- What must remain semantically equivalent across both surfaces.
- What interaction differences are explicitly allowed.
- How parity is verified and governed as features evolve.

## Scope

In scope:
- Shared capability families exposed in both surfaces: auth/connect, workflow operations, and import operations.
- Contract-level parity requirements for outcomes, safety, validation, and error semantics.
- Cross-surface traceability to implementation baseline FRs and flow docs.

Out of scope:
- Pixel/UI layout and terminal formatting specifics.
- Feature areas intentionally launcher-only (for example, template generation, connect-first navigation UX).
- Transport/backend internals beyond surface-observable behavior.

## Source Context

Primary references:
- docs/design/user-flows/Index.md
- docs/functional_requirements/FR-AdeptTools-Implementation-Baseline-2026-06-29.md
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Commands/AuthCommands.cs
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- src/AdeptTools.Cli/Commands/ImportCommands.cs
- src/AdeptTools.Launcher/ViewModels/MainViewModel.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs
- src/AdeptTools.Launcher/ViewModels/ImportViewModel.cs

## Decision Summary

1. Shared operations exposed by both surfaces must produce equivalent semantic outcomes when given equivalent inputs and backend mode.
2. Safety and mutation boundaries (dry-run, confirmation/cancel, eligibility filtering, fail-fast behavior) are parity-critical and must not diverge by surface.
3. Presentation differences (table/dialog/progress style) are allowed when they do not alter operation semantics or final state.
4. Launcher-only and CLI-only affordances are allowed only when classified as surface-specific enhancements, not semantic forks of shared operations.
5. Every cross-surface capability change must include parity impact review and explicit test/update coverage.

## Parity Model

### Level 1: Semantic parity (required)

For shared operations, both surfaces must agree on:
- Effective input interpretation.
- Validation pass/fail behavior.
- Mutation boundary behavior.
- Result classification and completion status.
- Error/failure semantics.

### Level 2: Interaction parity (required at intent level)

Both surfaces must provide equivalent user intent control for:
- Preflight validation (dry-run/review pathways).
- Cancellation/abort paths for long-running operations.
- Safety confirmation for destructive actions.

Implementation may differ (prompt vs dialog), but intent and effect must be equivalent.

### Level 3: Presentation parity (not required)

Differences are allowed for:
- Layout, grouping, icons, and status rendering.
- Table output versus list/cards.
- Console logging style versus UI result panes.

Constraint:
- Presentation differences must not obscure or change semantic result categories.

## Shared Capability Parity Contracts

## 1) Runtime and Backend Selection

Parity requirements:
1. Both surfaces must support backend-driven execution semantics (HTTP/COM/Mock behavior) through the same backend service contracts.
2. Mock mode must remain non-production simulation and must not silently replace production backend behavior on failure.
3. Server requirement for non-mock execution must be enforced consistently for runnable operations.

Allowed differences:
- CLI uses global command options.
- Launcher uses connect-form controls and profile/history affordances.

## 2) Authentication and Session Establishment

Parity requirements:
1. Authentication success/failure is authoritative from shared auth service behavior, not surface-local heuristics.
2. Multi-account selection (status-230 style disambiguation) must preserve identical account-selection semantics.
3. Logout must clear active auth context and prevent stale connected state reuse.

Allowed differences:
- Launcher may perform startup session resume and connect-first gating as surface UX policy.
- CLI may perform command-time session reuse/resume from its session store policy.

Non-parity note:
- Session persistence mechanism can differ by surface (launcher secure local store vs CLI lightweight local store), as long as resulting auth validity semantics remain equivalent.

## 3) Workflow List/Create/Modify/Delete

Parity requirements:
1. Shared workflow service behavior is authoritative for list filtering, create/modify validation, trustee mapping, and delete eligibility outcomes.
2. Dry-run must remain non-mutating in both surfaces.
3. Destructive delete must preserve equivalent safety posture:
   - explicit user acknowledgment path before mutation, unless force/explicit user intent is already given,
   - locked/non-deletable items excluded consistently,
   - skipped/failure reasons surfaced.
4. Final operation summaries must preserve equivalent counts and statuses (succeeded/failed/skipped/total semantics).

Allowed differences:
- CLI may use prompt-based confirmation and manifest output options.
- Launcher may use dialog-based confirm with pre-categorized deletable/skipped partitions.
- CLI may expose machine-readable list formats (csv/json) not present in launcher UI.

## 4) Import Pipeline (Validate/Run)

Parity requirements:
1. Import validate/run semantics must remain service-authoritative for both surfaces.
2. Dry-run import must not invoke mutation paths in either surface.
3. Row-level outcome categories (updated/created/skipped/failed) and summary rollups must remain semantically equivalent.
4. Cancellation must stop forward row processing and preserve partial results accumulated before cancellation.

Allowed differences:
- CLI may emphasize command-output logs and file-based logs.
- Launcher may emphasize interactive progress bars and results panel workflows.

## Safety and Failure Contract Parity

Required parity rules:
1. Validation failures are blocking and non-mutating in both surfaces.
2. Operation cancellation does not report successful completion.
3. Partial-success scenarios must report mixed outcomes (not collapsed to full success).
4. Unsupported mode/capability combinations must fail fast with explicit guidance in both surfaces.
5. Exceptions are surfaced with actionable operator-facing messages while preserving non-zero/failed outcome semantics.

## Surface-Specific Enhancements (Allowed)

### CLI enhancements

- Automation-focused flags and non-interactive paths.
- Explicit output format controls for list/export operations.
- Script-friendly exit code signaling and manifest/log artifacts.

### Launcher enhancements

- Connect-first navigation gating.
- Server URL history and COM profile management.
- Rich visual progress, dialogs, and result copy/export UX.

Governance constraint:
- Enhancements must not redefine shared operation semantics.

## Parity Matrix (Current Baseline)

| Capability | CLI | Launcher | Parity Class | Notes |
|---|---|---|---|---|
| Backend mode selection | Yes | Yes | Required semantic parity | Different control surfaces, same backend intent. |
| Auth test/connect | Yes | Yes | Required semantic parity | Different interaction shape, same auth outcome model. |
| Multi-account selection | Yes | Yes | Required semantic parity | Prompt vs dialog allowed. |
| Session resume | Yes | Yes | Equivalent intent, surface policy allowed | Resume timing/storage differs by surface. |
| Workflow list/filter | Yes | Yes | Required semantic parity | CLI has additional csv/json export affordance. |
| Workflow create/modify dry-run | Yes | Yes | Required semantic parity | Launcher staged apply UX is allowed. |
| Workflow delete safety | Yes | Yes | Required semantic parity | Force flag vs dialog confirmation mechanics differ. |
| Import validate/run dry-run | Yes | Yes | Required semantic parity | Progress rendering differs by surface. |
| Template generation | No | Yes | Launcher-only feature | Out of parity scope. |

## Traceability to FR Baseline

Shared parity anchors:
- FR-004, FR-005, FR-006, FR-007, FR-008 (CLI behavior)
- FR-031, FR-038 (Launcher workflow/import behavior)
- FR-015 through FR-022 (auth/session service behaviors and launcher resume)

Flow index anchors:
- UF-US-CLI-001 through UF-US-CLI-006
- UF-US-CONN-01 through UF-US-CONN-06
- UF-US-WF-006
- UF-US-IMP-007

## Parity Verification Checklist

For any change touching shared capabilities, verify:
1. Equivalent input scenarios produce equivalent semantic outcomes in both surfaces.
2. Dry-run remains non-mutating in both surfaces.
3. Cancellation paths preserve partial-state reporting without false success.
4. Safety confirmations remain present before destructive mutations.
5. Error classes map to failed command/result outcomes consistently.
6. Mode-specific unsupported paths fail fast with guidance in both surfaces.
7. FR/user-flow traceability remains current for parity-impacting changes.

## Regression Test Strategy (Minimum)

1. Auth: success, failure, multi-account selection, logout, resumed-session invalidation.
2. Workflow: list/filter parity, create/modify dry-run parity, delete eligibility and confirmation parity.
3. Import: validate parity, dry-run parity, mixed row outcomes parity, cancellation parity.
4. Mode matrix: HTTP/COM/Mock parity checks for shared operations.

## Change Governance

Any PR that changes shared CLI/Launcher capabilities should include:
1. Parity impact statement (affected contract sections).
2. Updated FR/flow references if behavior changed.
3. Evidence of parity validation for both surfaces.
4. Explicit note when a change is intentionally surface-specific and why it does not alter semantic parity.

## Open Questions

1. Should parity checks be formalized as an automated cross-surface conformance suite in CI?
2. Should result status vocabulary be normalized into a shared response envelope for both surfaces?
3. Should launcher expose additional machine-readable export paths for workflow/import summaries to tighten automation parity?
