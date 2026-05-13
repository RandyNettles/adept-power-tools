using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Tests;

public class ResultItemTests
{
    [Theory]
    [InlineData(ResultStatus.Ok, "[OK]")]
    [InlineData(ResultStatus.Fail, "[FAIL]")]
    [InlineData(ResultStatus.Skip, "[SKIP]")]
    public void StatusPrefix_MatchesStatus(ResultStatus status, string expectedPrefix)
    {
        var item = new ResultItem(status, "test message");

        Assert.Equal(expectedPrefix, item.StatusPrefix);
        Assert.Equal("test message", item.Message);
    }
}
