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

    private static string CreateTempXmlRaw(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_modify_test_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private class CapturingModifyClient : MockWorkflowApiClient
    {
        private readonly List<WorkflowEditModel> _saved;

        public CapturingModifyClient(List<WorkflowEditModel> saved)
        {
            _saved = saved;
        }

        public override Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
        {
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
                            Name = "Step 1"
                        }
                    }
                }
            });
        }

        public override Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
        {
            var existing = _saved.LastOrDefault(m =>
                string.Equals(m.WorkflowDefinition.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
                return Task.FromResult(existing);

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
                            Name = "Step 1"
                        }
                    }
                }
            });
        }

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            _saved.Add(model);
            return Task.FromResult(ApiResult.Success("Captured."));
        }
    }
}