using Microsoft.Extensions.Logging;

using YinYanMusic.Application;

namespace YinYanMusic.Api.Jobs;

/// <summary>
/// 「扫描类重活」的进程内互斥闸（V2.8）：目录定时扫描、后台手动扫描、老的手动导入、
/// 扫描时长，全都得先过这道闸。
/// <para>
/// 为什么连老接口也要收进来：这些任务都在写同一批表，而 <c>Artists</c> 没有唯一索引、
/// 去重全靠各自在内存里建的字典 —— 两条路径同时跑就会各建一份同名歌手/专辑。
/// 拿不到闸就让调用方**跳过**（返回 409），不排队：排队的下一轮扫的还是同一批文件。
/// </para>
/// <para>当前部署是单容器 / 单机，所以用进程内信号量就够，不做分布式锁。</para>
/// </summary>
public interface IScanGate
{
    /// <summary>拿到闸返回 true；已有任务在跑返回 false（不等待）。</summary>
    Task<bool> TryEnterAsync(string label, CancellationToken ct = default);

    /// <summary>归还闸。必须与成功的 <see cref="TryEnterAsync"/> 配对（放 finally 里）。</summary>
    void Release();
}

public sealed class ScanGate(ScanState state, ILogger<ScanGate> logger) : IScanGate, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>当前持有者的描述，只为日志可读。同一时刻只有一个持有者会写它。</summary>
    private string? _holder;

    public async Task<bool> TryEnterAsync(string label, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            logger.LogWarning("[扫描闸] {Label} 被跳过：{Holder} 仍在进行中。", label, _holder ?? "另一个扫描类任务");
            return false;
        }

        _holder = label;
        // 状态接口的 isRunning 也由闸统一维护：不管是谁在跑，后台都该看到"正在忙"
        // （否则手动导入跑着时，定时轮会以为空闲而进来）。
        state.SetRunning(true);
        return true;
    }

    public void Release()
    {
        _holder = null;
        state.SetRunning(false);
        _gate.Release();
    }

    public void Dispose() => _gate.Dispose();
}
