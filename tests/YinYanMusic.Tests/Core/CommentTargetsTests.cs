using YinYanMusic.Core;

namespace YinYanMusic.Tests.Core;

public class CommentTargetsTests
{
    [Theory]
    [InlineData("song")]
    [InlineData("playlist")]
    public void IsValid_AcceptsKnownTargets(string targetType)
        => Assert.True(CommentTargets.IsValid(targetType));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("album")]
    [InlineData("Song")]      // 取值区分大小写：库里存的就是小写，别让 "Song" 混进来
    public void IsValid_RejectsUnknownOrMismatchedCase(string? targetType)
        => Assert.False(CommentTargets.IsValid(targetType));

    [Fact]
    public void All_ContainsExactlyTheTwoTargets()
        => Assert.Equal(2, CommentTargets.All.Length);
}
