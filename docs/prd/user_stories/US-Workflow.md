# User Stories - Workflow

## US-WF-001 Multi-Source Workflow Input
As a workflow admin, I want to run workflow operations from Excel or XML so that I can use either source format.

Acceptance Criteria
1. Workflow list, create, modify, and delete are supported operations.
2. Create/modify accept Excel or XML setup input.
3. Invalid source input produces validation errors before apply.

FR Traceability
- FR-025
- FR-026

## US-WF-002 Trustee Identity Resolution
As a workflow admin, I want user trustees resolved against server users so that workflow assignments map to valid identities.

Acceptance Criteria
1. User-type trustees are matched against server-side user records.
2. Weak or missing matches fail operation before workflow mutation.
3. Failure feedback identifies unresolved trustee issues.

FR Traceability
- FR-027

## US-WF-003 Trustee Role Mapping
As a workflow admin, I want trustees routed by role type so that reviewer and notification behavior is configured correctly.

Acceptance Criteria
1. Trustee roles map to reviewer collection when applicable.
2. Trustee roles map to email notification collection when applicable.
3. Trustee roles map to alert notification collection when applicable.

FR Traceability
- FR-028

## US-WF-004 Safe and Selective Deletion
As a workflow admin, I want delete operations to respect eligibility constraints so that locked or non-deletable workflows are protected.

Acceptance Criteria
1. Delete excludes non-deletable and locked items.
2. Delete supports filter-based and ID-targeted paths.
3. Skipped items and reasons are included in output.

FR Traceability
- FR-029

## US-WF-005 Scalable Delete Execution and Auditability
As a workflow admin, I want bounded parallel deletion with optional manifests so that large deletes are efficient and auditable.

Acceptance Criteria
1. Delete execution runs with bounded parallelism.
2. Optional pre-delete manifest can be produced.
3. Manifest includes operation and workflow metadata needed for audit.

FR Traceability
- FR-030

## US-WF-006 Client Workflow Operations UX
As a client user, I want workflow operation controls with progress and cancellation so that long-running operations are manageable.

Acceptance Criteria
1. UI supports refresh, create, modify, and delete with confirmation.
2. UI supports dry-run execution mode.
3. UI shows progress and per-item result messages.
4. UI supports cancellation of active operation.

FR Traceability
- FR-031
