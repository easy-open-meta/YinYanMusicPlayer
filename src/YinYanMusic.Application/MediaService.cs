using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Application;

public interface IMediaService
{
    Task<ServiceResult<MediaUploadResult>> UploadAsync(IFormFile file, string kind);
}

public class MediaService(IWebHostEnvironment env, IConfiguration config) : IMediaService
{
    private static readonly string[] AudioExt = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];
    private static readonly string[] ImageExt = [".png", ".jpg", ".jpeg", ".webp", ".gif"];
    private static readonly string[] LyricExt = [".lrc", ".txt"];

    public async Task<ServiceResult<MediaUploadResult>> UploadAsync(IFormFile file, string kind)
    {
        if (file.Length == 0) return ServiceResult<MediaUploadResult>.Fail("文件为空。");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = kind switch
        {
            "audio" => AudioExt,
            "image" => ImageExt,
            "lyric" => LyricExt,
            _ => Array.Empty<string>()
        };
        if (allowed.Length == 0) return ServiceResult<MediaUploadResult>.Fail("kind 仅支持 audio/image/lyric。");
        if (!allowed.Contains(ext)) return ServiceResult<MediaUploadResult>.Fail($"不支持的文件类型 {ext}。");

        var root = Path.Combine(env.ContentRootPath, "wwwroot", "media", kind);
        Directory.CreateDirectory(root);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using var stream = File.Create(Path.Combine(root, fileName));
        await file.CopyToAsync(stream);

        var baseUrl = config["Media:BaseUrl"];
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? $"/media/{kind}/{fileName}"
            : $"{baseUrl.TrimEnd('/')}/media/{kind}/{fileName}";
        return ServiceResult<MediaUploadResult>.Ok(new MediaUploadResult(url));
    }
}