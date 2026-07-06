# TDN - Workflow Adept Power Tools Congruence and Validation Notes

## Purpose

This TDN records Adept Power Tools-specific congruence, parsing, and validation policy notes that do not belong in the Adept 12 AWC deep-dive.

## COM Path (11.4.5)

The provided Adept 11.4.5 workflow evidence shows that admin CRUD congruence must also be measured against the native/Desktop route:
- `CWorkFlowAdmin` add/edit/delete flows.
- `CCliWorkflowDefManager` edit-session and `Update()` commit boundary.
- Native child-list `Write()` persistence into WF/WFSTEP/WFTR/NOTIFY.

11.4.5-specific implication:
- Validation parity cannot be defined only against AWC HTTP save behavior; it also needs a native object-graph and table-write congruence lens.

## Timeout Round-Trip Congruence (Adept Power Tools vs AWC)

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

## Input Parsing and Normalization (Excel/XML)

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

## Validation Policy

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

## 11.4.5 Native-Specific Validation Notes

- Native admin CRUD is not exposed through a dedicated AdeptWeb admin controller in the provided slice; pre-save validation must therefore be satisfied before entering native `Update()` commit paths.
- Delete validation should include explicit active-document impact confirmation because native delete side effects reconcile active docs and workflow/library bindings during write-back.
- System workflow edit/delete guards remain hard validation boundaries in native/Desktop mode.
