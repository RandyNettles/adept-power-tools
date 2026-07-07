# ADR-0006: Multi-Trustee Encoding in Import Excel Template Uses Multi-Delimiter Splitting

- Status: Accepted
- Date: 2026-07-07
- Revised: 2026-07-07

## Context

US-IMP-009 (FR-040) requires that a single import row can specify multiple trustees for workflow configuration, rather than requiring one row per trustee.

Real-world customer data (Document Distribution Matrix spreadsheets) already uses comma-separated display names in trustee cells (e.g., `Smith, John, Doe, Jane, ...`). Supporting only pipe would force customers to reformat existing spreadsheets before use.

Three encoding strategies were considered for the column encoding approach:

**Option A — Delimiter-per-cell (single column, delimited string)**
Trustees are listed in one cell, separated by a delimiter.

**Option B — Fixed multi-column expansion**
Separate named columns for each trustee slot (e.g., `Trustee1`, `Trustee2`, `Trustee3`).

**Option C — Repeated rows with a shared row key**
One row per trustee, with a grouping ID column tying them together.

Option B was rejected because the fixed column count artificially caps the trustee count and spreads sparse data horizontally, making templates harder to read at scale. It also requires template changes whenever the max count grows.

Option C was rejected because it reintroduces row-per-trustee verbosity the user story explicitly wants to avoid, and adds a grouping key that must be managed and kept consistent by the operator.

Option A was selected. Within Option A, three delimiter approaches were considered:

**Single delimiter — pipe only**
Clean and unambiguous; pipe is not a legal character in Adept identity values. However, incompatible with existing customer data that uses comma separation, requiring manual reformatting.

**Single delimiter — comma or semicolon**
Naturally compatible with existing spreadsheet conventions. Risky because Adept display names can contain commas (e.g., `Last, First`) and semicolons appear in some locale-specific data, creating potential splitting ambiguity.

**Multi-delimiter — pipe, comma, and semicolon all accepted**
Maximum compatibility with existing operator data. Accepted that comma and semicolon may conflict with display names containing those characters; this is considered an unlikely edge case in practice, and operators retain pipe as a safe unambiguous option.

## Decision

Split the trustee cell value on any of the three delimiters: pipe (`|`), semicolon (`;`), or comma (`,`). All three are treated equivalently — there is no precedence ordering.

Parsing rules:
- The trustee column value is split on any occurrence of `|`, `;`, or `,`.
- Each token is trimmed of leading/trailing whitespace after splitting.
- Empty tokens (e.g., from trailing delimiters) are discarded.
- Each token is individually resolved and validated; an unresolvable token produces a per-row validation error but does not block other rows or other tokens.
- Single-trustee rows (no delimiter present) are parsed identically to the existing single-value path; no behavioral regression.

Operator guidance:
- Pipe (`|`) is the recommended delimiter for new templates because it is unambiguous in all known Adept identity formats.
- Comma and semicolon are accepted to support existing customer spreadsheets without reformatting.
- Operators whose Adept display names contain commas (e.g., `Last, First` format) should use pipe or semicolon to avoid splitting on the name separator.

## Consequences

- Positive: no template schema change required when trustee count grows.
- Positive: compatible with existing customer spreadsheet data without reformatting.
- Positive: operators retain pipe as a safe, unambiguous alternative.
- Positive: operator-visible format is compact and readable for typical 2–5 trustee cases.
- Trade-off: display names in `Last, First` format will be split at the comma, producing spurious tokens. Operators using comma-format names must switch to pipe or semicolon as the cell delimiter.
- Trade-off: cells with many trustees become difficult to read in Excel without column width adjustment.
- Trade-off: validation errors are per-token but reported at the row level; multi-trustee rows with partial failures will surface multiple messages.
- Accepted risk: the comma and semicolon conflict with certain display name formats is a known edge case. It is not expected to affect the majority of deployments. Operators are informed via template guidance.

## Implementation Notes

- Parsing lives in the import row reader; the trustee column is defined in the mapping config.
- The split regex is `[|;,]` applied to the raw cell string value.
- Token-level validation reuses the same trustee resolver used for single-trustee rows.
- Template generation (FR-039) should document all three supported delimiters and recommend pipe for new data.
- Tests should cover: single trustee, multiple trustees via each delimiter, mixed delimiters in one cell, whitespace padding, empty tokens, and a partially unresolvable multi-trustee row.

## Related

- US-IMP-009 — User story driving this decision.
- FR-040 — Functional requirement for multi-trustee row support.
- FR-039 — Template generation; should document all three delimiters and recommend pipe.
