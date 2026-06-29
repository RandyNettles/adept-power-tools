# UF-US-CLI-006A: Import Field Discovery

## Goal
Allow users to retrieve available system fields so they can prepare import data correctly.

## User Flow (Primary)

1. User runs `import fetch-fields`.
2. The system retrieves the list of available fields from the selected backend.
3. The system outputs the field list in a structured format (CSV or JSON).
4. The command exits successfully.

## Alternate and Exception Flows

### A1: Backend Retrieval Failure
- The system cannot retrieve field definitions
- CLI displays a clear error message
- Command exits with failure

## Postconditions
- User has a complete list of available fields for use in imports
- Output can be reused for mapping or documentation

## Acceptance Mapping
- AC1: Import group supports fetch-fields
  - Covered by User Flow step 1
- AC2: Required arguments validated
  - Covered by command structure (no required args beyond runtime)
- AC3: Failures return non-zero status
  - Covered by A1

## User Experience Notes
- Output should be consistent and machine-readable
- Field names should match those expected in mapping/configuration
