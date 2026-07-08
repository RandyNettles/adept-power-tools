# Functional Requirements - Implementation Baseline

- Status: Accepted baseline (implementation-derived)
- Date: 2026-06-29
- Source of truth: current code in src/ and tests/

## Purpose
This document captures functional requirements that are already implemented in code. It is intentionally code-first and does not include future or proposed behavior that is not present in the current implementation.

## Scope
- CLI capabilities
- Launcher capabilities
- Authentication and session behavior
- Workflow operations
- Import operations
- Validation and persistence behavior

## Functional Requirements

### CLI

FR-001
The system shall expose CLI global options for server URL, username, mock mode, backend type, verbose mode, and log path.
Evidence:
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Core/Configuration/AdeptToolSettings.cs

FR-002
The system shall require a server URL for non-mock executions and fail command execution when it is missing.
Evidence:
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Infrastructure/ServiceRegistration.cs

FR-003
The system shall provide CLI command groups for auth, workflow, and import.
Evidence:
- src/AdeptTools.Cli/Program.cs
- src/AdeptTools.Cli/Commands/AuthCommands.cs
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- src/AdeptTools.Cli/Commands/ImportCommands.cs

FR-004
The auth test command shall execute authentication and report result details including server/user/version state.
Evidence:
- src/AdeptTools.Cli/Commands/AuthCommands.cs
- src/AdeptTools.Core/Configuration/CredentialManager.cs

FR-005
The workflow list command shall support name filtering and output formats table, csv, and json.
Evidence:
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- src/AdeptTools.Workflow/Services/WorkflowService.cs

FR-006
The workflow create and modify commands shall accept Excel or XML input and support dry-run validation mode.
Evidence:
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- src/AdeptTools.Workflow/Services/WorkflowService.cs

FR-007
The workflow delete command shall support filter, status, dry-run, force, and manifest options, including safety checks for broad filters.
Evidence:
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs
- tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs

FR-008
The import command group shall support fetch-fields, map, validate, and run operations.
Evidence:
- src/AdeptTools.Cli/Commands/ImportCommands.cs

### Launcher Navigation and Connection

FR-009
The launcher shall default to a Connect-first flow and keep Workflow and Import pages disabled until connected.
Evidence:
- src/AdeptTools.Launcher/MainWindow.xaml.cs
- src/AdeptTools.Launcher/ViewModels/MainViewModel.cs

FR-010
The Connect page shall support backend selection (HTTP/COM) and mock mode toggling with backend-specific input fields.
Evidence:
- src/AdeptTools.Launcher/Views/ConnectPage.xaml
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs

FR-011
The Connect flow shall show connection states Disconnected, Connecting, Connected, and Error, with status text and error messaging.
Evidence:
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/Converters/ConnectionStatusToColorConverter.cs

FR-012
The Connect flow shall prompt for account selection when authentication returns a multi-user selection response.
Evidence:
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Launcher/ViewModels/UserSelectionDialogViewModel.cs
- src/AdeptTools.Core/Auth/IAdeptAuthService.cs

FR-013
The launcher shall persist successful server URL history and last username for later reuse.
Evidence:
- src/AdeptTools.Launcher/Services/ServerHistoryService.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs

FR-014
The launcher shall provide add/edit/remove behavior for persisted COM connection profiles.
Evidence:
- src/AdeptTools.Launcher/Services/ComProfileService.cs
- src/AdeptTools.Launcher/ViewModels/ComProfileDialogViewModel.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs

### Authentication and Session

FR-015
HTTP authentication shall determine login mode from server options and route among local password login, OAuth/Cognito browser SSO, or Windows SSO fallback.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs
- src/AdeptTools.Backend.Http/Models/ClientBootstrapResponse.cs
- src/AdeptTools.Backend.Http/Models/SsoSettingsResponse.cs
- src/AdeptTools.Backend.Http/Models/CognitoSettingsResponse.cs

FR-016
HTTP browser SSO shall use OAuth 2.0 Authorization Code with PKCE and localhost callback listener handling.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs

FR-017
The SSO callback flow shall validate state, ignore non-callback noise requests, and enforce timeout behavior.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs

FR-018
The HTTP authentication flow shall surface user-friendly errors for redirect mismatch/settings failures and known status code cases.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs

FR-019
The HTTP authentication flow shall support status-230 account disambiguation and complete login with selection context.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs
- src/AdeptTools.Backend.Http/Models/AuthenticateResponse.cs
- src/AdeptTools.Backend.Http/Models/OAuthLoginRequest.cs

FR-020
Authentication services shall support logout and refresh/session-check behavior appropriate to backend implementation.
Evidence:
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs
- src/AdeptTools.Backend.Com/Auth/ComAdeptAuthService.cs

FR-021
The launcher shall persist HTTP auth session state in encrypted per-user local storage and attempt automatic session resume on startup.
Evidence:
- src/AdeptTools.Launcher/Services/AuthSessionStore.cs
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs

FR-022
Session resume shall validate token expiry and clear invalid session data before connection is marked successful.
Evidence:
- src/AdeptTools.Launcher/ViewModels/ConnectViewModel.cs
- src/AdeptTools.Backend.Http/Auth/HttpAdeptAuthService.cs

FR-023
COM backend operations shall run through a dedicated STA execution model with a shared session manager for COM object lifetime.
Evidence:
- src/AdeptTools.Backend.Com/Infrastructure/ComOperationRunner.cs
- src/AdeptTools.Backend.Com/Infrastructure/ComSessionManager.cs

FR-024
COM session lifecycle shall include explicit disconnect and COM object release on logout/dispose.
Evidence:
- src/AdeptTools.Backend.Com/Infrastructure/ComSessionManager.cs
- src/AdeptTools.Backend.Com/Auth/ComAdeptAuthService.cs

### Workflow

FR-025
The workflow service shall support list, create, modify, and delete operations from Excel and XML inputs.
Evidence:
- src/AdeptTools.Workflow/Services/IWorkflowService.cs
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Input/WorkflowExcelReader.cs
- src/AdeptTools.Workflow/Input/WorkflowXmlReader.cs

FR-026
Workflow create and modify execution shall validate input models before applying server operations.
Evidence:
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Validation/WorkflowValidator.cs

FR-027
Workflow trustee resolution shall map user-type trustees to server users and fail execution when matches are weak or absent.
Evidence:
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Input/UserMatcher.cs

FR-028
Workflow step trustee mapping shall route trustees into reviewer, email-notification, and alert-notification collections.
Evidence:
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Input/TrusteeTypeMapper.cs

FR-029
Workflow delete shall exclude non-deletable and locked items and support ID-targeted and filter-based deletion.
Evidence:
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- src/AdeptTools.Workflow/Models/WorkflowAdminItem.cs

FR-030
Workflow delete execution shall support bounded parallel deletion and optional manifest output with operation metadata.
Evidence:
- src/AdeptTools.Workflow/Services/WorkflowService.cs
- tests/AdeptTools.Workflow.Tests/WorkflowServiceDeleteTests.cs

FR-031
Launcher workflow UX shall support refresh, create, modify, select-and-delete with confirmation, dry-run, progress display, and cancel.
Evidence:
- src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs
- src/AdeptTools.Launcher/ViewModels/ConfirmDeleteDialogViewModel.cs
- src/AdeptTools.Launcher/Views/WorkflowPage.xaml

### Import

FR-032
The import service shall read workbook and optional XML mapping config, resolve fields, validate data, and execute row processing.
Evidence:
- src/AdeptTools.Import/Services/ImportService.cs
- src/AdeptTools.Import/Readers/ImportExcelReader.cs
- src/AdeptTools.Import/Readers/ImportXmlConfigReader.cs

FR-033
Import validation shall enforce mapping constraints including required keys, mode consistency, duplicate mappings, and date-range requirements.
Evidence:
- src/AdeptTools.Import/Services/MappingValidator.cs
- tests/AdeptTools.Import.Tests/Services/MappingValidatorTests.cs

FR-034
Import search behavior shall build server search terms for supported operators and skip rows with missing required key values.
Evidence:
- src/AdeptTools.Import/Services/SearchBuilder.cs
- tests/AdeptTools.Import.Tests/Services/SearchBuilderTests.cs

FR-035
Import run behavior shall branch outcomes by search cardinality and mode (search-only, update, add-if-not-found).
Evidence:
- src/AdeptTools.Import/Services/ImportService.cs
- tests/AdeptTools.Import.Tests/Services/ImportServiceTests.cs

FR-036
Import dry-run mode shall avoid data mutation calls and return per-row dry-run result messages.
Evidence:
- src/AdeptTools.Import/Services/ImportService.cs
- tests/AdeptTools.Import.Tests/Services/ImportServiceTests.cs

FR-037
Import field resolution shall support canonical field names and display names and fail validation on unresolved mapped fields.
Evidence:
- src/AdeptTools.Import/Services/FieldResolver.cs

FR-038
Launcher import UX shall support workbook/config selection, drag-drop ingest, validation, dry-run run/cancel, and field-definition export.
Evidence:
- src/AdeptTools.Launcher/ViewModels/ImportViewModel.cs
- src/AdeptTools.Launcher/Views/ImportPage.xaml
- src/AdeptTools.Launcher/Views/ImportPage.cs

FR-039
Launcher template UX shall generate import/workflow template workbooks and support opening generated files/folders.
Evidence:
- src/AdeptTools.Launcher/ViewModels/TemplateViewModel.cs
- src/AdeptTools.Launcher/Views/TemplatePage.xaml

FR-040
The import Excel template and row parser shall support defining multiple trustees within a single row using a defined delimiter or column convention; each trustee entry shall be individually validated, and unresolvable entries shall produce a per-row validation error without blocking other rows.
Evidence:
- TBD

### Workflow — Export (Planned)

The following requirements describe planned behavior for exporting existing workflows to the Excel template format. They are not yet implemented; Evidence lists the intended locations.

FR-041
The workflow service shall export one or more selected workflows into a single Excel workbook containing one `WF-` worksheet per workflow plus a `Config` sheet, in the same template layout consumed by the workflow Excel reader, so the exported file is valid input for the modify operation without manual reformatting.
Evidence (planned):
- src/AdeptTools.Workflow/Input/WorkflowExcelWriter.cs
- src/AdeptTools.Workflow/Services/IWorkflowService.cs
- src/AdeptTools.Workflow/Services/WorkflowService.cs

FR-042
Workflow export shall fetch full per-workflow detail from the backend and faithfully reconstruct workflow-level fields (memo, deadline, active, shared) and each step's approvals-required, auto-advance, and trustees, mapping server reviewer, email-notification, and alert-notification collections back to the `Reviewer`, `Notify`, and `Alert` role values.
Evidence (planned):
- src/AdeptTools.Workflow/Api/IWorkflowApiClient.cs
- src/AdeptTools.Workflow/Models/WorkflowEditModel.cs
- src/AdeptTools.Workflow/Input/WorkflowExcelWriter.cs

FR-043
Workflow export shall name each worksheet using the `WF-` prefix and shall sanitize worksheet names to satisfy Excel constraints (maximum length and illegal characters) and to keep sheet names unique within the workbook; the authoritative workflow name shall be persisted in the worksheet so the modify operation resolves the correct target workflow even when the sheet name is sanitized. The workflow Excel reader shall prefer the persisted workflow name over the sheet-tab name when present, and fall back to the sheet-tab name for backward compatibility.
Evidence (planned):
- src/AdeptTools.Workflow/Input/WorkflowExcelWriter.cs
- src/AdeptTools.Workflow/Input/WorkflowExcelReader.cs

FR-044
The CLI shall provide a `workflow export` command that writes the export workbook to a specified output path and supports selecting workflows by filter, with script-friendly result reporting.
Evidence (planned):
- src/AdeptTools.Cli/Commands/WorkflowCommands.cs

FR-045
The launcher workflow UX shall support exporting the currently selected workflows to an Excel workbook via a save-file dialog, with the action gated on a non-empty selection and with progress, cancellation, and per-workflow result messages.
Evidence (planned):
- src/AdeptTools.Launcher/ViewModels/WorkflowViewModel.cs
- src/AdeptTools.Launcher/Views/WorkflowPage.xaml

## Notes
- This baseline is implementation-derived and should be updated when behavior changes.
- Requirement IDs are stable references and should be retained in future revisions.
