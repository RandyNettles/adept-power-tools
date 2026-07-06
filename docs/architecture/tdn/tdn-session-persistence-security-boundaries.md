# TDN - Session Persistence Security Boundaries

## Purpose

Define implementation-level security boundaries for persisted HTTP auth sessions, including:
- Data-at-rest protection boundaries.
- Threat assumptions and non-goals.
- Resume/reconnect/logout invalidation behavior.
- Cross-component trust and lifecycle responsibilities.

This TDN expands ADR-0005 with concrete operational and code-level boundaries.

## Scope

In scope:
- Launcher HTTP session persistence and resume paths.
- Auth token handling in `AdeptTools.Launcher` and `AdeptTools.Backend.Http`.
- Session invalidation rules on expiry, reconnect, and logout.
- Security boundaries for persisted session data and in-memory token state.

Out of scope:
- COM auth persistence (intentionally non-persisted).
- IdP-side session policy and server-side token issuance policy.
- OS hardening and endpoint security beyond application controls.

## Source Context

Primary references:
- docs/architecture/adr/ADR-0005-auth-session-persistence-secure-local-storage-and-expiry-aware-resume.md
- src/AdeptTools.Launcher/Services/AuthSessionStore.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/Services/ServerHistoryService.cs
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs
- src/AdeptTools.Launcher/App.xaml.cs

## Decision Summary

1. Persisted session state is allowed only for HTTP mode and only in secure local storage.
2. Persisted state must be treated as untrusted input until validated (integrity, expiry, and session viability checks).
3. Logout and backend-mode transitions must clear persisted HTTP session state.
4. Resume failures must fail closed: clear persisted state and require explicit re-authentication.
5. Convenience persistence (server history/last username) is separate from auth session state and must not imply authentication.

## Security Boundary Model

### Boundary A: Local persisted auth state (sensitive)

Component:
- `AuthSessionStore` (`http-auth-session.dat` in LocalApplicationData/AdeptTools).

Protection:
- DPAPI `ProtectedData` with `DataProtectionScope.CurrentUser`.
- Additional static entropy string namespace.

Data class:
- High-sensitivity auth material (access token, refresh token, expiry, identity metadata).

### Boundary B: In-memory active session state (sensitive)

Components:
- `HttpAdeptAuthService` (`AccessToken`, `_refreshToken`, auth header state).
- `HttpClientConfig.AccessToken` in launcher runtime.

Data class:
- Live bearer credentials used for API authorization.

### Boundary C: Convenience profile/history state (non-auth)

Component:
- `ServerHistoryService` (`server-history.json` in AppData).

Data class:
- Non-secret convenience fields (server URLs, last username).

Constraint:
- Must never be used to imply authenticated state.

## Threat Assumptions

Assumed mitigated by platform and deployment:
1. User-profile-level file access controls are intact.
2. DPAPI current-user key material is protected by OS account controls.
3. Process memory is not fully protected against a fully compromised local user session.

Explicit non-goals:
1. Defending against a fully compromised endpoint where attacker runs as current user.
2. Defending against memory scraping by privileged malware.
3. Cross-machine portability of persisted sessions.

## Persisted Session Content Policy

Persisted fields allowed:
- `ServerUrl`
- `AccessToken`
- `RefreshToken` (optional)
- `AccessTokenExpiresUtc` (optional)
- `UserId`, `UserName`, `DisplayName`, `EmailAddress`, `AppVersion`, `WorkAreaId`

Persisted fields prohibited:
- User passwords.
- PKCE verifier values.
- Raw OAuth state nonce chains beyond active flow memory.

## Resume and Validation Contract

## Resume preconditions

Before attempting resume:
1. Session file exists and decrypts/deserializes successfully.
2. `ServerUrl` and `AccessToken` are non-empty.
3. Token expiry is not in the past (using persisted expiry or JWT `exp`).

If any precondition fails:
- Clear persisted session and remain disconnected.

## Resume activation

Current implementation behavior:
- `ConnectViewModel.TryResumeHttpSessionAsync` loads persisted state, checks expiry, then calls `TryResumeSessionAsync`.
- `TryResumeSessionAsync` restores in-memory auth header and marks auth as successful when local checks pass.

Target security contract:
- Resume should include remote session viability confirmation before final `Connected` state is declared (for example `RefreshAsync`/`isLoggedIn` check).
- If remote check fails, clear persisted state and revert to disconnected.

## Fail-closed principle

Any resume ambiguity must fail closed:
- Do not keep partially valid persisted auth material active.
- Clear local persisted state when resume outcome is false/exceptional.

## Reconnect and Mode Transition Boundaries

### Explicit reconnect

On successful explicit HTTP reconnect:
- Overwrite persisted session with latest token and metadata.
- Update runtime auth header state.

On failed HTTP reconnect:
- Clear persisted HTTP session to avoid stale replay attempts.

### Backend/mode changes

When switching away from HTTP backend or enabling mock mode:
- Clear persisted HTTP session state.
- Remove active in-memory token from `HttpClientConfig`/auth service where applicable.

Rationale:
- Prevent credential carryover into non-HTTP execution contexts.

## Logout Invalidation Contract

On logout:
1. Clear auth service runtime state (`IsAuthenticated=false`, token fields null, auth header removed).
2. Clear launcher `HttpClientConfig.AccessToken`.
3. Clear persisted HTTP session file.
4. Reset UI connection status to disconnected.

Requirement:
- Logout must be idempotent and best-effort safe.
- Cleanup errors must not leave UI in connected state.

## Corruption and Recovery Paths

`AuthSessionStore.Load()` treats decrypt/parse failures as unreadable state and returns null.

Policy:
1. Corrupt/unreadable session state must not block startup.
2. Corrupt state must be treated as invalid and replaced only after successful fresh login.
3. Session parse/decrypt failure must never leak raw persisted content to logs/UI.

## Confidentiality and Exposure Rules

1. Tokens must never be displayed in UI dialogs, logs, or command output.
2. Server history persistence may include URLs/usernames but never bearer credentials.
3. Exception handling in launcher startup must avoid serializing sensitive auth/session payloads.
4. Diagnostic messages for resume failures should be reason-class based, not secret-value based.

## Session Lifetime and Expiry Policy

1. Token expiry is authoritative for local pre-check gating.
2. Unexpired token is necessary but not sufficient for trusted resume (requires server viability check per target contract).
3. Expired persisted tokens must be removed before user interaction proceeds.

## HTTP vs COM Boundary

HTTP:
- Persistent session allowed under secure store policy.

COM:
- No persisted resume credentials; process/session-scoped only.
- COM disconnect/release remains separate lifecycle contract.

## Security Invariants

1. No plaintext token persistence for launcher HTTP sessions.
2. No password persistence in launcher session store.
3. Any failed resume path leaves user unauthenticated and clears stale persisted state.
4. Logout always clears persisted HTTP auth state.
5. Non-HTTP and mock contexts do not retain HTTP session persistence.

## Operational Checklist

1. Resume with valid unexpired token reaches connected state only if viability checks pass.
2. Expired token is cleared automatically and does not attempt protected operations.
3. Failed reconnect in HTTP mode clears persisted session.
4. Backend switch HTTP -> COM/mock clears persisted HTTP state.
5. Logout clears runtime token/header and persisted session file.
6. Corrupt session file does not crash startup and does not authenticate user.

## Implementation Notes and Gaps

Observed implementation alignment:
- DPAPI-at-rest protection is implemented in `AuthSessionStore`.
- Expiry pre-check and fail-closed clear behavior are implemented in launcher resume flow.
- Logout and non-HTTP paths clear persisted session.

Gap to target contract:
- Startup resume currently does not perform an explicit remote `isLoggedIn`/refresh check before setting connected status.

Recommended hardening step:
1. After `TryResumeSessionAsync`, invoke `RefreshAsync` (or equivalent remote viability check) before finalizing connected state.
2. On failure, clear store and reset connection status.

## Traceability

Related decision:
- docs/architecture/adr/ADR-0005-auth-session-persistence-secure-local-storage-and-expiry-aware-resume.md

Implementation anchors:
- `AuthSessionStore.Save/Load/Clear`
- `ConnectViewModel.TryResumeHttpSessionAsync`
- `ConnectViewModel.TestConnectionAsync`
- `ConnectViewModel.LogoutAsync`
- `HttpAdeptAuthService.TryResumeSessionAsync`
- `HttpAdeptAuthService.RefreshAsync`
- `HttpAdeptAuthService.LogoutAsync`

## Open Questions

1. Should refresh-token rotation be persisted only after successful remote refresh to reduce replay of revoked refresh tokens?
2. Should session store writes include an integrity version marker for future schema migration hard-fail controls?
3. Should launcher expose an explicit "clear saved session" operator action in UI for support scenarios?
