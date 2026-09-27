#if WINDOWS
using Windows.Storage.Pickers;
using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.Services;

/// <summary>
/// Windows 端本地扫描：文件夹选择器 + 目录枚举（V2.6）。
///
/// 为什么不走"全盘自动扫描"：Windows 上枚举整个用户目录（含 OneDrive 占位文件、
/// 大量无关目录）既慢又扰民。让用户显式指定音乐目录，一次选择、后续自动记住
/// （已选目录列表存在 Preferences，下次点扫描直接复用，不用重选）。
///
/// 为什么不走 <c>broadFileSystemAccess</c>：那是打包应用的能力声明，
/// 商店审核麻烦，而本项目 <c>WindowsPackageType=None</c>（非打包）根本用不上。
/// </summary>
public class WindowsLocalMediaScanner(LocalLibraryStore store) : ILocalMediaScanner
{
    /// <summary>与 Scanner / MediaService 保持一致的音频扩展名集合。</summary>
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];

    /// <summary>用户选过的目录（下次扫描默认复用，避免每次都从"此电脑"开始点）。</summary>
    private const string PickedFoldersKey = "local_music_folders";

    public bool SupportsDirectoryPick => true;

    /// <summary>Windows 只有"选文件夹"这一种模式（没有全盘自动扫描的合理语义）。</summary>
    public bool SupportsScanModeChoice => false;

    public string ScanActionDescription =>
        "选择一个文件夹，扫描其中的音频文件（含子目录）";

    public async Task<LocalScanResult> ScanAsync(IProgress<int>? onProgress = null,
                                                CancellationToken ct = default,
                                                bool folderOnly = false)
    {
        try
        {
            var folders = await PickFoldersAsync();
            if (folders.Count == 0)
                return new LocalScanResult(LocalScanStatus.Cancelled, Message: "未选择文件夹，扫描已取消。");

            // 先把"文件早就删了但记录还在"的脏数据清掉，再扫。
            // 否则用户删了歌重扫，列表里仍留着那条失效记录，看着像 bug。
            var purged = await store.PurgeMissingAsync();

            var files = EnumerateAudioFiles(folders);
            if (files.Count == 0)
                return new LocalScanResult(LocalScanStatus.Success, Purged: purged,
                    Message: "所选文件夹里没有找到音频文件。");

            var (added, skipped, failed) = await ImportAsync(files, onProgress, ct);

            return new LocalScanResult(LocalScanStatus.Success, added, skipped, purged, failed,
                Message: BuildSummary(added, skipped, failed, purged));
        }
        catch (Exception ex)
        {
            return new LocalScanResult(LocalScanStatus.Failed, Message: $"扫描失败：{ex.Message}");
        }
    }

    private static string BuildSummary(int added, int skipped, int failed, int purged)
    {
        var parts = new List<string> { $"新入库 {added} 首" };
        if (skipped > 0) parts.Add($"已存在跳过 {skipped} 首");
        if (failed > 0) parts.Add($"解析失败 {failed} 首");
        if (purged > 0) parts.Add($"清理失效记录 {purged} 条");
        return string.Join("，", parts) + "。";
    }

    /// <summary>
    /// 弹文件夹选择器。优先预选上次的目录，让用户"确认一下就能继续"。
    /// </summary>
    private static async Task<IReadOnlyList<string>> PickFoldersAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");   // WinRT 要求至少一个过滤项，否则直接抛异常

        // 非打包（WindowsPackageType=None）应用必须显式把 picker 绑到窗口句柄，
        // 否则 ShowAsync 抛 COMException。这是 WinUI3 桌面应用的已知要求。
        var hwnd = GetMainWindowHandle();
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("拿不到主窗口句柄，无法打开文件夹选择器。");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        // 预选上次选过的目录（有的话）
        var remembered = LoadRememberedFolders();
        if (remembered.Count > 0)
        {
            try
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(remembered[0]);
                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                picker.CommitButtonText = "扫描此文件夹";
                _ = folder;   // SuggestedStartLocation 无法直接指向任意路径，仅用于给用户一个合理起点
            }
            catch { }
        }

        var picked = await picker.PickSingleFolderAsync();
        if (picked is null) return [];

        RememberFolder(picked.Path);
        return [picked.Path];
    }

    /// <summary>从 MAUI 的 WinUI 应用里取主窗口 HWND。</summary>
    private static IntPtr GetMainWindowHandle()
    {
        try
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                return WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        }
        catch { }
        return IntPtr.Zero;
    }

    private static List<string> LoadRememberedFolders()
    {
        try
        {
            var raw = Preferences.Default.Get(PickedFoldersKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return [.. raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Where(Directory.Exists)];
        }
        catch { return []; }
    }

    private static void RememberFolder(string path)
    {
        try
        {
            var list = LoadRememberedFolders();
            if (!list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Insert(0, path);
            Preferences.Default.Set(PickedFoldersKey, string.Join('\n', list.Take(5)));
        }
        catch { }
    }

    /// <summary>递归枚举音频文件。与 Scanner 的 <c>EnumerateAudioFiles</c> 同口径。</summary>
    private static List<string> EnumerateAudioFiles(IReadOnlyList<string> roots)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,     // 碰到没权限的子目录跳过，不要整个扫描崩掉
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        var result = new List<string>();
        foreach (var root in roots)
        {
            try
            {
                result.AddRange(Directory.EnumerateFiles(root, "*", options)
                    .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)));
            }
            catch
            {
                // 单个根目录读不了不影响其他目录
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<(int Added, int Skipped, int Failed)> ImportAsync(
        IReadOnlyList<string> files, IProgress<int>? onProgress, CancellationToken ct)
    {
        var added = 0;
        var skipped = 0;
        var failed = 0;
        var index = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            index++;
            onProgress?.Report(index);

            var fallbackTitle = Path.GetFileNameWithoutExtension(file);
            var meta = LocalMetadataReader.TryRead(file, fallbackTitle);
            if (meta is null)
            {
                failed++;
                continue;
            }

            var song = new LocalSong
            {
                FilePath = file,
                Title = meta.Title,
                ArtistName = meta.ArtistName,
                AlbumName = meta.AlbumName,
                DurationSeconds = meta.DurationSeconds,
                CoverPath = meta.CoverPath,
                DateAddedUtc = DateTime.UtcNow,
                LocalPlayCount = 0,
            };

            // 与 Android 侧一致用 Upsert：早期版本封面抽取有缺陷，重扫时要把封面补上。
            // 已有播放次数与 DateAddedUtc 不受影响。
            var outcome = await store.UpsertAsync(song);
            if (outcome == UpsertOutcome.Inserted) added++;
            else skipped++;
        }

        return (added, skipped, failed);
    }}
#endif
