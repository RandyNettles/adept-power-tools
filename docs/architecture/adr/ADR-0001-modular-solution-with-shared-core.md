# ADR-0001: Modular Solution with Shared Core

- Status: Accepted
- Date: 2026-06-29

## Context
Adept Tools supports multiple capabilities (auth, workflow, import) and multiple clients (CLI and WPF launcher). The implementation already separates responsibilities across projects in `src` and tests in `tests`.

## Decision
Use a modular .NET solution with a shared core and backend-specific implementations:
- `AdeptTools.Core` defines shared contracts, models, and cross-cutting behavior.
- `AdeptTools.Backend.Http` implements HTTP/OAuth/Cognito interactions.
- `AdeptTools.Backend.Com` implements COM SDK interactions for Adept 11.x environments.
- `AdeptTools.Workflow` and `AdeptTools.Import` provide feature-domain services and clients.
- `AdeptTools.Cli` and `AdeptTools.Launcher` are separate entry points over shared services.

## Consequences
- Positive: backend and UI concerns are decoupled from feature logic.
- Positive: testability improves via shared interfaces and isolated projects.
- Positive: capabilities can evolve independently without rewriting both clients.
- Trade-off: more projects and dependency wiring complexity.
