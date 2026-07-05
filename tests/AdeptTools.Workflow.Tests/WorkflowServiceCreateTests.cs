using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowServiceCreateTests
{
    private readonly MockWorkflowApiClient _mockClient = new();
    private readonly WorkflowExcelReader _excelReader = new();
    private readonly WorkflowXmlReader _xmlReader = new();
    private readonly WorkflowValidator _validator = new();

    private WorkflowService CreateService(IWorkflowApiClient? client = null) =>
        new(client ?? _mockClient, _excelReader, _xmlReader, _validator);

    [Fact]
    public async Task CreateAsync_DryRun_ReportsWhatWouldBeCreated()
    {
        var service = CreateService();
        var xmlPath = CreateTempXml(new[]
        {
            CreateSampleInput("Workflow A", new[] { "Step 1", "Step 2" }),
            CreateSampleInput("Workflow B", new[] { "Review" })
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = true });

            Assert.True(result.DryRun);
            Assert.Equal(2, result.Total);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(0, result.Failed);
            Assert.All(result.Results, r =>
            {
                Assert.Equal(WorkflowResultStatus.Success, r.Status);
                Assert.Contains("Would create", r.Message);
            });
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_TwoWorkflows_BothSucceed()
    {
        var service = CreateService();
        var xmlPath = CreateTempXml(new[]
        {
            CreateSampleInput("Test WF 1", new[] { "Step A", "Step B" }),
            CreateSampleInput("Test WF 2", new[] { "Review" })
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(2, result.Total);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(0, result.Failed);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ValidationFails_ReturnsErrors()
    {
        var service = CreateService();
        var xmlPath = CreateTempXml(new[]
        {
            CreateSampleInput("", new[] { "Step 1" }) // Empty name = validation error
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.True(result.Failed > 0);
            Assert.Contains(result.Results, r => r.Status == WorkflowResultStatus.Fail);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ReportsProgress()
    {
        var service = CreateService();
        var xmlPath = CreateTempXml(new[]
        {
            CreateSampleInput("WF Progress", new[] { "Step 1" })
        });

        var progressReports = new List<WorkflowProgress>();
        var progress = new Progress<WorkflowProgress>(p => progressReports.Add(p));

        try
        {
            await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false },
                progress);

            // Allow progress handler to fire (it's async)
            await Task.Delay(100);

            Assert.NotEmpty(progressReports);
            Assert.Equal("WF Progress", progressReports[0].WorkflowName);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_WithNotificationTrustees_SetsNotificationLists()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlWithRoles(new[]
        {
            new WorkflowInputModel
            {
                Name = "Notify WF",
                Active = true,
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Review Step",
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "reviewer1", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.Reviewer },
                            new() { TrusteeId = "notifyUser", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.EmailNotify },
                            new() { TrusteeId = "alertGroup", TrusteeType = WorkflowUserType.Group, Role = TrusteeRole.AlertNotify }
                        }
                    }
                }
            }
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var step = saved.WorkflowStepModels[0];

            // Reviewer trustees
            Assert.Single(step.WorkflowTrusteeDefinitions);
            Assert.Equal("reviewer1", step.WorkflowTrusteeDefinitions[0].TrusteeId);

            // Email notification trustees
            Assert.Single(step.EmailNotificationList);
            Assert.Equal("notifyUser", step.EmailNotificationList[0].TrusteeId);

            // Alert notification trustees
            Assert.Single(step.AlertNotificationList);
            Assert.Equal("alertGroup", step.AlertNotificationList[0].TrusteeId);

            // Workflow-level email notify flag
            Assert.True(saved.WorkflowDefinition.BDoEmailNotify);

            // Step-level email notify flag
            Assert.True(step.WorkflowStepDefinition.BDoEmailNotify);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
        public async Task CreateAsync_WithApproversNotify_UsesNullTargetId()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

                var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Approvers Notify WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""Approvers"" Type=""A"" Role=""Notify"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var step = savedModels[0].WorkflowStepModels[0];
            Assert.Single(step.EmailNotificationList);
            Assert.Null(step.EmailNotificationList[0].TargetId);
            Assert.Equal(string.Empty, step.EmailNotificationList[0].TrusteeId);
            Assert.Equal(WorkflowUserType.Approvers, step.EmailNotificationList[0].Type);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

        [Fact]
        public async Task CreateAsync_PropagatesWorkflowActiveFlag_ToSavedModel()
        {
                var savedModels = new List<WorkflowEditModel>();
                var capturingClient = new CapturingSaveClient(savedModels);
                var service = CreateService(capturingClient);

                var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Inactive WF"" Active=""false"">
            <Steps>
                <Step Name=""Review Step"">
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
                        var result = await service.CreateAsync(
                                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

                        Assert.Equal(1, result.Succeeded);
                        Assert.Single(savedModels);
                        Assert.False(savedModels[0].WorkflowDefinition.Active);
                }
                finally
                {
                        File.Delete(xmlPath);
                }
        }

    [Fact]
    public async Task CreateAsync_WithNoNotifications_BDoEmailNotifyFalse()
    {
        // Per deep-dive 9.12.1: ATP must explicitly drive BDoEmailNotify=false when no
        // notification recipients are declared (full-replace authoring semantics).
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Reviewer Only WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var step = saved.WorkflowStepModels[0];

            // No notifications declared → flag must be false on step and workflow.
            Assert.False(step.WorkflowStepDefinition.BDoEmailNotify);
            Assert.False(saved.WorkflowDefinition.BDoEmailNotify);

            // Notification lists must be empty.
            Assert.Empty(step.EmailNotificationList);
            Assert.Empty(step.AlertNotificationList);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
        public async Task CreateAsync_AllNotifyRecipientsInvalid_FailsWithActionableMessage()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

                var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Invalid Notify WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""bad-email"" Type=""Email"" Role=""Notify"" />
                        <Trustee Id=""also-bad"" Type=""Email"" Role=""Alert"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("all notify/alert recipients are invalid", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(savedModels);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_InvalidGroupTrustee_FailsBeforeSave()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(
            savedModels,
            groups: new List<AdeptGroupEntry>
            {
                new() { GroupId = "engineering", Name = "Engineering" }
            });
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Invalid Group WF"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""eng-managers"" Type=""Group"" Role=""Notify"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Empty(savedModels);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("invalid group ID", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_GroupNameTrustee_IsResolvedToGroupId_BeforeSave()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(
            savedModels,
            groups: new List<AdeptGroupEntry>
            {
                new() { GroupId = "grp-designers", Name = "Designers" }
            });
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Valid Group Name WF"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""Designers"" Type=""Group"" Role=""Notify"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var notify = savedModels[0].WorkflowStepModels[0].EmailNotificationList;
            Assert.Single(notify);
            Assert.Equal("grp-designers", notify[0].TrusteeId);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_TwoSteps_OnlyFirstStepNotifier_DoesNotAssignNotifierToSecondStep()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(
            savedModels,
            groups: new List<AdeptGroupEntry>
            {
                new() { GroupId = "grp-designers", Name = "Designers" }
            });
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Two Step Notify WF"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""Designers"" Type=""Group"" Role=""Notify"" />
                    </Trustees>
                </Step>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer2"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            Assert.Equal(2, saved.WorkflowStepModels.Count);

            var step1 = saved.WorkflowStepModels[0];
            var step2 = saved.WorkflowStepModels[1];

            Assert.Contains(step1.EmailNotificationList, n =>
                n.Type == WorkflowUserType.Group &&
                n.TrusteeId == "grp-designers");
            Assert.All(step1.EmailNotificationList, n => Assert.Equal(step1.WorkflowStepDefinition.StepId, n.StepId));
            Assert.Empty(step2.EmailNotificationList);
            Assert.Empty(step2.AlertNotificationList);

            // Keep workflow-level lists empty to avoid duplicate application.
            Assert.Empty(saved.EmailNotificationList);
            Assert.Empty(saved.AlertNotificationList);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenServerStepsOutOfOrder_AssignsNotificationsByStepOrder()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new OutOfOrderCreateStepsClient(
            savedModels,
            groups: new List<AdeptGroupEntry>
            {
                new() { GroupId = "grp-designers", Name = "Designers" }
            });
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Create Order Mapping WF"" Active=""true"">
            <Steps>
                <Step Name=""Draft"">
                    <Trustees>
                        <Trustee Id=""reviewer1"" Type=""User"" Role=""Reviewer"" />
                        <Trustee Id=""Designers"" Type=""Group"" Role=""Notify"" />
                    </Trustees>
                </Step>
                <Step Name=""Review"">
                    <Trustees>
                        <Trustee Id=""reviewer2"" Type=""User"" Role=""Reviewer"" />
                    </Trustees>
                </Step>
            </Steps>
        </Workflow>
    </Workflows>
</AdeptWorkflowConfig>");

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);

            var saved = savedModels[0];
            var draft = saved.WorkflowStepModels.Single(s =>
                string.Equals(s.WorkflowStepDefinition.Name, "Draft", StringComparison.OrdinalIgnoreCase));
            var review = saved.WorkflowStepModels.Single(s =>
                string.Equals(s.WorkflowStepDefinition.Name, "Review", StringComparison.OrdinalIgnoreCase));

            Assert.Single(draft.EmailNotificationList);
            Assert.Equal("grp-designers", draft.EmailNotificationList[0].TrusteeId);
            Assert.Empty(review.EmailNotificationList);
            Assert.Empty(review.AlertNotificationList);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidReviewerTrusteeType()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Invalid Reviewer Type WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

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
    public async Task CreateAsync_DeduplicatesStepNotificationRecipients()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Dedup Notify WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

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
    public async Task CreateAsync_SaveTransientDisposedContext_RetriesOnce_ThenSucceeds()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new TransientSaveFailureClient(savedModels);
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Retry Save Create WF"" Active=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);
            Assert.Equal(2, client.SaveAttemptCount);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateStepNamesInWorkflow()
    {
        var savedModels = new List<WorkflowEditModel>();
        var capturingClient = new CapturingSaveClient(savedModels);
        var service = CreateService(capturingClient);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Duplicate Steps WF"" Active=""true"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

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
        public async Task CreateAsync_PropagatesWorkflowSharedFlag_ToSavedModel()
        {
                var savedModels = new List<WorkflowEditModel>();
            var sharedCalls = new List<(string WorkflowId, bool Shared)>();
            var capturingClient = new CapturingSaveClient(savedModels, sharedCalls);
                var service = CreateService(capturingClient);

                var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Shared WF"" Active=""true"" Shared=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
                        var result = await service.CreateAsync(
                                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

                        Assert.Equal(1, result.Succeeded);
                        Assert.Single(savedModels);
                        Assert.True(savedModels[0].WorkflowDefinition.Shared);
                        Assert.Contains(sharedCalls, c => c.Shared);
                }
                finally
                {
                        File.Delete(xmlPath);
                }
        }

    [Fact]
    public async Task CreateAsync_DryRun_AllowsLoginIdTrustees_WhenUserListIsIncomplete()
    {
        var client = new IncompleteUsersClient();
        var service = CreateService(client);

        var xmlPath = CreateTempXmlWithRoles(new[]
        {
            new WorkflowInputModel
            {
                Name = "TRN-20260702-01",
                Active = true,
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Draft",
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "bill.stamp", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.Reviewer }
                        }
                    }
                }
            },
            new WorkflowInputModel
            {
                Name = "Design Review",
                Active = true,
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Review",
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "asmith", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.Reviewer }
                        }
                    }
                }
            }
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = true });

            Assert.Equal(2, result.Total);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(0, result.Failed);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_CanonicalizesExactUserIdCasing_BeforeSave()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapturingSaveClient(savedModels, users: new List<AdeptUserEntry>
        {
            new() { UserId = "Asmith", DisplayName = "Al Smith" }
        });
        var service = CreateService(client);

        var xmlPath = CreateTempXmlWithRoles(new[]
        {
            new WorkflowInputModel
            {
                Name = "Case Canon WF",
                Active = true,
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Review",
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "asmith", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.Reviewer }
                        }
                    }
                }
            }
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);
            Assert.Equal("Asmith", savedModels[0].WorkflowStepModels[0].WorkflowTrusteeDefinitions[0].TrusteeId);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ComLikeBackend_RejectsShareStateMutationClearly()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapabilityLimitedClient(savedModels)
        {
            ShareMutationSupported = false,
            UserDirectoryLookupSupported = true,
            GroupDirectoryLookupSupported = true
        };
        var service = CreateService(client);

        var xmlPath = CreateTempXmlRaw(@"<AdeptWorkflowConfig>
    <Workflows>
        <Workflow Name=""Shared WF"" Active=""true"" Shared=""true"">
            <Steps>
                <Step Name=""Review Step"">
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
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Failed);
            Assert.Contains(result.Results, r =>
                r.Status == WorkflowResultStatus.Fail &&
                (r.Message ?? string.Empty).Contains("does not support workflow share-state changes", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ComLikeBackend_AllowsExactLoginIdsWithoutDirectoryLookup()
    {
        var savedModels = new List<WorkflowEditModel>();
        var client = new CapabilityLimitedClient(savedModels)
        {
            ShareMutationSupported = true,
            UserDirectoryLookupSupported = false,
            GroupDirectoryLookupSupported = false
        };
        var service = CreateService(client);

        var xmlPath = CreateTempXmlWithRoles(new[]
        {
            new WorkflowInputModel
            {
                Name = "COM Safe WF",
                Active = true,
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Review",
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "reviewer1", TrusteeType = WorkflowUserType.User, Role = TrusteeRole.Reviewer }
                        }
                    }
                }
            }
        });

        try
        {
            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = xmlPath, DryRun = false });

            Assert.Equal(1, result.Succeeded);
            Assert.Single(savedModels);
            Assert.Equal("reviewer1", savedModels[0].WorkflowStepModels[0].WorkflowTrusteeDefinitions[0].TrusteeId);
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    // --- helpers ---

    private static WorkflowInputModel CreateSampleInput(string name, string[] stepNames)
    {
        return new WorkflowInputModel
        {
            Name = name,
            Active = true,
            Steps = stepNames.Select(s => new WorkflowInputStep
            {
                Name = s,
                Trustees = new List<WorkflowInputTrustee>
                {
                    new() { TrusteeId = "user1", TrusteeType = WorkflowUserType.User }
                }
            }).ToList()
        };
    }

    private static string CreateTempXml(WorkflowInputModel[] workflows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_test_{Guid.NewGuid():N}.xml");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<AdeptWorkflowConfig>");
        sb.AppendLine("  <Workflows>");
        foreach (var wf in workflows)
        {
            sb.AppendLine($"    <Workflow Name=\"{EscapeXml(wf.Name)}\" Active=\"{wf.Active.ToString().ToLower()}\">");
            sb.AppendLine("      <Steps>");
            foreach (var step in wf.Steps)
            {
                sb.AppendLine($"        <Step Name=\"{EscapeXml(step.Name)}\" ApprovalsRequired=\"{step.RequiredApprovalsCount}\">");
                if (step.Trustees.Count > 0)
                {
                    sb.AppendLine("          <Trustees>");
                    foreach (var t in step.Trustees)
                    {
                        var typeStr = t.TrusteeType switch
                        {
                            WorkflowUserType.User => "User",
                            WorkflowUserType.Group => "Group",
                            WorkflowUserType.Key => "Meta",
                            WorkflowUserType.Email => "Email",
                            _ => "User"
                        };
                        sb.AppendLine($"            <Trustee Id=\"{EscapeXml(t.TrusteeId)}\" Type=\"{typeStr}\" />");
                    }
                    sb.AppendLine("          </Trustees>");
                }
                sb.AppendLine("        </Step>");
            }
            sb.AppendLine("      </Steps>");
            sb.AppendLine("    </Workflow>");
        }
        sb.AppendLine("  </Workflows>");
        sb.AppendLine("</AdeptWorkflowConfig>");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string CreateTempXmlWithRoles(WorkflowInputModel[] workflows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_test_{Guid.NewGuid():N}.xml");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<AdeptWorkflowConfig>");
        sb.AppendLine("  <Workflows>");
        foreach (var wf in workflows)
        {
            sb.AppendLine($"    <Workflow Name=\"{EscapeXml(wf.Name)}\" Active=\"{wf.Active.ToString().ToLower()}\">");
            sb.AppendLine("      <Steps>");
            foreach (var step in wf.Steps)
            {
                sb.AppendLine($"        <Step Name=\"{EscapeXml(step.Name)}\" ApprovalsRequired=\"{step.RequiredApprovalsCount}\">");
                if (step.Trustees.Count > 0)
                {
                    sb.AppendLine("          <Trustees>");
                    foreach (var t in step.Trustees)
                    {
                        var typeStr = t.TrusteeType switch
                        {
                            WorkflowUserType.User => "User",
                            WorkflowUserType.Group => "Group",
                            WorkflowUserType.Key => "Meta",
                            WorkflowUserType.Email => "Email",
                            _ => "User"
                        };
                        var roleStr = t.Role switch
                        {
                            TrusteeRole.EmailNotify => "Notify",
                            TrusteeRole.AlertNotify => "Alert",
                            _ => "Reviewer"
                        };
                        sb.AppendLine($"            <Trustee Id=\"{EscapeXml(t.TrusteeId)}\" Type=\"{typeStr}\" Role=\"{roleStr}\" />");
                    }
                    sb.AppendLine("          </Trustees>");
                }
                sb.AppendLine("        </Step>");
            }
            sb.AppendLine("      </Steps>");
            sb.AppendLine("    </Workflow>");
        }
        sb.AppendLine("  </Workflows>");
        sb.AppendLine("</AdeptWorkflowConfig>");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string CreateTempXmlRaw(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_test_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    /// <summary>
    /// Mock that captures the WorkflowEditModel passed to SaveWorkflowAsync.
    /// </summary>
    private class CapturingSaveClient : MockWorkflowApiClient
    {
        private readonly List<WorkflowEditModel> _saved;
        private readonly List<(string WorkflowId, bool Shared)> _sharedCalls;
        private readonly List<AdeptUserEntry>? _users;
        private readonly List<AdeptGroupEntry>? _groups;

        public CapturingSaveClient(
            List<WorkflowEditModel> saved,
            List<(string WorkflowId, bool Shared)>? sharedCalls = null,
            List<AdeptUserEntry>? users = null,
            List<AdeptGroupEntry>? groups = null)
        {
            _saved = saved;
            _sharedCalls = sharedCalls ?? new List<(string WorkflowId, bool Shared)>();
            _users = users;
            _groups = groups;
        }

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            _saved.Add(model);
            return Task.FromResult(ApiResult.Success("Captured."));
        }

        public override Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            var model = _saved.LastOrDefault(m =>
                string.Equals(m.WorkflowDefinition.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase));

            if (model is not null)
            {
                return Task.FromResult(model);
            }

            return base.GetWorkflowAsync(workflowId, ct);
        }

        public override Task<ApiResult> SetWorkflowSharedAsync(string workflowId, bool shared, CancellationToken ct = default)
        {
            _sharedCalls.Add((workflowId, shared));
            return Task.FromResult(ApiResult.Success("Captured share state."));
        }

        public override async Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
        {
            if (_users is not null)
                return _users;

            return await base.GetUsersAsync(ct);
        }

        public override async Task<List<AdeptGroupEntry>> GetGroupsAsync(CancellationToken ct = default)
        {
            if (_groups is not null)
                return _groups;

            return await base.GetGroupsAsync(ct);
        }
    }

    private class IncompleteUsersClient : MockWorkflowApiClient
    {
        public override Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
        {
            // Simulate a server response that does not include all valid login IDs.
            return Task.FromResult(new List<AdeptUserEntry>
            {
                new() { UserId = "reviewer1", DisplayName = "Reviewer, First" }
            });
        }
    }

    private sealed class CapabilityLimitedClient : CapturingSaveClient
    {
        public CapabilityLimitedClient(List<WorkflowEditModel> saved)
            : base(saved)
        {
        }

        public bool ShareMutationSupported { get; init; }
        public bool UserDirectoryLookupSupported { get; init; }
        public bool GroupDirectoryLookupSupported { get; init; }

        public override WorkflowApiCapabilities Capabilities => new()
        {
            SupportsShareMutation = ShareMutationSupported,
            SupportsUserDirectoryLookup = UserDirectoryLookupSupported,
            SupportsGroupDirectoryLookup = GroupDirectoryLookupSupported
        };

        public override Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
        {
            if (!UserDirectoryLookupSupported)
                throw new NotSupportedException("User directory lookup disabled for this backend.");

            return base.GetUsersAsync(ct);
        }

        public override Task<AdeptUserEntry?> GetUserByIdAsync(string userId, CancellationToken ct = default)
        {
            if (!UserDirectoryLookupSupported)
                throw new NotSupportedException("User directory lookup disabled for this backend.");

            return base.GetUserByIdAsync(userId, ct);
        }

        public override Task<List<AdeptGroupEntry>> GetGroupsAsync(CancellationToken ct = default)
        {
            if (!GroupDirectoryLookupSupported)
                throw new NotSupportedException("Group directory lookup disabled for this backend.");

            return base.GetGroupsAsync(ct);
        }
    }

    private sealed class TransientSaveFailureClient : CapturingSaveClient
    {
        public TransientSaveFailureClient(List<WorkflowEditModel> saved)
            : base(saved)
        {
        }

        public int SaveAttemptCount { get; private set; }

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            SaveAttemptCount++;
            if (SaveAttemptCount == 1)
            {
                return Task.FromResult(ApiResult.Failure(
                    139,
                    "Cannot access a disposed context instance. Object name: 'MsSqlDatabaseContext'."));
            }

            return base.SaveWorkflowAsync(model, ct);
        }
    }

    private sealed class OutOfOrderCreateStepsClient : CapturingSaveClient
    {
        public OutOfOrderCreateStepsClient(
            List<WorkflowEditModel> saved,
            List<(string WorkflowId, bool Shared)>? sharedCalls = null,
            List<AdeptUserEntry>? users = null,
            List<AdeptGroupEntry>? groups = null)
            : base(saved, sharedCalls, users, groups)
        {
        }

        public override Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
        {
            return base.AddStepAsync(model, position, ct).ContinueWith(t =>
            {
                var updated = t.Result;
                // Simulate backend returning collection order that does not match step order.
                updated.WorkflowStepModels = updated.WorkflowStepModels
                    .OrderByDescending(s => s.WorkflowStepDefinition.Order)
                    .ToList();
                return updated;
            }, ct);
        }
    }
}
