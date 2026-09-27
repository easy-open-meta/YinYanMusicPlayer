using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

/// <summary>入队/下载的最终状态。</summary>
public enum CacheEnqueueStatus
{
    /// <summary>已进入队列并**下载完成**（<see cref="CacheDownloadService.EnqueueAndWaitAsync"/>）
    /// 或已在后台开始下载（<see cref="CacheDownloadService.EnqueueAsync"/>）。</summary>
    Queued,
    /// <summary>本机已有这份缓存，无需重复下载。</summary>
    AlreadyCached,
    /// <summary>被拒绝：不适用（本地歌没有 songId）或没有可用的音频地址。</summary>
    Rejected,
    /// <summary>
    /// 被**容量上限**拒绝（TC-2.7-07）。与 <see cref="Rejected"/> 分开，是因为"播完自动缓存"
    /// 需要据此单独计数（缓存管理页会告诉用户"有 N 首因为超上限没自动缓存"），
    /// 而其它拒绝原因不该混进这个统计。
    /// </summary>
    RejectedNoSpace,
    /// <summary>下载失败（重试用尽）。</summary>
    Failed,
}

/// <summary>一次入队尝试的结果。<see cref="Message"/> 直接面向用户（弹提示/Toast）。</summary>
public sealed record CacheEnqueueResult(CacheEnqueueStatus Status, string Message);

/// <summary>下载进度（<see cref="TotalBytes"/> 为 0 表示服务器没给 Content-Length，百分比未知）。</summary>
public sealed record CacheDownloadProgress(long SongId, long BytesReceived, long TotalBytes)
{
    public double Percent => TotalBytes > 0 ? Math.Clamp(BytesReceived * 100d / TotalBytes, 0, 100) : 0;
}

/// <summary>下载收尾通知（成功/失败都有）。UI 靠它显示"缓存完成/失败"。</summary>
public sealed record CacheDownloadCompleted(long SongId, string Title, bool Success, string Message);

/// <summary>
/// 一首**正在缓存**的歌（缓存管理页「缓存中」标签页 + 通知栏进度都绑它）。
/// 纯展示用，不是数据库实体。
/// </summary>
public sealed record ActiveCacheDownload(
    long SongId,
    string Title,
    string? ArtistName,
    string? AlbumName,
    string? CoverUrl,
    long BytesReceived,
    long TotalBytes)
{
    /// <summary>百分比（0~100）。服务器没给 Content-Length 时为 0（此时看 <see cref="PercentText"/>）。</summary>
    public double Percent => TotalBytes > 0 ? Math.Clamp(BytesReceived * 100d / TotalBytes, 0, 100) : 0;

    /// <summary>给进度条用的 0~1 值。</summary>
    public double ProgressValue => TotalBytes > 0 ? Percent / 100d : 0;

    /// <summary>百分比文案；拿不到总大小时退化为"已下载体积"。</summary>
    public string PercentText => TotalBytes > 0 ? $"{Percent:F0}%" : CachedSong.FormatSize(BytesReceived);

    /// <summary>体积文案：<c>12.4 MB / 45.9 MB</c>（拿不到总值时只显示已下载）。</summary>
    public string SizeText => TotalBytes > 0
        ? $"{CachedSong.FormatSize(BytesReceived)} / {CachedSong.FormatSize(TotalBytes)}"
        : CachedSong.FormatSize(BytesReceived);

    /// <summary>
    /// 「艺术家 · 专辑」副标题 —— 与「缓存完成」列表同一口径（缓存管理页三个标签页样式统一）。
    /// 缺哪个就退化成占位文案，避免出现" · 向日葵"这种空半边。
    /// </summary>
    public string SubtitleText =>
        $"{(string.IsNullOrWhiteSpace(ArtistName) ? "未知艺术家" : ArtistName)} · " +
        $"{(string.IsNullOrWhiteSpace(AlbumName) ? "未知专辑" : AlbumName)}";
}

/// <summary>
/// 歌曲缓存下载服务（V2.7）。
///
/// <para><b>四个硬约束</b>（都来自设计文档 §3.7.2 / §3.7.3）：</para>
/// <list type="number">
/// <item><b>流式写盘</b>：<c>HttpCompletionOption.ResponseHeadersRead</c> 边收边写，
/// 绝不把整首 FLAC 读进内存。</item>
/// <item><b>并发上限 3</b>：用 <see cref="SemaphoreSlim"/> 当闸门，多首一起缓存时同时最多 3 个在跑
/// （TC-2.7-11）。</item>
/// <item><b>断点续传</b>：未完成的文件留在 <c>&lt;songId&gt;.part</c>，续传时带 Range 头从断点接着写
/// （TC-2.7-05）。服务器若忽略 Range 返回 200（整段），则截断重写。</item>
/// <item><b>失败重试</b>：指数退避重试 2 次（共 3 次尝试），失败后保留 <c>.part</c> 供下次续传。</item>
/// </list>
///
/// <para><b>不带鉴权</b>：音频走 <c>/media/audio</c> 静态托管，不需要 token；也因此这里自建
/// <see cref="HttpClient"/> —— DI 里那个单例的 <c>Timeout=30s</c> 对大文件下载太短，
/// 会被整体超时掐断。自建客户端的超时放宽到 20 分钟。</para>
/// </summary>
public sealed class CacheDownloadService
{
    /// <summary>同时下载的最大数量（TC-2.7-11）。</summary>
    public const int MaxConcurrency = 3;

    /// <summary>失败后的重试次数（不含首次尝试）。</summary>
    private const int MaxRetries = 2;

    /// <summary>进度上报节流：每累计这么多字节报一次，避免小包大文件刷爆 UI 线程。</summary>
    private const int ProgressStepBytes = 128 * 1024;

    private readonly CacheStore _store;
    private readonly CacheHistoryStore _history;
    private readonly HttpClient _http;
    private readonly INetworkPolicyService _networkPolicy;
    private readonly SemaphoreSlim _slots = new(MaxConcurrency, MaxConcurrency);

    /// <summary>
    /// 正在下载的歌（songId → 进度快照）。既用于"这首歌是否已在下载中"的去重判断，
    /// 也是「缓存中」标签页与通知栏进度条的数据源。
    /// </summary>
    private readonly ConcurrentDictionary<long, ActiveCacheDownload> _active = new();

    public CacheDownloadService(CacheStore store, CacheHistoryStore history, INetworkPolicyService networkPolicy)
    {
        _store = store;
        _history = history;
        _networkPolicy = networkPolicy;
        _http = new HttpClient
        {
            // 单曲下载不该超过这个时长；真卡死在这里比挂着占满 3 个并发位更好排查
            Timeout = TimeSpan.FromMinutes(20),
        };
    }

    /// <summary>进度变化（可能在后台线程触发，订阅方自己切主线程）。</summary>
    public event EventHandler<CacheDownloadProgress>? ProgressChanged;

    /// <summary>单曲下载收尾（成功或失败）。</summary>
    public event EventHandler<CacheDownloadCompleted>? Completed;

    /// <summary>「正在下载的歌」集合发生变化（入队/出队/进度推进）。</summary>
    public event EventHandler? ActiveDownloadsChanged;

    /// <summary>当前正在下载的歌曲数（用于给用户看"正在缓存 N 首"）。</summary>
    public int ActiveCount => _active.Count;

    /// <summary>正在下载的歌（快照，新的在前 —— 先入队的排后面，视觉上更像队列）。</summary>
    public IReadOnlyList<ActiveCacheDownload> ActiveDownloads => [.. _active.Values];

    /// <summary>这首歌是否正在下载中（避免重复入队）。</summary>
    public bool IsDownloading(long songId) => _active.ContainsKey(songId);

    /// <summary>
    /// 统一的进度出口：更新「缓存中」快照 → 抛进度事件 → 刷新通知栏。
    /// 所有进度更新都必须走这里，否则会出现"列表在动、通知栏不动"这种不一致。
    /// </summary>
    private void ReportProgress(long songId, long bytesReceived, long totalBytes)
    {
        if (_active.TryGetValue(songId, out var current))
        {
            _active[songId] = current with { BytesReceived = bytesReceived, TotalBytes = totalBytes };
            ActiveDownloadsChanged?.Invoke(this, EventArgs.Empty);
        }

        ProgressChanged?.Invoke(this, new CacheDownloadProgress(songId, bytesReceived, totalBytes));
        UpdateNotification();
    }

    /// <summary>把「缓存中」的当前状态同步到系统通知栏（仅 Android 有通知栏）。</summary>
    private void UpdateNotification()
    {
#if ANDROID
        try
        {
            var list = ActiveDownloads;
            if (list.Count == 0)
            {
                CacheNotificationManager.Instance.Clear();
                return;
            }

            // 通知栏只放得下一条：显示"第 1 首 + 总进度"，够表达"正在缓存、大概到哪了"
            var lead = list[0];
            var totalBytes = list.Sum(x => x.TotalBytes);
            var receivedBytes = list.Sum(x => x.BytesReceived);
            var percent = totalBytes > 0 ? Math.Clamp(receivedBytes * 100d / totalBytes, 0, 100) : 0;

            CacheNotificationManager.Instance.Update(
                activeCount: list.Count,
                title: lead.Title,
                percent: percent,
                indeterminate: totalBytes <= 0);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[Cache] 通知栏更新失败：{ex.Message}");
        }
#endif
    }

    /// <summary>正在下载集合有变化（入队/出队）时统一刷新 UI 与通知栏。</summary>
    private void RaiseActiveChanged()
    {
        ActiveDownloadsChanged?.Invoke(this, EventArgs.Empty);
        UpdateNotification();
    }

    /// <summary>
    /// 入队并**等待下载完成**：单曲手动缓存（歌曲菜单）用这个，
    /// 完成后把结果原样弹给用户（TC-2.7-01 的"进度可见 + 完成提示"）。
    /// </summary>
    public async Task<CacheEnqueueResult> EnqueueAndWaitAsync(SongDto song, CancellationToken ct = default)
    {
        var precheck = await PrecheckAsync(song, ct).ConfigureAwait(false);
        if (precheck.Status != CacheEnqueueStatus.Queued) return precheck;

        return await RunWithSlotAsync(song, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 入队后**立刻返回**（后台下载）：批量缓存用这个，返回的只是"能不能下"的预检结论。
    /// 并发闸门保证同时最多 3 个在跑。
    /// </summary>
    /// <param name="silent">
    /// true = 不广播 <see cref="Completed"/>（自动缓存用）：用户只是听完了歌，
    /// 不该被"已缓存 xx"的提示打扰。进度事件照发，缓存管理页仍能看到"正在缓存 N 首"。
    /// </param>
    public async Task<CacheEnqueueResult> EnqueueAsync(SongDto song, CancellationToken ct = default, bool silent = false)
    {
        var precheck = await PrecheckAsync(song, ct).ConfigureAwait(false);
        if (precheck.Status != CacheEnqueueStatus.Queued) return precheck;

        _ = Task.Run(() => RunWithSlotAsync(song, ct, silent), CancellationToken.None);
        return precheck;
    }

    // ── 播完自动缓存（2026-09-22 用户定案的三条策略） ────────────────────────

    /// <summary>自动缓存开始前让切歌先安顿好，避免跟"下一首的缓冲"抢带宽。</summary>
    private const int AutoCacheDelayMs = 2500;

    /// <summary>
    /// 「整首听完后自动缓存」—— 由播放器在 <c>MediaEnded</c> 时调用（**只有完整听完才会走到这里**，
    /// 手动切歌不触发，所以"只存听完的"这条策略由调用时机天然保证）。
    ///
    /// <para>另两条策略在这里实现：</para>
    /// <list type="bullet">
    /// <item><b>只在 WiFi 下自动缓存</b>：流量/以太网/判断不出来时**一律跳过**，绝不偷跑流量；</item>
    /// <item><b>撞上限静默跳过</b>：不弹窗、不打断播放，只在后台累计次数，
    /// 由缓存管理页告诉用户"有 N 首因为超过上限没有自动缓存"（绝不自动删旧缓存）。</item>
    /// </list>
    ///
    /// <para>整体是 fire-and-forget 语义：任何异常都只记日志，绝不冒泡到播放流程。</para>
    /// </summary>
    public async Task TryAutoCacheAsync(SongDto song)
    {
        try
        {
            if (song.IsLocal || song.Id <= 0) return;
            if (!_store.AutoCacheEnabled) return;

            if (!IsOnWifi())
            {
                AppLog.Info($"[AutoCache] 当前不是 WiFi，跳过「{song.Title}」");
                return;
            }

            // V2.16 省流量模式：系统开启 Data Saver 时也跳过自动缓存（用户手动缓存不受限）
            if (_networkPolicy.IsDataSaverEnabled)
            {
                AppLog.Info($"[AutoCache] 系统省流量模式开启，跳过自动缓存「{song.Title}」");
                return;
            }

            try { await Task.Delay(AutoCacheDelayMs).ConfigureAwait(false); }
            catch { /* 取消/中断都无所谓，继续尝试 */ }

            var result = await EnqueueAsync(song, silent: true).ConfigureAwait(false);
            switch (result.Status)
            {
                case CacheEnqueueStatus.Queued:
                    AppLog.Info($"[AutoCache] 已入队自动缓存：「{song.Title}」");
                    break;
                case CacheEnqueueStatus.AlreadyCached:
                    break;
                case CacheEnqueueStatus.RejectedNoSpace:
                    _store.IncrementAutoCacheSkipped();
                    // 也写进历史：用户能在「缓存历史」里看到"哪几首因为超上限没缓存上"，
                    // 而不是只知道一个数字
                    await RecordHistoryAsync(song, CacheHistoryEvent.SkippedByLimit, 0).ConfigureAwait(false);
                    AppLog.Warn($"[AutoCache] 超上限，跳过「{song.Title}」（已累计 {_store.AutoCacheSkippedCount} 首）");
                    break;
                default:
                    AppLog.Warn($"[AutoCache] 跳过「{song.Title}」：{result.Message}");
                    break;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[AutoCache] 失败（不影响播放）：{ex.Message}");
        }
    }

    /// <summary>
    /// 当前是否连在 WiFi 上。**判断不出来就返回 false**（保守）：宁可漏自动缓存，
    /// 也不要因为一次探测异常在移动网络上偷偷下载整首歌。
    /// </summary>
    private static bool IsOnWifi()
    {
        try
        {
            var connectivity = Connectivity.Current;
            return connectivity.NetworkAccess == NetworkAccess.Internet
                   && connectivity.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        }
        catch
        {
            return false;
        }
    }

    // ── 预检：本地歌 / 已缓存 / 正在下载 / 容量上限 ──────────────────────────

    private async Task<CacheEnqueueResult> PrecheckAsync(SongDto song, CancellationToken ct)
    {
        // 本地曲库的歌没有 songId，也不是服务端资源，没有"缓存"这个概念
        if (song.IsLocal || song.Id <= 0)
            return new(CacheEnqueueStatus.Rejected, "本地音乐无需缓存");

        await _store.EnsureIndexAsync().ConfigureAwait(false);

        if (_store.IsCached(song.Id))
            return new(CacheEnqueueStatus.AlreadyCached, $"「{song.Title}」已在本地缓存中");

        if (IsDownloading(song.Id))
            return new(CacheEnqueueStatus.Queued, $"「{song.Title}」正在缓存中…");

        // 容量预检：已用 + 预计大小 > 上限 → 直接拒绝并提示（TC-2.7-07）
        var url = ApiConfig.Absolute(song.AudioUrl);
        if (string.IsNullOrWhiteSpace(url))
            return new(CacheEnqueueStatus.Rejected, "这首歌没有可用的音频地址");

        var expected = await TryGetRemoteLengthAsync(url, ct).ConfigureAwait(false);
        if (expected > 0)
        {
            var used = await _store.TotalBytesAsync().ConfigureAwait(false);
            if (used + expected > _store.LimitBytes)
            {
                return new(CacheEnqueueStatus.RejectedNoSpace,
                    $"缓存空间不足：已用 {CachedSong.FormatSize(used)}，这首还需 {CachedSong.FormatSize(expected)}，" +
                    $"超过上限 {_store.LimitGb:0.#} GB。\n请到「缓存管理 → 缓存设置」清理缓存或调高上限。");
            }
        }

        return new(CacheEnqueueStatus.Queued, $"已开始缓存「{song.Title}」");
    }

    /// <summary>HEAD 探一下远端文件大小。拿不到（服务器不支持 HEAD / 无 Content-Length）返回 0。</summary>
    private async Task<long> TryGetRemoteLengthAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, new Uri(url));
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return 0;
            return resp.Content.Headers.ContentLength ?? 0;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[Cache] 探测远端大小失败（跳过容量预检）: {ex.Message}");
            return 0;
        }
    }

    // ── 并发闸门 + 收尾 ─────────────────────────────────────────────────────

    private async Task<CacheEnqueueResult> RunWithSlotAsync(SongDto song, CancellationToken ct, bool silent = false)
    {
        try
        {
            await _slots.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(CacheEnqueueStatus.Failed, "已取消");
        }

        _active[song.Id] = new ActiveCacheDownload(
            // 歌手传显示文案（全部歌手）：缓存中 / 已缓存 / 缓存历史三处卡片口径一致
            song.Id, song.Title, song.ArtistsDisplay, song.AlbumName, song.CoverUrl, 0, 0);
        RaiseActiveChanged();

        try
        {
            var result = await DownloadWithRetryAsync(song, ct).ConfigureAwait(false);
            if (!silent)
                Completed?.Invoke(this, new CacheDownloadCompleted(
                    song.Id, song.Title, result.Status == CacheEnqueueStatus.Queued, result.Message));
            return result;
        }
        catch (Exception ex)
        {
            AppLog.Error($"[Cache] 下载异常 id={song.Id}", ex);
            if (!silent)
                Completed?.Invoke(this, new CacheDownloadCompleted(song.Id, song.Title, false, $"缓存失败：{ex.Message}"));
            return new(CacheEnqueueStatus.Failed, $"缓存失败：{ex.Message}");
        }
        finally
        {
            _active.TryRemove(song.Id, out _);
            _slots.Release();
            RaiseActiveChanged();   // 「缓存中」少了一首，通知栏进度也随之收尾
        }
    }

    private async Task<CacheEnqueueResult> DownloadWithRetryAsync(SongDto song, CancellationToken ct)
    {
        string? lastError = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await DownloadOnceAsync(song, ct).ConfigureAwait(false);
                return new(CacheEnqueueStatus.Queued, $"已缓存「{song.Title}」");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                AppLog.Warn($"[Cache] 下载失败 id={song.Id} attempt={attempt + 1}/{MaxRetries + 1}: {ex.Message}");

                if (attempt < MaxRetries)
                {
                    // 指数退避：1s、2s。网络刚断时立刻重试基本必然再失败
                    try { await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                }
            }
        }

        // 重试用尽：留一条历史，用户能在「缓存历史」里看到哪些歌一直下不下来
        await RecordHistoryAsync(song, CacheHistoryEvent.Failed, 0).ConfigureAwait(false);
        return new(CacheEnqueueStatus.Failed, $"缓存失败：{lastError ?? "未知错误"}");
    }

    /// <summary>写一条缓存历史（失败也不影响主流程，<see cref="CacheHistoryStore.RecordAsync"/> 内部已兜异常）。</summary>
    private Task RecordHistoryAsync(SongDto song, string eventType, long sizeBytes) =>
        _history.RecordAsync(new CacheHistoryEntry
        {
            SongId = song.Id,
            Title = song.Title,
            ArtistName = song.ArtistsDisplay,   // 全部歌手：与「缓存中 / 已缓存」两张卡片同一口径
            CoverUrl = song.CoverUrl,
            Event = eventType,
            SizeBytes = sizeBytes,
        });

    // ── 单次下载（流式 + 断点续传） ─────────────────────────────────────────

    private async Task DownloadOnceAsync(SongDto song, CancellationToken ct)
    {
        var url = ApiConfig.Absolute(song.AudioUrl);
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("音频地址为空");

        CacheStore.EnsureDirectory();
        var partPath = CacheStore.PartialPath(song.Id);

        // 已有半成品 → 从断点续下去（TC-2.7-05）
        long resumeFrom = 0;
        if (File.Exists(partPath))
        {
            try { resumeFrom = new FileInfo(partPath).Length; }
            catch { resumeFrom = 0; }
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        if (resumeFrom > 0)
            req.Headers.Range = new RangeHeaderValue(resumeFrom, null);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        // 服务器不支持 Range（返回 200 整段）→ 断点作废，从 0 重写
        if (resumeFrom > 0 && resp.StatusCode != HttpStatusCode.PartialContent)
        {
            AppLog.Info($"[Cache] 服务器不支持断点续传，从头下载 id={song.Id}");
            resumeFrom = 0;
        }

        var extension = ResolveExtension(url, resp.Content.Headers.ContentType?.MediaType);
        var finalPath = CacheStore.TargetPath(song.Id, extension);

        var bodyLength = resp.Content.Headers.ContentLength ?? 0;
        var totalBytes = bodyLength > 0 ? bodyLength + resumeFrom : 0;

        AppLog.Info($"[Cache] 开始下载 id={song.Id} resumeFrom={resumeFrom} total={totalBytes} url={url}");

        long written = resumeFrom;
        var lastReported = written;

        await using (var source = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = new FileStream(
                         partPath,
                         resumeFrom > 0 ? FileMode.Append : FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                written += read;

                if (written - lastReported >= ProgressStepBytes)
                {
                    lastReported = written;
                    ReportProgress(song.Id, written, totalBytes);
                }
            }

            await target.FlushAsync(ct).ConfigureAwait(false);
        }

        // 大小明显不符 → 当作失败（保留 .part 供续传；半截文件绝不能进索引）
        if (totalBytes > 0 && written < totalBytes)
            throw new IOException($"下载不完整：{written}/{totalBytes} 字节");

        ReportProgress(song.Id, written, totalBytes);

        // 落定：先删旧文件（比如换了扩展名）再就位，最后才写索引 —— 顺序不能反
        if (File.Exists(finalPath)) TryDelete(finalPath);
        try
        {
            File.Move(partPath, finalPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 同名文件被占用（典型场景：这份缓存正在被播放器读取，Windows 既删不掉也覆盖不了）
            // → 换个不冲突的文件名就位。索引里存的是完整路径，播放侧只认索引，不依赖命名规则。
            AppLog.Warn($"[Cache] 目标文件被占用，改用备用文件名 id={song.Id}: {ex.Message}");
            finalPath = CacheStore.TargetPath(song.Id, $".{DateTime.UtcNow.Ticks}{extension}");
            File.Move(partPath, finalPath, overwrite: true);
        }

        long size;
        try { size = new FileInfo(finalPath).Length; }
        catch { size = written; }

        await _store.AddAsync(new CachedSong
        {
            SongId = song.Id,
            FilePath = finalPath,
            SizeBytes = size,
            CachedAtUtc = DateTime.UtcNow,
            Title = song.Title,
            ArtistName = song.ArtistName,
            // 冗余全部歌手（联合创作才非空）：离线时缓存页与播放页也要能显示合作歌手
            ArtistsText = song.Artists is { Count: > 0 } ? string.Join(" / ", song.Artists.Select(a => a.Name)) : null,
            AlbumName = song.AlbumName,
            CoverUrl = song.CoverUrl,
            AudioUrl = song.AudioUrl,
            DurationSeconds = song.DurationSeconds,
        }).ConfigureAwait(false);

        AppLog.Info($"[Cache] 下载完成 id={song.Id} size={CachedSong.FormatSize(size)} path={finalPath}");

        // 记一条历史（「缓存历史」标签页的数据源）
        await RecordHistoryAsync(song, CacheHistoryEvent.Completed, size).ConfigureAwait(false);
    }

    /// <summary>
    /// 定扩展名：优先取 URL 后缀；取不到就用响应 Content-Type 反推。
    /// 扩展名不只影响观感——Android ExoPlayer 与 Windows 播放器都靠它选解复用器，
    /// 缓存文件一旦没有后缀，播放器可能直接拒播。
    /// </summary>
    private static string ResolveExtension(string url, string? contentType)
    {
        try
        {
            var path = url.Split('?', '#')[0];
            var ext = Path.GetExtension(path);
            if (ext.Length is > 1 and <= 5) return ext.ToLowerInvariant();
        }
        catch { }

        return contentType?.ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/mp4" or "audio/x-m4a" or "audio/aac" => ".m4a",
            "audio/ogg" or "application/ogg" => ".ogg",
            "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
            "audio/opus" => ".opus",
            _ => ".mp3",
        };
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
