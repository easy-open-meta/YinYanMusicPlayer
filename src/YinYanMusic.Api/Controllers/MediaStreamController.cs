using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 媒体资源的 API 出口（Nginx 反代部署只反代 <c>/api</c>，<c>/media/*</c> 静态路径到不了后端）。
/// <para>
/// 音频 / 封面 / 歌词在数据库里仍以 <c>/media/audio/...</c>、<c>/media/image/...</c> 相对路径存储，
/// 客户端取到后统一重写为 <c>/api/media/...</c> 再请求这里 —— 这样域名部署时走 Nginx 的
/// <c>/api</c> 反代即可，不再依赖额外反代 <c>/media</c>（本地开发 Vite 的 <c>/media</c> 代理仍然兼容）。
/// </para>
/// <para>Range 请求必须支持：客户端「缓存到本地」的断点续传靠 <c>Range</c> 头实现。</para>
/// </summary>
[ApiController]
[Route("api/media")]
public class MediaStreamController(IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = CreateContentTypeProvider();

    private static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        // .flac / .m4a / .wav / .ogg 不在 ASP.NET Core 默认 MIME 映射表里（与 Program.cs 静态托管同一套）
        provider.Mappings[".flac"] = "audio/flac";
        provider.Mappings[".m4a"] = "audio/mp4";
        provider.Mappings[".wav"] = "audio/wav";
        provider.Mappings[".ogg"] = "audio/ogg";
        provider.Mappings[".aac"] = "audio/aac";
        provider.Mappings[".lrc"] = "text/plain";
        return provider;
    }

    /// <summary>音频 / 歌词流。路径参数已由 ASP.NET Core 做 URL 解码，逐段相对 <c>Media:MusicDirectory</c>。</summary>
    [HttpGet("audio/{**path}")]
    public IActionResult GetAudio(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return NotFound();

        var musicDir = config["Media:MusicDirectory"];
        if (string.IsNullOrWhiteSpace(musicDir)) return NotFound();

        var root = Path.GetFullPath(musicDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, path));
        // 防路径穿越：解析后的绝对路径必须仍位于音乐目录之下
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return NotFound();
        if (!System.IO.File.Exists(full)) return NotFound();

        return PhysicalFile(full, ResolveContentType(full), enableRangeProcessing: true);
    }

    /// <summary>封面流（<c>song-{id}.jpg</c> 等）。目录解析与 AudioMetadataService 保持一致：
    /// 优先 <c>Media:ImageDirectory</c>（Docker bind mount 封面目录），回退 <c>wwwroot/media/image</c>。</summary>
    [HttpGet("image/{fileName}")]
    public IActionResult GetImage(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return NotFound();

        var imageDir = config["Media:ImageDirectory"];
        if (string.IsNullOrWhiteSpace(imageDir))
            imageDir = Path.Combine(env.ContentRootPath, "wwwroot", "media", "image");

        var root = Path.GetFullPath(imageDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, fileName));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return NotFound();
        if (!System.IO.File.Exists(full)) return NotFound();

        return PhysicalFile(full, ResolveContentType(full), enableRangeProcessing: true);
    }

    private static string ResolveContentType(string path) =>
        ContentTypes.TryGetContentType(path, out var contentType) ? contentType : "application/octet-stream";
}