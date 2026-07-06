# TDN - Workflow Adept Power Tools Share and Trustee Identity Notes

## Purpose

This TDN captures Adept Power Tools-specific implementation details for sharing semantics and trustee identity handling that should stay outside the Adept 12 AWC deep-dive.

## COM Path (11.4.5)

Provided Adept 11.4.5 evidence separates two concerns:
- Runtime workflow visibility uses `WFList` ownership/share/group membership filtering in native/Web runtime helper flows.
- Admin workflow CRUD is Desktop/Core-first and does not expose a dedicated AdeptWeb admin share-mutation controller in the supplied slice.

11.4.5-specific implication:
- COM/Desktop parity work should treat share visibility and admin workflow edit persistence as distinct contracts.
- Native sharing is layered on workflow-definition persistence through workflow share-list containers (`CNTT_WFLIST`), not workflow table columns.
- New workflows are automatically given a private share-list entry on first save.
- Explicit share changes are a second native action after create/modify, not an intrinsic part of workflow-definition creation.

## Share and Container Ownership Semantics

### 11.4.5 Native Share-List Model

Observed native sharing data model:
- Workflow definition identity lives in workflow tables (`WF`, `WFSTEP`, `WFTR`, `NOTIFY`).
- Workflow sharing identity lives in a named-list/container record of type `CNTT_WFLIST`.
- Native list naming convention is `WF_ID_PREFIX + workflowId` via `CreateNameFromWFID`.
- Reverse mapping is `GetWFIDFromWFListName`.
- Owner/global/share state is carried on the named-list object rather than on workflow table columns.

11.4.5 native implication:
- A third client using COM/native routes must treat workflow-definition save and workflow share updates as linked but separate persistence concerns.

### 11.4.5 Native Create-Time Share Behavior

Observed native create behavior:
- `CWorkFlowAdmin::OnBnClickedAddWF` starts workflow creation.
- `CCliWorkflowDefList::Add` creates the in-memory workflow object.
- `CCliWorkflowDefManager::Update` commits workflow persistence.
- `CCliWorkflowDefList::Write` persists the workflow row and, for new workflows, calls `HLIB_Cache::CreateSharedWFList(workflowId, false, ownerUserId, workflowName)`.

Result:
- A newly created workflow exists in workflow tables and also has a private workflow share-list container keyed by workflow id.
- Private-by-default visibility is the native baseline; global share is not the default.

### Container-Centric Sharing

Adept Power Tools HTTP share operations are container-centric and resolved from workflow list metadata.

For shared=true:

- Read share packet for container.
- Force global share flag true.
- Ensure share model list is non-null.
- Apply share packet.

For shared=false:

- Apply unshare operation for container.

### Missing Container Metadata Behavior

- Missing workflow in list: hard failure.
- Missing or blank containerId: hard failure.
- COM backend does not support share-state mutation and should fail fast with guidance to use HTTP backend.

Operational guidance:

- Use HTTP backend for share/unshare operations.
- Treat missing containerId as data integrity/configuration issue.

11.4.5 native qualification:
- The provided Adept 11.4.5 docs do not show an equivalent AdeptWeb admin share/unshare API path.
- Native explicit share path is `CWorkFlowAdmin::OnBnClickedShareWF` -> `CreateNameFromWFID` -> `GetWFListFromName` -> `UI_ShareObj` in `SHARE_WF` mode -> update list sharing flags from dialog result.
- Optional native global-promotion helper exists through `CWorkFlowAdmin::OfferToShareWFGlobally` -> `HLIB_Cache::MakeListGlobal`.

### 11.4.5 Native Visibility Retrieval and Assignment Rules

Observed native retrieval paths:
- `HLIB_Cache::GetListOfSharedWF` returns workflows shared with the current user.
- `HLIB_Cache::GetListOfAllWF` returns all workflow lists for admin/ownership scenarios.

Library Admin visibility rule:
- `CLibAdminDialog::InitWFCombo` filters workflow definitions through `IsWFSharedWithMe(wf)`.
- A workflow generally must be shared with the current user, or be global, to appear in Library Admin workflow selection.

Important native exception:
- If the workflow is already in use by the library, Library Admin keeps it visible even if current sharing would otherwise hide it.

Operational implication:
- Third-client parity should preserve the in-use workflow visibility exception to avoid orphaned or uneditable library-assignment UX.

## Trustee Identity and AWC Display Fidelity

### Problem Summary

Adept Power Tools could persist WFUT_USER trustee IDs in a form that passed server-side persistence checks but failed to materialize in AWC reviewer display.

### Why It Failed in Display

AWC reviewer rehydration resolves WFUT_USER entries by exact match against usersCache targetId (from /api/user/users user.id).

If persisted trusteeId does not equal that user.id format, recipient rehydration silently drops the trustee row.

### Root Cause in Adept Power Tools

A merge path in HTTP user loading could overwrite primary GUID-like notification target IDs with legacy login-name IDs when both feeds contained the same user.

### Fix Applied

Preserve primary notification target ID when a primary entry already exists.
Only use legacy login-name target IDs for users absent from the primary feed.

### Invariant

For WFUT_USER trustees, persisted trusteeId must match the value AWC uses as usersCache targetId (from /api/user/users user.id) in the target environment.

Note:

- Some environments may use GUID ids.
- Other environments may use login-name ids.
- The hard requirement is exact identity congruence with AWC user.id, not a single universal format.

## Practical Detection Check

After create/modify, inspect saved workflow trustee rows and verify WFUT_USER trusteeId values align with current /api/user/users id format.

## 11.4.5 Native-Specific Trustee Notes

- Native trustee persistence writes raw trustee id plus type; no HTTP-side normalization step exists in the observed native write path.
- For third-client parity, preserve exact trustee IDs supplied to native add/write routes unless an explicit verified canonicalization rule is applied before commit.
- Unknown recipient targets on reload should remain preserved and marked unresolved rather than discarded.

## 11.4.5 Native Sharing Checklist

- New workflow save creates a private workflow share-list container by default.
- Share changes are applied against the workflow share-list/container, not workflow table columns.
- Shared workflow pick lists should be refreshed before rendering assignment/admin selectors.
- Library Admin visibility should honor both current share state and the in-use workflow exception.
