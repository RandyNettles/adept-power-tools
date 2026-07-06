# TDN - Workflow Adept Power Tools Share and Trustee Identity Notes

## Purpose

This TDN captures Adept Power Tools-specific implementation details for sharing semantics and trustee identity handling that should stay outside the Adept 12 AWC deep-dive.

## Share and Container Ownership Semantics

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
