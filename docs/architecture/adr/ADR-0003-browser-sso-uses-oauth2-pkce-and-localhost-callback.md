# ADR-0003: Browser SSO Uses OAuth 2.0 PKCE and Localhost Callback

- Status: Accepted
- Date: 2026-06-29

## Context
The launcher and HTTP backend need a reliable SSO flow across OAuth providers and Cognito-backed environments. 

## Decision
For HTTP authentication, use browser-based OAuth 2.0 Authorization Code with PKCE and a localhost callback listener.

Implementation choices:
- Generate PKCE code verifier/challenge for each login attempt.
- Use `state` validation to bind callback to the initiating login attempt.
- For OAuth providers, bind callback listener on a local high-port range (49100-49110).
- For Cognito, use fixed callback `http://localhost:51555/callback` to match registered app-client settings.
- Ignore non-callback noise requests and enforce timeout for callback wait.

## Consequences
- Positive: improved compatibility with IdP-hosted login and modern OAuth requirements.
- Positive: reduced auth failures from mismatched callback handling.
- Positive: clear operational requirement for allowed localhost callback URLs.
- Trade-off: local listener availability and port conflicts must be handled.
