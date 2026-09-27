using System.Text.Json.Serialization;

namespace YinYanMusic.Application;

/// <summary>
/// 定时扫描任务的运行时状态（V2.8）。
/// <para>
/// 只描述"这一台 API 进程"的定时器：当前部署是单容器 / 单机，不做分布式协调。
/// 存在进程内存里 —— 重启后归零是刻意的，重启后"上一轮结果"已不代表任何东西，
/// 不该让后台页面展示重启前的陈旧数据。
/// </para>
/// </summary>
public sealed class ScanState
{
    private readonly object _lock = new();

    public ScanState(bool enabled, int intervalMinutes)
    {
        Enabled = enabled;
        IntervalMinutes = intervalMinutes;
        Current = new ScanSnapshot(null, null, null, 0, 0, 0, 0, null, false, 0, enabled, intervalMinutes, 0);
    }

    /// <summary>定时器是否启用（间隔 &gt; 0 且未被运行时关闭）。</summary>
    public bool Enabled { get; private set; }

    /// <summary>扫描间隔（分钟）。0 = 关闭。</summary>
    public int IntervalMinutes { get; private set; }

    /// <summary>下一次自动扫描的时间（UTC，前端自行换算本地时区）。</summary>
    public DateTimeOffset? NextRunAtUtc { get; private set; }

    /// <summary>是否正在扫描（单实例保护的可观测面）。</summary>
    public bool IsRunning { get; private set; }

    /// <summary>从进程启动起已完成的轮次。</summary>
    public int RunCount { get; private set; }

    public ScanSnapshot Current { get; private set; }

    /// <summary>启动时按配置登记一次；后台「开关」也只是改这两个标记。</summary>
    public void Configure(bool enabled, int intervalMinutes)
    {
        lock (_lock)
        {
            Enabled = enabled;
            IntervalMinutes = intervalMinutes;
            if (!enabled) NextRunAtUtc = null;
            Current = Current with { Enabled = enabled, IntervalMinutes = intervalMinutes };
        }
    }

    public void SetNextRun(DateTimeOffset? nextRunAtUtc)
    {
        lock (_lock) NextRunAtUtc = nextRunAtUtc;
    }

    public void SetRunning(bool running)
    {
        lock (_lock) IsRunning = running;
    }

    /// <summary>一轮扫描（无论成败）结束后登记结果。</summary>
    public ScanSnapshot Complete(ScanSnapshot snapshot)
    {
        lock (_lock)
        {
            RunCount = snapshot.RunCount;
            Current = snapshot;
            return snapshot;
        }
    }
}

/// <summary>一次扫描的结果快照：既是后台状态接口的返回体，也在日志里复用。</summary>
/// <param name="Trigger">触发来源：<c>auto</c> 定时 / <c>manual</c> 后台立即扫描 / <c>startup</c> 启动首轮。</param>
public record ScanSnapshot(
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? Trigger,
    int Total,
    int Imported,
    int Updated,
    int Failed,
    string? Error,
    bool Succeeded,
    int RunCount = 0,
    bool Enabled = false,
    int IntervalMinutes = 0,
    long DurationMs = 0)
{
    /// <summary>跳过的文件数（已存在且无需修正）。</summary>
    [JsonIgnore]
    public int Skipped => Math.Max(Total - Imported - Failed, 0);
}

/// <summary>
/// 定时扫描配置（配置节 <c>Media</c>）。与 <c>AuthOptions</c> / <c>SmtpOptions</c> 同款：
/// 由 Program.cs 绑一次、单例注入，不给 Application 层加 <c>IOptions</c> 依赖。
/// 环境变量写法：<c>Media__ScanIntervalMinutes</c>。
/// </summary>
public sealed class ScanOptions
{
    /// <summary>扫描间隔（分钟），默认 60；<c>0</c> 或负数 = 关闭定时任务。</summary>
    public int ScanIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 是否在启动时立刻跑第一轮（默认 false）。
    /// 默认关是刻意的：大目录（1000+ 文件）会拖慢启动，CI 的健康检查在首轮结束前
    /// 拿不到 200 → 部署判定失败（TC-2.8-05）。
    /// </summary>
    public bool ScanOnStartup { get; set; }

    /// <summary>
    /// 是否递归子目录（默认 true）。运维往挂载目录丢歌时通常按专辑分子目录；
    /// 关掉则只扫根目录一层（不递归子目录）。
    /// </summary>
    public bool ScanRecursive { get; set; } = true;

    /// <summary>间隔 &gt; 0 才算启用。</summary>
    public bool IsEnabled => ScanIntervalMinutes > 0;
}
