# TDN - Error Taxonomy and Exit-Code Policy

## Purpose

Define a single architecture-level error taxonomy and exit-code policy for Adept Power Tools CLI operations so that:
- Automation can reliably branch on process outcomes.
- Support workflows can classify failures consistently.
- Command families (auth, workflow, import) follow the same outcome contract.

## Scope

In scope:
- CLI process exit semantics.
- Error classification taxonomy for command and service failures.
- Mapping rules from operation outcomes to stable exit codes.
- Guidance for logging and support triage.

Out of scope:
- Launcher UI presentation semantics.
- Backend-specific wire payload schemas.
- Detailed retry strategies for individual services.

## Source Context

Primary references:
- docs/runbooks/CLI-runbook.md
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Commands/AuthCommands.cs
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- src/AdeptTools.Cli/Commands/ImportCommands.cs
- src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs
- src/AdeptTools.Core/Models/ApiResult.cs
- src/AdeptTools.Core/Auth/AuthResult.cs
- src/AdeptTools.Workflow/Results/WorkflowOperationResult.cs
- src/AdeptTools.Import/Enums/ImportOutcome.cs

## Problem Statement

Current CLI behavior includes command-specific non-zero signaling, but no single architecture policy defines:
- A canonical error class set.
- Stable code meanings across command families.
- Consistent treatment of cancellation, usage errors, validation failures, and partial failures.

This causes ambiguity in CI scripts and support triage when different commands reuse the same code for different failure causes.

## Decision Summary

1. Introduce a canonical CLI error taxonomy with explicit class identifiers.
2. Assign stable process exit codes by taxonomy class, not by command implementation detail.
3. Require all CLI commands to map final outcome into the same policy table.
4. Preserve non-zero as failure for automation while improving diagnosability through class-level consistency.
5. Keep operation-level result statuses (OK/FAIL/SKIP/ADD) independent from process-level exit code mapping.

## Error Taxonomy

Each failure must map to one primary class.

### ET-001 Usage and Argument Errors

Definition:
- Invalid/missing required inputs or invalid option combinations.

Examples:
- Missing server in non-mock runs.
- Invalid format option value.
- Missing required source file argument.

Operator action:
- Correct command invocation and rerun.

### ET-002 Environment and Configuration Errors

Definition:
- Runtime setup, dependency, or host environment issues before operation logic can complete.

Examples:
- Service registration/configuration failures.
- COM runtime availability/marshalling prerequisites not met.
- Invalid server URL normalization/base address setup.

Operator action:
- Fix environment/configuration, then rerun.

### ET-003 Authentication and Authorization Errors

Definition:
- Login/session establishment failure, account-selection failure, or permission denial that prevents requested operation.

Examples:
- Auth test fails due to login failure.
- Multi-account selection invalid/aborted in mandatory selection path.
- Session resume rejected and fresh auth fails.

Operator action:
- Re-authenticate, correct account/context, or verify privileges.

### ET-004 Validation and Policy Errors

Definition:
- Business-rule or input-content validation failures where execution is intentionally blocked before mutation.

Examples:
- Import mapping validation errors.
- Workflow input validation/trustee resolution validation errors.
- Safety-policy rejection (for example broad delete without required force intent).

Operator action:
- Correct input/policy conditions and rerun.

### ET-005 Operation Execution Failures

Definition:
- Runtime execution attempted but one or more operations failed due to API/backend/business execution failure.

Examples:
- Workflow create/modify/delete returns failed items.
- Import run returns failed rows.
- API operation returns failure status with actionable message.

Operator action:
- Inspect detailed result messages/logs, remediate data/system issue, and retry as appropriate.

### ET-006 No-Op / Empty Match Outcomes

Definition:
- Command completed but no eligible targets or actionable rows were found.

Examples:
- Workflow delete found no deletable matches for filter/status criteria.

Operator action:
- Adjust filter/scope if action was expected.

### ET-007 Cancellation Outcomes

Definition:
- Operation was intentionally cancelled by user/operator.

Examples:
- In-flight import/workflow operation cancelled via cancellation token.

Operator action:
- None required unless operation should be resumed.

### ET-008 Unexpected Internal Errors

Definition:
- Unhandled exceptions or unknown failures outside expected/typed operation paths.

Examples:
- Unexpected exception handled by global CLI exception handler.

Operator action:
- Capture logs/context and escalate for engineering investigation.

## Exit-Code Policy

Canonical process exit codes:

| Exit Code | Class | Meaning |
|---|---|---|
| 0 | Success | Operation succeeded with no failed items and no blocking errors. |
| 1 | ET-005 | Operation execution failure (one or more attempted items failed). |
| 2 | ET-004 or ET-006 | Validation/policy-blocked operation or no actionable targets found. |
| 3 | ET-001 | Usage/argument error. |
| 4 | ET-003 | Authentication/authorization failure. |
| 5 | ET-002 | Environment/configuration failure. |
| 6 | ET-007 | Operation cancelled by user/operator. |
| 9 | ET-008 | Unexpected internal/unhandled error. |

Notes:
- Automation rule remains simple: any non-zero code is failure.
- Class-level code identity enables deterministic triage and retry policy decisions.

## Mapping Rules

Apply in priority order (first matching terminal condition wins):

1. Unhandled exception boundary reached: return 9.
2. Explicit cancellation observed: return 6.
3. Usage/argument parse or guard rejection: return 3.
4. Environment/configuration bootstrap failure: return 5.
5. Authentication/authorization failure: return 4.
6. Validation/policy block before execution: return 2.
7. No actionable targets (successful no-op completion): return 2.
8. Execution completed with failed items/rows: return 1.
9. Execution completed with zero failures: return 0.

## Command Family Policy

### Auth commands

- Success path: 0.
- Authentication failure: 4.
- Invalid mandatory user selection input in auth selection flow: 4.
- Argument/usage error: 3.
- Unexpected exception: 9.

### Workflow commands

- Usage mistakes (missing required file arg, invalid option values): 3.
- Safety policy block (for example unsafe broad delete without force): 2.
- No deletable matches: 2.
- Completed run with failed operations: 1.
- Cancelled operation: 6.
- Fully successful run: 0.

### Import commands

- Validation failure in validate command: 2.
- Import run with pre-row blocking errors list: 2.
- Import run with row-level failures: 1.
- Cancelled run: 6.
- Fully successful run: 0.

## Relationship to Result Statuses

Process exit code and per-item result statuses serve different layers:

- Per-item statuses represent item granularity:
  - Workflow: Success, Fail, Skip.
  - Import: Updated, Created, Skipped, Failed.
- Exit code represents process-level terminal outcome class.

Policy:
- Do not infer a new taxonomy from item labels.
- Determine process code from terminal class mapping rules.

## Logging and Diagnostics Contract

On non-zero exit, CLI output should include:
1. A one-line terminal error class tag (for example ET-004).
2. Human-readable summary message.
3. Command-specific detail lines (validation errors, failed items, row failures).
4. Pointers to produced artifacts where applicable (manifest, log file).

Recommended structured pattern:
- ET-xxx: summary message
- Details: ...
- Action: ...

## Backward Compatibility and Rollout

Current runbook describes coarse meanings for 0, 1, and 2.
This TDN refines policy with additional stable codes while preserving non-zero failure semantics.

Rollout guidance:
1. Update CLI command handlers to return canonical class codes.
2. Update global exception handler to return 9 for unhandled exceptions.
3. Update runbook exit-code table and troubleshooting sections.
4. Add lightweight tests asserting expected exit code for representative failure classes.

## Regression Checklist

1. Missing required server in non-mock run returns code 3.
2. Invalid option value returns code 3.
3. Service bootstrap/configuration failure returns code 5.
4. Auth failure returns code 4.
5. Validation-only failure returns code 2.
6. No-op empty-target workflow delete returns code 2.
7. Partial execution failures return code 1.
8. User cancellation returns code 6.
9. Unhandled exception returns code 9.

## Open Questions

1. Should an explicit ET class field be added to structured JSON output modes for machine parsing?
2. Should support tooling map ET classes to runbook remediation playbooks automatically?
3. Should launcher operational logs adopt the same ET taxonomy labels even without process exit codes?
