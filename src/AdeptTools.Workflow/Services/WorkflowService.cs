using System.Text.Json;
using System.Text.RegularExpressions;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Validation;

namespace AdeptTools.Workflow.Services;

public class WorkflowService : IWorkflowService
{
    private const int MaxDeleteConcurrency = 5;

    private readonly IWorkflowApiClient _apiClient;
    private readonly WorkflowExcelReader _excelReader;
    private readonly WorkflowXmlReader _xmlReader;
    private readonly WorkflowValidator _validator;

    public WorkflowService(
        IWorkflowApiClient apiClient,
        WorkflowExcelReader excelReader,
        WorkflowXmlReader xmlReader,
        WorkflowValidator validator)
    {
        _apiClient = apiClient;
        _excelReader = excelReader;
        _xmlReader = xmlReader;
        _validator = validator;
    }

    public async Task<WorkflowBatchResult> CreateAsync(
        WorkflowCreateRequest request,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken ct = default)
    {
        var input = ReadInput(request.InputFilePath);
        var batch = new WorkflowBatchResult { Total = input.Workflows.Count, DryRun = request.DryRun };

        // Validate
        var setup = await _apiClient.GetSetupAsync(ct);
        var validation = _validator.Validate(input.Workflows, setup);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = error.WorkflowName ?? "Unknown",
                    Status = WorkflowResultStatus.Fail,
                    Message = error.Message
                });
                batch.Failed++;
            }
            return batch;
        }

        // Name resolution — resolve all User-type trustees before proceeding
        var resolutionResult = await ResolveTrusteesAsync(input.Workflows, ct);
        if (!resolutionResult.AllResolved)
        {
            foreach (var failure in resolutionResult.Failures)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = failure.WorkflowName,
                    Status = WorkflowResultStatus.Fail,
                    Message = failure.Message
                });
                batch.Failed++;
            }
            batch.Total = resolutionResult.Failures.Count;
            return batch;
        }

        if (request.DryRun)
        {
            foreach (var wf in input.Workflows)
            {
                var trusteeCount = wf.Steps.Sum(s => s.Trustees.Count);
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = wf.Name,
                    Status = WorkflowResultStatus.Success,
                    Message = $"Would create ({wf.Steps.Count} steps, {trusteeCount} trustees)",
                    StepCount = wf.Steps.Count,
                    TrusteeCount = trusteeCount
                });
                batch.Succeeded++;
            }
            return batch;
        }

        // Execute creates
        for (int i = 0; i < input.Workflows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var wf = input.Workflows[i];
            var result = await CreateSingleWorkflowAsync(wf, ct);

            batch.Results.Add(result);
            if (result.Status == WorkflowResultStatus.Success) batch.Succeeded++;
            else if (result.Status == WorkflowResultStatus.Fail) batch.Failed++;
            else batch.Skipped++;

            progress?.Report(new WorkflowProgress
            {
                CurrentIndex = i + 1,
                TotalCount = input.Workflows.Count,
                WorkflowName = wf.Name,
                Status = result.Status,
                Message = result.Message
            });
        }

        return batch;
    }

    private async Task<WorkflowOperationResult> CreateSingleWorkflowAsync(
        WorkflowInputModel input, CancellationToken ct)
    {
        string? workflowId = null;
        try
        {
            // 1. Create new blank workflow (auto-tagged)
            var model = await _apiClient.CreateNewAsync(ct);
            workflowId = model.WorkflowDefinition.WorkflowId;

            // 2. Add all additional steps first (each call re-reads the model from the server,
            //    discarding in-memory changes, so we must add all steps before configuring any)
            for (int s = 1; s < input.Steps.Count; s++)
            {
                model = await _apiClient.AddStepAsync(model, -1, ct);
            }

            // 3. Set workflow-level properties (after all re-reads)
            model.WorkflowDefinition.Name = input.Name;
            model.WorkflowDefinition.Active = input.Active;
            model.WorkflowDefinition.Shared = input.Shared;
            model.WorkflowDefinition.Memo = input.Memo;

            if (input.TimeoutDays.HasValue)
            {
                model.WorkflowDefinition.BTimeoutOn = true;
                model.WorkflowDefinition.Timeout = TimeSpan.FromDays(input.TimeoutDays.Value).ToString();
            }

            if (input.RecurringTimeoutDays.HasValue)
            {
                model.WorkflowDefinition.BRecurringTimeoutOn = true;
                model.WorkflowDefinition.RecurringTimeout = TimeSpan.FromDays(input.RecurringTimeoutDays.Value).ToString();
            }

            model.WorkflowDefinition.BTimeoutIncludeSaturday = !input.ExcludeSaturday;
            model.WorkflowDefinition.BTimeoutIncludeSunday = !input.ExcludeSunday;

            // 4. Configure all steps (after final model is stable)
            for (int s = 0; s < input.Steps.Count && s < model.WorkflowStepModels.Count; s++)
            {
                var notificationValidationError = ConfigureStep(model.WorkflowStepModels[s], input.Steps[s], workflowId);
                if (!string.IsNullOrWhiteSpace(notificationValidationError))
                {
                    await TryUntagAsync(workflowId, ct);
                    return new WorkflowOperationResult
                    {
                        WorkflowName = input.Name,
                        Status = WorkflowResultStatus.Fail,
                        Message = notificationValidationError
                    };
                }
            }

            // Enable workflow-level email notify if any step has notification trustees
            if (model.WorkflowStepModels.Any(s => s.EmailNotificationList.Count > 0 || s.AlertNotificationList.Count > 0))
            {
                model.WorkflowDefinition.BDoEmailNotify = true;
            }

            // 5. Save
            var saveResult = await _apiClient.SaveWorkflowAsync(model, ct);
            if (!saveResult.IsSuccess)
            {
                await TryUntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = saveResult.ErrorMessage ?? "Save failed."
                };
            }

            var shareResult = await _apiClient.SetWorkflowSharedAsync(workflowId, input.Shared, ct);
            if (!shareResult.IsSuccess)
            {
                await TryUntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = shareResult.ErrorMessage ?? "Failed to apply workflow share state."
                };
            }

            // Guard: fail creation if reviewer trustees were dropped during persistence.
            var trusteePersistenceError = await ValidateReviewerTrusteePersistenceAsync(input, workflowId, ct);
            if (!string.IsNullOrWhiteSpace(trusteePersistenceError))
            {
                await TryUntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = trusteePersistenceError
                };
            }

            // 6. Untag
            await _apiClient.UntagAsync(workflowId, ct);

            // 7. Post-save visibility check (same auth/context as this create call)
            var (visibleToCurrentContext, ownerDisplay, ownerUserId, shareStatus) =
                await CheckWorkflowVisibilityAsync(workflowId, input.Name, ct);

            var totalTrustees = input.Steps.Sum(s => s.Trustees.Count);
            var ownership = string.IsNullOrWhiteSpace(ownerDisplay)
                ? (string.IsNullOrWhiteSpace(ownerUserId) ? "unknown" : ownerUserId)
                : ownerDisplay;
            var sharedText = input.Shared ? "shared" : "not-shared";
            var visibilityWarning = visibleToCurrentContext
                ? string.Empty
                : " WARNING: workflow was saved but is not visible in current list context.";

            return new WorkflowOperationResult
            {
                WorkflowName = input.Name,
                Status = WorkflowResultStatus.Success,
                Message = $"Created ({input.Steps.Count} steps, {totalTrustees} trustees, owner: {ownership}, shared: {sharedText}, share-status: {shareStatus ?? "unknown"}).{visibilityWarning}",
                StepCount = input.Steps.Count,
                TrusteeCount = totalTrustees
            };
        }
        catch (Exception ex)
        {
            if (workflowId is not null)
                await TryUntagAsync(workflowId, ct);

            return new WorkflowOperationResult
            {
                WorkflowName = input.Name,
                Status = WorkflowResultStatus.Fail,
                Message = ex.Message
            };
        }
    }

    public async Task<WorkflowBatchResult> ModifyAsync(
        WorkflowModifyRequest request,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken ct = default)
    {
        var input = ReadInput(request.InputFilePath);
        var batch = new WorkflowBatchResult { Total = input.Workflows.Count, DryRun = request.DryRun };

        // Name resolution — resolve all User-type trustees before proceeding
        var resolutionResult = await ResolveTrusteesAsync(input.Workflows, ct);
        if (!resolutionResult.AllResolved)
        {
            foreach (var failure in resolutionResult.Failures)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = failure.WorkflowName,
                    Status = WorkflowResultStatus.Fail,
                    Message = failure.Message
                });
                batch.Failed++;
            }
            batch.Total = resolutionResult.Failures.Count;
            return batch;
        }

        // Get existing workflows for name → ID mapping
        var existing = await _apiClient.GetWorkflowsBasicAsync(ct);
        var nameMap = existing.Workflows.ToDictionary(
            w => w.WorkflowName,
            w => w.WorkflowId,
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < input.Workflows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var wf = input.Workflows[i];

            if (!nameMap.TryGetValue(wf.Name, out var workflowId))
            {
                var skipResult = new WorkflowOperationResult
                {
                    WorkflowName = wf.Name,
                    Status = WorkflowResultStatus.Skip,
                    Message = $"Workflow not found: '{wf.Name}'"
                };
                batch.Results.Add(skipResult);
                batch.Skipped++;
                progress?.Report(new WorkflowProgress
                {
                    CurrentIndex = i + 1, TotalCount = input.Workflows.Count,
                    WorkflowName = wf.Name, Status = WorkflowResultStatus.Skip, Message = skipResult.Message
                });
                continue;
            }

            if (request.DryRun)
            {
                var trusteeCount = wf.Steps.Sum(s => s.Trustees.Count);
                var dryResult = new WorkflowOperationResult
                {
                    WorkflowName = wf.Name,
                    Status = WorkflowResultStatus.Success,
                    Message = $"{wf.Name}: would modify ({wf.Steps.Count} steps, {trusteeCount} trustees)",
                    StepCount = wf.Steps.Count,
                    TrusteeCount = trusteeCount
                };
                batch.Results.Add(dryResult);
                batch.Succeeded++;
                progress?.Report(new WorkflowProgress
                {
                    CurrentIndex = i + 1, TotalCount = input.Workflows.Count,
                    WorkflowName = wf.Name, Status = WorkflowResultStatus.Success, Message = dryResult.Message
                });
                continue;
            }

            var result = await ModifySingleWorkflowAsync(wf, workflowId, ct);
            batch.Results.Add(result);
            if (result.Status == WorkflowResultStatus.Success) batch.Succeeded++;
            else if (result.Status == WorkflowResultStatus.Fail) batch.Failed++;
            else batch.Skipped++;

            progress?.Report(new WorkflowProgress
            {
                CurrentIndex = i + 1, TotalCount = input.Workflows.Count,
                WorkflowName = wf.Name, Status = result.Status, Message = result.Message
            });
        }

        return batch;
    }

    private async Task<WorkflowOperationResult> ModifySingleWorkflowAsync(
        WorkflowInputModel input, string workflowId, CancellationToken ct)
    {
        try
        {
            // Tag for editing
            var tagResult = await _apiClient.TagAsync(workflowId, ct);
            if (!tagResult.BEditable)
            {
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Skip,
                    Message = $"Locked by {tagResult.AlreadyTaggedByName ?? "another user"}"
                };
            }

            // Get current state
            var model = await _apiClient.GetWorkflowAsync(workflowId, ct);

            // Add new steps if input has more (each call re-reads the model,
            // so we must add all steps before configuring any)
            for (int s = model.WorkflowStepModels.Count; s < input.Steps.Count; s++)
            {
                model = await _apiClient.AddStepAsync(model, -1, ct);
            }

            // Apply workflow-level properties (after AddStepAsync re-reads)
            model.WorkflowDefinition.Active = input.Active;
            model.WorkflowDefinition.Shared = input.Shared;
            model.WorkflowDefinition.Memo = input.Memo;

            if (input.TimeoutDays.HasValue)
            {
                model.WorkflowDefinition.BTimeoutOn = true;
                model.WorkflowDefinition.Timeout = TimeSpan.FromDays(input.TimeoutDays.Value).ToString();
            }

            if (input.RecurringTimeoutDays.HasValue)
            {
                model.WorkflowDefinition.BRecurringTimeoutOn = true;
                model.WorkflowDefinition.RecurringTimeout = TimeSpan.FromDays(input.RecurringTimeoutDays.Value).ToString();
            }

            model.WorkflowDefinition.BTimeoutIncludeSaturday = !input.ExcludeSaturday;
            model.WorkflowDefinition.BTimeoutIncludeSunday = !input.ExcludeSunday;

            // Configure all steps (after final model is stable)
            for (int s = 0; s < input.Steps.Count && s < model.WorkflowStepModels.Count; s++)
            {
                var notificationValidationError = ConfigureStep(model.WorkflowStepModels[s], input.Steps[s], workflowId);
                if (!string.IsNullOrWhiteSpace(notificationValidationError))
                {
                    await _apiClient.UntagAsync(workflowId, ct);
                    return new WorkflowOperationResult
                    {
                        WorkflowName = input.Name,
                        Status = WorkflowResultStatus.Fail,
                        Message = notificationValidationError
                    };
                }
            }

            // Enable workflow-level email notify if any step has notification trustees
            if (model.WorkflowStepModels.Any(s => s.EmailNotificationList.Count > 0 || s.AlertNotificationList.Count > 0))
            {
                model.WorkflowDefinition.BDoEmailNotify = true;
            }

            // Save
            var saveResult = await _apiClient.SaveWorkflowAsync(model, ct);
            if (!saveResult.IsSuccess)
            {
                await _apiClient.UntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = saveResult.ErrorMessage ?? "Save failed."
                };
            }

            var shareResult = await _apiClient.SetWorkflowSharedAsync(workflowId, input.Shared, ct);
            await _apiClient.UntagAsync(workflowId, ct);

            if (!shareResult.IsSuccess)
            {
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = shareResult.ErrorMessage ?? "Failed to apply workflow share state."
                };
            }

            var totalTrustees = input.Steps.Sum(s => s.Trustees.Count);
            return new WorkflowOperationResult
            {
                WorkflowName = input.Name,
                Status = WorkflowResultStatus.Success,
                Message = $"{input.Name}: modified ({input.Steps.Count} steps, {totalTrustees} trustees)",
                StepCount = input.Steps.Count,
                TrusteeCount = totalTrustees
            };
        }
        catch (Exception ex)
        {
            await TryUntagAsync(workflowId, ct);
            return new WorkflowOperationResult
            {
                WorkflowName = input.Name,
                Status = WorkflowResultStatus.Fail,
                Message = ex.Message
            };
        }
    }

    public async Task<WorkflowBatchResult> DeleteAsync(
        WorkflowDeleteRequest request,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken ct = default)
    {
        var batch = new WorkflowBatchResult { DryRun = request.DryRun };

        // 1. Get full workflow list (use pre-fetched if available)
        var packet = request.PreFetchedPacket ?? await _apiClient.GetWorkflowsAsync(ct);
        var workflows = packet.Workflows;

        // 2. Apply filter — ID-based or name-glob
        List<WorkflowAdminItem> matched;
        if (request.WorkflowIds is { Count: > 0 })
        {
            var idSet = new HashSet<string>(request.WorkflowIds, StringComparer.OrdinalIgnoreCase);
            matched = workflows
                .Where(w => idSet.Contains(w.WorkflowId) && w.Delete && string.IsNullOrWhiteSpace(w.LockedByDisplayName))
                .ToList();
        }
        else
        {
            matched = ApplyFilters(workflows, request.Filter ?? "*", request.Status);
        }

        batch.Total = matched.Count;

        if (matched.Count == 0)
        {
            return batch;
        }

        // 3. Write manifest if requested
        if (request.ManifestPath is not null)
        {
            var manifest = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                user = packet.CurrentUserId,
                dryRun = request.DryRun,
                filter = request.Filter,
                workflows = matched.Select(w => new
                {
                    w.WorkflowId,
                    name = w.WorkflowName,
                    w.Active,
                    w.StepCount,
                    w.InProcessCount
                }).ToList()
            };
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(request.ManifestPath, json, ct);
        }

        // 4. Dry-run: report what would be deleted
        if (request.DryRun)
        {
            foreach (var wf in matched)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = wf.WorkflowName,
                    Status = WorkflowResultStatus.Success,
                    Message = $"Would delete ({wf.StepCount} steps, {wf.InProcessCount} in-process)"
                });
                batch.Succeeded++;
            }
            return batch;
        }

        // 5. Execute deletions in parallel with bounded concurrency
        var semaphore = new SemaphoreSlim(MaxDeleteConcurrency);
        var progressIndex = 0;

        var tasks = matched.Select(async wf =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                WorkflowOperationResult result;
                try
                {
                    var deleteResult = await _apiClient.DeleteWorkflowAsync(wf.WorkflowId, ct);
                    if (deleteResult.IsSuccess)
                    {
                        var msg = wf.InProcessCount > 0
                            ? $"Deleted ({wf.InProcessCount} docs moved to System Workflow)"
                            : "Deleted";
                        result = new WorkflowOperationResult
                        {
                            WorkflowName = wf.WorkflowName,
                            Status = WorkflowResultStatus.Success,
                            Message = msg
                        };
                    }
                    else
                    {
                        result = new WorkflowOperationResult
                        {
                            WorkflowName = wf.WorkflowName,
                            Status = WorkflowResultStatus.Fail,
                            Message = deleteResult.ErrorMessage ?? "Delete failed."
                        };
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result = new WorkflowOperationResult
                    {
                        WorkflowName = wf.WorkflowName,
                        Status = WorkflowResultStatus.Fail,
                        Message = ex.Message
                    };
                }

                lock (batch)
                {
                    batch.Results.Add(result);
                    if (result.Status == WorkflowResultStatus.Success)
                        batch.Succeeded++;
                    else
                        batch.Failed++;
                }

                var idx = Interlocked.Increment(ref progressIndex);
                progress?.Report(new WorkflowProgress
                {
                    CurrentIndex = idx,
                    TotalCount = matched.Count,
                    WorkflowName = wf.WorkflowName,
                    Status = result.Status,
                    Message = result.Message
                });
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return batch;
    }

    public async Task<WorkflowListResult> ListAsync(
        WorkflowListRequest request, CancellationToken ct = default)
    {
        var packet = await _apiClient.GetWorkflowsAsync(ct);
        var workflows = packet.Workflows;

        // Enrich list items with trustee counts for CLI reporting.
        foreach (var wf in workflows)
        {
            try
            {
                var detail = await _apiClient.GetWorkflowAsync(wf.WorkflowId, ct);
                wf.ReviewerCount = detail.WorkflowStepModels
                    .Where(s => !s.BDeleted)
                    .Sum(s => s.WorkflowTrusteeDefinitions?.Count ?? 0);

                wf.NotifyCount = detail.WorkflowStepModels
                    .Where(s => !s.BDeleted)
                    .Sum(s => s.EmailNotificationList?.Count ?? 0);

                wf.AlertCount = detail.WorkflowStepModels
                    .Where(s => !s.BDeleted)
                    .Sum(s => s.AlertNotificationList?.Count ?? 0);

                wf.TrusteeCount = wf.ReviewerCount + wf.NotifyCount + wf.AlertCount;
            }
            catch
            {
                // Best effort: keep list responsive even if a detail call fails.
                wf.TrusteeCount = 0;
                wf.ReviewerCount = 0;
                wf.NotifyCount = 0;
                wf.AlertCount = 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Filter))
        {
            var pattern = GlobToRegex(request.Filter);
            workflows = workflows
                .Where(w => Regex.IsMatch(w.WorkflowName, pattern, RegexOptions.IgnoreCase))
                .ToList();
        }

        return new WorkflowListResult
        {
            Workflows = workflows,
            TotalCount = workflows.Count,
            AppliedFilter = request.Filter,
            Packet = packet
        };
    }

    // --- helpers ---

    private WorkflowExcelInput ReadInput(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => _excelReader.Read(filePath),
            ".xml" => _xmlReader.Read(filePath),
            _ => throw new ArgumentException($"Unsupported file type: {ext}. Use .xlsx or .xml.")
        };
    }

    private static string? ConfigureStep(WorkflowStepModel stepModel, WorkflowInputStep input, string workflowId)
    {
        stepModel.WorkflowStepDefinition.Name = input.Name;
        stepModel.WorkflowStepDefinition.RequiredApprovalsCount = input.RequiredApprovalsCount;
        stepModel.WorkflowStepDefinition.AutoAdvance = input.AutoAdvance;

        var stepId = stepModel.WorkflowStepDefinition.StepId;

        // Split trustees by role
        var reviewers = input.Trustees.Where(t => t.Role == TrusteeRole.Reviewer);
        var emailNotify = input.Trustees.Where(t => t.Role == TrusteeRole.EmailNotify);
        var alertNotify = input.Trustees.Where(t => t.Role == TrusteeRole.AlertNotify);

        stepModel.WorkflowTrusteeDefinitions = reviewers.Select(t => new WorkflowTrusteeDefinition
        {
            WorkflowId = workflowId,
            StepId = stepId,
            TrusteeId = t.TrusteeId,
            Type = t.TrusteeType
        }).ToList();

        stepModel.EmailNotificationList = BuildNotificationDefinitions(emailNotify, workflowId, stepId, out var invalidEmailNotifyCount);
        stepModel.AlertNotificationList = BuildNotificationDefinitions(alertNotify, workflowId, stepId, out var invalidAlertNotifyCount);

        var allNotifyInputCount = emailNotify.Count() + alertNotify.Count();
        var validNotifyCount = stepModel.EmailNotificationList.Count + stepModel.AlertNotificationList.Count;
        var invalidNotifyCount = invalidEmailNotifyCount + invalidAlertNotifyCount;
        if (allNotifyInputCount > 0 && validNotifyCount == 0 && invalidNotifyCount > 0)
        {
            return $"Step '{input.Name}': all notify/alert recipients are invalid. " +
                   "Provide valid user/group/key/email recipients, or use Approvers.";
        }

        // Enable email notify flag if notification trustees exist
        if (stepModel.EmailNotificationList.Count > 0 || stepModel.AlertNotificationList.Count > 0)
        {
            stepModel.WorkflowStepDefinition.BDoEmailNotify = true;
        }

        return null;
    }

    private static string NormalizeNotificationTrusteeId(WorkflowInputTrustee trustee)
    {
        if (trustee.TrusteeType == WorkflowUserType.Approvers)
            return WorkflowParticipantConstants.ApproversSentinelTargetId;

        return trustee.TrusteeId?.Trim() ?? string.Empty;
    }

    private static List<WorkflowNotificationDefinition> BuildNotificationDefinitions(
        IEnumerable<WorkflowInputTrustee> trustees,
        string workflowId,
        string stepId,
        out int invalidCount)
    {
        invalidCount = 0;
        var notifications = new List<WorkflowNotificationDefinition>();

        foreach (var trustee in trustees)
        {
            var normalizedId = NormalizeNotificationTrusteeId(trustee);
            if (!IsValidNotificationTrustee(trustee.TrusteeType, normalizedId))
            {
                invalidCount++;
                continue;
            }

            notifications.Add(new WorkflowNotificationDefinition
            {
                WorkflowId = workflowId,
                StepId = stepId,
                TrusteeId = normalizedId,
                Type = trustee.TrusteeType,
                TargetId = normalizedId,
                TargetType = trustee.TrusteeType,
                Email = trustee.TrusteeType == WorkflowUserType.Email ? normalizedId : string.Empty
            });
        }

        return notifications;
    }

    private static bool IsValidNotificationTrustee(WorkflowUserType type, string trusteeId)
    {
        if (string.IsNullOrWhiteSpace(trusteeId))
            return false;

        if (type == WorkflowUserType.Approvers)
            return true;

        if (type == WorkflowUserType.Email)
            return trusteeId.Contains('@', StringComparison.Ordinal) && !trusteeId.EndsWith("@", StringComparison.Ordinal);

        return true;
    }

    private static List<WorkflowAdminItem> ApplyFilters(
        List<WorkflowAdminItem> workflows, string filter, string status)
    {
        var result = workflows.AsEnumerable();

        // Name filter (glob)
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var pattern = GlobToRegex(filter);
            result = result.Where(w => Regex.IsMatch(w.WorkflowName, pattern, RegexOptions.IgnoreCase));
        }

        // Status filter
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            result = result.Where(w => w.Active);
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            result = result.Where(w => !w.Active);

        // Exclude workflows without delete permission
        result = result.Where(w => w.Delete);

        // Exclude locked workflows
        result = result.Where(w => string.IsNullOrWhiteSpace(w.LockedByDisplayName));

        return result.ToList();
    }

    private static string GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob);
        escaped = escaped.Replace(@"\*", ".*").Replace(@"\?", ".");
        return $"^{escaped}$";
    }

    private async Task TryUntagAsync(string workflowId, CancellationToken ct)
    {
        try
        {
            await _apiClient.UntagAsync(workflowId, ct);
        }
        catch
        {
            // Best-effort untag — don't mask the original error
        }
    }

    private async Task<(bool Visible, string? OwnerDisplayName, string? OwnerUserId, string? ShareStatus)> CheckWorkflowVisibilityAsync(
        string workflowId,
        string workflowName,
        CancellationToken ct)
    {
        try
        {
            var packet = await _apiClient.GetWorkflowsBasicAsync(ct);
            var found = packet.Workflows.FirstOrDefault(w =>
                string.Equals(w.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(w.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase));

            if (found is null)
                return (false, null, null, null);

            return (true, found.OwnerDisplayName, found.OwnerUserId, found.ShareStatus);
        }
        catch
        {
            // Visibility check should never fail the create operation.
            return (true, null, null, null);
        }
    }

    private async Task<string?> ValidateReviewerTrusteePersistenceAsync(
        WorkflowInputModel input,
        string workflowId,
        CancellationToken ct)
    {
        var expectedReviewerByStep = input.Steps
            .Select(s => new
            {
                StepName = s.Name?.Trim() ?? string.Empty,
                ReviewerIds = s.Trustees
                    .Where(t => t.Role == TrusteeRole.Reviewer)
                    .Select(t => t.TrusteeId?.Trim() ?? string.Empty)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            })
            .Where(x => x.ReviewerIds.Count > 0)
            .ToList();

        if (expectedReviewerByStep.Count == 0)
            return null;

        var persisted = await _apiClient.GetWorkflowAsync(workflowId, ct);

        foreach (var expected in expectedReviewerByStep)
        {
            var persistedStep = persisted.WorkflowStepModels
                .Where(s => !s.BDeleted)
                .FirstOrDefault(s =>
                    string.Equals(s.WorkflowStepDefinition.Name?.Trim(), expected.StepName, StringComparison.OrdinalIgnoreCase));

            if (persistedStep is null)
            {
                return $"Workflow saved but step '{expected.StepName}' was not found during trustee persistence verification.";
            }

            var actualReviewerIds = (persistedStep.WorkflowTrusteeDefinitions ?? new List<WorkflowTrusteeDefinition>())
                .Select(t => t.TrusteeId?.Trim() ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = expected.ReviewerIds
                .Where(id => !actualReviewerIds.Contains(id))
                .ToList();

            if (missing.Count > 0)
            {
                return $"Workflow saved but reviewer trustees did not persist on step '{expected.StepName}'. " +
                       $"Missing: {string.Join(", ", missing)}.";
            }
        }

        return null;
    }

    private async Task<TrusteeResolutionResult> ResolveTrusteesAsync(
        List<WorkflowInputModel> workflows, CancellationToken ct)
    {
        var result = new TrusteeResolutionResult();
        var userTrustees = workflows
            .SelectMany(wf => wf.Steps.SelectMany(step => step.Trustees.Select(trustee => new
            {
                Workflow = wf,
                Step = step,
                Trustee = trustee
            })))
            .Where(x => x.Trustee.TrusteeType == WorkflowUserType.User)
            .ToList();

        // Collect all User-type trustees
        if (userTrustees.Count == 0)
        {
            result.AllResolved = true;
            return result;
        }

        List<AdeptUserEntry> users;
        try
        {
            users = await _apiClient.GetUsersAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is NotSupportedException)
        {
            var trusteesNeedingLookup = userTrustees
                .Where(x => RequiresUserResolution(x.Trustee.TrusteeId))
                .ToList();

            if (trusteesNeedingLookup.Count == 0)
            {
                result.AllResolved = true;
                return result;
            }

            foreach (var entry in trusteesNeedingLookup)
            {
                result.Failures.Add(new TrusteeResolutionFailure
                {
                    WorkflowName = entry.Workflow.Name,
                    Message = $"Trustee \"{entry.Trustee.TrusteeId}\" in step \"{entry.Step.Name}\": " +
                              "this server does not expose the user list endpoint needed for name resolution. " +
                              "Provide the exact Adept user ID/login name instead."
                });
            }

            result.AllResolved = false;
            return result;
        }

        var matcher = new UserMatcher(users);

        foreach (var entry in userTrustees)
        {
            var matchResult = matcher.Match(entry.Trustee.TrusteeId);

            switch (matchResult.Confidence)
            {
                case MatchConfidence.Exact:
                    // Already valid — no change needed
                    break;

                case MatchConfidence.Strong:
                    // Auto-resolve: replace input with the actual user ID
                    entry.Trustee.TrusteeId = matchResult.ResolvedUserId!;
                    result.Resolved.Add(matchResult);
                    break;

                case MatchConfidence.Weak:
                    result.Failures.Add(new TrusteeResolutionFailure
                    {
                        WorkflowName = entry.Workflow.Name,
                        Message = $"Trustee \"{matchResult.InputValue}\" in step \"{entry.Step.Name}\": " +
                                  $"weak match — did you mean \"{matchResult.ResolvedUserId}\" " +
                                  $"({matchResult.MatchedDisplayName})?"
                    });
                    break;

                case MatchConfidence.None:
                    result.Failures.Add(new TrusteeResolutionFailure
                    {
                        WorkflowName = entry.Workflow.Name,
                        Message = $"Trustee \"{matchResult.InputValue}\" in step \"{entry.Step.Name}\": " +
                                  "no match found. Provide the Adept user ID."
                    });
                    break;
            }
        }

        result.AllResolved = result.Failures.Count == 0;
        return result;
    }

    private static bool RequiresUserResolution(string trusteeId)
    {
        if (string.IsNullOrWhiteSpace(trusteeId))
            return true;

        var trimmed = trusteeId.Trim();
        return trimmed.Contains(' ') || trimmed.Contains(',');
    }

    private class TrusteeResolutionResult
    {
        public bool AllResolved { get; set; }
        public List<UserMatchResult> Resolved { get; } = new();
        public List<TrusteeResolutionFailure> Failures { get; } = new();
    }

    private class TrusteeResolutionFailure
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
