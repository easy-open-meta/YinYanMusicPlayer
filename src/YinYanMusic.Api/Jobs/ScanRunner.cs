using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using YinYanMusic.Application;

namespace YinYanMusic.Api.Jobs;

/// <summary>一次扫描触发的结局。</summary>
public enum ScanRunStatus
{
    /// <summary>跑完了（成功或失败由 <c>Snapshot.Succeeded</c> 表达，比如目录不存在）。</summary>
    Completed,

    /// <summary>已有扫描在跑，本次被跳过（单实例保护）。</summary>
    Busy,

    /// <summary>连扫描都没跑起来（依赖注入/环境级异常）。</summary>
    Failed
}

public sealed record ScanRunOutcome(ScanRunStatus Status, ScanSnapshot? Snapshot = null, string? Error = null);

/// <summary>
/// 目录扫描的执行入口（V2.8）：定时任务、后台「立即扫描」、后台「执行导入」都走这里，
/// 单实例保护由 <see cref="IScanGate"/> 提供（扫描时长同样过那道闸）。
/// </summary>
public interface IScanRunner
{
    /// <summary>
    /// 尝试跑一轮。拿不到执行权时返回 <see cref="ScanRunStatus.Busy"/>，不阻塞等待。
    /// <paramref name="dir"/> 留空 = 扫配置里的 <c>Media:MusicDirectory</c>；后台「执行导入」用它指定目录。
    /// </summary>
    Task<ScanRunOutcome> TryRunAsync(string trigger, CancellationToken ct = default, string? dir = null);
}

public sealed class ScanRunner(
    IServiceScopeFactory scopeFactory,
    IScanGate gate,
    ILogger<ScanRunner> logger) : IScanRunner
{
    public async Task<ScanRunOutcome> TryRunAsync(string trigger, CancellationToken ct = default, string? dir = null)
    {
        if (!await gate.TryEnterAsync($"目录扫描（{trigger}）", ct))
            return new ScanRunOutcome(ScanRunStatus.Busy);

        try
        {
            // 每轮一个独立 scope：DbContext 是 scoped，BackgroundService / 控制器单例都不能直接吃它。
            await using var scope = scopeFactory.CreateAsyncScope();
            var scan = scope.ServiceProvider.GetRequiredService<IDirectoryScanService>();
            var snapshot = await scan.ScanAsync(trigger, dir, ct);
            return new ScanRunOutcome(ScanRunStatus.Completed, snapshot);
        }
        catch (OperationCanceledException)
        {
            // 进程停止时的正常路径，不算失败。
            logger.LogInformation("[目录扫描] 本轮在进程停止时被取消（触发来源：{Trigger}）。", trigger);
            return new ScanRunOutcome(ScanRunStatus.Completed);
        }
        catch (Exception ex)
        {
            // 兜底：一轮扫描出错绝不能把后台服务带崩 —— 否则定时器就此死掉，
            // 表现为"新歌再也不会自动入库"，而日志里只有一条启动时的信息。
            logger.LogError(ex, "[目录扫描] 本轮异常终止：{Message}", ex.Message);
            return new ScanRunOutcome(ScanRunStatus.Failed, null, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }
}
