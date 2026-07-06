# ADR-0005: Auth Session Persistence Uses Secure Local Storage with Expiry-Aware Resume

- Status: Accepted
- Date: 2026-06-29

## Context
Users expect short restart windows to preserve authentication when their IdP and Adept sessions are still valid. The tool must improve restart UX without storing credentials in plaintext.

The utility supports HTTP and COM backends:
- HTTP uses bearer tokens and server-side session validation.
- COM uses SDK session lifetimes tied to process/session objects.

## Decision
Persist HTTP auth session state securely at rest and attempt silent resume on startup before forcing interactive login.

Current policy:
- HTTP auth state is persisted using DPAPI (CurrentUser scope) in LocalApplicationData.
- Persisted state includes access token, optional refresh token, token expiry metadata, server URL, and user display metadata.
- Startup performs a silent resume path for HTTP:
	- Reject persisted state if token is expired (using token exp claim and stored expiry metadata).
	- If not expired, restore bearer auth and run session validation via refresh/session-check logic.
	- If validation fails, clear persisted state and fall back to normal login.
- COM sessions remain process-bound and are disconnected/released on shutdown; COM auth is not persisted for resume.
- Convenience settings (server URL history, last username, COM profiles) continue to persist in user AppData.

## Consequences
- Positive: improved restart UX when token/session remains valid.
- Positive: secure-at-rest storage avoids plaintext token files.
- Positive: expiry is explicitly respected before reuse.
- Positive: stale/corrupt state self-heals by clearing persisted auth and requiring re-login.
- Trade-off: additional implementation complexity in startup/auth orchestration.
- Trade-off: DPAPI scope is user+machine specific; persisted sessions are not portable.
- Trade-off: if server invalidates session while token is unexpired, silent resume still fails and requires login.

## Implementation Notes
- Secure storage service: AuthSessionStore.
- HTTP resume logic: TryResumeSessionAsync in HttpAdeptAuthService.
- Startup orchestration: ConnectViewModel constructor triggers background resume attempt.
- On successful HTTP login, persisted session state is refreshed with latest token/metadata.

## Related
- See Product Decision Record: docs/prd/PDR-AdeptTools-Launcher-Auth-and-Session-UX.md.
