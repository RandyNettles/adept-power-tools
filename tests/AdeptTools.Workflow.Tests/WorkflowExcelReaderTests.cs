using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using OfficeOpenXml;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowExcelReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public void Read_SingleStepWithMultipleTrustees_Vertical()
    {
        var path = CreateVerticalWorkbook("TestWF", sheet =>
        {
            // Step "Draft" with 3 trustees in vertical rows
            sheet.Cells[8, 1].Value = "Draft";
            sheet.Cells[8, 2].Value = 0;
            sheet.Cells[8, 3].Value = "false";
            sheet.Cells[8, 4].Value = "jsmith";
            sheet.Cells[8, 5].Value = "User";
            sheet.Cells[8, 6].Value = "Reviewer";

            sheet.Cells[9, 4].Value = "Engineering";
            sheet.Cells[9, 5].Value = "Group";
            sheet.Cells[9, 6].Value = "Reviewer";

            sheet.Cells[10, 4].Value = "bob@partner.com";
            sheet.Cells[10, 5].Value = "Email";
            sheet.Cells[10, 6].Value = "Notify";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        Assert.Single(result.Workflows);
        var wf = result.Workflows[0];
        Assert.Equal("TestWF", wf.Name);
        Assert.Single(wf.Steps);

        var step = wf.Steps[0];
        Assert.Equal("Draft", step.Name);
        Assert.Equal(3, step.Trustees.Count);

        Assert.Equal("jsmith", step.Trustees[0].TrusteeId);
        Assert.Equal(WorkflowUserType.User, step.Trustees[0].TrusteeType);
        Assert.Equal(TrusteeRole.Reviewer, step.Trustees[0].Role);

        Assert.Equal("Engineering", step.Trustees[1].TrusteeId);
        Assert.Equal(WorkflowUserType.Group, step.Trustees[1].TrusteeType);

        Assert.Equal("bob@partner.com", step.Trustees[2].TrusteeId);
        Assert.Equal(WorkflowUserType.Email, step.Trustees[2].TrusteeType);
        Assert.Equal(TrusteeRole.EmailNotify, step.Trustees[2].Role);
    }

    [Fact]
    public void Read_MultipleStepsWithVaryingTrustees()
    {
        var path = CreateVerticalWorkbook("MultiStep", sheet =>
        {
            // Step 1: "Draft" with 1 trustee
            sheet.Cells[8, 1].Value = "Draft";
            sheet.Cells[8, 2].Value = 0;
            sheet.Cells[8, 3].Value = "false";
            sheet.Cells[8, 4].Value = "jsmith";
            sheet.Cells[8, 5].Value = "User";
            sheet.Cells[8, 6].Value = "Reviewer";

            // Step 2: "Review" with 3 trustees
            sheet.Cells[9, 1].Value = "Review";
            sheet.Cells[9, 2].Value = 2;
            sheet.Cells[9, 3].Value = "true";
            sheet.Cells[9, 4].Value = "mjones";
            sheet.Cells[9, 5].Value = "User";
            sheet.Cells[9, 6].Value = "Reviewer";
            sheet.Cells[10, 4].Value = "akhan";
            sheet.Cells[10, 5].Value = "User";
            sheet.Cells[10, 6].Value = "Reviewer";
            sheet.Cells[11, 4].Value = "pm-notify";
            sheet.Cells[11, 5].Value = "Group";
            sheet.Cells[11, 6].Value = "Alert";

            // Step 3: "Approved" with no trustees (terminal)
            sheet.Cells[12, 1].Value = "Approved";
            sheet.Cells[12, 2].Value = 0;
            sheet.Cells[12, 3].Value = "false";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var wf = result.Workflows[0];
        Assert.Equal(3, wf.Steps.Count);

        Assert.Equal("Draft", wf.Steps[0].Name);
        Assert.Single(wf.Steps[0].Trustees);

        Assert.Equal("Review", wf.Steps[1].Name);
        Assert.Equal(2, wf.Steps[1].RequiredApprovalsCount);
        Assert.True(wf.Steps[1].AutoAdvance);
        Assert.Equal(3, wf.Steps[1].Trustees.Count);
        Assert.Equal(TrusteeRole.AlertNotify, wf.Steps[1].Trustees[2].Role);

        Assert.Equal("Approved", wf.Steps[2].Name);
        Assert.Empty(wf.Steps[2].Trustees);
    }

    [Fact]
    public void Read_StepWithNoTrustees()
    {
        var path = CreateVerticalWorkbook("NoTrustees", sheet =>
        {
            sheet.Cells[8, 1].Value = "Terminal";
            sheet.Cells[8, 2].Value = 0;
            sheet.Cells[8, 3].Value = "false";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var step = result.Workflows[0].Steps[0];
        Assert.Equal("Terminal", step.Name);
        Assert.Empty(step.Trustees);
    }

    [Fact]
    public void Read_CommaSeparatedTrustees_SplitsIntoMultiple()
    {
        var path = CreateVerticalWorkbook("CommaSplit", sheet =>
        {
            sheet.Cells[8, 1].Value = "Review";
            sheet.Cells[8, 2].Value = 1;
            sheet.Cells[8, 3].Value = "false";
            sheet.Cells[8, 4].Value = "jsmith, mdoe, akhan";
            sheet.Cells[8, 5].Value = "User";
            sheet.Cells[8, 6].Value = "Reviewer";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var step = result.Workflows[0].Steps[0];
        Assert.Equal(3, step.Trustees.Count);
        Assert.Equal("jsmith", step.Trustees[0].TrusteeId);
        Assert.Equal("mdoe", step.Trustees[1].TrusteeId);
        Assert.Equal("akhan", step.Trustees[2].TrusteeId);

        // All inherit the same type and role
        Assert.All(step.Trustees, t =>
        {
            Assert.Equal(WorkflowUserType.User, t.TrusteeType);
            Assert.Equal(TrusteeRole.Reviewer, t.Role);
        });
    }

    [Fact]
    public void Read_EmptyRowsBetweenSteps_Ignored()
    {
        var path = CreateVerticalWorkbook("EmptyRows", sheet =>
        {
            sheet.Cells[8, 1].Value = "Draft";
            sheet.Cells[8, 4].Value = "jsmith";
            sheet.Cells[8, 5].Value = "User";
            sheet.Cells[8, 6].Value = "Reviewer";

            // Empty row 9 (no step name, no trustee) — should be ignored

            sheet.Cells[10, 1].Value = "Review";
            sheet.Cells[10, 4].Value = "mjones";
            sheet.Cells[10, 5].Value = "User";
            sheet.Cells[10, 6].Value = "Reviewer";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var wf = result.Workflows[0];
        Assert.Equal(2, wf.Steps.Count);
        Assert.Equal("Draft", wf.Steps[0].Name);
        Assert.Equal("Review", wf.Steps[1].Name);
    }

    [Fact]
    public void Read_AllRoleValues_MappedCorrectly()
    {
        var path = CreateVerticalWorkbook("RoleTest", sheet =>
        {
            sheet.Cells[8, 1].Value = "Step1";
            sheet.Cells[8, 4].Value = "user1";
            sheet.Cells[8, 5].Value = "User";
            sheet.Cells[8, 6].Value = "Reviewer";

            sheet.Cells[9, 4].Value = "user2";
            sheet.Cells[9, 5].Value = "User";
            sheet.Cells[9, 6].Value = "Notify";

            sheet.Cells[10, 4].Value = "user3";
            sheet.Cells[10, 5].Value = "User";
            sheet.Cells[10, 6].Value = "Alert";
        });

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var step = result.Workflows[0].Steps[0];
        Assert.Equal(TrusteeRole.Reviewer, step.Trustees[0].Role);
        Assert.Equal(TrusteeRole.EmailNotify, step.Trustees[1].Role);
        Assert.Equal(TrusteeRole.AlertNotify, step.Trustees[2].Role);
    }

    [Fact]
    public void Read_WorkflowProperties_Populated()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var path = Path.Combine(Path.GetTempPath(), $"wf_prop_{Guid.NewGuid():N}.xlsx");
        _tempFiles.Add(path);

        using (var package = new ExcelPackage())
        {
            var config = package.Workbook.Worksheets.Add("Config");
            config.Cells[1, 1].Value = "Setting";
            config.Cells[1, 2].Value = "Value";

            var wf = package.Workbook.Worksheets.Add("WF-MyWorkflow");
            wf.Cells[3, 1].Value = "Memo:";
            wf.Cells[3, 2].Value = "Test memo";
            wf.Cells[4, 1].Value = "Deadline (days):";
            wf.Cells[4, 2].Value = "14";
            wf.Cells[5, 1].Value = "Active:";
            wf.Cells[5, 2].Value = "true";

            wf.Cells[7, 1].Value = "Step Name";
            wf.Cells[7, 2].Value = "Approvals Required";
            wf.Cells[7, 3].Value = "Auto Advance";
            wf.Cells[7, 4].Value = "Trustee";
            wf.Cells[7, 5].Value = "Type";
            wf.Cells[7, 6].Value = "Role";

            wf.Cells[8, 1].Value = "Step1";

            package.SaveAs(new FileInfo(path));
        }

        var reader = new WorkflowExcelReader();
        var result = reader.Read(path);

        var workflow = result.Workflows[0];
        Assert.Equal("MyWorkflow", workflow.Name);
        Assert.Equal("Test memo", workflow.Memo);
        Assert.Equal(14, workflow.TimeoutDays);
        Assert.True(workflow.Active);
    }

    // --- Helper ---

    private string CreateVerticalWorkbook(string workflowName, Action<ExcelWorksheet> configureSheet)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var path = Path.Combine(Path.GetTempPath(), $"wf_test_{Guid.NewGuid():N}.xlsx");
        _tempFiles.Add(path);

        using var package = new ExcelPackage();

        var config = package.Workbook.Worksheets.Add("Config");
        config.Cells[1, 1].Value = "Setting";
        config.Cells[1, 2].Value = "Value";

        var wf = package.Workbook.Worksheets.Add($"WF-{workflowName}");

        // Standard property rows
        wf.Cells[3, 1].Value = "Memo:";
        wf.Cells[4, 1].Value = "Deadline (days):";
        wf.Cells[5, 1].Value = "Active:";
        wf.Cells[5, 2].Value = "true";

        // Vertical layout headers
        wf.Cells[7, 1].Value = "Step Name";
        wf.Cells[7, 2].Value = "Approvals Required";
        wf.Cells[7, 3].Value = "Auto Advance";
        wf.Cells[7, 4].Value = "Trustee";
        wf.Cells[7, 5].Value = "Type";
        wf.Cells[7, 6].Value = "Role";

        configureSheet(wf);

        package.SaveAs(new FileInfo(path));
        return path;
    }
}
