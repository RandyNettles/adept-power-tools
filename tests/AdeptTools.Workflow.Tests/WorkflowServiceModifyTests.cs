using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowServiceModifyTests
{
    private readonly WorkflowExcelReader _excelReader = new();
    private readonly WorkflowXmlReader _xmlReader = new();
    private readonly WorkflowValidator _validator = new();

    private WorkflowService CreateService(IWorkflowApiClient client) =>
        new(client, _excelReader, _xmlReader, _validator);

    [Fact]
    public async Task ModifyAsync_RejectsInvalidReviewerTrusteeType()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewers@company.com"" Type=""Email"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Empty(savedModels);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("reviewer trustee type", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_PreflightValidationError_BlocksBeforeSave()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Empty(savedModels);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("Step must have at least one trustee", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_PreflightValidationWarning_DoesNotBlockSave()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"" AllowEmptyTrustees=""true"">
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_DeduplicatesStepNotificationRecipients()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""notifyUser"" Type=""User"" Role=""Notify"" />
                        <Trustee Id=""notifyUser"" Type=""User"" Role=""Notify"" />
                        <Trustee Id=""alerts@company.com"" Type=""Email"" Role=""Alert"" />
                        <Trustee Id=""alerts@company.com"" Type=""Email"" Role=""Alert"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var step = savedModels[0].WorkflowStepModels[0];
            Assert.Single(step.EmailNotificationList);
            Assert.Single(step.AlertNotificationList);
            Assert.Equal(WorkflowNotificationAction.Approve, step.EmailNotificationList[0].Action);
            Assert.Equal(WorkflowNotificationAction.Timeout, step.AlertNotificationList[0].Action);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_WhenNotificationsRemoved_ClearsBDoEmailNotify()
    {
        // Per deep-dive 9.12.1: a modify that removes all notification recipients must
        // set BDoEmailNotify=false (full-replace authoring semantics — desired state is
        // declared completely in the input file).
        var savedModels = new List<WorkflowEditModel>();

        // Server has a step that previously had notifications (BDoEmailNotify=true).
        var existingStep = new WorkflowStepModel
        {
            WorkflowStepDefinition = new WorkflowStepDefinition
            {
                StepId = "step-1",
                Name = "Review",
                BDoEmailNotify = true   // stale state from previous config
            },
            EmailNotificationList = new List<WorkflowNotificationDefinition>
            {
                new() { TrusteeId = "oldUser", Action = WorkflowNotificationAction.Approve }
            },
            AlertNotificationList = new List<WorkflowNotificationDefinition>()
        };

        var client = new CapturingModifyClient(savedModels, existingStep: existingStep);
        var service = CreateService(client);

        // New input has NO notification trustees on that step.
        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var step = saved.WorkflowStepModels[0];

            // No notifications in input → flag must be cleared on step and workflow.
            Assert.False(step.WorkflowStepDefinition.BDoEmailNotify);
            Assert.False(saved.WorkflowDefinition.BDoEmailNotify);

            Assert.Empty(step.EmailNotificationList);
            Assert.Empty(step.AlertNotificationList);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_WhenExcludeWeekendOmitted_RetainsExistingIncludeFlags()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(
            savedModels,
            workflowDefinitionOverride: new WorkflowDefinition
            {
                WorkflowId = "wf-001",
                Name = "Design Review",
                BTimeoutIncludeSaturday = false,
                BTimeoutIncludeSunday = true
            });
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0].WorkflowDefinition;
            Assert.False(saved.BTimeoutIncludeSaturday);
            Assert.True(saved.BTimeoutIncludeSunday);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_WhenExcludeWeekendSpecified_OverwritesIncludeFlags()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(
            savedModels,
            workflowDefinitionOverride: new WorkflowDefinition
            {
                WorkflowId = "wf-001",
                Name = "Design Review",
                BTimeoutIncludeSaturday = true,
                BTimeoutIncludeSunday = true
            });
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"" ExcludeSaturday=""true"" ExcludeSunday=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0].WorkflowDefinition;
            Assert.False(saved.BTimeoutIncludeSaturday);
            Assert.False(saved.BTimeoutIncludeSunday);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_RejectsDuplicateStepNamesInWorkflow()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingModifyClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
                <Step Name="" review "">
                    <Trustees>
                        <Trustee Id=""notifyUser"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Empty(savedModels);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("duplicate step name", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_ReorderedExistingSteps_MatchesByNameNotIndex()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new ReorderedExistingStepsClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""draftReviewer"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
                <Step Name=""Approve"">
                    <Trustees>
                        <Trustee Id=""approveReviewer"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var draftStep = saved.WorkflowStepModels.Single(s => s.WorkflowStepDefinition.StepId == "step-draft");
            var approveStep = saved.WorkflowStepModels.Single(s => s.WorkflowStepDefinition.StepId == "step-approve");

            Assert.Equal("Draft", draftStep.WorkflowStepDefinition.Name);
            Assert.Equal("draftReviewer", draftStep.WorkflowTrusteeDefinitions.Single().TrusteeId);
            Assert.Equal("Approve", approveStep.WorkflowStepDefinition.Name);
            Assert.Equal("approveReviewer", approveStep.WorkflowTrusteeDefinitions.Single().TrusteeId);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_DeletedExistingStep_DoesNotShiftActiveStepMapping()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new DeletedMiddleStepClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""draftReviewer"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
                <Step Name=""Approve"">
                    <Trustees>
                        <Trustee Id=""approveReviewer"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var draftStep = saved.WorkflowStepModels.Single(s => s.WorkflowStepDefinition.StepId == "step-draft");
            var deletedStep = saved.WorkflowStepModels.Single(s => s.WorkflowStepDefinition.StepId == "step-old");
            var approveStep = saved.WorkflowStepModels.Single(s => s.WorkflowStepDefinition.StepId == "step-approve");

            Assert.Equal("draftReviewer", draftStep.WorkflowTrusteeDefinitions.Single().TrusteeId);
            Assert.Empty(deletedStep.WorkflowTrusteeDefinitions);
            Assert.True(deletedStep.BDeleted);
            Assert.Equal("approveReviewer", approveStep.WorkflowTrusteeDefinitions.Single().TrusteeId);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_UntagHappensAfterPostSaveTrusteeVerification()
    {
        var client = new TrackingModifyClient();
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Equal(new[] { "tag", "get", "save", "share", "verify", "untag" }, client.CallSequence);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task ModifyAsync_TrusteeVerificationFailureStillUntags()
    {
        var client = new TrackingModifyClient(dropReviewersOnReadback: true);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Design Review"" Active=""true"">
            <Steps>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Equal(new[] { "tag", "get", "save", "share", "verify", "untag" }, client.CallSequence);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("did not persist", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static string CreateTempXmlRaw(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_modify_test_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private class CapturingModifyClient : MockWorkflowApiClient
    {
        protected readonly List<WorkflowEditModel> SavedModels;
        private readonly WorkflowStepModel? _overrideStep;
        private readonly WorkflowDefinition? _overrideWorkflowDefinition;

        public CapturingModifyClient(
            List<WorkflowEditModel> saved,
            WorkflowStepModel? existingStep = null,
            WorkflowDefinition? workflowDefinitionOverride = null)
        {
            SavedModels = saved;
            _overrideStep = existingStep;
            _overrideWorkflowDefinition = workflowDefinitionOverride;
        }

        public override Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
        {
            return Task.FromResult(BuildInitialWorkflow(workflowId));
        }

        public override Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            var existing = SavedModels.LastOrDefault(m =>
                string.Equals(m.WorkflowDefinition.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
                return Task.FromResult(existing);

            return Task.FromResult(BuildInitialWorkflow(workflowId));
        }

        protected virtual WorkflowEditModel BuildInitialWorkflow(string workflowId)
        {
            var steps = _overrideStep is not null
                ? new List<WorkflowStepModel> { _overrideStep }
                : new List<WorkflowStepModel>
                {
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-001",
                            Order = 1,
                            Name = "Review"
                        }
                    }
                };

            return new WorkflowEditModel
            {
                BEditable = true,
                WorkflowDefinition = _overrideWorkflowDefinition ?? new WorkflowDefinition
                {
                    WorkflowId = workflowId,
                    Name = "Design Review"
                },
                WorkflowStepModels = steps
            };
        }

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            SavedModels.Add(model);
            return Task.FromResult(ApiResult.Success("Captured."));
        }
    }

    private sealed class ReorderedExistingStepsClient : CapturingModifyClient
    {
        public ReorderedExistingStepsClient(List<WorkflowEditModel> saved) : base(saved)
        {
        }

        protected override WorkflowEditModel BuildInitialWorkflow(string workflowId)
        {
            return new WorkflowEditModel
            {
                BEditable = true,
                WorkflowDefinition = new WorkflowDefinition
                {
                    WorkflowId = workflowId,
                    Name = "Design Review"
                },
                WorkflowStepModels = new List<WorkflowStepModel>
                {
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-approve",
                            Order = 2,
                            Name = "Approve"
                        }
                    },
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-draft",
                            Order = 1,
                            Name = "Draft"
                        }
                    }
                }
            };
        }
    }

    private sealed class DeletedMiddleStepClient : CapturingModifyClient
    {
        public DeletedMiddleStepClient(List<WorkflowEditModel> saved) : base(saved)
        {
        }

        protected override WorkflowEditModel BuildInitialWorkflow(string workflowId)
        {
            return new WorkflowEditModel
            {
                BEditable = true,
                WorkflowDefinition = new WorkflowDefinition
                {
                    WorkflowId = workflowId,
                    Name = "Design Review"
                },
                WorkflowStepModels = new List<WorkflowStepModel>
                {
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-draft",
                            Order = 1,
                            Name = "Draft"
                        }
                    },
                    new()
                    {
                        BDeleted = true,
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-old",
                            Order = 2,
                            Name = "Obsolete"
                        }
                    },
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-approve",
                            Order = 3,
                            Name = "Approve"
                        }
                    }
                }
            };
        }
    }

    private sealed class TrackingModifyClient : MockWorkflowApiClient
    {
        private WorkflowEditModel? _savedModel;
        private readonly bool _dropReviewersOnReadback;

        public TrackingModifyClient(bool dropReviewersOnReadback = false)
        {
            _dropReviewersOnReadback = dropReviewersOnReadback;
        }

        public List<string> CallSequence { get; } = new();

        public override Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
        {
            CallSequence.Add("tag");
            return Task.FromResult(new WorkflowEditModel
            {
                BEditable = true,
                WorkflowDefinition = new WorkflowDefinition
                {
                    WorkflowId = workflowId,
                    Name = "Design Review"
                },
                WorkflowStepModels = new List<WorkflowStepModel>
                {
                    new()
                    {
                        WorkflowStepDefinition = new WorkflowStepDefinition
                        {
                            WorkflowId = workflowId,
                            StepId = "step-001",
                            Order = 1,
                            Name = "Review"
                        }
                    }
                }
            });
        }

        public override Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            if (_savedModel is null)
            {
                CallSequence.Add("get");
                return Task.FromResult(new WorkflowEditModel
                {
                    BEditable = true,
                    WorkflowDefinition = new WorkflowDefinition
                    {
                        WorkflowId = workflowId,
                        Name = "Design Review"
                    },
                    WorkflowStepModels = new List<WorkflowStepModel>
                    {
                        new()
                        {
                            WorkflowStepDefinition = new WorkflowStepDefinition
                            {
                                WorkflowId = workflowId,
                                StepId = "step-001",
                                Order = 1,
                                Name = "Review"
                            }
                        }
                    }
                });
            }

            CallSequence.Add("verify");
            if (!_dropReviewersOnReadback)
            {
                return Task.FromResult(_savedModel);
            }

            return Task.FromResult(new WorkflowEditModel
            {
                BEditable = _savedModel.BEditable,
                WorkflowDefinition = _savedModel.WorkflowDefinition,
                WorkflowStepModels = _savedModel.WorkflowStepModels.Select(step => new WorkflowStepModel
                {
                    BDeleted = step.BDeleted,
                    WorkflowStepDefinition = step.WorkflowStepDefinition,
                    WorkflowTrusteeDefinitions = new List<WorkflowTrusteeDefinition>(),
                    EmailNotificationList = step.EmailNotificationList,
                    AlertNotificationList = step.AlertNotificationList
                }).ToList()
            });
        }

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            CallSequence.Add("save");
            _savedModel = model;
            return Task.FromResult(ApiResult.Success());
        }

        public override Task<ApiResult> SetWorkflowSharedAsync(string workflowId, bool shared, CancellationToken ct = default)
        {
            CallSequence.Add("share");
            return Task.FromResult(ApiResult.Success());
        }

        public override Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
        {
            CallSequence.Add("untag");
            return Task.FromResult(ApiResult.Success());
        }
    }
}