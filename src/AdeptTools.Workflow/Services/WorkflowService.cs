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

        var groupValidation = await ValidateGroupTrusteesAsync(input.Workflows, ct);
        if (!groupValidation.IsValid)
        {
            foreach (var failure in groupValidation.Failures)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = failure.WorkflowName,
                    Status = WorkflowResultStatus.Fail,
                    Message = failure.Message
                });
                batch.Failed++;
            }

            batch.Total = groupValidation.Failures.Count;
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
            var duplicateStepNameError = ValidateDistinctStepNames(input);
            if (!string.IsNullOrWhiteSpace(duplicateStepNameError))
            {
                await TryUntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = duplicateStepNameError
                };
            }

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
                    Message = FormatApiFailure("Save failed.", saveResult)
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
                    Message = FormatApiFailure("Failed to apply workflow share state.", shareResult)
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
                Message = $"{ex.GetType().Name}: {ex.Message} | {ex.StackTrace}"
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

        var groupValidation = await ValidateGroupTrusteesAsync(input.Workflows, ct);
        if (!groupValidation.IsValid)
        {
            foreach (var failure in groupValidation.Failures)
            {
                batch.Results.Add(new WorkflowOperationResult
                {
                    WorkflowName = failure.WorkflowName,
                    Status = WorkflowResultStatus.Fail,
                    Message = failure.Message
                });
                batch.Failed++;
            }

            batch.Total = groupValidation.Failures.Count;
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
            var duplicateStepNameError = ValidateDistinctStepNames(input);
            if (!string.IsNullOrWhiteSpace(duplicateStepNameError))
            {
                await _apiClient.UntagAsync(workflowId, ct);
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = duplicateStepNameError
                };
            }

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
                    Message = FormatApiFailure("Save failed.", saveResult)
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
                    Message = FormatApiFailure("Failed to apply workflow share state.", shareResult)
                };
            }

            var trusteePersistenceError = await ValidateReviewerTrusteePersistenceAsync(input, workflowId, ct);
            if (!string.IsNullOrWhiteSpace(trusteePersistenceError))
            {
                return new WorkflowOperationResult
                {
                    WorkflowName = input.Name,
                    Status = WorkflowResultStatus.Fail,
                    Message = trusteePersistenceError
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
                Message = $"{ex.GetType().Name}: {ex.Message} | {ex.StackTrace}"
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
                            Message = FormatApiFailure("Delete failed.", deleteResult)
                        };
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result = new WorkflowOperationResult
                    {
                        WorkflowName = wf.WorkflowName,
                        Status = WorkflowResultStatus.Fail,
                        Message = $"{ex.GetType().Name}: {ex.Message} | {ex.StackTrace}"
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
        stepModel.WorkflowStepDefinition.Name = input.Name?.Trim() ?? string.Empty;
        stepModel.WorkflowStepDefinition.RequiredApprovalsCount = input.RequiredApprovalsCount;
        stepModel.WorkflowStepDefinition.AutoAdvance = input.AutoAdvance;

        var stepId = stepModel.WorkflowStepDefinition.StepId;

        // Split trustees by role
        var reviewers = input.Trustees.Where(t => t.Role == TrusteeRole.Reviewer).ToList();
        var emailNotify = input.Trustees.Where(t => t.Role == TrusteeRole.EmailNotify).ToList();
        var alertNotify = input.Trustees.Where(t => t.Role == TrusteeRole.AlertNotify).ToList();

        var invalidReviewer = reviewers.FirstOrDefault(t => !IsValidReviewerTrusteeType(t.TrusteeType));
        if (invalidReviewer is not null)
        {
            return $"Step '{stepModel.WorkflowStepDefinition.Name}': reviewer trustee type '{invalidReviewer.TrusteeType}' is invalid. " +
                   "Reviewer trustees must be User, Group, or Key.";
        }

        stepModel.WorkflowTrusteeDefinitions = reviewers.Select(t => new WorkflowTrusteeDefinition
        {
            WorkflowId = workflowId,
            StepId = stepId,
            TrusteeId = t.TrusteeId,
            Type = t.TrusteeType
        }).ToList();

        stepModel.EmailNotificationList = BuildNotificationDefinitions(
            emailNotify,
            workflowId,
            stepId,
            WorkflowNotificationAction.Approve,
            out var invalidEmailNotifyCount);

        stepModel.AlertNotificationList = BuildNotificationDefinitions(
            alertNotify,
            workflowId,
            stepId,
            WorkflowNotificationAction.Timeout,
            out var invalidAlertNotifyCount);

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

    private static bool IsValidReviewerTrusteeType(WorkflowUserType type)
    {
        return type == WorkflowUserType.User
               || type == WorkflowUserType.Group
               || type == WorkflowUserType.Key;
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
        WorkflowNotificationAction action,
        out int invalidCount)
    {
        invalidCount = 0;
        var notifications = new List<WorkflowNotificationDefinition>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var trustee in trustees)
        {
            var normalizedId = NormalizeNotificationTrusteeId(trustee);
            if (!IsValidNotificationTrustee(trustee.TrusteeType, normalizedId))
            {
                invalidCount++;
                continue;
            }

            var recipientKey = trustee.TrusteeType == WorkflowUserType.Email
                ? $"E:{normalizedId.Trim()}"
                : $"{(int)trustee.TrusteeType}:{normalizedId.Trim()}";

            if (!seenRecipients.Add(recipientKey))
                continue;

            notifications.Add(new WorkflowNotificationDefinition
            {
                WorkflowId = workflowId,
                StepId = stepId,
                WorkflowObjectId = stepId,
                TrusteeId = normalizedId,
                Type = trustee.TrusteeType,
                Action = action,
                TargetId = normalizedId,
                TargetType = trustee.TrusteeType,
                Email = trustee.TrusteeType == WorkflowUserType.Email ? normalizedId : string.Empty
            });
        }

        return notifications;
    }

    private static string? ValidateDistinctStepNames(WorkflowInputModel input)
    {
        var duplicate = input.Steps
            .Select(s => s.Name?.Trim() ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is null)
            return null;

        return $"Workflow '{input.Name}': duplicate step name '{duplicate.Key}' is not allowed.";
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
        var expectedByStep = input.Steps
            .Select(s => new
            {
                StepName = s.Name?.Trim() ?? string.Empty,
                Reviewers = s.Trustees
                    .Where(t => t.Role == TrusteeRole.Reviewer)
                    .Select(t => (Id: t.TrusteeId?.Trim() ?? string.Empty, Type: t.TrusteeType))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                    .ToList()
            })
            .ToList();

        if (expectedByStep.All(x => x.Reviewers.Count == 0))
            return null;

        var persisted = await _apiClient.GetWorkflowAsync(workflowId, ct);

        foreach (var expected in expectedByStep)
        {
            var persistedStep = persisted.WorkflowStepModels
                .Where(s => !s.BDeleted)
                .FirstOrDefault(s =>
                    string.Equals(s.WorkflowStepDefinition.Name?.Trim(), expected.StepName, StringComparison.OrdinalIgnoreCase));

            if (persistedStep is null)
            {
                return $"Workflow saved but step '{expected.StepName}' was not found during trustee persistence verification.";
            }

            var actualReviewers = (persistedStep.WorkflowTrusteeDefinitions ?? new List<WorkflowTrusteeDefinition>())
                .Select(t => (Id: t.TrusteeId?.Trim() ?? string.Empty, Type: t.Type))
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .ToList();

            var missingReviewers = await FindMissingTrusteesAsync(expected.Reviewers, actualReviewers, ct, requireTypeMatch: true);

            if (missingReviewers.Count > 0)
            {
                var actualReviewerText = actualReviewers.Count == 0
                    ? "none"
                    : string.Join(", ", actualReviewers.Select(x => $"{x.Id}({(char)x.Type})"));

                return $"Workflow saved but reviewer trustees did not persist on step '{expected.StepName}'. " +
                       $"Missing: [{string.Join(", ", missingReviewers)}]. " +
                       $"Persisted reviewers: [{actualReviewerText}].";
            }
        }

        return null;
    }

    private async Task<List<string>> FindMissingTrusteesAsync(
        List<(string Id, WorkflowUserType Type)> expected,
        List<(string Id, WorkflowUserType Type)> actual,
        CancellationToken ct,
        bool requireTypeMatch)
    {
        if (expected.Count == 0)
            return new List<string>();

        var userAliasCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var userDirectoryAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var users = await _apiClient.GetUsersAsync(ct);
            foreach (var user in users)
            {
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(user.UserId))
                    aliases.Add(user.UserId.Trim().ToUpperInvariant());

                if (!string.IsNullOrWhiteSpace(user.NotificationTargetId))
                    aliases.Add(user.NotificationTargetId.Trim().ToUpperInvariant());

                if (aliases.Count == 0)
                    continue;

                if (!string.IsNullOrWhiteSpace(user.UserId))
                    userDirectoryAliases[user.UserId.Trim()] = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(user.NotificationTargetId))
                    userDirectoryAliases[user.NotificationTargetId.Trim()] = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is NotSupportedException)
        {
            // Best effort only; fallback lookup path below handles partial environments.
        }

        async Task<HashSet<string>> ResolveAliasesAsync((string Id, WorkflowUserType Type) item)
        {
            var key = item.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (item.Type != WorkflowUserType.User)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    key.ToUpperInvariant()
                };
            }

            if (userAliasCache.TryGetValue(key, out var cached))
                return cached;

            if (userDirectoryAliases.TryGetValue(key, out var fromDirectory))
            {
                userAliasCache[key] = fromDirectory;
                return fromDirectory;
            }

            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                key.ToUpperInvariant()
            };

            try
            {
                var user = await _apiClient.GetUserByIdAsync(key, ct);
                if (user is not null)
                {
                    if (!string.IsNullOrWhiteSpace(user.UserId))
                        aliases.Add(user.UserId.Trim().ToUpperInvariant());

                    if (!string.IsNullOrWhiteSpace(user.NotificationTargetId))
                        aliases.Add(user.NotificationTargetId.Trim().ToUpperInvariant());
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is NotSupportedException)
            {
                // Best effort only; if lookup is unavailable, compare raw IDs.
            }

            userAliasCache[key] = aliases;
            return aliases;
        }

        var actualAliasSets = new List<(WorkflowUserType Type, HashSet<string> Aliases)>();
        foreach (var item in actual)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;

            actualAliasSets.Add((item.Type, await ResolveAliasesAsync(item)));
        }

        var missing = new List<string>();
        foreach (var item in expected)
        {
            var expectedAliases = await ResolveAliasesAsync(item);
            var found = actualAliasSets.Any(a =>
                (!requireTypeMatch || a.Type == item.Type) &&
                a.Aliases.Overlaps(expectedAliases));

            if (!found)
                missing.Add($"{item.Id}({(char)item.Type})");
        }

        return missing;
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
        var usersById = users
            .Where(u => !string.IsNullOrWhiteSpace(u.UserId))
            .GroupBy(u => u.UserId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var canonicalUserTargetCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static string ToPersistedUserTrusteeId(AdeptUserEntry? user, string fallback)
        {
            if (user is null)
                return fallback;

            if (!string.IsNullOrWhiteSpace(user.NotificationTargetId))
                return user.NotificationTargetId.Trim();

            if (!string.IsNullOrWhiteSpace(user.UserId))
                return user.UserId.Trim();

            return fallback;
        }

        static bool LooksLikeGuidValue(string value)
        {
            return Guid.TryParse(value, out _);
        }

        async Task<string> ResolveCanonicalPersistedIdAsync(string resolvedUserId, string fallback)
        {
            if (string.IsNullOrWhiteSpace(resolvedUserId))
                return fallback;

            if (canonicalUserTargetCache.TryGetValue(resolvedUserId, out var cached))
                return cached;

            usersById.TryGetValue(resolvedUserId, out var matchedUser);
            var fromDirectory = ToPersistedUserTrusteeId(matchedUser, resolvedUserId);

            // If we already have a GUID target from directory enumeration, use it directly.
            if (LooksLikeGuidValue(fromDirectory))
            {
                canonicalUserTargetCache[resolvedUserId] = fromDirectory;
                return fromDirectory;
            }

            // Some servers expose canonical notification target IDs only on per-user lookup.
            try
            {
                var verified = await _apiClient.GetUserByIdAsync(resolvedUserId, ct);
                var verifiedId = ToPersistedUserTrusteeId(verified, fromDirectory);
                canonicalUserTargetCache[resolvedUserId] = verifiedId;
                return verifiedId;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is NotSupportedException)
            {
                canonicalUserTargetCache[resolvedUserId] = fromDirectory;
                return fromDirectory;
            }
        }

        foreach (var entry in userTrustees)
        {
            var matchResult = matcher.Match(entry.Trustee.TrusteeId);

            switch (matchResult.Confidence)
            {
                case MatchConfidence.Exact:
                    if (!string.IsNullOrWhiteSpace(matchResult.ResolvedUserId))
                    {
                        var canonicalId = await ResolveCanonicalPersistedIdAsync(
                            matchResult.ResolvedUserId,
                            matchResult.ResolvedUserId);

                        if (!string.Equals(entry.Trustee.TrusteeId, canonicalId, StringComparison.Ordinal))
                            entry.Trustee.TrusteeId = canonicalId;
                    }
                    break;

                case MatchConfidence.Strong:
                    // Persist canonical target IDs for HTTP consistency (GUIDs when provided).
                    entry.Trustee.TrusteeId = await ResolveCanonicalPersistedIdAsync(
                        matchResult.ResolvedUserId!,
                        matchResult.ResolvedUserId!);
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
                    if (LooksLikeLoginId(entry.Trustee.TrusteeId))
                    {
                        // Some environments return incomplete user lists from lookup endpoints.
                        // Verify directly by user ID if possible, then persist canonical target ID.
                        var verified = await _apiClient.GetUserByIdAsync(entry.Trustee.TrusteeId, ct);
                        if (verified is not null && !string.IsNullOrWhiteSpace(verified.UserId))
                        {
                            entry.Trustee.TrusteeId = ToPersistedUserTrusteeId(verified, verified.UserId);

                            break;
                        }

                        // If direct verification is unavailable or blocked, allow token-like IDs
                        // to continue and rely on save-time persistence guardrails.
                        break;
                    }

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

    private static bool LooksLikeLoginId(string trusteeId)
    {
        if (string.IsNullOrWhiteSpace(trusteeId))
            return false;

        var trimmed = trusteeId.Trim();
        if (trimmed.Contains(' ') || trimmed.Contains(','))
            return false;

        return trimmed.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-');
    }

    private async Task<GroupTrusteeValidationResult> ValidateGroupTrusteesAsync(
        List<WorkflowInputModel> workflows,
        CancellationToken ct)
    {
        var result = new GroupTrusteeValidationResult();

        var groupTrustees = workflows
            .SelectMany(wf => wf.Steps.SelectMany(step => step.Trustees.Select(trustee => new
            {
                Workflow = wf,
                Step = step,
                Trustee = trustee
            })))
            .Where(x => x.Trustee.TrusteeType == WorkflowUserType.Group)
            .ToList();

        if (groupTrustees.Count == 0)
        {
            result.IsValid = true;
            return result;
        }

        List<AdeptGroupEntry> groups;
        try
        {
            groups = await _apiClient.GetGroupsAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is NotSupportedException)
        {
            foreach (var entry in groupTrustees)
            {
                result.Failures.Add(new TrusteeResolutionFailure
                {
                    WorkflowName = entry.Workflow.Name,
                    Message = $"Trustee \"{entry.Trustee.TrusteeId}\" in step \"{entry.Step.Name}\": " +
                              "this server does not expose the group list endpoint needed for group validation. " +
                              "Provide a verified Adept group ID."
                });
            }

            result.IsValid = false;
            return result;
        }

        var byId = new Dictionary<string, AdeptGroupEntry>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, AdeptGroupEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            if (!string.IsNullOrWhiteSpace(g.GroupId))
                byId[g.GroupId.Trim()] = g;

            if (!string.IsNullOrWhiteSpace(g.Name))
                byName[g.Name.Trim()] = g;
        }

        foreach (var entry in groupTrustees)
        {
            var id = entry.Trustee.TrusteeId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Failures.Add(new TrusteeResolutionFailure
                {
                    WorkflowName = entry.Workflow.Name,
                    Message = $"Trustee group ID is blank in step \"{entry.Step.Name}\"."
                });
                continue;
            }

            if (byId.TryGetValue(id, out var byIdGroup))
            {
                entry.Trustee.TrusteeId = byIdGroup.GroupId;
                continue;
            }

            if (byName.TryGetValue(id, out var byNameGroup))
            {
                entry.Trustee.TrusteeId = byNameGroup.GroupId;
                continue;
            }

            if (TryFindSingleCaseInsensitiveNameMatch(groups, id, out var fuzzyByName))
            {
                entry.Trustee.TrusteeId = fuzzyByName!.GroupId;
                continue;
            }

            if (TryFindSingleCaseInsensitiveIdMatch(groups, id, out var fuzzyById))
            {
                entry.Trustee.TrusteeId = fuzzyById!.GroupId;
                continue;
            }

            if (TryFindSingleNormalizedNameMatch(groups, id, out var normalizedByName))
            {
                entry.Trustee.TrusteeId = normalizedByName!.GroupId;
                continue;
            }

            if (TryFindSingleNormalizedIdMatch(groups, id, out var normalizedById))
            {
                entry.Trustee.TrusteeId = normalizedById!.GroupId;
                continue;
            }

            var hint = FindClosestGroupNameHint(groups, id);
            var hintText = string.IsNullOrWhiteSpace(hint)
                ? string.Empty
                : $" Did you mean '{hint}'?";

            if (!byId.ContainsKey(id) && !byName.ContainsKey(id))
            {
                result.Failures.Add(new TrusteeResolutionFailure
                {
                    WorkflowName = entry.Workflow.Name,
                    Message = $"Trustee \"{id}\" in step \"{entry.Step.Name}\": invalid group ID. " +
                              "Use an existing Adept group ID or group name from the Group API." + hintText
                });
            }
        }

        result.IsValid = result.Failures.Count == 0;
        return result;
    }

    private static bool TryFindSingleCaseInsensitiveNameMatch(
        List<AdeptGroupEntry> groups,
        string candidate,
        out AdeptGroupEntry? match)
    {
        var matches = groups
            .Where(g => string.Equals(g.Name?.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
            .ToList();

        match = matches.Count == 1 ? matches[0] : null;
        return match is not null;
    }

    private static bool TryFindSingleCaseInsensitiveIdMatch(
        List<AdeptGroupEntry> groups,
        string candidate,
        out AdeptGroupEntry? match)
    {
        var matches = groups
            .Where(g => string.Equals(g.GroupId?.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
            .ToList();

        match = matches.Count == 1 ? matches[0] : null;
        return match is not null;
    }

    private static bool TryFindSingleNormalizedNameMatch(
        List<AdeptGroupEntry> groups,
        string candidate,
        out AdeptGroupEntry? match)
    {
        var key = NormalizeToken(candidate);
        if (string.IsNullOrWhiteSpace(key))
        {
            match = null;
            return false;
        }

        var matches = groups
            .Where(g => string.Equals(NormalizeToken(g.Name), key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        match = matches.Count == 1 ? matches[0] : null;
        return match is not null;
    }

    private static bool TryFindSingleNormalizedIdMatch(
        List<AdeptGroupEntry> groups,
        string candidate,
        out AdeptGroupEntry? match)
    {
        var key = NormalizeToken(candidate);
        if (string.IsNullOrWhiteSpace(key))
        {
            match = null;
            return false;
        }

        var matches = groups
            .Where(g => string.Equals(NormalizeToken(g.GroupId), key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        match = matches.Count == 1 ? matches[0] : null;
        return match is not null;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static string? FindClosestGroupNameHint(List<AdeptGroupEntry> groups, string input)
    {
        var normalized = NormalizeToken(input);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return groups
            .Select(g => g.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => LevenshteinDistance(NormalizeToken(n), normalized))
            .FirstOrDefault();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    private static string FormatApiFailure(string fallbackMessage, AdeptTools.Core.Models.ApiResult? result)
    {
        if (result is null)
            return fallbackMessage;

        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            details.Add($"ErrorMessage: {result.ErrorMessage}");

        if (!string.IsNullOrWhiteSpace(result.ResultMsg))
            details.Add($"ResultMsg: {result.ResultMsg}");

        details.Add($"StatusCode: {result.StatusCode}");

        return details.Count == 0
            ? fallbackMessage
            : $"{fallbackMessage} {string.Join(" | ", details)}";
    }

    private class GroupTrusteeValidationResult
    {
        public bool IsValid { get; set; }
        public List<TrusteeResolutionFailure> Failures { get; } = new();
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
