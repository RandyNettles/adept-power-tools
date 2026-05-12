using AdeptTools.Workflow.Api;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class MockWorkflowApiClientTests
{
    private readonly MockWorkflowApiClient _client = new();

    [Fact]
    public async Task GetSetupAsync_ReturnsValidSetup()
    {
        var setup = await _client.GetSetupAsync();

        Assert.True(setup.MaximumLengthWorkflowName > 0);
        Assert.True(setup.MaximumLengthStepName > 0);
        Assert.True(setup.MaximumWorkflows > 0);
    }

    [Fact]
    public async Task GetWorkflowsAsync_ReturnsThreeWorkflows()
    {
        var packet = await _client.GetWorkflowsAsync();

        Assert.Equal(3, packet.Workflows.Count);
        Assert.Equal("Design Review", packet.Workflows[0].WorkflowName);
        Assert.Equal("Piping Approval", packet.Workflows[1].WorkflowName);
        Assert.Equal("Final Check", packet.Workflows[2].WorkflowName);
    }

    [Fact]
    public async Task CreateNewAsync_ReturnsEditableModel()
    {
        var model = await _client.CreateNewAsync();

        Assert.True(model.BEditable);
        Assert.NotEmpty(model.WorkflowDefinition.WorkflowId);
        Assert.Single(model.WorkflowStepModels);
    }

    [Fact]
    public async Task AddStepAsync_AddsStepToModel()
    {
        var model = await _client.CreateNewAsync();
        var initialCount = model.WorkflowStepModels.Count;

        model = await _client.AddStepAsync(model, -1);

        Assert.Equal(initialCount + 1, model.WorkflowStepModels.Count);
        Assert.NotNull(model.EAddStep);
        Assert.NotNull(model.EStepId);
    }

    [Fact]
    public async Task SaveWorkflowAsync_ReturnsSuccess()
    {
        var model = await _client.CreateNewAsync();
        var result = await _client.SaveWorkflowAsync(model);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteWorkflowAsync_ReturnsSuccess()
    {
        var result = await _client.DeleteWorkflowAsync("wf-001");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetMetagroupsAsync_ReturnsList()
    {
        var metagroups = await _client.GetMetagroupsAsync();

        Assert.NotEmpty(metagroups);
        Assert.Contains(metagroups, m => m.Key == "ALL_USERS");
    }
}
