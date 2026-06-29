# ADR-0002: Runtime Backend Selection via Interfaces and DI

- Status: Accepted
- Date: 2026-06-29

## Context
The tool must operate against different execution modes:
- HTTP backend (12.x modern server/API environments).
- COM backend (11.x desktop SDK environments).
- Mock mode (testing and demos).

Both CLI and launcher need consistent behavior without duplicating backend-specific branches throughout feature code.

## Decision
Use interface-driven abstractions with dependency injection to select implementations at runtime.

Key contracts include:
- `IAdeptAuthService`
- `IAdeptApiClient`
- `IWorkflowApiClient`
- `IImportApiClient`

Entry points configure services and select implementations based on mode and backend choice.

## Consequences
- Positive: feature services remain backend-agnostic.
- Positive: mock mode is first-class and easier to test.
- Positive: adding a new backend can be done by implementing existing contracts.
- Trade-off: DI configuration must be carefully maintained in both clients.
