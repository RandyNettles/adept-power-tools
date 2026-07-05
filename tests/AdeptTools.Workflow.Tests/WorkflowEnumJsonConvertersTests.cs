using System.Text.Json;
using AdeptTools.Workflow.Models;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowEnumJsonConvertersTests
{
    private static readonly JsonSerializerOptions ConverterOptions = new()
    {
        Converters =
        {
            new WorkflowUserTypeJsonConverter(),
            new WorkflowNotificationActionJsonConverter()
        }
    };

    [Fact]
    public void WorkflowUserTypeConverter_SerializesAsNumericCode()
    {
        var json = JsonSerializer.Serialize(WorkflowUserType.User, ConverterOptions);
        Assert.Equal("85", json);
    }

    [Fact]
    public void WorkflowNotificationActionConverter_SerializesAsNumericCode()
    {
        var json = JsonSerializer.Serialize(WorkflowNotificationAction.Timeout, ConverterOptions);
        Assert.Equal("84", json);
    }

    [Fact]
    public void WorkflowNotificationDefinition_ContainsNumericTypeAndAction_WhenConvertersAreRegistered()
    {
        var model = new WorkflowNotificationDefinition
        {
            WorkflowId = "wf",
            StepId = "step",
            TrusteeId = "randy.nettles.wf",
            Type = WorkflowUserType.User,
            Action = WorkflowNotificationAction.Timeout
        };

        var json = JsonSerializer.Serialize(model, ConverterOptions);

        Assert.Contains("\"type\":85", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"action\":84", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"U\"", WorkflowUserType.User)]
    [InlineData("85", WorkflowUserType.User)]
    [InlineData("\"A\"", WorkflowUserType.Approvers)]
    [InlineData("65", WorkflowUserType.Approvers)]
    public void WorkflowUserTypeConverter_ReadsStringAndNumericForms(string json, WorkflowUserType expected)
    {
        var value = JsonSerializer.Deserialize<WorkflowUserType>(json, ConverterOptions);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("\"T\"", WorkflowNotificationAction.Timeout)]
    [InlineData("84", WorkflowNotificationAction.Timeout)]
    [InlineData("\"A\"", WorkflowNotificationAction.Approve)]
    [InlineData("65", WorkflowNotificationAction.Approve)]
    public void WorkflowNotificationActionConverter_ReadsStringAndNumericForms(string json, WorkflowNotificationAction expected)
    {
        var value = JsonSerializer.Deserialize<WorkflowNotificationAction>(json, ConverterOptions);
        Assert.Equal(expected, value);
    }
}
