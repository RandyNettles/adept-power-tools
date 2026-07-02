# CLI Runbook

## Purpose
This runbook describes how to build, run, validate, and troubleshoot the Adept Tools CLI.

Use this for:
- Local developer execution
- QA and support verification
- Operating published binaries from the `publish/` folder

## Scope
This runbook covers commands implemented in the CLI:
- `auth`
- `workflow`
- `import`

## Prerequisites
- Windows machine with network access to the target Adept server (unless using `--mock`)
- .NET SDK installed
- Repository cloned locally

Optional:
- Excel workbook (`.xlsx`) and XML config (`.xml`) for workflow and import operations

## Paths
Repository root:
- `C:\dev\code\Product\AdeptPowerTools`

CLI project:
- `src/AdeptTools.Cli`

Published output:
- `publish/`

## Global CLI Options
These options are available to all commands:
- `--server`, `-s`: Adept server URL (required unless `--mock`)
- `--user`, `-u`: Username
- `--mock`, `-m`: Mock mode (no server required)
- `--backend`, `-b`: Backend type (`http` or `com`), default `http`
- `--verbose`, `-v`: Verbose output
- `--log`: Log file path

Behavior note:
- If `--mock` is not used, `--server` is required for command execution.

## Quick Start
From repository root:

```powershell
dotnet build
```

Run CLI in mock mode:

```powershell
dotnet run --project src/AdeptTools.Cli -- auth test --mock
```

Show help:

```powershell
dotnet run --project src/AdeptTools.Cli -- --help
```

Use helper script:

```powershell
.\run.ps1 -Project cli -Args "auth test --mock"
```

## Publish and Run Published EXE
Publish both CLI and launcher:

```powershell
.\publish.ps1
```

Run published CLI directly:

```powershell
.\publish\adept-tool.exe --server https://adept.example.com --user ADM auth test
```

## Command Run Procedures

### 1) Authentication Smoke Test
Mock mode:

```powershell
dotnet run --project src/AdeptTools.Cli -- auth test --mock
```

HTTP backend (SSO):

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com --user ADM --backend http auth test
```

COM backend (prompts for password):

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com --user ADM --backend com auth test
```

Expected result:
- Exit code `0`
- Status reports connected

### 2) Workflow Listing
Table output:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow list --filter "AP_*" --format table
```

CSV output:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow list --format csv
```

JSON output:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow list --format json
```

### 3) Workflow Create or Modify
Create from Excel:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow create --excel C:\temp\workflow.xlsx
```

Modify from XML:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow modify --xml C:\temp\workflow.xml
```

Dry-run validation:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow create --excel C:\temp\workflow.xlsx --dry-run
```

### 4) Workflow Delete (High Risk)
Preview only:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow delete --filter "TEST_*" --dry-run
```

Delete with confirmation prompt:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow delete --filter "TEST_*"
```

Delete without prompt (automation):

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow delete --filter "TEST_*" --force
```

Write manifest while deleting:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow delete --filter "TEST_*" --manifest C:\temp\delete-manifest.json --force
```

Safety notes:
- Avoid wildcard-only filter (`*`) unless explicitly intended.
- Wildcard-only deletion requires `--force`.
- If documents are in-process, they are moved to the system workflow.

### 5) Import Flow
Fetch field definitions:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import fetch-fields --output C:\temp\fields.csv --format csv
```

Generate mapping from Excel headers:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import map --excel C:\temp\input.xlsx --output C:\temp\mapping.xml
```

Validate import:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import validate --excel C:\temp\input.xlsx --config C:\temp\mapping.xml
```

Run import dry-run:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import run --excel C:\temp\input.xlsx --config C:\temp\mapping.xml --dry-run
```

Run import with detailed log:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import run --excel C:\temp\input.xlsx --config C:\temp\mapping.xml --log-file C:\temp\import-run.log
```

## Exit Codes
Common meanings observed from command handlers:
- `0`: Success (or user-cancel in some interactive paths)
- `1`: Command completed with failures or validation/auth failure
- `2`: Special case errors (for example, no workflows matched in delete, or import error list returned)

Operational recommendation:
- Treat any non-zero exit code as failure in CI/automation.

## Logging and Diagnostics
- Use `--verbose` to print detailed progress
- Use `--log` global option when supported by service components
- For import runs, use `--log-file` to write detailed row outcomes

Example:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com --verbose import run --excel C:\temp\input.xlsx --config C:\temp\mapping.xml --log-file C:\temp\import.log
```

## Troubleshooting

### Error: `--server is required unless --mock is specified`
Cause:
- Command was executed without server URL and without mock mode.

Fix:
- Add `--server https://your-server` or add `--mock` for local testing.

### Auth fails in HTTP mode
Cause:
- SSO/browser auth failed, wrong tenant/session, or endpoint mismatch.

Fix:
- Confirm URL and user
- Retry auth test with explicit user
- Validate browser and SSO access outside CLI

### No workflows matched filter
Cause:
- Filter too narrow or wrong naming pattern.

Fix:
- Re-run `workflow list` with broader filter
- Confirm environment/server is correct

### Import validation fails
Cause:
- Missing/incorrect mapping, invalid source columns, or data issues.

Fix:
- Run `import validate`
- Regenerate mapping with `import map`
- Correct input workbook and rerun

## Automation Guidance
For scripts and pipelines:
- Always pass explicit `--server`, `--backend`, and target file paths
- Use `--dry-run` before destructive operations
- Capture process exit code and fail pipeline on non-zero
- Persist logs/artifacts (manifest JSON, import log files)

## Related Files
- `README.md`
- `run.ps1`
- `publish.ps1`
- `publish/list-workflows.bat`
- `src/AdeptTools.Cli/Program.cs`
- `src/AdeptTools.Cli/Commands/AuthCommands.cs`
- `src/AdeptTools.Cli/Commands/WorkflowCommands.cs`
- `src/AdeptTools.Cli/Commands/ImportCommands.cs`
