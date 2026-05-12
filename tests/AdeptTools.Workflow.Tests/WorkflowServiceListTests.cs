using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowServiceListTests
{
    private readonly MockWorkflowApiClient _mockClient = new();
    private readonly WorkflowExcelReader _excelReader = new();
    private readonly WorkflowXmlReader _xmlReader = new();
    private readonly WorkflowValidator _validator = new();

    private WorkflowService CreateService() =>
        new(_mockClient, _excelReader, _xmlReader, _validator);

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsAll()
    {
        var service = CreateService();

        var result = await service.ListAsync(new WorkflowListRequest());

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Workflows.Count);
    }

    [Fact]
    public async Task ListAsync_WithFilter_ReturnsMatched()
    {
        var service = CreateService();

        var result = await service.ListAsync(new WorkflowListRequest { Filter = "*Review*" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Design Review", result.Workflows[0].WorkflowName);
    }

    [Fact]
    public async Task ListAsync_FilterNoMatch_ReturnsEmpty()
    {
        var service = CreateService();

        var result = await service.ListAsync(new WorkflowListRequest { Filter = "NonExistent" });

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Workflows);
    }
}
