# Workflow Performance Runbook

## Purpose
This runbook provides a coarse performance harness for workflow operations so you can measure gross latency and throughput before deeper optimization or architecture changes.

The goal is decision-grade evidence for whether the current service path is operationally acceptable, and whether DB-direct query work should even be considered.

## Scenarios Covered
The harness can measure these four scenarios:

1. Create one workflow as a smoke test to prove backend viability.
2. List/delete candidate discovery for a customer with about 3000 workflows selecting about 600 for bulk delete.
3. Modify one existing workflow and replace 60 trustees.
4. Create 40 workflows in one batch.

## Safety Model
Default mode is dry-run for all three scenarios.

This means:
- Delete measures discovery/filtering and service execution path without deleting workflows.
- Modify measures parsing, validation, lookup, mapping, and dry-run service path without changing the workflow.
- Create measures batch parsing and validation path without creating workflows.

Live mutations are supported, but only when `-RunLiveMutations` is supplied.

## Files
Harness:
- scripts/perf/Run-ComWorkflowPerf.ps1

Scenario inputs:
- scripts/perf/scenario-create-1-workflow-smoke.xml
- scripts/perf/scenario-modify-1-workflow-60-trustees.xml
- scripts/perf/scenario-create-40-workflows.xml

Output artifacts:
- artifacts/perf/workflow-perf-<backend-set>-<timestamp>/measurements.csv
- artifacts/perf/workflow-perf-<backend-set>-<timestamp>/summary.csv
- artifacts/perf/workflow-perf-<backend-set>-<timestamp>/backend-comparison.csv
- artifacts/perf/workflow-perf-<backend-set>-<timestamp>/db-direct-signals.txt

## Prerequisites
- Published CLI exists at publish/adept-tool.exe
- Valid COM server URL
- Valid HTTP server URL if comparing against HTTP
- Test environment with representative workflow volume
- For live modify/create runs, the scenario workflow names must match safe test targets

## Recommended Test Order
1. Run single-workflow COM smoke create.
2. If that passes, run COM dry-run only.
3. Run HTTP dry-run only.
4. Run combined COM and HTTP comparison.
5. Review p95 and ratio deltas.
6. Only if dry-run results are promising, run limited live mutation tests.

## Example Commands

### Single-workflow COM smoke test
Use this first when you are not sure COM create works at all.

```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://hd-srv-mgr.hd.helpdesk:443" \
  -Backends com \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -IncludeSingleCreateSmoke \
  -WarmupRuns 0 \
  -MeasuredRuns 1
```

For a real persisted smoke test in a safe test environment:

```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://hd-srv-mgr.hd.helpdesk:443" \
  -Backends com \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -IncludeSingleCreateSmoke \
  -WarmupRuns 0 \
  -MeasuredRuns 1 \
  -RunLiveMutations
```

### COM only
```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://hd-srv-mgr.hd.helpdesk:443" \
  -Backends com \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -BulkDeleteFilter "PERF_*" \
  -BulkDeleteTargetCount 600 \
  -WarmupRuns 1 \
  -MeasuredRuns 3
```

### HTTP only
```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://synergis-pm.adept-qa-private.synergissoftware.com/synergis.webapi" \
  -Backends http \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -BulkDeleteFilter "PERF_*" \
  -BulkDeleteTargetCount 600 \
  -WarmupRuns 1 \
  -MeasuredRuns 3
```

### Side-by-side COM and HTTP
```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://hd-srv-mgr.hd.helpdesk:443" \
  -HttpServer "https://synergis-pm.adept-qa-private.synergissoftware.com/synergis.webapi" \
  -Backends com,http \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -BulkDeleteFilter "PERF_*" \
  -BulkDeleteTargetCount 600 \
  -WarmupRuns 1 \
  -MeasuredRuns 5
```

### Live mutation run
Use only in a safe test environment.

```powershell
Set-Location C:\dev\code\Product\AdeptPowerTools
.\scripts\perf\Run-ComWorkflowPerf.ps1 \
  -Server "https://hd-srv-mgr.hd.helpdesk:443" \
  -Backends com \
  -SingleCreateXmlPath ".\scripts\perf\scenario-create-1-workflow-smoke.xml" \
  -ModifyXmlPath ".\scripts\perf\scenario-modify-1-workflow-60-trustees.xml" \
  -CreateXmlPath ".\scripts\perf\scenario-create-40-workflows.xml" \
  -BulkDeleteFilter "PERF_*" \
  -BulkDeleteTargetCount 600 \
  -WarmupRuns 1 \
  -MeasuredRuns 3 \
  -RunLiveMutations
```

## What the Harness Reports
Per backend and scenario:
- Minimum latency
- Average latency
- p50 latency
- p95 latency
- Maximum latency
- Estimated operations per second at p95
- Failure count

When both COM and HTTP are run together, it also writes:
- `backend-comparison.csv`

This compares:
- HTTP p95
- COM p95
- delta milliseconds
- COM-to-HTTP p95 ratio

## How To Interpret Results
The first decision is not “can we optimize it?”
The first decision is “is the current path grossly acceptable?”

### Suggested coarse thresholds
Single-workflow smoke create:
- Good: succeeds at all
- Escalate immediately: any failure in live COM mode

Bulk delete candidate selection for 600 workflows:
- Good: p95 under 60s
- Watch: 60s to 180s
- Escalate: over 180s

Modify one workflow replacing 60 trustees:
- Good: p95 under 10s
- Watch: 10s to 30s
- Escalate: over 30s

Create 40 workflows batch:
- Good: p95 under 45s
- Watch: 45s to 120s
- Escalate: over 120s

### COM versus HTTP comparison guidance
If COM is within about 2x HTTP on p95 for all three scenarios, gross performance is probably acceptable for the current phase.

If COM is worse than HTTP by more than about 3x on multiple scenarios, profile first before committing to DB-direct work.

If only one scenario is materially slower in COM, optimize that path specifically rather than assuming a broad architectural problem.

## When DB-Direct Queries Become Plausible
DB-direct read/query work is worth considering only if:
- Dry-run results already show gross threshold breaches.
- The same scenario is repeatedly slow across runs.
- The bottleneck is candidate discovery or metadata read amplification, not validation or business-rule enforcement.
- COM path remains materially worse than HTTP after simple batching/filtering improvements.

DB-direct write paths should remain a last resort because they risk violating workflow invariants, save-boundary semantics, and lock/recovery contracts.

## Recommended Decision Sequence
1. Prove a single workflow can be created over COM.
2. Run dry-run COM and HTTP comparison.
3. Identify whether the pain is delete discovery, trustee-heavy modify, or batch create.
4. If needed, add one focused profiler/logging pass around that path.
5. Prefer service-level batching or reduced read amplification before any DB-direct design.
6. Only pursue DB-direct reads when the measurements show clear and repeated threshold breaches.

## Notes
- `BulkDeleteTargetCount` is used for throughput normalization and threshold interpretation. The actual delete candidate count comes from the filter and environment.
- The single create smoke XML uses workflow name `PERF_Create_Smoke` and is intended to prove viability before scale testing.
- For the 3000-workflow customer scenario, use a filter that realistically yields about 600 deletable workflows.
- The modify XML assumes a workflow named `PERF_Modify_Target` exists for modify tests.
- The create XML uses workflow names `PERF_Create_001` through `PERF_Create_040`.
