using YinYanMusic.Application;

namespace YinYanMusic.Tests.Application;

public class LikePatternTests
{
    [Fact]
    public void Contains_WrapsKeywordWithPercent()
    {
        Assert.Equal("%周杰伦%", LikePattern.Contains("周杰伦"));
    }

    [Fact]
    public void Contains_EscapesPercentSign()
    {
        Assert.Equal("%100\\%%", LikePattern.Contains("100%"));
    }

    [Fact]
    public void Contains_EscapesUnderscore()
    {
        Assert.Equal("%ck\\_yeun9%", LikePattern.Contains("ck_yeun9"));
    }

    [Fact]
    public void Contains_EscapesBackslashFirst()
    {
        Assert.Equal("%a\\\\b%", LikePattern.Contains(@"a\b"));
    }

    [Fact]
    public void Contains_EmptyKeyword_ReturnsPercentPercent()
    {
        Assert.Equal("%%", LikePattern.Contains(string.Empty));
    }

    [Fact]
    public void EscapeChar_IsBackslash()
    {
        Assert.Equal("\\", LikePattern.EscapeChar);
    }
}