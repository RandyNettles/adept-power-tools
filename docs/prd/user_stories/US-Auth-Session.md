# User Stories - Authentication and Session

## US-AUTH-001 Adaptive Login Mode Routing
As an operator, I want login mode auto-detection so that authentication uses the right flow for each server.

Acceptance Criteria
1. HTTP auth checks server options/bootstrap to determine mode.
2. Flow routes to local password, OAuth/Cognito browser SSO, or Windows SSO fallback.
3. Unsupported/incomplete server settings return explicit errors.

FR Traceability
- FR-015

## US-AUTH-002 OAuth PKCE Browser Sign-In
As an SSO user, I want secure browser login with callback handling so that auth works with modern IdPs.

Acceptance Criteria
1. OAuth uses Authorization Code + PKCE.
2. Localhost callback listener is started before browser launch.
3. Callback listener handles expected callback URI forms for supported providers.

FR Traceability
- FR-016

## US-AUTH-003 Robust Callback Handling
As an SSO user, I want callback safety checks so that stale or irrelevant browser requests do not break login.

Acceptance Criteria
1. Callback state must match expected value for success path.
2. Non-callback/noise requests are ignored and do not fail the flow.
3. Timeout is enforced with a user-friendly message.

FR Traceability
- FR-017

## US-AUTH-004 Friendly Auth Errors
As a user, I want understandable auth errors so that I know what to fix or try next.

Acceptance Criteria
1. Redirect mismatch and settings-retrieval issues show human-readable guidance.
2. Known server status-code failures are normalized to user-friendly messages.
3. Raw technical payloads are not required for basic user comprehension.

FR Traceability
- FR-018

## US-AUTH-005 Account Disambiguation Completion
As an SSO user with multiple mapped accounts, I want selection context preserved so that the selected account can be applied without restarting login.

Acceptance Criteria
1. Pending auth context is retained after status-230 response.
2. Selected user is submitted through a selection-completion call.
3. Successful completion yields authenticated session state.

FR Traceability
- FR-019

## US-AUTH-006 Secure Session Resume on Startup
As a returning client user, I want my HTTP session resumed securely when still valid so that I can continue quickly.

Acceptance Criteria
1. HTTP session state is persisted in encrypted per-user storage.
2. Startup attempts resume using stored token/session metadata.
3. Expired or invalid state is cleared automatically.
4. Successful resume sets connected state and API auth context.

FR Traceability
- FR-021
- FR-022

## US-AUTH-007 Backend Session Lifecycle Controls
As an administrator, I want consistent logout/refresh behavior so that session validity is maintained across HTTP and COM backends.

Acceptance Criteria
1. HTTP backend supports logout and refresh/session-check semantics.
2. COM backend supports session connected-state checks.
3. COM logout/dispose triggers disconnect and object release.

FR Traceability
- FR-020
- FR-023
- FR-024
