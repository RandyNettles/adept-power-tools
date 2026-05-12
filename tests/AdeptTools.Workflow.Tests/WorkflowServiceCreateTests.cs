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

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
