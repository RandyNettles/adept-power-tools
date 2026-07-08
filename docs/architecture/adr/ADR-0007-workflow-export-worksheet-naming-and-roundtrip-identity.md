# ADR-0007: Workflow Export Worksheet Naming and Round-Trip Identity

- Status: Accepted
- Date: 2026-07-08
- Revised: 2026-07-08

## Context

US-WF-007 (FR-041..FR-045) requires exporting one or more existing workflows to a single Excel
workbook with one workflow per worksheet, where the exported workbook is directly consumable by
the `workflow modify` operation.

The workflow Excel reader derives the target workflow name from the worksheet tab name: sheets are
named `WF-<name>`, and the reader strips the `WF-` prefix to obtain the workflow name. The modify
operation then matches that name against server workflows (case-insensitive) to locate the workflow
to update.

Excel imposes constraints on worksheet names that workflow names do not share:
- Worksheet names are limited to 31 characters.
- Worksheet names may not contain `: \ / ? * [ ]`.
- Worksheet names must be unique within a workbook.

Workflow names in Adept can exceed 28 characters (31 minus the `WF-` prefix), can contain the
illegal characters, and two distinct workflows can produce the same sheet name after sanitization.
Because modify resolves the target workflow from the sheet name, any lossy transformation of the
name breaks the export → modify round trip: the operator edits an export and the apply either
targets the wrong workflow or fails to match.

Three strategies were considered.

**Option A — Sanitize/truncate the sheet name only (no other identity carrier)**
The workflow name is sanitized and truncated to fit the sheet-name rules, and the sheet name
remains the sole identity carrier for modify.

**Option B — Fail export for names that cannot be represented exactly**
Any workflow whose name does not fit the sheet-name rules is skipped or rejected during export.

**Option C — Sanitize the sheet name but persist the authoritative workflow name in the sheet, and
teach the reader to prefer it**
The sheet name is sanitized/truncated/uniquified for display and Excel validity, while the exact
workflow name is written into a dedicated cell in the sheet. The reader prefers the persisted name
for identity and falls back to the sheet-tab name when the cell is absent.

Option A was rejected because truncation and character removal are lossy: a truncated or altered
name no longer matches the server workflow, silently causing modify to target the wrong workflow or
fail. It also cannot disambiguate two workflows that sanitize to the same sheet name.

Option B was rejected because it blocks legitimate, common workflows (long or specially-named)
from being exported at all, defeating the purpose of the feature for exactly the workflows an
operator is most likely to want to edit in bulk.

Option C was selected. It keeps sheet names Excel-valid and human-readable while preserving exact
identity, and it degrades gracefully for existing hand-authored templates that have no persisted
name cell.

## Decision

Export writes each workflow to a sheet named `WF-<sanitized-name>` and persists the authoritative
workflow name inside the sheet.

Sheet-name rules:
- Start with the `WF-` prefix.
- Remove or replace Excel-illegal characters (`: \ / ? * [ ]`).
- Truncate the full sheet name (including `WF-`) to 31 characters.
- Ensure uniqueness within the workbook by appending a numeric disambiguator when two workflows
  would otherwise collide.

Identity rules:
- The exact, unmodified workflow name is written to a fixed cell in the sheet (Row 1).
- The workflow Excel reader prefers the persisted in-sheet workflow name when present.
- When the persisted name cell is absent (existing hand-authored templates), the reader falls back
  to the sheet-tab name after the `WF-` prefix.

Modify resolves the target workflow using the name the reader returns, so identity survives sheet-
name sanitization.

## Consequences

- Positive: workflows with long names, illegal characters, or colliding sanitized names can all be
  exported and round-tripped through modify.
- Positive: sheet names remain Excel-valid and readable.
- Positive: backward compatible — existing templates with no persisted name cell still work via the
  sheet-tab fallback.
- Positive: two distinct workflows that sanitize to the same sheet name remain distinguishable by
  their persisted names.
- Trade-off: the workflow Excel reader must change to read and prefer the persisted name cell; this
  is a reader behavior change, not only a writer addition.
- Trade-off: operators who manually rename a sheet tab expecting to retarget a different workflow
  must also update the persisted name cell; the sheet tab is no longer the sole source of identity.
- Accepted risk: if an operator deletes or corrupts the persisted name cell, identity falls back to
  the (possibly sanitized) sheet-tab name. This is documented in the runbook.

## Implementation Notes

- Sanitization and uniqueness live in the export writer (`WorkflowExcelWriter`).
- The persisted name cell is Row 1 of each `WF-` sheet, consistent with the template layout.
- The reader preference (persisted name over sheet-tab name, with fallback) lives in
  `WorkflowExcelReader`.
- Tests should cover: names over 31 characters, names containing each illegal character, two
  workflows sanitizing to the same sheet name, reader preference when the cell is present, and
  reader fallback when the cell is absent.
- Runbook guidance should explain that the persisted name cell is authoritative for modify and
  that renaming only the sheet tab does not retarget a different workflow.

## Related

- US-WF-007 — User story driving this decision.
- FR-043 — Worksheet naming and round-trip identity requirement.
- FR-041, FR-042 — Export service and detail reconstruction.
- tdn-workflow-export-roundtrip-fidelity-contract.md — full export/round-trip contract.
- Workflow-template-runbook.md — operator-facing template format.
