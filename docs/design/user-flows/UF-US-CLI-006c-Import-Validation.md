# UF-US-CLI-006C: Import Validation

## Goal
Allow users to verify import data and mappings before execution to prevent errors.

## User Flow (Primary)

1. User runs `import validate` with input data and mapping configuration.
2. The system checks the data and mappings for errors and warnings.
3. Validation results are displayed, including any issues found.
4. The command exits successfully if no errors are found.

## Alternate and Exception Flows

### A1: Validation Errors
- The system detects errors in data or mapping
- CLI displays a list of issues
- Command exits with failure

### A2: Invalid Inputs
- Required files are missing or invalid
- CLI displays a clear error message
- Command exits with failure

## Postconditions
- User understands whether the import is safe to run
- Errors can be corrected before execution

## Acceptance Mapping
- AC1: Import group supports validate
  - Covered by User Flow step 1
- AC2: Required arguments validated
  - Covered by A2
- AC3: Failures return non-zero status
  - Covered by A1

## User Experience Notes
- Errors should clearly identify the affected records or fields
- Warnings should not block execution but should be visible
- Output should support iterative correction and re-validation