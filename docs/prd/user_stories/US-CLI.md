# User Stories - CLI

## US-CLI-001 Global Runtime Options
As a CLI operator, I want global options for server, user, backend, and mock mode so that I can run commands consistently in different environments.

Acceptance Criteria
1. CLI accepts global options for server URL, username, backend type, mock mode, verbose output, and log path.
2. Global options are available across command groups.
3. Non-mock execution without server input fails with a clear error.

FR Traceability
- FR-001
- FR-002
- FR-003

## US-CLI-002 Authentication Smoke Test
As an administrator, I want an auth test command so that I can validate connectivity and login configuration before running operations.

Acceptance Criteria
1. `auth test` invokes backend-specific authentication.
2. On success, command reports identity/server details.
3. On failure, command reports actionable error output.

FR Traceability
- FR-004

## US-CLI-003 Workflow Listing and Filtering
As a workflow admin, I want to list workflows with filters and export formats so that I can inspect inventory and automate reporting.

Acceptance Criteria
1. `workflow list` supports filter input.
2. Output supports table, csv, and json formats.
3. Filtered results exclude non-matching items.

FR Traceability
- FR-005

## US-CLI-004 Workflow Change Execution
As a workflow admin, I want to create or modify workflows from files with dry-run support so that I can validate before making server changes.

Acceptance Criteria
1. `workflow create` accepts Excel or XML input.
2. `workflow modify` accepts Excel or XML input.
3. `--dry-run` performs validation-only behavior without mutating server workflows.

FR Traceability
- FR-006

## US-CLI-005 Safe Workflow Deletion
As a workflow admin, I want controlled delete options and guardrails so that I do not accidentally delete broad sets of workflows.

Acceptance Criteria
1. `workflow delete` supports filter, status, dry-run, force, and manifest options.
2. Broad or wildcard deletion requires explicit force behavior.
3. Dry-run and manifest output provide pre-delete visibility.

FR Traceability
- FR-007

## US-CLI-006 Import Pipeline Commands
As an import operator, I want command-level access to fetch, map, validate, and run steps so that I can execute imports non-interactively.

Acceptance Criteria
1. Import group supports fetch-fields, map, validate, and run commands.
2. Each command validates required arguments before execution.
3. Failures return non-zero status with error output.

FR Traceability
- FR-008
