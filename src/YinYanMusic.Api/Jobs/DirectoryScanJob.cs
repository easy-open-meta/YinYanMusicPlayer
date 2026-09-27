using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using YinYanMusic.Application;

namespace YinYanMusic.Api.Jobs;

/// <summary>
/// 定时扫描 <c>Media:MusicDirectory</c> 并增量入库（V2.8）。只负责"什么时候跑"，
/// "怎么跑"（含单实例保护）在 <see cref="IScanRunner"/>。
/// <para>
/// 三条硬约束（对应 TC-2.8-05 / 07，以及后台的定时器开关）：
/// <list type="number">
/// <item><b>不阻塞启动</b>：默认启动后先等一个间隔再跑第一轮（<c>Media:ScanOnStartup</c> 可改）。
/// 大目录（1000+ 文件）首轮要跑一会儿，挡在启动路径上会让健康检查拿不到 200 → CI 判定部署失败。</item>
/// <item><b>可被唤醒</b>：间隔与开关都是运行时可改的（见 <see cref="ScanState"/>），
/// 改完调 <see cref="RequestReschedule"/> 立刻重算，不用等当前这一轮睡完。</item>
/// <item><b>优雅停止</b>：停止令牌一路传到数据库写入，不抛未处理异常，已提交的批次保持完整。</item>
/// </list>
/// </para>
/// </summary>
public sealed class DirectoryScanJob(
    IScanRunner runner,
    ScanOptions options,
    ScanState state,
    ILogger<DirectoryScanJob> logger) : BackgroundService
{
    /// <summary>唤醒信号（计数 0/1）：<c>WaitAsync</c> 超时 = 到点了，返回 true = 被要求重算。</summary>
    private readonly SemaphoreSlim _wake = new(0, 1);

    /// <summary>运行时的间隔 / 开关变了，叫醒循环重算下次时间。</summary>
    public void RequestReschedule()
    {
        // 已有未消费的信号就不再堆（信号量上限 1），避免多次改动后连跑好几轮。
        try
        {
            if (_wake.CurrentCount == 0) _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // 并发下可能刚好被别的线程塞满，忽略即可。
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.ScanOnStartup && state.Enabled)
        {
            logger.LogInformation("[目录扫描] 启动时首轮扫描（Media:ScanOnStartup=true）…");
            await RunOnceAsync("startup", stoppingToken);
        }
        else
        {
            logger.LogInformation("[目录扫描] 定时任务{State}：间隔 {Minutes} 分钟，启动后延迟一个间隔再跑首轮。",
                state.Enabled ? "已启用" : "未启用（Media:ScanIntervalMinutes=0，后台仍可手动扫描）",
                state.IntervalMinutes);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // 关闭状态：无限期挂着等唤醒（唤醒后重算，可能变成启用）。
            if (!state.Enabled)
            {
                if (await WaitAsync(Timeout.InfiniteTimeSpan, stoppingToken)) continue;
                break;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, state.IntervalMinutes));
            state.SetNextRun(DateTimeOffset.UtcNow + interval);

            // 被唤醒（改间隔 / 关开关）→ 本轮不扫，回到循环开头重算。
            if (await WaitAsync(interval, stoppingToken)) continue;
            if (stoppingToken.IsCancellationRequested) break;
            if (!state.Enabled) continue;

            await RunOnceAsync("auto", stoppingToken);
        }

        logger.LogInformation("[目录扫描] 定时任务已停止。");
    }

    /// <summary>等到点或等到唤醒。<c>true</c> = 被唤醒（需重算），<c>false</c> = 到点或要停了。</summary>
    private async Task<bool> WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            return await _wake.WaitAsync(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // 进程停止时的正常路径
            return false;
        }
    }

    private async Task RunOnceAsync(string trigger, CancellationToken ct)
    {
        // 结果（含 Busy / 失败）已由 runner 记日志，这里不重复处理。
        await runner.TryRunAsync(trigger, ct);
    }

    public override void Dispose()
    {
        _wake.Dispose();
        base.Dispose();
    }
}
