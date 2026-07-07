# TDN - Import Multi-Trustee Delimiter Encoding

## Purpose

This TDN specifies the implementation contract for encoding and parsing multiple trustees within a single Excel import row.

This TDN makes explicit:
- The accepted delimiter characters and the edge-case risks each carries.
- The exact parsing rules and edge-case handling.
- Validation scope at the token level and the row level.
- Mode and surface behavior boundaries.
- Test coverage expectations.

See ADR-0006 for the decision record capturing why multi-delimiter splitting was chosen and the tradeoffs accepted.

## Scope

In scope:
- Trustee column parsing in the Excel import reader.
- Per-token trustee resolution and validation.
- Row-level error aggregation for multi-trustee validation failures.
- Template generation guidance for the trustee column.
- Test coverage expectations.

Out of scope:
- Non-trustee multi-value column encoding (not generalized by this TDN).
- Trustee resolution mechanics beyond the delimiter layer (covered by the share/trustee identity TDN).
- UI-specific picker interaction for trustee selection.
- Workflow step notification mapping (separate concern).

## Boundary Model

### Layer 1: Shared parsing and validation contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- The accepted delimiter characters.
- Token extraction and normalization rules.
- Token-level validation obligation.
- Row-level failure semantics when one or more tokens are invalid.

Does not define:
- How an individual trustee token is resolved against the backend (existing trustee resolution contract).
- Backend-mode-specific trustee identity format expectations beyond those already defined in trustee identity TDNs.

### Layer 2: Mode and surface realization

Applies to:
- HTTP and COM adapter-level trustee resolution mechanics.
- CLI output formatting of per-token errors.
- Client presentation of partial-failure states.

Defines:
- Where mode-specific resolution differences are permitted.
- Where CLI and Client may differ in error presentation.

Does not redefine:
- The core delimiter, parsing rules, or row-level failure semantics.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode resolves each trustee token via API lookup (user/group search or identity validation endpoint).

HTTP-specific behavior:
- Each token is individually resolved; a token that returns no match or an ambiguous match is treated as unresolvable.
- HTTP error payloads for individual token failures should be captured per-token in row diagnostic output.

Boundary rule:
- HTTP-specific resolution mechanics may differ, but must produce the same per-token resolved/unresolved outcome fed to the shared row aggregation logic.

### COM mode

COM mode resolves each trustee token through native identity lookup.

COM-specific behavior:
- Token resolution follows the same identity lookup path as single-trustee rows.
- COM-specific identity format constraints (e.g., login ID vs. display name) apply per-token identically to single-trustee behavior.

Boundary rule:
- COM resolution differences must not alter the shared parsing or row-level failure semantics.

### Mock mode

Mock mode is a deterministic simulation path.

Mock-specific behavior:
- May simulate per-token resolution outcomes (resolved, unresolved, ambiguous) for test scenarios.
- Must exercise the same token-splitting and aggregation logic as production paths.

Boundary rule:
- Mock mode must not silently skip multi-trustee parsing or collapse multi-token rows to a single synthetic outcome.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns script-friendly output of import results including per-row trustee validation failures.

CLI contract:
- Per-token validation errors must appear in row-level result output.
- Partial-failure rows (some tokens valid, some not) must be reported as row errors, not silently applied with valid tokens only.
- Output format should make the failing token identifiable (e.g., include the raw token value in the error message).

### Client surface

Client owns interactive result presentation including validation error display.

Client contract:
- Must surface per-row trustee errors in the results pane with sufficient detail to identify the failing token.
- May present richer formatting (e.g., highlighting the raw token text) without altering the underlying error semantics.

Boundary rule:
- Surface differences may affect presentation, not row outcome semantics or error completeness.

## Delimiter Specification

**Accepted delimiters:** pipe (`|`), semicolon (`;`), comma (`,`). All three are treated equivalently; there is no precedence ordering.

**Split expression:** `[|;,]` applied to the raw cell string value.

**Recommended delimiter for new data:** pipe (`|`).
Pipe is not a legal character in any known Adept identity format, making it unambiguous regardless of display name style.

**Comma and semicolon acceptance rationale:**
Existing customer spreadsheets (Document Distribution Matrix data) already use comma-separated trustee names. Supporting comma and semicolon avoids requiring reformatting of existing operator data.

**Accepted edge case:**
Adept display names formatted as `Last, First` contain a comma. If such a name is used as a trustee token in a comma-delimited cell, the parser will split at the embedded comma and produce spurious tokens. This is an accepted and documented limitation. Operators affected by this pattern must use pipe or semicolon as the cell delimiter. Validation failures on spurious tokens will surface a per-token resolution error, prompting the operator to correct the delimiter choice.

## Parsing Rules

These rules apply to the trustee column value after it is read from the Excel cell.

1. **Split on any accepted delimiter.** Split the raw cell value on any occurrence of `|`, `;`, or `,` using the expression `[|;,]`. The split produces one or more tokens.
2. **Trim each token.** Leading and trailing whitespace is stripped from each token after splitting.
3. **Discard empty tokens.** Tokens that are empty after trimming are silently discarded. This handles trailing delimiters (e.g., `Alice|Bob|`) without error.
4. **Single-token identity.** A cell value containing no delimiter character produces exactly one token. This path is semantically identical to the existing single-trustee path; no behavioral regression is introduced.
5. **Duplicate tokens.** If the same token value appears more than once after normalization, the row should be treated as a validation error (duplicate trustee specified). Do not silently deduplicate.
6. **Mixed delimiters.** A cell value may mix delimiter characters (e.g., `Alice|Bob,Carol`). All three are treated uniformly; no error is raised for mixing. The resulting tokens are `Alice`, `Bob`, and `Carol`.

## Validation Contract

### Token-level validation

Each extracted token is independently validated using the same trustee resolution path as single-trustee rows.

A token is valid if and only if it resolves to exactly one trustee identity. A token is invalid if:
- It resolves to zero identities (unresolvable).
- It resolves to more than one identity (ambiguous).

### Row-level aggregation

A row is considered to have a trustee validation error if **any** token is invalid.

**Partial success is not permitted:** a row with mixed valid and invalid tokens must not apply the valid tokens and silently skip the invalid ones. The entire trustee assignment for that row is held pending correction.

Rationale: partial application would produce a workflow configuration that silently diverges from operator intent, which is a harder failure mode to detect than a clear row error.

### Error message content

Per-token error messages must include:
- The raw token value that failed resolution.
- The failure reason (unresolvable or ambiguous).
- The row identifier (row number or key field value).

Error messages should not include only the row-level aggregate ("trustee validation failed") without token detail.

## Template Generation Guidance

The generated import template (FR-039) must include, for the trustee column:
- A header comment or tooltip listing all three accepted delimiters (`|`, `;`, `,`) and recommending pipe for new data.
- At minimum one example value demonstrating multi-trustee syntax using pipe (e.g., `user1@domain.com|user2@domain.com`).
- A note that comma is accepted for compatibility with existing spreadsheets, but may conflict with `Last, First` display name formats.
- Documentation that whitespace around any delimiter is tolerated.

## Test Coverage Expectations

The following scenarios must have test coverage:

| Scenario | Expected outcome |
|---|---|
| Single trustee, no delimiter | Identical to existing single-trustee behavior |
| Two valid trustees, pipe-separated | Both tokens resolved; row succeeds |
| Two valid trustees, semicolon-separated | Both tokens resolved; row succeeds |
| Two valid trustees, comma-separated | Both tokens resolved; row succeeds |
| Mixed delimiters in one cell (e.g., `A\|B,C`) | All three tokens extracted and resolved; row succeeds |
| Three or more valid trustees | All tokens resolved; row succeeds |
| Trailing delimiter (e.g., `A\|B\|`) | Empty token discarded; row processed with A and B |
| Whitespace padding around tokens (e.g., ` A \| B `) | Tokens trimmed; row processed with A and B |
| Display name with embedded comma used with comma delimiter (e.g., `Last, First`) | Token split at comma; spurious tokens produce per-token resolution error |
| One valid token, one unresolvable token | Row-level validation error; no partial apply |
| All tokens unresolvable | Row-level validation error |
| Ambiguous token (resolves to multiple identities) | Row-level validation error with ambiguous-resolution message |
| Duplicate token values in same cell | Row-level validation error for duplicate |
| Delimiter-only cell value (e.g., `\|`) | All tokens empty after trim/discard; treated as empty trustee value — existing empty-trustee validation applies |

## Related

- ADR-0006 — Decision record: delimiter selection rationale and alternatives considered.
- US-IMP-009 — User story driving multi-trustee row support.
- FR-040 — Functional requirement for multi-trustee row support.
- FR-039 — Template generation; must document all three accepted delimiters and recommend pipe.
- tdn-workflow-adept-power-tools-share-and-trustee-identity.md — Trustee identity resolution contract.
- tdn-import-pipeline-architecture-and-failure-model.md — Import pipeline stage and row-outcome semantics.
