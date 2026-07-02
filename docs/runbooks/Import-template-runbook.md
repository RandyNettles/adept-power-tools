# Import Template Runbook

## Purpose
This runbook describes the expected structure of the import Excel template and how Adept Power Tools reads it when running import validate and import run operations.

Use this runbook for:
- Filling out a generated import template workbook
- Defining config values for import behavior
- Defining field mappings and data rows in a single workbook

## Scope
This runbook covers the Excel import template consumed by:
- `import validate --excel <path>`
- `import run --excel <path>`

The workbook format described here is based on the current template generator, workbook reader, and import service behavior.

## Expected Format

### Workbook Layout
The import workbook is expected to contain exactly these sheets:
- `Config`
- `IMP-Mapping`
- `IMP-Data`

These sheet names are significant:
- `Config` is required
- `IMP-Mapping` is required
- `IMP-Data` is required

If any of these sheets is missing, the workbook is rejected.

### Self-Contained Workbook Behavior
The generated import template is an all-in-one workbook.

That means:
- Import settings come from the `Config` sheet
- Column mappings come from the `IMP-Mapping` sheet
- Source rows come from the `IMP-Data` sheet

When you run the CLI with only `--excel`, the service reads all three from the workbook.

Example:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import validate --excel C:\temp\import-template.xlsx
```

You can still use a separate XML config with `--config`, but the generated template does not require one.

## Config Sheet

### Expected Settings
The generated template includes these settings on the `Config` sheet:
- `ImportMode`
- `AddIfNotFound`
- `WorkAreaId`
- `HeaderRows`
- `SkipHiddenRows`
- `DryRun`

### Meaning of Each Setting
- `ImportMode`: Controls whether matching rows update documents or only report matches.
- `AddIfNotFound`: Controls whether a missing search match should create a document.
- `WorkAreaId`: Optional work area target used when creating documents.
- `HeaderRows`: The row number in `IMP-Data` that contains the column headers.
- `SkipHiddenRows`: If `true`, hidden rows in `IMP-Data` are ignored.
- `DryRun`: If `true`, the import validates and reports results without making changes.

### Accepted Values

`ImportMode`:
- `Update`
- `UpdateDataCard`
- `Search`
- `Search Results Only`
- `SearchResultsOnly`

Reader behavior:
- Blank or unrecognized values default to update mode.
- Search-only values map to `SearchResultsOnly`.

`AddIfNotFound`:
- Accepted true values: `true`, `yes`, `1`, `✓`
- Any other value is treated as `false`

`SkipHiddenRows`:
- Accepted true values: `true`, `yes`, `1`, `✓`
- Any other value is treated as `false`

`DryRun`:
- Accepted true values: `true`, `yes`, `1`, `✓`
- Any other value is treated as `false`

`HeaderRows`:
- Positive integer
- Default is `1`

### Generated Defaults
The generated template starts with:
- `ImportMode = Update`
- `AddIfNotFound = false`
- `WorkAreaId =` blank
- `HeaderRows = 1`
- `SkipHiddenRows = true`
- `DryRun = true`

## IMP-Mapping Sheet

### Required Columns
The mapping table must include these required columns:
- `ExcelColumn`
- `AdeptField`
- `Action`

Optional columns supported by the reader:
- `Operator`
- `DateRangeColumn`

Header matching is flexible for a few names:
- `ExcelColumn` or `Excel Column`
- `AdeptField` or `Adept Field`
- `DateRangeColumn` or `Date Range Column`

The reader scans the first 10 rows of the sheet to find the mapping header row.

### Column Meanings
- `ExcelColumn`: Name of the column header in `IMP-Data`
- `AdeptField`: Adept field name to map to
- `Action`: How the field is used during import
- `Operator`: Optional search operator for search-key mappings
- `DateRangeColumn`: Optional companion column used for date-between searches

### Accepted Action Values
The reader accepts these action families:

Search key:
- `SearchKey`
- `Search Key`
- `Search`
- `0`, `1`, `2`, `3`

Fill if empty:
- `FillIfEmpty`
- `Fill If Empty`
- `IfEmpty`
- `4`

Fill overwrite:
- `FillOverwrite`
- `Fill Overwrite`
- `Overwrite`
- `Fill`
- `5`

Do not import:
- `DoNotImport`
- `Do Not Import`
- `Skip`
- `Ignore`
- `6`

Reader behavior:
- Blank or unrecognized values default to `DoNotImport`
- Rows marked `DoNotImport` are skipped from the effective mapping set

### Accepted Operator Values
The reader accepts these search operator families:

Equals:
- `Equals`
- `Eq`
- `=`
- `0`

Date after:
- `DateAfter`
- `Date After`
- `After`
- `1`

Date before:
- `DateBefore`
- `Date Before`
- `Before`
- `2`

Date between:
- `DateBetween`
- `Date Between`
- `Between`
- `3`

Reader behavior:
- Blank or unrecognized values default to `Equals`

### Minimum Valid Mapping
At least one effective mapping row should exist, and every `ExcelColumn` referenced here must match a header in `IMP-Data`.

Generated starter row:

| ExcelColumn | AdeptField | Action |
| --- | --- | --- |
| Title | DESCRIPTION | Set |

Important note:
- The generated workbook uses `Set` in the sample row.
- The current reader does not recognize `Set` as a special action keyword.
- Unrecognized actions are treated as `DoNotImport`.

Practical implication:
- Replace `Set` with a recognized action such as `FillOverwrite` before using the workbook.

## IMP-Data Sheet

### Expected Format
`IMP-Data` contains the source rows to import.

Rules:
- The header row is determined by `HeaderRows` from the `Config` sheet.
- Each mapping in `IMP-Mapping` must reference a header that exists in `IMP-Data`.
- Rows below the header are treated as candidate data rows.
- Entirely blank rows are ignored.
- Hidden rows are skipped when `SkipHiddenRows=true`.

### Example
If `HeaderRows = 1`, a simple data sheet might look like this:

| Title | DocumentNumber | Revision |
| --- | --- | --- |
| Pump Datasheet | P-1001 | A |
| Valve Schedule | V-2040 | B |

In that case:
- `Title`, `DocumentNumber`, and `Revision` are the header names available to mappings
- Each subsequent non-empty row becomes one import row

## Define a Complete Import Workbook

### Pattern
To define a complete import workbook:
1. Fill in import behavior on the `Config` sheet.
2. Define column mappings on `IMP-Mapping`.
3. Populate source data rows on `IMP-Data`.
4. Ensure every mapped `ExcelColumn` exactly matches a header in `IMP-Data`.

### Example Workbook

`Config`:

| Setting | Value |
| --- | --- |
| ImportMode | Update |
| AddIfNotFound | false |
| WorkAreaId | |
| HeaderRows | 1 |
| SkipHiddenRows | true |
| DryRun | true |

`IMP-Mapping`:

| ExcelColumn | AdeptField | Action | Operator |
| --- | --- | --- | --- |
| DocumentNumber | S_LONGNAME | SearchKey | Equals |
| Title | DESCRIPTION | FillOverwrite | |
| Revision | REVISION | FillIfEmpty | |

`IMP-Data`:

| DocumentNumber | Title | Revision |
| --- | --- | --- |
| P-1001 | Pump Datasheet | A |
| V-2040 | Valve Schedule | B |

How this is interpreted:
- `DocumentNumber` is used to locate the target document
- `Title` overwrites the target `DESCRIPTION` field
- `Revision` fills `REVISION` only if the target value is empty

## Define Search-Only Imports

To produce match-only reporting without updating data:
1. Set `ImportMode` to `Search` or `Search Results Only`
2. Define one or more search-key mappings
3. Run validate or run

Behavior:
- Matching rows are reported
- Data is not updated in search-results-only mode

## Define Create-When-Missing Imports

To allow creation when a match is not found:
1. Set `AddIfNotFound` to `true`
2. Provide a search key that can identify the document to create
3. Ensure required creation fields are available

Important behavior:
- When creating a document, the service expects `S_LONGNAME` to resolve from a search-key mapping value.
- If `S_LONGNAME` is empty for a row, creation fails for that row.

## Recommended Authoring Steps
1. Generate a fresh import template from the client.
2. Update `Config` defaults for your scenario.
3. Replace the sample mapping row with valid actions.
4. Add search-key mappings first.
5. Add fill mappings second.
6. Paste or enter source rows into `IMP-Data`.
7. Run validation before running the import.

## Example Commands

Validate the workbook only:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import validate --excel C:\temp\import-template.xlsx
```

Run in dry-run mode:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import run --excel C:\temp\import-template.xlsx --dry-run
```

Run with the workbook’s own settings:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import run --excel C:\temp\import-template.xlsx
```

Run with a detailed log:

```powershell
dotnet run --project src/AdeptTools.Cli -- --server https://adept.example.com import run --excel C:\temp\import-template.xlsx --log-file C:\temp\import.log
```

## Troubleshooting

### Config Sheet Not Found
Cause:
- The workbook is missing the `Config` sheet.

Fix:
- Restore the required sheet name exactly as `Config`.

### IMP-Mapping Table Not Found
Cause:
- The mapping sheet is missing, renamed, or does not contain recognizable headers.

Fix:
- Ensure the worksheet is named `IMP-Mapping` and includes `ExcelColumn`, `AdeptField`, and `Action`.

### Mapping References Excel Column Not Found
Cause:
- A value in `ExcelColumn` does not exactly match any header in `IMP-Data`.

Fix:
- Make the `ExcelColumn` value match the `IMP-Data` header text exactly.

### Rows Are Ignored Unexpectedly
Cause:
- Rows are blank, hidden while `SkipHiddenRows=true`, or actions resolved to `DoNotImport`.

Fix:
- Check row visibility, data presence, and `Action` values.

### Sample Mapping Does Nothing
Cause:
- The generated sample action `Set` is not recognized by the current reader.

Fix:
- Replace `Set` with `FillOverwrite`, `FillIfEmpty`, or `SearchKey` as appropriate.

### Dry Run Produces Only Skipped Results
Cause:
- Dry run reports validation success without making changes.

Fix:
- This is expected. Remove `--dry-run` or set `DryRun=false` in the workbook when you are ready to apply changes.