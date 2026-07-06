# TDN - Workflow Adept Power Tools Congruence and Validation Notes

## Purpose

This TDN records Adept Power Tools-specific congruence, parsing, and validation policy notes that do not belong in the Adept 12 AWC deep-dive.

This TDN also makes mode and surface congruence boundaries explicit:
- backend modes: HTTP, COM, Mock
- product surfaces: CLI and Client

## Boundary Model

This TDN has two contract layers.

### Layer 1: Shared congruence and validation contract

Applies to:
- All backend modes: HTTP, COM, Mock.
- Both product surfaces: CLI and Client.

Defines:
- What “congruent” persistence and validation behavior means.
- Which parsing/normalization/validation rules are semantically required.
- Which outcomes are hard errors versus warnings.

Does not define:
- Exact adapter mechanics.
- Exact UI flow shape.
- Mode-specific transport details beyond preserving the same semantic contract.

### Layer 2: Mode and surface realization

Applies to:
- Mode-specific persistence and parsing behavior.
- CLI and Client orchestration/presentation behavior.

Defines:
- Where HTTP/COM/Mock realization differs.
- Where CLI and Client may differ in interaction style.

Does not redefine:
- The congruence contract itself.
- The validation pass/fail meaning.

## Mode Boundaries (HTTP, COM, Mock)

### HTTP mode

HTTP mode is the primary current implementation surface for the congruence fixes described in this TDN.

HTTP-specific characteristics:
- Full-snapshot save behavior and fetched-model retention semantics are central.
- XML parsing and service-side explicit overwrite behavior are directly observable in ATP implementation.

Boundary rule:
- HTTP-specific model wiring may differ, but it must preserve the same validation and congruence outcomes defined in this TDN.

### COM mode

COM mode must preserve the same congruence outcomes through native/Desktop object-graph and write-back behavior.

COM-specific characteristics:
- Persistence occurs through native manager/list edit and `Update()` commit behavior.
- Validation parity must consider native write-path side effects and guard rules.

Boundary rule:
- COM realization may differ from HTTP full-snapshot transport mechanics, but it must preserve the same validated final state and failure boundaries.

### Mock mode

Mock mode is a deterministic simulation path for validation and orchestration behavior.

Mock-specific characteristics:
- Parsing and validation rules can be exercised without live backend dependencies.
- Persistence congruence scenarios may be simulated rather than performed against real adapters.

Boundary rule:
- Mock mode must simulate the same semantic validation/congruence contract and must not silently weaken hard-error conditions.

## Surface Boundaries (CLI and Client)

### CLI surface

CLI owns command-driven execution and textual reporting of validation/congruence outcomes.

CLI contract:
- Must preserve shared parsing, normalization, and validation semantics.
- May present warnings and failures in script-friendly form.

### Client surface

Client owns interactive workflow authoring orchestration and presentation of congruence-sensitive outcomes.

Client contract:
- Must preserve the same validation gates and congruent final-state expectations.
- May use richer interaction or staged UX without altering semantic validation/pass-fail behavior.

Boundary rule:
- Surface differences may change presentation, but not the meaning of congruence success, warning, or failure.

## COM Path (11.4.5)

The provided Adept 11.4.5 workflow evidence shows that admin CRUD congruence must also be measured against the native/Desktop route:
- `CWorkFlowAdmin` add/edit/delete flows.
- `CCliWorkflowDefManager` edit-session and `Update()` commit boundary.
- Native child-list `Write()` persistence into WF/WFSTEP/WFTR/NOTIFY.

11.4.5-specific implication:
- Validation parity cannot be defined only against AWC HTTP save behavior; it also needs a native object-graph and table-write congruence lens.

Mode/surface implication:
- COM-path evidence qualifies mode-specific realization; it does not replace the shared validation/congruence contract both CLI and Client must preserve.

## Timeout Round-Trip Congruence (Adept Power Tools vs AWC)

Shared-contract boundary:
- The congruence expectations below define semantic behavior expected across modes and both surfaces.
- The concrete implementation examples that follow are currently strongest in HTTP-mode ATP evidence.

### What Matches AWC

- Full-snapshot persistence model at save.
- Timeout overwrite only when input supplies timeout days.
- Recurring timeout overwrite only when input supplies recurring timeout days.
- Omitted timeout fields retain server values in fetched model.

### Gap Found and Fixed

Original gap:

- Weekend include flags were overwritten even when input did not declare weekend intent.

Root cause:

- Non-nullable ExcludeSaturday and ExcludeSunday defaulted into unconditional assignments.

Fix:

- ExcludeSaturday and ExcludeSunday changed to nullable booleans.
- XML reader parses weekend attributes as optional nullable values.
- XML schema allows optional weekend attributes without implicit defaults.
- Service overwrites include flags only when values are explicitly present.

Result:

- Omitted weekend attributes retain existing include flags.
- Explicit weekend attributes overwrite include flags intentionally.

### Coverage Added

- XML parse: omitted weekend attributes parse as null.
- XML parse: explicit weekend attributes parse as provided.
- Modify semantics: omitted weekend inputs retain existing include flags.
- Modify semantics: explicit weekend inputs overwrite include flags.

Mode boundary:
- The nullable-weekend-field fix is an HTTP-oriented implementation correction, but the congruence rule it protects is shared.

## Input Parsing and Normalization (Excel/XML)

Shared-contract boundary:
- The parsing and normalization rules below are semantic input-contract expectations for both CLI and Client.
- Surfaces may differ in how users author/select files, but not in how those files are interpreted once handed to the import/workflow readers.

### Trustee Continuation and Defaults

Excel behavior:

- Non-empty Step Name starts a new step.
- Empty Step Name rows continue trustee rows for current step.
- Missing Type and Role can inherit prior values in step context.
- Missing Type can default to User.
- Missing/blank Role defaults to Reviewer.
- Comma-separated trustee values split into multiple trustees.

XML behavior:

- No continuation semantics.
- Trustees are explicit per element.
- Missing/blank Role defaults to Reviewer.
- Missing/blank Type is skipped by mapping.

### Step Boundary and Empty Trustee Handling

- Excel uses header detection and Step Name boundaries.
- XML uses explicit Step element boundaries.
- AllowEmptyTrustees controls zero-trustee permissiveness in validation policy.

### XML vs Excel Parity

- Partial parity only.
- XML supports recurring timeout and weekend exclusion semantics.
- Excel currently represents reduced timeout authoring surface.

Operational guidance:

- Prefer XML for highest timeout fidelity.
- Treat Excel as convenience format with inferred trustee metadata.

Surface boundary:
- CLI and Client may offer different authoring or file-selection UX, but both must preserve the same reader semantics once execution begins.

## Validation Policy

Shared-contract boundary:
- The validation rules below are semantic pass/fail contracts shared across modes and both surfaces.
- Mode-specific mechanics may affect how violations are detected, but not whether they are blocking versus warning-level.

### Preflight Validation Scope

- Input shape and server limit checks.
- Structural checks for workflow and step naming.
- Required approvals sanity checks.
- Trustee resolution and trustee-type compatibility checks.

### Post-Save Verification Scope

- Reviewer trustee persistence verification by step.
- Workflow visibility and share-state confirmation.
- Canonical model adoption from server responses.

### Error and Warning Policy

Hard errors:

- Validation errors.
- Unresolved or invalid trustees.
- Invalid reviewer trustee type.
- Save API failures.
- Post-save trustee persistence mismatch.

Warnings:

- AllowEmptyTrustees true with zero trustees.
- Context visibility warnings that do not block save.

Surface/mode boundary:
- CLI and Client may present these warnings differently, and HTTP/COM/Mock may encounter them through different mechanics, but the warning-versus-hard-error distinction is shared.

## 11.4.5 Native-Specific Validation Notes

- Native admin CRUD is not exposed through a dedicated AdeptWeb admin controller in the provided slice; pre-save validation must therefore be satisfied before entering native `Update()` commit paths.
- Delete validation should include explicit active-document impact confirmation because native delete side effects reconcile active docs and workflow/library bindings during write-back.
- System workflow edit/delete guards remain hard validation boundaries in native/Desktop mode.

## Surface/Mode Separation Checklist

1. Does HTTP preserve the same validated final state and congruence rules after full-snapshot save?
2. Does COM/native preserve the same validation and congruent end state after `Update()` write-back?
3. Does Mock preserve the same hard-error and warning distinctions rather than weakening them?
4. Do CLI and Client surface the same semantic validation outcome even if their interaction model differs?
5. Are HTTP-specific implementation details, such as fetched-model overwrite mechanics, kept distinct from the shared congruence contract?
