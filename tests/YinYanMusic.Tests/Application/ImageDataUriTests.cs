using YinYanMusic.Application;

namespace YinYanMusic.Tests.Application;

public class ImageDataUriTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/img/cover.jpg")]
    [InlineData("https://example.com/a.png")]
    public void Validate_NonDataUri_ReturnsNull(string? value)
    {
        Assert.Null(ImageDataUri.Validate(value));
    }

    [Fact]
    public void Validate_ValidPng_ReturnsNull()
    {
        // 1x1 红色 PNG 的 base64
        var png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        Assert.Null(ImageDataUri.Validate($"data:image/png;base64,{png}"));
    }

    [Fact]
    public void Validate_UnsupportedMime_ReturnsError()
    {
        var result = ImageDataUri.Validate("data:image/bmp;base64,AAAA");
        Assert.NotNull(result);
        Assert.Contains("不支持的图片格式", result);
    }

    [Fact]
    public void Validate_InvalidBase64_ReturnsError()
    {
        var result = ImageDataUri.Validate("data:image/png;base64,!!!not-base64!!!");
        Assert.NotNull(result);
        Assert.Contains("图片数据格式不正确", result);
    }

    [Fact]
    public void Validate_MissingHeader_ReturnsError()
    {
        // 逗号前没有图片类型头（comma <= 5），直接判格式不正确
        var result = ImageDataUri.Validate("data:,AAAA");
        Assert.NotNull(result);
        Assert.Contains("图片数据格式不正确", result);
    }

    [Fact]
    public void Validate_OversizedImage_ReturnsError()
    {
        // 超过 2MB 的数据（base64 长度约 2.8M）
        var big = new byte[ImageDataUri.MaxDecodedBytes + 1];
        var base64 = Convert.ToBase64String(big);
        var result = ImageDataUri.Validate($"data:image/png;base64,{base64}");
        Assert.NotNull(result);
        Assert.Contains("图片过大", result);
    }

    [Fact]
    public void IsDataUri_DetectsPrefix()
    {
        Assert.True(ImageDataUri.IsDataUri("data:image/png;base64,AAAA"));
        Assert.False(ImageDataUri.IsDataUri("/img/a.jpg"));
        Assert.False(ImageDataUri.IsDataUri(null));
    }
}