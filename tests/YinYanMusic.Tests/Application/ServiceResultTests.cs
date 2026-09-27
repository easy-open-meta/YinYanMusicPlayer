using YinYanMusic.Application;

namespace YinYanMusic.Tests.Application;

public class ServiceResultTests
{
    [Fact]
    public void Ok_SetsSuccess()
    {
        var result = ServiceResult.Ok();
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_SetsError()
    {
        var result = ServiceResult.Fail("出错了");
        Assert.False(result.Success);
        Assert.Equal("出错了", result.Error);
    }

    [Fact]
    public void Generic_Ok_ContainsData()
    {
        var result = ServiceResult<int>.Ok(42);
        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Generic_Fail_SetsErrorAndNoData()
    {
        var result = ServiceResult<int>.Fail("失败");
        Assert.False(result.Success);
        Assert.Equal("失败", result.Error);
        Assert.Equal(default, result.Data);
    }
}