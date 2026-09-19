using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController(IMediaService media) : ControllerBase
{
    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<MediaUploadResult>> Upload(IFormFile file, [FromQuery] string kind = "audio")
    {
        var result = await media.UploadAsync(file, kind);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }
}
