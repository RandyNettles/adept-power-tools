# User Stories - Import

## US-IMP-001 End-to-End Import Processing
As an import operator, I want workbook/config-based import processing so that I can execute mapping validation and row processing from prepared files.

Acceptance Criteria
1. Import reads workbook and optional XML config.
2. Import resolves field mappings and validates data before run.
3. Import executes row processing with outcome reporting.

FR Traceability
- FR-032

## US-IMP-002 Mapping Rule Enforcement
As an import operator, I want strict mapping validation so that invalid configurations are blocked before runtime mutation.

Acceptance Criteria
1. Validation enforces required keys and mode consistency.
2. Validation detects duplicate or conflicting mapping definitions.
3. Validation enforces date-range/operator requirements.

FR Traceability
- FR-033

## US-IMP-003 Predictable Search Construction
As an import operator, I want deterministic search term building so that matching behavior is consistent and explainable.

Acceptance Criteria
1. Search terms are built for supported operators.
2. Date values are normalized as required by search behavior.
3. Rows with missing required key data are skipped from search execution.

FR Traceability
- FR-034

## US-IMP-004 Outcome-Based Row Handling
As an import operator, I want row actions to branch on match cardinality and mode so that updates/creates/skips are applied correctly.

Acceptance Criteria
1. No-match rows follow configured mode behavior.
2. Single-match rows support update or search-only skip behavior.
3. Multi-match rows are skipped with explanatory result output.

FR Traceability
- FR-035

## US-IMP-005 Safe Dry-Run Import
As an import operator, I want dry-run mode to simulate results without mutation so that I can verify impact before executing.

Acceptance Criteria
1. Dry-run does not invoke mutation APIs.
2. Dry-run returns per-row synthetic results.
3. Result messages clearly indicate dry-run semantics.

FR Traceability
- FR-036

## US-IMP-006 Field Resolution Reliability
As an import operator, I want mapped fields resolved by canonical or display names so that templates remain usable across naming variants.

Acceptance Criteria
1. Resolver supports canonical field names.
2. Resolver supports display-name lookups.
3. Unresolved mapped fields fail validation before run.

FR Traceability
- FR-037

## US-IMP-007 Client Import UX Controls
As a client user, I want an import UI with file ingest, validation, run/cancel, and field export so that I can execute import workflows interactively.

Acceptance Criteria
1. UI supports workbook/config browse and drag-drop ingest.
2. UI supports validation and dry-run execution.
3. UI supports run and cancel behavior with progress/results.
4. UI supports field-definition export.

FR Traceability
- FR-038

## US-IMP-008 Template Generation UX
As a client user, I want to generate and open import/workflow templates so that I can start with valid spreadsheet structures.

Acceptance Criteria
1. UI generates import template workbook artifacts.
2. UI generates workflow template workbook artifacts.
3. UI can open generated files or containing folders.

FR Traceability
- FR-039
