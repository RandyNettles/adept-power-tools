# TDN - Workflow Export and Round-Trip Fidelity Contract

- Status: Proposed (planned feature — not yet implemented)
- Date: 2026-07-08
- Related: US-WF-007; FR-041, FR-042, FR-043, FR-044, FR-045; ADR-0007

## Purpose

This TDN defines the implementation contract for exporting one or more existing workflows to a
single Excel workbook (one workflow per worksheet) that is directly consumable by the workflow
`modify` operation.

The controlling requirement is round-trip fidelity: exporting a workflow and then running
`workflow modify` against the unmodified export must be a semantic no-op. An operator exports a
workflow, edits the workbook, and applies the edits through modify.

## Scope

In scope:
- The reverse-mapping contract from the server-canonical workflow model to the Excel template.
- The exact workbook layout the writer produces, defined to match the reader.
- Worksheet naming, sanitization, uniqueness, and authoritative-name persistence.
- The reader change required to prefer the authoritative in-sheet workflow name.
- CLI and Launcher surface obligations for export.

Out of scope:
- The save-boundary and canonicalization contract for create/modify (see
  `tdn-workflow-save-boundary-and-canonicalization-contract.md`).
- Trustee identity resolution semantics (see
  `tdn-workflow-adept-power-tools-share-and-trustee-identity.md`).
- Import pipeline template contracts.

## Data Source

Export reads server-canonical state; it does not derive from the workflow list packet.

- The workflow list model (`WorkflowAdminItem`) carries only aggregate counts (step count,
  trustee count, reviewer/notify/alert counts). It does not carry step names, trustee IDs, memo,
  or shared/deadline detail.
- Export therefore fetches full detail per selected workflow via
  `IWorkflowApiClient.GetWorkflowAsync(workflowId)`, which returns a `WorkflowEditModel`:
  - `WorkflowDefinition`: name, active, shared, memo, deadline/timeout fields.
  - `WorkflowStepModels`: ordered steps, each with `WorkflowStepDefinition`
    (name, order, required-approvals count, auto-advance, timeout flags),
    `WorkflowTrusteeDefinitions`, and per-step email/alert notification lists.

The `WorkflowId` from the list selection is the key used to fetch detail. Selection is by
explicit checkbox (Launcher) or by filter (CLI).

## Produced Workbook Layout

The writer MUST produce the layout the reader consumes. The reader is the source of truth; the
writer mirrors it (and mirrors the launcher template generator that produces the blank template).

### Config sheet

A `Config` sheet with named ranges:
- `ServerUrl`
- `ProjectName`
- `DryRun` — written as `true` by default so an accidental immediate apply is a dry run.

### Workflow sheets

One sheet per workflow, named `WF-<sanitized-name>` (see Worksheet Naming). The blank template
sheet `WF-_Template`, if present, is ignored by the reader and is not emitted per workflow.

Fixed header cells (column 2):
- Row 1: authoritative workflow name (see Worksheet Naming and ADR-0007).
- Row 3: `Memo`
- Row 4: `Deadline (days)`
- Row 5: `Active` (`true`/`false`)
- Row 6: `Shared` (`true`/`false`)

Step table:
- The reader locates the header row by scanning column 1 for `Step Name`; the writer emits the
  header row in the same fixed position the template generator uses.
- Header columns, in order:
  `Step Name` | `Approvals Required` | `Auto Advance` | `Allow Empty Trustees` | `Trustee` | `Type` | `Role`
- Each step's first trustee is written on the step's header data row (with `Step Name`
  populated). Additional trustees for the same step are written on continuation rows with an
  empty `Step Name` cell; `Type` and `Role` are written explicitly on each row.

Boolean cells use the reader-accepted canonical values `true` / `false`.

## Reverse Mapping Contract

The export inverts the mapping the reader/`TrusteeTypeMapper` apply on ingest.

### Trustee type

`WorkflowUserType` (`User`, `Group`, `Meta`/`Key`, `Email`, `Approvers`) is written to the `Type`
column using the same string tokens the reader accepts. Trustees are written as their
login/trustee IDs (reader-native), not display names, so the exported file is directly
re-consumable by modify without re-resolution.

### Role

Role is reconstructed from the step's server collections:
- `Reviewer` — from `WorkflowStepModel.WorkflowTrusteeDefinitions` (the reviewer/approver
  collection).
- `Notify` — from the step's email-notification list (approval/advance notification action).
- `Alert` — from the step's alert-notification list (timeout/deadline escalation action).

Notification entries whose action is approval/advance map to `Notify`; entries whose action is
timeout map to `Alert`. Where the server model distinguishes reviewer versus approver via trustee
flags, the writer preserves the reviewer role token and relies on the same flag semantics the
reader applies on ingest.

Note: trustee flag semantics (reviewer vs approver) are defined by the active API client
implementation (HTTP/COM). The writer's reverse mapping is specified as best-effort congruent
with that implementation; the round-trip test suite is the authority that keeps the two aligned.

## Worksheet Naming and Round-Trip Identity

See ADR-0007 for the decision and rejected alternatives. Contract summary:

- Worksheet name is `WF-` + a sanitized form of the workflow name:
  - Excel-illegal characters (`: \ / ? * [ ]`) are removed or replaced.
  - The full sheet name (including the `WF-` prefix) is truncated to Excel's 31-character limit.
  - Collisions within the workbook are disambiguated with a numeric suffix so every sheet name is
    unique.
- The authoritative, unmodified workflow name is written to the worksheet (Row 1) so identity is
  never lost to sanitization.
- The workflow Excel reader MUST prefer the authoritative in-sheet workflow name when present and
  fall back to the sheet-tab name after `WF-` when it is absent. This preserves backward
  compatibility with existing hand-authored templates and keeps modify targeting the correct
  server workflow even when the sheet name was sanitized.

## Round-Trip Guarantee

- Export → modify against an unmodified export is a semantic no-op: the same steps, trustees,
  roles, and workflow-level fields resolve to the same server state.
- Identity resolution for modify uses the authoritative workflow name, not the possibly-sanitized
  sheet-tab name.
- Dry-run remains the default posture of the exported `Config` sheet; CLI `--dry-run` continues to
  control whether an apply mutates the server.

## Surface Boundaries (CLI and Client)

### CLI surface

- `workflow export` writes the workbook to a specified output path and selects workflows by
  filter. Output and result reporting are script-friendly.
- The command reuses the shared workflow service export path; it does not implement its own
  workbook writer.

### Client surface

- The launcher exposes "Export Selected to Excel", gated on a non-empty selection, using a
  save-file dialog for the output path.
- Export reuses the shared operation-runner pattern for progress, cancellation, and per-workflow
  result messages.

Boundary rule:
- Surface differences may affect interaction style but not the produced workbook layout, the
  reverse-mapping contract, or the round-trip guarantee.

## Test Obligations

- Layout parity: exported workbook is accepted by `WorkflowExcelReader` without error.
- Round-trip: export → read produces an input model equal to the source workflow's semantic state
  (steps, trustees, roles, memo, deadline, active, shared).
- Naming: long names, names with each illegal character, and duplicate sanitized names all produce
  valid, unique sheet names while preserving authoritative identity.
- Reader preference: reader resolves identity from the in-sheet name when present and from the
  sheet-tab name when absent.
- Role mapping: reviewer, notify (approval), and alert (timeout) collections each round-trip to the
  correct `Role` token.
- Multi-workflow: exporting multiple selected workflows yields one sheet each plus a single
  `Config` sheet.

## Related

- US-WF-007 — User story driving this feature.
- FR-041..FR-045 — Functional requirements for export service, detail reconstruction, naming,
  CLI, and launcher UX.
- ADR-0007 — Worksheet naming and round-trip identity decision.
- tdn-workflow-save-boundary-and-canonicalization-contract.md — modify save semantics.
- Workflow-template-runbook.md — operator-facing template format.
