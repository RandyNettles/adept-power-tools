using AdeptTools.Workflow.Input;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class UserMatcherTests
{
    private static List<AdeptUserEntry> CreateTestUsers() => new()
    {
        new() { UserId = "jsmith", DisplayName = "Smith, John" },
        new() { UserId = "mjones", DisplayName = "Jones, Mary" },
        new() { UserId = "akhan", DisplayName = "Khan, Amir" },
        new() { UserId = "cchiboroski", DisplayName = "Chiboroski, Craig" },
        new() { UserId = "rbabu", DisplayName = "Rameshbabu, Vijay" },
        new() { UserId = "bwilson", DisplayName = "Wilson, Bob" },
        new() { UserId = "tlee", DisplayName = "Lee, Thomas" }
    };

    [Fact]
    public void Match_ExactUserId_ReturnsExact()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("jsmith");

        Assert.Equal(MatchConfidence.Exact, result.Confidence);
        Assert.Equal("jsmith", result.ResolvedUserId);
        Assert.Equal("Smith, John", result.MatchedDisplayName);
    }

    [Fact]
    public void Match_ExactUserId_CaseInsensitive()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("JSmith");

        Assert.Equal(MatchConfidence.Exact, result.Confidence);
        Assert.Equal("jsmith", result.ResolvedUserId);
    }

    [Fact]
    public void Match_DisplayNameExact_ReturnsStrong()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("Smith, John");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("jsmith", result.ResolvedUserId);
    }

    [Fact]
    public void Match_DisplayNameReversed_ReturnsStrong()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("Craig Chiboroski");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("cchiboroski", result.ResolvedUserId);
        Assert.Equal("Chiboroski, Craig", result.MatchedDisplayName);
    }

    [Fact]
    public void Match_LastNameOnly_UniqueMatch_ReturnsWeak()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("Rameshbabu");

        Assert.Equal(MatchConfidence.Weak, result.Confidence);
        Assert.Equal("rbabu", result.ResolvedUserId);
        Assert.Equal("Rameshbabu, Vijay", result.MatchedDisplayName);
    }

    [Fact]
    public void Match_NoMatch_ReturnsNone()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("S.M.");

        Assert.Equal(MatchConfidence.None, result.Confidence);
        Assert.Null(result.ResolvedUserId);
    }

    [Fact]
    public void Match_EmptyInput_ReturnsNone()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("");

        Assert.Equal(MatchConfidence.None, result.Confidence);
    }

    [Fact]
    public void Match_WhitespaceInput_ReturnsNone()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("   ");

        Assert.Equal(MatchConfidence.None, result.Confidence);
    }

    [Fact]
    public void Match_DisplayNameCaseInsensitive_ReturnsStrong()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("smith, john");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("jsmith", result.ResolvedUserId);
    }

    [Fact]
    public void Match_FirstLast_ReturnsStrong()
    {
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("John Smith");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("jsmith", result.ResolvedUserId);
    }

    [Fact]
    public void Match_PartialLastNameWithInitial_ReturnsWeak()
    {
        // "Khan" is unique as a last name in our test data
        var matcher = new UserMatcher(CreateTestUsers());
        var result = matcher.Match("Khan");

        Assert.Equal(MatchConfidence.Weak, result.Confidence);
        Assert.Equal("akhan", result.ResolvedUserId);
    }

    [Fact]
    public void Match_CanonicalLoginId_DotVsCompact_ReturnsStrong()
    {
        var users = new List<AdeptUserEntry>
        {
            new() { UserId = "billstamp", DisplayName = "Bill Stamp" }
        };

        var matcher = new UserMatcher(users);
        var result = matcher.Match("bill.stamp");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("billstamp", result.ResolvedUserId);
    }

    [Fact]
    public void Match_CanonicalLoginId_CaseInsensitiveWithDot_ReturnsStrong()
    {
        var users = new List<AdeptUserEntry>
        {
            new() { UserId = "A.Smith", DisplayName = "Smith, Amy" }
        };

        var matcher = new UserMatcher(users);
        var result = matcher.Match("asmith");

        Assert.Equal(MatchConfidence.Strong, result.Confidence);
        Assert.Equal("A.Smith", result.ResolvedUserId);
    }
}
