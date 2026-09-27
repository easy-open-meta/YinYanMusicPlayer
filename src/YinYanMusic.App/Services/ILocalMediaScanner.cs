using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.Services;

/// <summary>
/// 扫描结果状态。刻意用枚举而不是抛异常：权限被拒是**正常的用户选择**，
/// 不是程序错误，UI 需要据此给出引导文案（TC-2.6-03）。
/// </summary>
public enum LocalScanStatus
{
    /// <summary>扫描完成（可能有 0 条，那是设备里确实没有音频）。</summary>
    Success,
    /// <summary>存储权限被拒绝。</summary>
    PermissionDenied,
    /// <summary>用户在选择目录/授权过程中主动取消。</summary>
    Cancelled,
    /// <summary>平台不支持或其他错误，<see cref="LocalScanResult.Message"/> 带原因。</summary>
    Failed,
}

/// <summary>一次扫描的结果。</summary>
public record LocalScanResult(
    LocalScanStatus Status,
    /// <summary>本次新入库的条数。</summary>
    int Added = 0,
    /// <summary>设备上发现、但库里已有的条数（去重跳过的）。</summary>
    int Skipped = 0,
    /// <summary>顺带清掉的"文件已不存在"记录数。</summary>
    int Purged = 0,
    /// <summary>解析失败（损坏文件等）的条数，不阻断整体扫描。</summary>
    int Failed = 0,
    /// <summary>面向用户的提示文案（失败/拒绝时必填）。</summary>
    string? Message = null);

/// <summary>
/// 平台相关的本地音频发现能力（V2.6）。
///
/// 两端实现口径差异很大（Android 走 MediaStore 查询，Windows 走文件夹选择器 + 目录枚举），
/// 所以抽象成接口由平台实现；**元数据解析统一走 ATL**（LocalMetadataReader），
/// 保证"同一个文件在两台设备上入库的标题/歌手/专辑/时长一致"（TC-2.6-12）。
/// </summary>
public interface ILocalMediaScanner
{
    /// <summary>
    /// 扫描设备上的本地音频。
    /// <paramref name="onProgress"/> 报告已处理条数（扫描几百个文件时 UI 要有反馈）。
    /// <paramref name="folderOnly"/>
    /// 为 true 时只扫用户**指定目录**（Android 走 SAF 目录授权，Windows 弹文件夹选择器）；
    /// 为 false 时按平台默认行为扫描（Android 扫全设备媒体库，Windows 仍弹选择器）。
    /// </summary>
    Task<LocalScanResult> ScanAsync(IProgress<int>? onProgress = null, CancellationToken ct = default,
                                    bool folderOnly = false);

    /// <summary>
    /// 该平台是否支持"再选一个目录继续扫"。
    /// Windows 是 true（每次都弹文件夹选择器）；Android 也是 true（SAF 目录授权）。
    /// </summary>
    bool SupportsDirectoryPick { get; }

    /// <summary>按钮/引导文案里给用户看的操作说明（两端措辞不同）。</summary>
    string ScanActionDescription { get; }

    /// <summary>
    /// 扫描入口是否需要先让用户选方式（"扫描全部" / "选择文件夹"）。
    /// Android 是 true（两种模式都有意义）；Windows 是 false（只有选文件夹这一种）。
    /// </summary>
    bool SupportsScanModeChoice { get; }
}
