#if ANDROID
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.Services;

/// <summary>
/// Android 端本地扫描：运行时权限 + <see cref="MediaStore"/> 查询 + SAF 自选目录（V2.6）。
///
/// <b>两种扫描模式</b>：
/// <list type="bullet">
/// <item><b>全盘</b>（默认）：查 <see cref="MediaStore"/>。这是系统维护的媒体索引，
/// 一次查询拿到全设备音频，且返回的 <c>content://</c> URI 在分区存储下照样能播放 ——
/// 比目录遍历快一个量级，也完全合规。</item>
/// <item><b>自选目录</b>：走 SAF（<c>ACTION_OPEN_DOCUMENT_TREE</c>）让用户指定文件夹，
/// 再用 <c>DocumentFile</c> 递归枚举。适合"我只要听某个专辑目录"的场景，
/// 也绕开了"媒体库还没索引到新拷进来的文件"的问题。</item>
/// </list>
///
/// 权限分界：Android 13（API 33）起用 <c>READ_MEDIA_AUDIO</c>，
/// 之前用 <c>READ_EXTERNAL_STORAGE</c>。两个都要在 Manifest 里声明，
/// 运行时按系统版本二选一申请。**自选目录模式不需要这个权限**
/// —— SAF 授权本身就赋予了读取该目录的资格。
/// </summary>
public class AndroidLocalMediaScanner(LocalLibraryStore store) : ILocalMediaScanner
{
    /// <summary>MediaStore 只索引它认识的媒体类型，扩展名过滤是双保险（防第三方 Provider 塞脏数据）。</summary>
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];

    /// <summary>SAF 目录授权的请求码。</summary>
    private const int FolderPickRequestCode = 1003;

    /// <summary>用户选过的目录 URI（持久授权，下次可直接复用）。</summary>
    private const string PickedTreeKey = "local_music_tree_uris";

    public bool SupportsDirectoryPick => true;

    public bool SupportsScanModeChoice => true;

    public string ScanActionDescription =>
        "扫描设备媒体库中的全部音频，或选择某个文件夹";

    public async Task<LocalScanResult> ScanAsync(IProgress<int>? onProgress = null,
                                                CancellationToken ct = default,
                                                bool folderOnly = false)
    {
        try
        {
            if (folderOnly)
                return await ScanPickedFolderAsync(onProgress, ct);

            // 全盘模式需要存储权限；被拒时给出明确引导（TC-2.6-03）
            if (!await EnsurePermissionAsync())
            {
                return new LocalScanResult(LocalScanStatus.PermissionDenied,
                    Message: "没有存储权限，无法扫描本地音乐。请在系统设置里允许「音乐和音频」权限后重试。");
            }

            var purged = await store.PurgeMissingAsync();

            var items = QueryMediaStore();
            if (items.Count == 0)
            {
                return new LocalScanResult(LocalScanStatus.Success, Purged: purged,
                    Message: "设备媒体库里没有找到音频文件。");
            }

            var (added, skipped, failed) = await ImportAsync(items, onProgress, ct);

            return new LocalScanResult(LocalScanStatus.Success, added, skipped, purged, failed,
                Message: BuildSummary(added, skipped, failed, purged));
        }
        catch (System.OperationCanceledException)
        {
            return new LocalScanResult(LocalScanStatus.Cancelled, Message: "扫描已取消。");
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

    // ── SAF 自选目录 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 让用户选一个目录（SAF），再递归枚举其中的音频文件。
    /// SAF 授权对普通目录无需任何运行时权限 —— 用户点了"允许"就是授权。
    /// </summary>
    private async Task<LocalScanResult> ScanPickedFolderAsync(IProgress<int>? onProgress, CancellationToken ct)
    {
        var treeUri = await PickFolderAsync();
        if (treeUri is null)
            return new LocalScanResult(LocalScanStatus.Cancelled, Message: "未选择文件夹，扫描已取消。");

        var purged = await store.PurgeMissingAsync();

        var items = await Task.Run(() => EnumerateFolder(treeUri, ct), ct);
        if (items.Count == 0)
        {
            return new LocalScanResult(LocalScanStatus.Success, Purged: purged,
                Message: "所选文件夹里没有找到音频文件。");
        }

        var (added, skipped, failed) = await ImportAsync(items, onProgress, ct);
        return new LocalScanResult(LocalScanStatus.Success, added, skipped, purged, failed,
            Message: BuildSummary(added, skipped, failed, purged));
    }

    /// <summary>
    /// 弹 SAF 目录选择器。返回被授权的 tree URI；用户取消返回 null。
    /// <para>必须调 <c>TakePersistableUriPermission</c>：否则授权只在本次会话有效，
    /// 重启后 <c>DocumentFile</c> 读不到内容，用户会觉得"选了目录下次又失效了"。</para>
    /// </summary>
    private static Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var activity = Platform.CurrentActivity;
                if (activity is null) { tcs.TrySetResult(null); return; }

                var intent = new Intent(Intent.ActionOpenDocumentTree);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission
                              | ActivityFlags.GrantPersistableUriPermission
                              | ActivityFlags.GrantPrefixUriPermission);

                // ⚠️ 必须显式把起点指到**存储根**。
                // 曾用 INITIAL_URI 指向上次选过的目录想"省一次点击"，真机反馈是
                // "文件夹显示不全，看不到其他文件夹" —— 选择器直接打开在那个目录**内部**，
                // 只列它自己的子项（若该目录只有文件，就一个文件夹都看不到）。
                // 而且光把 INITIAL_URI 去掉也不行：DocumentsUI 自己会记住上次浏览位置。
                // 所以这里主动指定 primary 根，用户每次都能从存储根自由导航。
                try
                {
                    intent.PutExtra("android.provider.extra.INITIAL_URI",
                        Android.Net.Uri.Parse("content://com.android.externalstorage.documents/root/primary"));
                }
                catch { /* 个别 ROM 不认这个 extra，忽略即可（退化为系统默认位置） */ }

                FolderPickResultHandler.Current = new FolderPickResultHandler(tcs);
                activity.StartActivityForResult(intent, FolderPickRequestCode);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("YinYan", $"PickFolder failed: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// 递归枚举 SAF 目录下的音频文件。
    /// 入库的 <c>FilePath</c> 用 document URI —— 它自带授权、能直接播放，
    /// 且比 <c>_data</c> 路径稳定（分区存储下 _data 未必可读）。
    ///
    /// <para>用系统内置的 <see cref="DocumentsContract"/> 而不是 AndroidX 的
    /// <c>DocumentFile</c>：后者要额外引 <c>Xamarin.AndroidX.DocumentFile</c> 包，
    /// 而 DocumentsContract 在 Mono.Android 里就有，功能完全够用（列子项 + 递归）。</para>
    /// </summary>
    private static List<MediaItem> EnumerateFolder(string treeUri, CancellationToken ct)
    {
        var result = new List<MediaItem>();
        try
        {
            var context = global::Android.App.Application.Context;
            var tree = Android.Net.Uri.Parse(treeUri);
            if (tree is null) return result;

            var rootId = DocumentsContract.GetTreeDocumentId(tree);
            if (string.IsNullOrEmpty(rootId)) return result;

            var rootDocUri = DocumentsContract.BuildDocumentUriUsingTree(tree, rootId);
            if (rootDocUri is null) return result;

            Walk(context, tree, rootDocUri, result, ct);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYan", $"EnumerateFolder failed: {ex.Message}");
        }
        return result;
    }

    /// <summary>递归遍历一个 document 目录。</summary>
    private static void Walk(global::Android.Content.Context context, Android.Net.Uri tree,
                             Android.Net.Uri dirDocUri, List<MediaItem> sink, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // 列子项：把 dirDocUri 的 documentId 拆出来，换成 children URI
        var dirId = DocumentsContract.GetDocumentId(dirDocUri);
        if (string.IsNullOrEmpty(dirId)) return;
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, dirId);
        if (childrenUri is null) return;

        string[] projection =
        [
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnMimeType,
        ];

        var subDirs = new List<Android.Net.Uri>();

        using (var cursor = context.ContentResolver?.Query(childrenUri, projection, null, null, null))
        {
            if (cursor is null) return;

            var idCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
            var nameCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            var mimeCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnMimeType);

            while (cursor.MoveToNext())
            {
                ct.ThrowIfCancellationRequested();

                var docId = idCol >= 0 ? cursor.GetString(idCol) : null;
                var name = nameCol >= 0 ? cursor.GetString(nameCol) : null;
                var mime = mimeCol >= 0 ? cursor.GetString(mimeCol) : null;
                if (string.IsNullOrEmpty(docId) || string.IsNullOrEmpty(name)) continue;

                // 子目录：记下来，等游标关闭后再递归（游标未关时递归查询同一 provider 可能出错）
                if (mime == DocumentsContract.Document.MimeTypeDir)
                {
                    var subUri = DocumentsContract.BuildDocumentUriUsingTree(tree, docId);
                    if (subUri is not null) subDirs.Add(subUri);
                    continue;
                }

                var ext = Path.GetExtension(name);
                if (!AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var fileUri = DocumentsContract.BuildDocumentUriUsingTree(tree, docId);
                if (fileUri is null) continue;

                // SAF 没有 MediaStore 那样的标签列，标题先用文件名，稍后由 ATL 补全
                sink.Add(new MediaItem(fileUri.ToString()!, name, null, null, null, 0));
            }
        }

        foreach (var sub in subDirs)
        {
            ct.ThrowIfCancellationRequested();
            Walk(context, tree, sub, sink, ct);
        }
    }

    private static void RememberTree(string treeUri)
    {
        // 只做记录（便于排查"上次扫的是哪个目录"），**不再用于预选起点**：
        // 预选会让选择器打开在该目录内部、看不到其他文件夹（真机反馈）。
        try { Preferences.Default.Set(PickedTreeKey, treeUri); } catch { }
    }

    // ── 权限 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 按系统版本申请对应权限。
    /// 用户拒绝时返回 false（**不抛异常**）——由 UI 给出引导文案，符合 TC-2.6-03。
    /// </summary>
    private static async Task<bool> EnsurePermissionAsync()
    {
        var permission = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            ? global::Android.Manifest.Permission.ReadMediaAudio
            : global::Android.Manifest.Permission.ReadExternalStorage;

        // MAUI 的 Permissions.StorageRead 在 API 33+ 上映射的仍是 READ_EXTERNAL_STORAGE，
        // 对 READ_MEDIA_AUDIO 的判断不可靠，所以这里直接查原生权限状态。
        if (CheckNativePermission(permission)) return true;
        return await RequestNativePermissionAsync(permission);
    }

    private static bool CheckNativePermission(string permission) =>
        global::Android.App.Application.Context.CheckSelfPermission(permission) == Permission.Granted;

    /// <summary>
    /// 运行时申请单条权限。除了本地扫描（读媒体），「保存封面」在 Android 9 及以下
    /// 写公共 Download 时也走这里 —— 复用同一个桥接器（同一时刻只有一次申请）。
    /// </summary>
    internal static Task<bool> RequestNativePermissionAsync(string permission)
    {
        var tcs = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var activity = Platform.CurrentActivity;
                if (activity is null)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                PermissionRequestHandler.Current = new PermissionRequestHandler(tcs);
                activity.RequestPermissions([permission], PermissionRequestHandler.RequestCode);
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });

        return tcs.Task;
    }

    // ── MediaStore 查询 ───────────────────────────────────────────────────────

    /// <summary>一条待导入的媒体记录。</summary>
    private record MediaItem(string ContentUri, string DisplayName, string? Title, string? Artist, string? Album, int DurationMs);

    /// <summary>
    /// 查 <c>MediaStore.Audio.Media</c> 里所有 <c>IS_MUSIC</c> 的条目。
    /// 只查音乐（排除铃声/通知音/录音），避免把系统提示音也扫进来。
    /// </summary>
    private static List<MediaItem> QueryMediaStore()
    {
        var result = new List<MediaItem>();
        var context = global::Android.App.Application.Context;
        var collection = Build.VERSION.SdkInt >= BuildVersionCodes.Q
            ? MediaStore.Audio.Media.GetContentUri(MediaStore.VolumeExternal)
            : MediaStore.Audio.Media.ExternalContentUri;

        string[] projection =
        [
            MediaStore.Audio.Media.InterfaceConsts.Id,
            MediaStore.Audio.Media.InterfaceConsts.DisplayName,
            MediaStore.Audio.Media.InterfaceConsts.Title,
            MediaStore.Audio.Media.InterfaceConsts.Artist,
            MediaStore.Audio.Media.InterfaceConsts.Album,
            MediaStore.Audio.Media.InterfaceConsts.Duration,
        ];

        // 只要音乐（IS_MUSIC != 0）
        // 只要音乐：IS_MUSIC != 0。
        // ⚠️ 同时接受 IS_MUSIC IS NULL —— 刚拷进设备的文件在媒体库还没解析完时
        // 这个列会是 NULL（真机实测：adb push 进去的文件全是 NULL，被原本的
        // "IS_MUSIC != 0" 过滤掉了，用户看到"扫描不到我刚放进来的歌"）。
        // 安全性由下面的扩展名白名单保证，不会把铃声/通知音扫进来。
        const string selection =
            MediaStore.Audio.Media.InterfaceConsts.IsMusic + " != 0 OR " +
            MediaStore.Audio.Media.InterfaceConsts.IsMusic + " IS NULL";

        using var cursor = context.ContentResolver?.Query(
            collection, projection, selection, null,
            MediaStore.Audio.Media.InterfaceConsts.Title + " ASC");

        if (cursor is null) return result;

        var idCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Id);
        var nameCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.DisplayName);
        var titleCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Title);
        var artistCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Artist);
        var albumCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Album);
        var durationCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Duration);

        while (cursor.MoveToNext())
        {
            var id = idCol >= 0 ? cursor.GetLong(idCol) : 0;
            if (id <= 0) continue;

            var displayName = nameCol >= 0 ? cursor.GetString(nameCol) : null;
            // 扩展名双保险：MediaStore 偶尔会把非音频文件也标成 IS_MUSIC
            var ext = Path.GetExtension(displayName ?? string.Empty);
            if (!AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

            // 用 MediaStore 的稳定 content:// URI 而不是 _data 路径：
            // 分区存储下 _data 可能不可读，而这个 URI 可以直接喂给 ExoPlayer。
            var contentUri = global::Android.Content.ContentUris.WithAppendedId(collection!, id)?.ToString();
            if (string.IsNullOrEmpty(contentUri)) continue;

            result.Add(new MediaItem(
                contentUri,
                displayName ?? string.Empty,
                titleCol >= 0 ? cursor.GetString(titleCol) : null,
                artistCol >= 0 ? cursor.GetString(artistCol) : null,
                albumCol >= 0 ? cursor.GetString(albumCol) : null,
                durationCol >= 0 ? cursor.GetInt(durationCol) : 0));
        }

        return result;
    }

    // ── 导入 ─────────────────────────────────────────────────────────────────

    private async Task<(int Added, int Skipped, int Failed)> ImportAsync(
        IReadOnlyList<MediaItem> items, IProgress<int>? onProgress, CancellationToken ct)
    {
        var added = 0;
        var skipped = 0;
        var failed = 0;
        var index = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            index++;
            onProgress?.Report(index);

            // ATL 只吃文件路径（读不了 content:// 流），先落地成临时文件再解析（解析完即删）。
            // ⚠️ displayName 必须传：MediaStore 的 URI 末段是纯数字 ID，
            // 不传就没有扩展名 → 旧实现（TagLib）按扩展名判格式会整个失败（真机实测踩到）。
            // ATL 靠内容嗅探、没扩展名也能读，但这里仍传着它 —— 多一层保险，且日志里看得出是什么文件。
            var fallbackTitle = !string.IsNullOrWhiteSpace(item.Title)
                ? item.Title!
                : Path.GetFileNameWithoutExtension(item.DisplayName);

            var meta = LocalMetadataReader.TryReadContentUri(item.ContentUri, fallbackTitle, item.DisplayName);

            // 标签解析失败时不算"解析失败"：MediaStore 本身已经给了标题/歌手/专辑/时长，
            // 足够入库并正常播放，只是没有内嵌封面。宁可降级入库，也不要让用户看到一堆失败。
            var song = new LocalSong
            {
                FilePath = item.ContentUri,
                Title = meta?.Title ?? fallbackTitle,
                ArtistName = meta?.ArtistName ?? NullIfUnknown(item.Artist),
                AlbumName = meta?.AlbumName ?? NullIfUnknown(item.Album),
                DurationSeconds = meta?.DurationSeconds > 0
                    ? meta.DurationSeconds
                    : (int)(item.DurationMs / 1000),
                CoverPath = meta?.CoverPath,
                DateAddedUtc = DateTime.UtcNow,
                LocalPlayCount = 0,
            };

            // 用 Upsert 而不是 AddIfAbsent：老记录可能是在"封面抽取失效"的版本里入库的
            // （CoverPath 为空），重扫时要把封面补上。已有播放次数等字段不受影响。
            var outcome = await store.UpsertAsync(song);
            if (outcome == UpsertOutcome.Inserted) added++;
            else skipped++;
        }

        return (added, skipped, failed);
    }

    /// <summary>MediaStore 对未知标签会返回 <c>"&lt;unknown&gt;"</c> 字面量，转成 null 更干净。</summary>
    private static string? NullIfUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "<unknown>" ? null : value;

    /// <summary>
    /// 接收 <c>RequestPermissions</c> 结果的桥接器。
    /// MainActivity 的 <c>OnRequestPermissionsResult</c> 会把结果转发到这里。
    /// </summary>
    internal sealed class PermissionRequestHandler(TaskCompletionSource<bool> tcs)
    {
        public const int RequestCode = 1002;

        /// <summary>当前等待中的请求。同一时刻只会有一次权限申请。</summary>
        public static PermissionRequestHandler? Current;

        public void OnResult(string[] permissions, Permission[] grantResults)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            Current = null;
            tcs.TrySetResult(granted);
        }
    }

    /// <summary>
    /// 接收 SAF 目录选择结果的桥接器。MainActivity 的 <c>OnActivityResult</c> 会转发到这里。
    /// </summary>
    internal sealed class FolderPickResultHandler(TaskCompletionSource<string?> tcs)
    {
        /// <summary>当前等待中的请求。</summary>
        public static FolderPickResultHandler? Current;

        /// <summary>是否由本处理器消费了这次结果（避免影响其他 ActivityResult 逻辑）。</summary>
        public bool Handles(int requestCode) => requestCode == FolderPickRequestCode;

        public void OnResult(global::Android.App.Result resultCode, Intent? data)
        {
            Current = null;
            if (resultCode != global::Android.App.Result.Ok || data?.Data is null)
            {
                tcs.TrySetResult(null);
                return;
            }

            var uri = data.Data;
            try
            {
                // 持久化授权：不调这个的话，重启后这个 URI 就读不到了
                var context = global::Android.App.Application.Context;
                context.ContentResolver?.TakePersistableUriPermission(
                    uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPrefixUriPermission);
                RememberTree(uri.ToString()!);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("YinYan", $"TakePersistableUriPermission failed: {ex.Message}");
            }

            tcs.TrySetResult(uri.ToString());
        }
    }
}
#endif
