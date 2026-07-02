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

        public CapturingSaveClient(List<WorkflowEditModel> saved) => _saved = saved;

        public override Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
        {
            _saved.Add(model);
            return Task.FromResult(ApiResult.Success("Captured."));
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
}
