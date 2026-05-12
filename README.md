# Adept Tool

A CLI and WPF utility for Adept administrative operations — workflow management, data import, and template generation.

## Quick Start

```bash
# Build
dotnet build

# Run CLI (mock mode)
dotnet run --project src/AdeptTools.Cli -- auth test --mock

# Run tests
dotnet test

# Publish single-file exe
dotnet publish src/AdeptTools.Cli -r win-x64 --self-contained
```

## Solution Structure

- `src/AdeptTools.Core/` — Shared interfaces, models, infrastructure
- `src/AdeptTools.Backend.Http/` — 12.X HTTP REST backend implementations
- `src/AdeptTools.Cli/` — Console entry point (adept-tool.exe)
- `src/AdeptTools.Launcher/` — WPF GUI launcher (future)
- `tests/AdeptTools.Core.Tests/` — Core library unit tests
- `tests/AdeptTools.Cli.Tests/` — CLI integration tests

## CLI Usage

```
adept-tool auth test --server https://adept.example.com --user ADM
adept-tool auth test --mock
adept-tool --help
adept-tool --version
```
