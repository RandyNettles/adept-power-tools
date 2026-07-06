# TDN - Configuration Precedence and Environment Model

## Purpose

Define the architecture-level configuration model for Adept Power Tools, including:
- Configuration sources by surface (CLI and Launcher).
- Precedence and override rules.
- Environment mode model (HTTP, COM, Mock).
- Validation and normalization boundaries before runtime service wiring.

## Scope

In scope:
- CLI global runtime options and invocation-time settings construction.
- Launcher persisted connection convenience data and profile model.
- Backend/mode selection effects on active configuration.
- Runtime override behavior and safety guards.

Out of scope:
- Deep feature-level command options unrelated to runtime environment selection.
- Backend server-side configuration policy.
- Secret management design beyond existing auth/session TDNs.

## Source Context

Primary references:
- docs/design/user-flows/UF-US-CLI-001-Global-Runtime-Options.md
- docs/design/user-flows/UF-US-CONN-05-06-Connection-Persistence-and-Profiles.md
- src/AdeptTools.Core/Configuration/AdeptToolSettings.cs
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs
- src/AdeptTools.Launcher/App.xaml.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/Services/ServerHistoryService.cs
- src/AdeptTools.Launcher/Services/ComProfileService.cs
- src/AdeptTools.Launcher/Models/ComConnectionProfile.cs

## Problem Statement

Functional behavior is implemented and documented per flow, but there is no single architecture contract that answers:
- Which source wins when multiple configuration values exist.
- How defaults, persisted values, and runtime choices interact.
- How mode and backend selections shape required inputs and service registration.

This TDN provides that contract.

## Decision Summary

1. Configuration precedence is surface-specific but deterministic.
2. CLI is invocation-authoritative: command-line options override all runtime defaults.
3. Launcher uses layered convenience state: defaults -> persisted history/profile -> user runtime edits -> connection-time resolution.
4. Mock mode is a hard override that bypasses server-required validation and selects mock services.
5. Service registration and backend binding must occur only after normalization and source-precedence resolution.

## Canonical Configuration Domains

Define these domains across surfaces:
1. `backendMode` (`http` | `com`)
2. `mockMode` (bool)
3. `serverEndpoint` (URL/host endpoint as applicable)
4. `userIdentifier` (username/login input)
5. `verbosity`/`logPath` (CLI-oriented diagnostics settings)
6. `connectionProfile` (launcher COM profile object)

## Environment Model

## Runtime modes

- HTTP mode:
  - Requires a valid server endpoint unless mock mode is enabled.
  - Uses HTTP auth/api client registrations.

- COM mode:
  - Uses COM auth/api service registrations.
  - In launcher, may derive endpoint from selected COM profile.

- Mock mode:
  - Bypasses server requirement checks.
  - Uses mock auth/api/service implementations.

## Environment identity tuple

Effective runtime environment is defined by:
- `(mockMode, backendMode, resolvedEndpoint, resolvedUser)`

All service resolution and command execution must use values from this effective tuple, not partially resolved source values.

## CLI Configuration Precedence

CLI precedence (highest to lowest):
1. Command-line global options for current invocation:
   - `--server`, `--user`, `--mock`, `--backend`, `--verbose`, `--log`
2. Option default values defined by parser:
   - backend default `http`
   - bool defaults `false` for mock/verbose
3. In-command fallback logic where explicitly implemented:
   - login user fallback to `ADM` when user not provided
   - password fallback from `ADEPTTOOLS_PASSWORD` environment variable for login attempt

Important boundary:
- CLI currently does not read persisted general settings for these domains; each invocation is authoritative.

## CLI Validation and Normalization Rules

1. Non-mock commands require `serverEndpoint`.
2. HTTP service registration normalizes server URL to trailing-slash base URI.
3. Service registration fails fast on invalid mode/source combinations.
4. Help/version flows bypass operational server requirement enforcement.

## Launcher Configuration Precedence

Launcher precedence is interaction-driven and layered:

### Startup seed precedence

1. In-memory defaults from app/service initialization:
   - selected backend initial default
   - mock mode initial state
2. Persisted convenience state load:
   - server URL history list
   - last username
   - COM profile list
3. Startup resume path may populate active HTTP auth/session context when persisted session is valid.

### Runtime interaction precedence

At connection execution time, effective values resolve in this order:
1. Mock mode override:
   - if enabled, use mock endpoint/user defaults and mock services.
2. Backend-specific input source:
   - COM backend: selected profile address + entered username
   - HTTP backend: current server URL field + current username field
3. User edits in current session override initially loaded history values.
4. Successful connect persists convenience updates (server history/last username) and HTTP auth session state (HTTP only).

### Profile precedence (COM)

1. Explicit currently selected profile in UI.
2. First available profile when current selection is invalid/missing.
3. No profile available -> connection cannot proceed in COM mode until profile/user requirements are satisfied.

## Cross-Surface Precedence Invariants

1. Mock mode always overrides server-required constraints.
2. Backend resolution happens before backend-specific service factory selection.
3. Effective endpoint must be normalized before HTTP client base address usage.
4. Persisted convenience values must not silently override explicit runtime user input.
5. Persisted auth/session state is separate from configuration convenience state and follows security-boundary TDN rules.

## Source-Type Classification

Classify configuration sources by trust and mutability:

- `S1` Explicit runtime input (CLI flags, launcher field edits, profile selection) — highest precedence.
- `S2` Persisted convenience settings (history/profile lists) — medium precedence, startup seed only.
- `S3` Code defaults (parser/service initialization defaults) — lowest precedence.

Policy:
- Runtime execution must be based on `S1` when available.
- `S2` should seed UI/initial state, not force runtime overrides.

## Configuration Lifecycle

1. Collect source values.
2. Resolve precedence into effective tuple.
3. Validate mode-dependent requirements.
4. Normalize endpoint/user representations.
5. Bind services and execute operation.
6. Persist allowed post-operation updates (history/profile/session) according to outcome and mode.

## Persistence Boundaries

Allowed persistent categories:
- Convenience settings:
  - Server history, last username.
  - COM profiles.
- HTTP auth session state (secure store, separate policy).

Not in scope for persistence in this model:
- General CLI runtime options as durable profile/config file.
- Per-command feature overrides as global defaults.

## Conflict Resolution Examples

### Example A: CLI explicit backend vs default

- Input: no `--backend` -> default `http`.
- Input: `--backend com` -> explicit input wins.

### Example B: CLI server missing in non-mock

- Input: `--mock false`, no `--server`.
- Result: validation fails before command execution.

### Example C: Launcher history vs user edit

- Startup seeds `ServerUrl` from history first entry.
- User edits `ServerUrl` manually.
- Connection uses edited value, not seeded history value.

### Example D: Launcher COM profile selection

- Multiple profiles persisted.
- User selects profile B.
- COM connect uses profile B address regardless of last used history URL.

### Example E: Mock mode runtime toggle

- User toggles mock mode on.
- Effective environment ignores server/profile endpoint requirements and uses mock services.

## Validation Contract by Mode

HTTP mode:
1. Require non-empty, valid server endpoint when not mock.
2. Normalize trailing slash before HTTP client registration/use.

COM mode:
1. Require selected profile and username in launcher flow.
2. Resolve endpoint from profile address.

Mock mode:
1. Skip server endpoint requirement.
2. Route all service factories to mock implementations.

## Governance Requirements

Any change affecting configuration sources or precedence must:
1. Update this TDN precedence tables and examples.
2. Update impacted user-flow docs.
3. Verify runtime tuple resolution for CLI and Launcher.
4. Add/adjust tests for precedence and validation outcomes.

## Operational Checklist

1. CLI explicit options always control current invocation behavior.
2. CLI non-mock server guard is enforced before execution.
3. Launcher startup seeds from persisted convenience state without overriding explicit user edits.
4. COM profile selection deterministically controls COM endpoint resolution.
5. Mock mode bypasses server requirements and binds mock services.
6. Successful launcher connect updates allowed persistence stores only.

## Traceability

Flow anchors:
- UF-US-CLI-001
- UF-US-CONN-05-06

Implementation anchors:
- `AdeptToolSettings`
- CLI middleware option parsing and validation in `Program.cs`
- backend/service registration in `ServiceRegistration.cs`
- launcher startup/config seed in `App.xaml.cs` and `ConnectViewModel`
- persistence services: `ServerHistoryService`, `ComProfileService`

## Open Questions

1. Should a first-class persisted `AdeptToolSettings` profile be introduced for CLI to support non-interactive default environments?
2. Should launcher support named HTTP connection profiles (not just history) to mirror COM profile ergonomics?
3. Should endpoint normalization be centralized in Core to eliminate per-surface drift risk?
