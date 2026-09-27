using System.Net;
using System.Net.Http;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

/// <summary>
/// 离线播放补报调度器（V2.11）：把本地队列里"离线听过的缓存歌"批量补交到服务端。
///
/// <para><b>触发时机</b>（设计文档 §3.11.2）：App 启动、网络恢复联网、每次播放结束后各尝试一次。
/// 失败按**指数退避**递增间隔（30s → 1m → 2m → 4m → …封顶 30 分钟），不给服务端施压（TC-2.11-07）；
/// 连续失败留到下一次触发，队列里的数据不丢（TC-2.11-06）。</para>
///
/// <para><b>结果处置</b>：accepted / duplicated / unknownSongs 三类都从本地队列删除、**不重试**——
/// duplicated 重交只会永远拿到重复（TC-2.11-02），unknown 的歌已从曲库消失（TC-2.11-03）。</para>
///
/// <para><b>账号维度</b>（3.11.6）：只提交属于当前登录账号的行；没登录就什么都不做，
/// 绝不"顺手"把别人的离线记录报上来（TC-2.11-11）。</para>
/// </summary>
public sealed class PlayReportFlusher
{
    /// <summary>单次提交的批量大小（200/批，服务端上限 500）。</summary>
    public const int BatchSize = 200;

    /// <summary>失败退避的起始与封顶间隔。</summary>
    private static readonly TimeSpan BackoffStart = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackoffMax = TimeSpan.FromMinutes(30);

    private readonly IMusicApi _api;
    private readonly IAuthService _auth;
    private readonly PendingPlayReportStore _store;

    private DateTime _nextAllowedAt = DateTime.MinValue;
    private int _consecutiveFailures;
    private bool _hooked;

    public PlayReportFlusher(IMusicApi api, IAuthService auth, PendingPlayReportStore store)
    {
        _api = api;
        _auth = auth;
        _store = store;
    }

    /// <summary>挂网络恢复监听（只挂一次；播放结束等其余触发由调用方直接调 <see cref="FlushAsync"/>）。</summary>
    public void Start()
    {
        if (_hooked) return;
        _hooked = true;

        Connectivity.ConnectivityChanged += (_, args) =>
        {
            // 只有"从断到通"才值得跑一趟；断网事件交给下一次触发
            if (args.NetworkAccess == NetworkAccess.Internet)
                _ = FlushAsync("网络恢复");
        };
    }

    /// <summary>
    /// 尝试补报一轮。任何调用点都可以无脑 fire-and-forget：内部自己兜全部异常、
    /// 自己做退避节流（正在退避期内直接跳过，不打服务端）。
    /// </summary>
    public async Task FlushAsync(string trigger)
    {
        try
        {
            // 未登录：本地缓存歌没有 songId 的根本不会入队，这里没有可上报的主体
            var userId = _auth.CurrentUser?.Id ?? 0;
            if (userId <= 0) return;

            // 退避期内不发起（TC-2.11-07）
            if (DateTime.UtcNow < _nextAllowedAt) return;

            // 只报当前账号的行（3.11.6 账号维度）
            var batch = await _store.TakeBatchAsync(userId, BatchSize);
            if (batch.Count == 0) return;

            PlayReportsAck ack;
            try
            {
                ack = await _api.ReportPlaysAsync(batch.Select(r => new PlayReportRequest(
                    r.SongId, r.ClientReportKey, r.PlayedAtUtc, r.PositionSeconds)).ToList());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or HttpIOException)
            {
                // 网络/超时：队列保留，退避重试（TC-2.11-06）
                OnFailure(ex);
                AppLog.Warn($"[补报] 触发（{trigger}）失败，已保留 {batch.Count} 条，退避至 {_nextAllowedAt:HH:mm:ss}");
                return;
            }

            ResetBackoff();
            AppLog.Info($"[补报] 触发（{trigger}）成功：accepted={ack.Accepted} duplicated={ack.Duplicated} unknown={ack.UnknownSongs.Count}");

            // 三类结果统一删除、不重试
            var handled = batch.Select(r => r.ClientReportKey).ToList();
            await _store.DeleteByKeysAsync(handled);
        }
        catch (Exception ex)
        {
            // 兜底：调度器绝不能把异常抛进 fire-and-forget 的黑洞
            AppLog.Warn($"[补报] 触发（{trigger}）异常: {ex.Message}");
        }
    }

    private void OnFailure(Exception _)
    {
        _consecutiveFailures++;
        var seconds = BackoffStart.TotalSeconds * Math.Pow(2, _consecutiveFailures - 1);
        if (seconds > BackoffMax.TotalSeconds) seconds = BackoffMax.TotalSeconds;
        _nextAllowedAt = DateTime.UtcNow.AddSeconds(seconds);
    }

    private void ResetBackoff()
    {
        _consecutiveFailures = 0;
        _nextAllowedAt = DateTime.MinValue;
    }
}
