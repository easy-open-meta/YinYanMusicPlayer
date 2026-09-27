using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 分区详情页（ZonePage）查询串构造。HomeViewModel / ZonesViewModel 两处共用，
/// 参数名与 ZonePage 的 QueryProperty 保持一致，避免漂移。Shell 收端会自动做一次 URL 解码。
/// </summary>
internal static class ZoneQuery
{
    public static string Build(CategoryDto zone) =>
        $"categoryId={zone.Id}" +
        $"&name={Uri.EscapeDataString(zone.Name)}" +
        $"&slogan={Uri.EscapeDataString(zone.Slogan ?? string.Empty)}" +
        $"&colorHex={Uri.EscapeDataString(zone.ColorHex ?? string.Empty)}" +
        $"&icon={Uri.EscapeDataString(zone.IconGlyph ?? string.Empty)}" +
        // 内容类型（both/songs/playlists）：决定详情页展示哪几段。顺带带上，免得多请求一次分区列表。
        // Normalize 兜底：脏值一律当 both —— 宁可多显示一段，也别让专区变成空白页。
        $"&mode={Uri.EscapeDataString(CategoryContentModes.Normalize(zone.ContentMode))}";
}
