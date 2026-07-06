# TDN - Observability and Operational Diagnostics

## Purpose

Define the architecture-level observability contract for Adept Power Tools so support and operations can reliably diagnose failures across CLI and Launcher.

This TDN establishes:
- Correlation ID policy.
- Structured diagnostic event schema.
- Redaction and data-handling rules.
- Minimum telemetry required for operational support.

## Scope

In scope:
- CLI and Launcher diagnostics behavior.
- Core logging and progress output contracts.
- Authentication/session-related diagnostic safety rules.
- Support-ready minimum telemetry fields and retention guidance.

Out of scope:
- External APM/SIEM vendor integration specifics.
- Backend server-side logging implementation details.
- Full metrics platform design.

## Source Context

Primary references:
- docs/runbooks/CLI-runbook.md
- src/AdeptTools.Core/Logging/ResultLogger.cs
- src/AdeptTools.Core/Progress/ConsoleProgress.cs
- src/AdeptTools.Core/Configuration/AdeptToolSettings.cs
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Infrastructure/CliAuthSessionStore.cs
- src/AdeptTools.Launcher/App.xaml.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/Services/AuthSessionStore.cs
- src/AdeptTools.Launcher/Services/HttpClientConfig.cs
- src/AdeptTools.Launcher/Services/ServerHistoryService.cs

## Problem Statement

Current diagnostics are useful but heterogeneous:
- CLI produces console and optional file logs via `ResultLogger` and command-local output.
- Launcher surfaces runtime exceptions primarily through modal dialogs.
- No shared architecture contract defines correlation IDs, standard event fields, redaction classes, or minimum telemetry expectations.

This increases support effort for cross-surface incident triage.

## Decision Summary

1. All operation-scoped diagnostics must carry a correlation identifier.
2. Diagnostics should use a canonical structured event envelope, even when rendered as plain text.
3. Sensitive values (tokens, passwords, raw secrets) are never logged.
4. Minimum telemetry fields are required for every error and operation summary event.
5. CLI and Launcher may differ in presentation but must preserve equivalent diagnostic semantics.

## Observability Principles

1. Diagnose-first: every non-trivial failure must produce enough context for first-pass triage.
2. Least exposure: diagnostics must not leak credentials, bearer tokens, or private user data beyond operational need.
3. Surface parity: CLI and Launcher diagnostics should be semantically equivalent for shared operations.
4. Deterministic correlation: all events in one operation chain must be traceable by one correlation ID.
5. Stable schema: field names must be versioned and backward-compatible for support tooling.

## Correlation ID Policy

### Correlation ID format

- Use `operationId` as the primary correlation key.
- Recommended format: GUID/UUID string (`N` or canonical hyphenated form).
- Optional secondary `parentOperationId` for chained flows (for example resume -> login -> operation).

### Correlation boundaries

Create a new `operationId` for:
- Each CLI command invocation.
- Each Launcher user-initiated operation (connect, create/modify/delete batch, import validate/run).

Reuse same `operationId` for:
- Sub-steps and progress updates inside one operation.
- Retry attempts within same operation boundary.

### Propagation

- Include `operationId` in all local diagnostics.
- When HTTP headers are available, propagate as `X-Correlation-Id` (or equivalent configured header) on outbound API calls.
- Include `operationId` in support-facing error dialogs/messages where practical.

## Structured Diagnostic Event Envelope

All diagnostic events should map to this logical schema:

Required fields:
- `timestampUtc`
- `level` (`Debug` | `Info` | `Warn` | `Error`)
- `surface` (`CLI` | `Launcher`)
- `component` (for example `Auth`, `Workflow`, `Import`, `Backend.Http`, `Backend.Com`)
- `operation` (for example `workflow.modify`, `import.run`, `auth.login`)
- `operationId`
- `outcome` (`success` | `partial` | `fail` | `cancelled` | `skip`)
- `message`

Recommended fields:
- `errorClass` (aligned with error taxonomy TDN where applicable)
- `backendMode` (`http` | `com` | `mock`)
- `userContext` (sanitized; non-secret identifier only)
- `targetServerHost` (host only, no query/fragments)
- `durationMs`
- `attempt`
- `itemCounts` (for batch/row summaries)

Optional fields:
- `exceptionType`
- `statusCode` (API/business status)
- `details` (sanitized supplemental text)

## Redaction and Data Classification Rules

## Never log (Class R1 - secret)

- Access tokens and refresh tokens.
- Authorization headers and bearer strings.
- Raw passwords, password prompts, and secure input buffers.
- PKCE verifier values or equivalent auth secrets.

## Log only in masked form (Class R2 - sensitive)

- User identifiers and emails when not required for support context.
- Server URL path/query details.
- File paths containing user home or customer identifiers.

Masking guidance:
- Email: preserve domain, partially mask local part.
- User ID/login: partial mask except first/last character where feasible.
- URL: log scheme + host (+ optional port) only by default.
- Paths: prefer basename or redacted root aliases.

## Allowed in plain diagnostics (Class R3 - operational)

- Backend mode, operation name, result counts, status classes, retry attempt count.
- Non-secret feature flags and capability indicators.
- Workflow/import aggregate counts and high-level outcome categories.

## Session Storage Interaction Rules

Because session stores contain sensitive material:
- `AuthSessionStore` and `CliAuthSessionStore` operations must never emit token values.
- Diagnostic events may indicate `session_resume_attempted`, `session_resume_succeeded`, `session_resume_failed`, but must not include raw session payload.
- On parse/decrypt errors, log class + reason category only (for example `corrupt_state`, `expired_state`, `invalid_state`), not raw content.

## Minimum Telemetry Contract

Emit at least these events per operation lifecycle:

1. `operation.start`
- Required: operation, operationId, surface, backendMode, input-mode summary (sanitized).

2. `operation.progress` (for long-running flows)
- Required: operationId, stage/phase, index counters as available.
- For import/workflow batches include item progress counts.

3. `operation.summary`
- Required: operationId, outcome, durationMs, totals (succeeded/failed/skipped/created/updated as applicable).

4. `operation.error` (on failure)
- Required: operationId, errorClass, message, component, outcome=fail.
- Recommended: exceptionType, statusCode, attempt.

5. `operation.cancelled` (when applicable)
- Required: operationId, message, partial completion counts.

## CLI Diagnostics Contract

Current behavior baseline:
- Console-first output with optional file logging (`ResultLogger`, `--log`, `--log-file`).

Required policy:
1. CLI non-zero exits must include `operationId` and class-tagged terminal summary.
2. File log entries should be line-oriented structured text or JSONL-compatible shape using envelope fields.
3. `--verbose` may increase detail but must not bypass redaction rules.
4. Progress output should include stage/index without secret payloads.

## Launcher Diagnostics Contract

Current behavior baseline:
- User-facing message dialogs for unhandled/fatal/task exceptions.
- Operation status shown in ViewModels.

Required policy:
1. Runtime exception handlers must log structured `operation.error` events before showing dialogs.
2. Dialog messages should expose a support reference containing `operationId`.
3. Connect/auth/import/workflow operations should emit start/progress/summary/error events matching CLI semantics.
4. Session resume/connect failures should be classed and logged without exposing credential/session material.

## Operational Diagnostic Levels

- `Info`: operation starts, successful summaries, expected state transitions.
- `Warn`: recoverable anomalies (retry used, partial failures, cleanup uncertainty).
- `Error`: operation failures and unhandled exceptions.
- `Debug`: deep diagnostic details (enabled by verbose/debug mode only, still redacted).

## Support Bundle Minimum

For incident support, minimum artifacts should include:
1. Timestamped operation logs with `operationId`.
2. Command/surface context and backend mode.
3. Error class and exception type (if present).
4. Operation summary counts.
5. Version/build metadata (app version, optional server version when available).

## Retention and Storage Guidance

- Default local log storage should use user profile application data locations.
- Logs containing operational metadata should have bounded retention (for example rolling files or max age).
- Session-state files are not diagnostic logs and must remain separately protected.

## Implementation Rollout Guidance

1. Introduce a shared `DiagnosticContext` model in Core containing `operationId`, `surface`, `backendMode`, and optional parent ID.
2. Extend `ResultLogger` (or add a structured wrapper) to emit canonical envelope fields.
3. Add operationId creation/propagation in CLI entrypoint and launcher operation commands.
4. Add a centralized redaction helper in Core and require all diagnostic emitters to pass through it.
5. Update runbooks to document support collection steps using operation IDs.

## Regression Checklist

1. Every CLI command emits an `operationId` at start and terminal summary.
2. Launcher connect/workflow/import operations emit start + summary + error events with same correlation semantics.
3. Access/refresh tokens never appear in logs or dialogs.
4. Password and authorization headers never appear in logs.
5. Error events include class + operationId + component.
6. Cancelled operations produce explicit cancelled outcome telemetry.
7. Verbose mode adds detail without leaking redacted data classes.

## Open Questions

1. Should default format be human-readable text with embedded key-value fields or JSONL as first-class output?
2. Should operationId be copied into outbound HTTP response/request diagnostics by default in all clients?
3. Should a lightweight local support-bundle command be added to package recent logs + environment metadata?
