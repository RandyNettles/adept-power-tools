# Workflow Template Runbook

## Purpose
This runbook describes the expected structure of the workflow Excel template and how Adept Power Tools reads it when running workflow create or modify operations.

Use this runbook for:
- Filling out a generated workflow template workbook
- Defining one workflow with multiple steps
- Defining multiple workflows in a single workbook

## Scope
This runbook covers the Excel workflow template consumed by:
- `workflow create --excel <path>`
- `workflow modify --excel <path>`

The workbook format described here is based on the current template generator and Excel reader implementation.

## Expected Format

### Workbook Layout
The workflow workbook uses:
- One `Config` sheet
- One or more workflow sheets whose names start with `WF-`

Important rules:
- Only sheets with names beginning with `WF-` are treated as workflows.
- The template sheet `WF-_Template` is ignored by the reader.
- The workflow name comes from the worksheet name after the `WF-` prefix.

Example:
- `WF-Document Review` creates a workflow named `Document Review`
- `WF-Change Order Approval` creates a workflow named `Change Order Approval`

### Config Sheet
The `Config` sheet is optional for workflow ingestion, but the generated template includes it.

For the workflow workbook, the relevant setting is:
- `DryRun`: `true` or `false`

Notes:
- The launcher generates `DryRun=true` by default.
- CLI `--dry-run` still controls whether the operation mutates the server.

### Workflow Sheet Structure
Each workflow sheet is expected to follow this layout:

Rows above the step table:
- Row 3, column 2: `Memo`
- Row 4, column 2: `Deadline (days)`
- Row 5, column 2: `Active`

Step table header row:
- Row 7 must contain these headers:
  - `Step Name`
  - `Approvals Required`
  - `Auto Advance`
  - `Trustee`
  - `Type`
  - `Role`

Data rows:
- Step data starts on row 8.
- A new workflow step begins whenever `Step Name` is non-empty.
- Rows below that step with an empty `Step Name` cell are treated as additional trustee rows for the current step.

### Column Meanings
- `Step Name`: Name of the workflow step. Required for the first row of each step.
- `Approvals Required`: Integer value, `0` or greater.
- `Auto Advance`: Boolean. Accepted true values include `true`, `yes`, `1`, `✓`. Accepted false values include `false`, `no`, `0`, `✗`.
- `Trustee`: Trustee identifier. This may be a single ID or a comma-separated list of IDs.
- `Type`: One of `User`, `Group`, `Meta`, or `Email`.
- `Role`: One of `Reviewer`, `Notify`, or `Alert`.

### Validation Rules
The validator currently enforces these core rules:
- The workbook must contain at least one workflow.
- Each workflow must have a name.
- Each workflow must contain at least one step.
- Each step must have a name.
- `Approvals Required` cannot be negative.
- Duplicate workflow names in the same batch are rejected.

Warnings:
- A step without trustees is allowed, but generates a warning.

Practical constraints:
- Workflow and step name maximum lengths are enforced using server-provided limits when available.
- Maximum workflows per batch and maximum steps per workflow are also checked against server limits when available.

## Define One Workflow With Multiple Steps

### Pattern
To define a single workflow:
1. Copy or rename the template sheet so the sheet name starts with `WF-`.
2. Put workflow-level settings in the fixed cells near the top of the sheet.
3. Add one row for each new step using a non-empty `Step Name` cell.
4. Add extra trustee rows under a step by leaving `Step Name` blank and filling only `Trustee`, `Type`, and `Role`.

### Example
This example defines one workflow named `Document Review` with three steps.

Sheet name:
- `WF-Document Review`

Top-of-sheet values:
- `Memo`: `Review and approval flow for controlled documents`
- `Deadline (days)`: `5`
- `Active`: `true`

Step table:

| Step Name | Approvals Required | Auto Advance | Trustee | Type | Role |
| --- | ---: | --- | --- | --- | --- |
| Draft | 0 | false | jsmith | User | Reviewer |
|  |  |  | eng-managers | Group | Notify |
| Review | 2 | true | mjones | User | Reviewer |
|  |  |  | akhan | User | Reviewer |
|  |  |  | pm-notify | Group | Alert |
| Approved | 0 | false |  |  |  |

How the reader interprets this:
- `Draft` is the first step.
- The next row belongs to `Draft` because `Step Name` is blank.
- `Review` starts a new step.
- The next two rows belong to `Review`.
- `Approved` starts a third step and has no trustees.

### Comma-Separated Trustees
If multiple trustees share the same type and role, they may be placed in a single cell.

Example:

| Step Name | Approvals Required | Auto Advance | Trustee | Type | Role |
| --- | ---: | --- | --- | --- | --- |
| Review | 1 | false | jsmith, mdoe, akhan | User | Reviewer |

This is equivalent to creating three reviewer trustees for the `Review` step.

## Define Multiple Workflows

### Pattern
To define multiple workflows in one workbook:
1. Keep the single shared `Config` sheet.
2. Create one worksheet per workflow.
3. Ensure every workflow worksheet name starts with `WF-`.
4. Give each workflow a unique name by using a unique worksheet name after the prefix.

The reader processes all matching sheets in the workbook and builds one batch request containing all workflows.

### Example Workbook
Example worksheet set:
- `Config`
- `WF-Document Review`
- `WF-Change Order Approval`
- `WF-Vendor Submittal`
- `WF-_Template`

How this is interpreted:
- `WF-Document Review` is included as workflow `Document Review`
- `WF-Change Order Approval` is included as workflow `Change Order Approval`
- `WF-Vendor Submittal` is included as workflow `Vendor Submittal`
- `WF-_Template` is ignored

### Example Process
To create multiple workflows from one workbook:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow create --excel C:\temp\workflow-template.xlsx --dry-run
```

Then apply the batch:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow create --excel C:\temp\workflow-template.xlsx
```

For modifying existing workflows from the same workbook format:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com workflow modify --excel C:\temp\workflow-template.xlsx --dry-run
```

## Recommended Authoring Steps
1. Generate a fresh workflow template from the client.
2. Rename `WF-_Template` to your first workflow name, keeping the `WF-` prefix.
3. Duplicate that sheet for each additional workflow.
4. Update the duplicated sheet name so each workflow name is unique.
5. Fill in workflow metadata and step rows.
6. Run a dry-run before applying changes.

## Troubleshooting

### Workbook Loads but No Workflows Are Found
Cause:
- No worksheet names start with `WF-`.

Fix:
- Rename each workflow worksheet to begin with `WF-`.

### A Workflow Is Missing From the Batch
Cause:
- The worksheet name does not start with `WF-`, or the sheet is still named `WF-_Template`.

Fix:
- Rename the worksheet to `WF-YourWorkflowName`.

### Steps Merge Unexpectedly
Cause:
- A continuation row was intended to start a new step, but `Step Name` was left blank.

Fix:
- Enter a `Step Name` value on the first row of every new step.

### Trustees Are Not Parsed
Cause:
- `Type` is missing or not one of the accepted values.

Fix:
- Use only `User`, `Group`, `Meta`, or `Email` in the `Type` column.

### Duplicate Workflow Name Error
Cause:
- Two workflow sheets resolve to the same workflow name.

Fix:
- Ensure the portion of each worksheet name after `WF-` is unique.