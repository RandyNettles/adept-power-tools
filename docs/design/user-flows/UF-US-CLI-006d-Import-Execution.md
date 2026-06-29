# UF-US-CLI-006D: Import Execution

## Goal
Allow users to execute imports non-interactively with visibility into progress and outcomes.

## User Flow (Primary)

1. User runs `import run` with input data and mapping configuration.
2. The system validates the input before execution.
3. If valid, the system processes the import.
4. Progress is displayed during execution.
5. The system displays a summary of results (processed, succeeded, failed, skipped).
6. The command exits successfully if no failures occurred; otherwise, it exits with failure.

## Alternate and Exception Flows

### A1: Validation Failure
- Input data fails validation
- CLI displays errors
- Command exits with failure

### A2: Runtime Errors
- Errors occur during execution
- CLI displays error details
- Command exits with failure

### A3: Dry-Run Mode
- User specifies dry-run option
- The system simulates execution without making changes
- Progress and summary are still displayed
- Command exits successfully

### A4: Execution Cancelled
- User cancels the operation
- The system stops processing safely
- Command exits without applying further changes

## Postconditions
- Import is either executed or simulated safely
- User receives a clear summary of results

## Acceptance Mapping
- AC1: Import group supports run
  - Covered by User Flow step 1
- AC2: Required arguments validated
  - Covered by A1
- AC3: Failures return non-zero status
  - Covered by A1 and A2

## User Experience Notes
- Progress output should provide visibility into long-running operations
- Summary totals should align with detailed output
- Dry-run output should clearly indicate no changes were made
- Errors should be actionable and tied to specific items where possible