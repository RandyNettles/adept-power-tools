# TDN - Import Pipeline Architecture and Failure Model

## Purpose

This TDN defines the architecture-level execution model for the import pipeline, including:
- Pipeline stages and stage boundaries.
- Idempotency expectations by stage and operation type.
- Retry boundaries and non-retry boundaries.
- Row-level outcome semantics and failure reporting behavior.

## Scope

In scope:
- Import validate and import run behavior for Excel-driven and XML+Excel flows.
- End-to-end pipeline sequencing in ImportService.
- Row-level processing outcomes and summary rollup semantics.
- Failure and cancellation behavior at batch and row levels.

Out of scope:
- UI-specific rendering details.
- Transport-level resiliency policies outside import orchestration.
- Non-import workflow operations.

## Pipeline Architecture

The import pipeline is stage-oriented and gate-driven.

Authoritative stage sequence:
1. Parse input.
2. Resolve fields.
3. Validate mappings and data.
4. Dry-run gate.
5. Row processing loop.
6. Complete/finalize.

Progress phases exposed by service:
- Parsing
- Validating
- Processing
- Complete

## Stage Contracts

### Stage A: Parse input

Inputs:
- Excel-only all-in-one workbook, or
- XML config + Excel data.

Outputs:
- ImportConfig
- Column mappings
- Data rows

Failure behavior:
- Parse exceptions fail batch before row processing starts.
- No row-level outcomes are emitted before successful parse.

### Stage B: Resolve fields

Behavior:
- Resolve configured mappings against available Adept field definitions.
- Produce search-key mappings, fill-field mappings, and resolved field metadata.

Failure behavior:
- If resolution fails, batch returns with Errors populated.
- Row loop is not entered.

### Stage C: Validate

Validation checks include:
- Minimum mapping viability (search keys required).
- Fill-field requirements (except SearchResultsOnly mode).
- Duplicate search key and duplicate fill-field detection.
- DateBetween operand completeness.
- AddIfNotFound S_LONGNAME width checks where applicable.
- Date parsing warnings for date-typed search keys.

Failure behavior:
- Validation errors fail batch before execution.
- Validation warnings do not block execution by themselves.

### Stage D: Dry-run gate

Behavior:
- If config.DryRun is true (or request dry-run override is true), execution stops before any write operation.
- Synthetic row-level results are emitted as Skipped with dry-run messaging.

Contract:
- Dry-run is non-mutating.
- Dry-run still executes parse, resolve, and validation stages.

### Stage E: Row processing loop

For each row:
1. Build search request from search-key mappings.
2. Execute search.
3. Branch by match cardinality and config flags.
4. Update existing document, or create then update, or skip/fail.

Row processing decisions:
- Empty search key => Skipped.
- MatchCount = 0 and AddIfNotFound = false => Skipped (Not found).
- MatchCount = 0 and AddIfNotFound = true => Create then update path.
- MatchCount = 1 and SearchResultsOnly mode => Skipped (match-only reporting).
- MatchCount = 1 and mutable mode => Update path.
- MatchCount > 1 => Skipped (Multiple matches).

### Stage F: Complete/finalize

Behavior:
- Aggregate counts (Updated, Created, Skipped, Failed).
- Emit final Complete phase progress event.
- Return row result list plus batch-level Errors.

## Row-Level Outcome Semantics

Row outcomes are explicit and mutually exclusive:
- Updated
- Created
- Skipped
- Failed

Outcome meanings:
- Updated: SaveDataCard succeeded with at least one persisted field update.
- Created: Create succeeded and post-create update path completed.
- Skipped: Row intentionally not mutated (dry-run, no match, multiple matches, search-only mode, or no fields to update).
- Failed: Row encountered non-recoverable processing error.

Primary key display:
- Derived from first search key if available; otherwise row-number fallback.

## Failure Model

### Batch-fatal failures (pre-row)

These fail batch before row loop:
- Input parse failure.
- Field resolution failure.
- Validation failure.

Batch behavior:
- Errors list populated.
- Results may be empty when failure occurs before dry-run synthesis.

### Row-scoped failures (in-loop)

Row failures do not stop the full batch by default.
Examples:
- Create API failure.
- Save API failure.
- Exception during row processing.
- Create path with missing required creation identity (for example empty S_LONGNAME).

Batch behavior:
- Failed counter increments for that row.
- Processing continues to next row unless cancellation requested.

### Cancellation behavior

- CancellationToken is honored before each row and during awaited API calls.
- OperationCanceledException propagates to caller.
- Partial row results accumulated before cancellation remain valid.

## Idempotency Expectations

Import is not globally idempotent by default; idempotency is mode- and mapping-dependent.

### Idempotent or near-idempotent paths

- SearchResultsOnly mode: observational, non-mutating.
- Dry-run: non-mutating.
- UpdateDataCard with stable source values and deterministic search cardinality may be operationally idempotent for repeated runs.

### Non-idempotent risk paths

- AddIfNotFound create path can produce new documents when no match is found; repeated runs may create additional documents if search keys do not become unique/stable.
- FillOverwrite mappings can repeatedly reapply values and may re-trigger downstream system effects.

### Practical idempotency contract

1. Operators must use deterministic search keys to control create/update branch stability.
2. AddIfNotFound should be treated as non-idempotent unless search uniqueness is guaranteed.
3. Dry-run should be used as the preflight for high-risk runs.

## Retry Boundaries

### In-scope retries

Current import orchestration does not implement automatic row-level retry loops for create/save/search calls.

### Explicit boundary

- ImportService attempts each row operation once.
- Transient resilience, if any, must come from lower API client layers or external orchestration.

### Non-retry zones

Do not auto-retry inside this layer for:
- Validation errors.
- Deterministic mapping/data issues.
- Multiple-match ambiguity.

Rationale:
- These are semantic/data errors, not transient transport faults.

## Data Integrity and Mapping Rules

1. At least one search key is required.
2. Fill fields are required unless SearchResultsOnly mode.
3. Duplicate mapping targets are invalid and must be rejected.
4. DateBetween search operators require paired range-column configuration.
5. If FillIfEmpty is present, existing values are fetched before deciding writes.
6. Empty computed update set yields Skipped (no fields to update), not failure.

## Create Path Semantics

Create path preconditions:
- AddIfNotFound = true.
- Search returned zero matches.
- Create identity value available (S_LONGNAME-derived value).

Create path sequence:
1. CreateNewDocument(workAreaId, fileName).
2. Update mapped fields on new document.
3. Optional check-in to library when library mapping keys resolve.

Failure handling:
- Create failure => Failed row.
- Save failure after create => Failed row.
- Library resolution absent => no check-in, non-fatal.

## Observability Contract

Minimum per-row observability:
- RowNumber
- PrimaryKeyDisplay
- Outcome
- Message
- FieldsUpdated (when applicable)

Minimum per-batch observability:
- TotalRows
- Updated/Created/Skipped/Failed counters
- Errors list for pre-row fatal paths
- DryRun flag

Progress events should report:
- Current phase
- Current row and total rows during processing
- Current primary key and last row outcome message

## Mode Notes

### HTTP

- Primary production path for field lookup, search, save, create, and check-in API calls.
- Error messages are surfaced from API client responses into row/batch messages.

### COM

- Must preserve the same stage and outcome semantics.
- Backend-specific API mechanics may differ, but stage gates and row outcome contract remain unchanged.

### Mock

- Must emulate stage transitions and row outcome semantics for testability.
- Dry-run and SearchResultsOnly behavior must remain non-mutating in mock mode.

## Traceability

Primary source context:
- docs/runbooks/Import-template-runbook.md
- docs/design/user-flows/UF-US-IMP-007-Client-Import-Operations.md
- src/AdeptTools.Import/Services/ImportService.cs
- src/AdeptTools.Import/Services/MappingValidator.cs
- src/AdeptTools.Import/Models/ImportBatchResult.cs
- src/AdeptTools.Import/Api/IImportApiClient.cs

## Operational Checklist

1. Parse gate rejects invalid workbook structure before row processing.
2. Resolve/validate gates block run on invalid mappings.
3. Dry-run emits row-level synthetic results with no writes.
4. Row-level failures do not abort full batch unless cancelled.
5. Multiple-match rows are skipped with explicit cardinality message.
6. AddIfNotFound create path requires valid create identity input.
7. Batch summary counters equal the sum of row outcomes.
