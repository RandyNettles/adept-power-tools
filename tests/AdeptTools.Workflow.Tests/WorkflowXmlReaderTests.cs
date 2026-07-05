using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowXmlReaderTests
{
    private readonly WorkflowXmlReader _reader = new();

    [Fact]
    public void Read_ValidXml_ParsesWorkflows()
    {
        var xml = @"<AdeptWorkflowConfig>
  <ServerUrl>https://test.server.com</ServerUrl>
  <ProjectName>TestProject</ProjectName>
  <DryRun>true</DryRun>
  <Workflows>
    <Workflow Name=""Test WF"" Active=""true"" Shared=""true"">
      <Memo>Test description</Memo>
      <TimeoutDays>5</TimeoutDays>
      <Steps>
        <Step Name=""Review"" ApprovalsRequired=""2"" AutoAdvance=""true"">
          <Trustees>
            <Trustee Id=""user1"" Type=""User"" />
            <Trustee Id=""eng_group"" Type=""Group"" />
          </Trustees>
        </Step>
        <Step Name=""Approve"" ApprovalsRequired=""1"">
          <Trustees>
            <Trustee Id=""manager"" Type=""User"" />
          </Trustees>
        </Step>
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Equal("https://test.server.com", result.ServerUrl);
            Assert.Equal("TestProject", result.ProjectName);
            Assert.True(result.DryRun);
            Assert.Single(result.Workflows);

            var wf = result.Workflows[0];
            Assert.Equal("Test WF", wf.Name);
            Assert.True(wf.Active);
            Assert.True(wf.Shared);
            Assert.Equal("Test description", wf.Memo);
            Assert.Equal(5, wf.TimeoutDays);
            Assert.Equal(2, wf.Steps.Count);

            Assert.Equal("Review", wf.Steps[0].Name);
            Assert.Equal(2, wf.Steps[0].RequiredApprovalsCount);
            Assert.True(wf.Steps[0].AutoAdvance);
            Assert.Equal(2, wf.Steps[0].Trustees.Count);
            Assert.Equal(WorkflowUserType.User, wf.Steps[0].Trustees[0].TrusteeType);
            Assert.Equal(WorkflowUserType.Group, wf.Steps[0].Trustees[1].TrusteeType);

            Assert.Equal("Approve", wf.Steps[1].Name);
            Assert.Single(wf.Steps[1].Trustees);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MultipleWorkflows_ParsesAll()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF1"" Active=""true"" Shared=""true"">
      <Steps><Step Name=""S1""><Trustees><Trustee Id=""u1"" Type=""User"" /></Trustees></Step></Steps>
    </Workflow>
    <Workflow Name=""WF2"" Active=""false"" Shared=""false"">
      <Steps><Step Name=""S1""><Trustees><Trustee Id=""u2"" Type=""Email"" /></Trustees></Step></Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Equal(2, result.Workflows.Count);
            Assert.Equal("WF1", result.Workflows[0].Name);
            Assert.True(result.Workflows[0].Active);
            Assert.True(result.Workflows[0].Shared);
            Assert.Equal("WF2", result.Workflows[1].Name);
            Assert.False(result.Workflows[1].Active);
            Assert.False(result.Workflows[1].Shared);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_EmptyWorkflows_ReturnsEmptyList()
    {
        var xml = @"<AdeptWorkflowConfig><Workflows></Workflows></AdeptWorkflowConfig>";
        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Empty(result.Workflows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_StepAllowEmptyTrustees_ParsesAttribute()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF"" Active=""true"">
      <Steps>
        <Step Name=""Terminal"" AutoAdvance=""true"" AllowEmptyTrustees=""true"" />
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Single(result.Workflows);
            Assert.Single(result.Workflows[0].Steps);
            Assert.True(result.Workflows[0].Steps[0].AllowEmptyTrustees);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WorkflowExcludeWeekend_Omitted_RemainsNull()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF"" Active=""true"">
      <Steps>
        <Step Name=""Review"">
          <Trustees>
            <Trustee Id=""user1"" Type=""User"" />
          </Trustees>
        </Step>
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Single(result.Workflows);
            Assert.Null(result.Workflows[0].ExcludeSaturday);
            Assert.Null(result.Workflows[0].ExcludeSunday);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WorkflowExcludeWeekend_Present_ParsesValues()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF"" Active=""true"" ExcludeSaturday=""true"" ExcludeSunday=""false"">
      <Steps>
        <Step Name=""Review"">
          <Trustees>
            <Trustee Id=""user1"" Type=""User"" />
          </Trustees>
        </Step>
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Single(result.Workflows);
            Assert.True(result.Workflows[0].ExcludeSaturday);
            Assert.False(result.Workflows[0].ExcludeSunday);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_XmlTrusteeMissingType_IsSkipped()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF"" Active=""true"">
      <Steps>
        <Step Name=""Review""><Trustees><Trustee Id=""user1"" /></Trustees></Step>
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Single(result.Workflows);
            Assert.Single(result.Workflows[0].Steps);
            Assert.Empty(result.Workflows[0].Steps[0].Trustees);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_XmlTrusteeMissingRole_DefaultsToReviewer()
    {
        var xml = @"<AdeptWorkflowConfig>
  <Workflows>
    <Workflow Name=""WF"" Active=""true"">
      <Steps>
        <Step Name=""Review""><Trustees><Trustee Id=""user1"" Type=""User"" /></Trustees></Step>
      </Steps>
    </Workflow>
  </Workflows>
</AdeptWorkflowConfig>";

        var path = WriteTempFile(xml);

        try
        {
            var result = _reader.Read(path);

            Assert.Single(result.Workflows);
            Assert.Single(result.Workflows[0].Steps);
            Assert.Single(result.Workflows[0].Steps[0].Trustees);
            Assert.Equal(TrusteeRole.Reviewer, result.Workflows[0].Steps[0].Trustees[0].Role);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"xmltest_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        return path;
    }
}
