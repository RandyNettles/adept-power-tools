# ADR-0004: Handle Status 230 with Explicit User Disambiguation

- Status: Accepted
- Date: 2026-06-29

## Context
In some SSO environments, one IdP identity maps to multiple Adept accounts. The server returns status code 230 and requires account selection before final session establishment.

## Decision
Treat status 230 as a structured, expected flow rather than a generic error.

Flow:
- Parse user choices from the server payload.
- Surface a user-selection UI in the launcher.
- Retain pending SSO context (`access_token`, `sso_state_*`, `sso_nonce`) for completion.
- Complete authentication through `SelectUserAsync` using the selected account.

## Consequences
- Positive: users can complete login in multi-account environments.
- Positive: avoids forcing user retries with unclear server errors.
- Trade-off: additional state management is required between initial login and final selection.
- Trade-off: parsing must remain resilient to server payload variations.
