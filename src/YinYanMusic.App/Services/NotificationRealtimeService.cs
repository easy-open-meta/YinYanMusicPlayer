using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace YinYanMusic.App.Services;

/// <summary>SignalR 推到 App 的新通知载荷（与服务端 <c>NotificationPush</c> 同形）。</summary>
public record NotificationPush(
    Core.Dtos.NotificationDto Notification,
    int UnreadCount);

/// <summary>
/// V2.15 通知实时通道：登录后连 <c>/hubs/notifications</c>，断线自动重连。
/// 登出断开；未登录时永不建连。
///
/// <para>
/// 为什么单独一个单例而不是塞进 MusicApiService：REST（HttpClient + AuthTokenHandler）
/// 与长连接（Query 字符串带 JWT）是两条完全不同的管道；混在一起会把 BaseAddress 约束
/// 和 token 刷新路径搅复杂。这里只负责连接生命周期 + 转发两个服务端事件。
/// </para>
///
/// <para>
/// 断线窗口里丢掉的实时事件无法重放 —— 重连成功后主动 <c>GetUnreadCount</c> 对齐角标，
/// 列表靠页面自己再拉一次（设计 3.15.2 / TC-2.15-08）。
/// </para>
/// </summary>
public sealed class NotificationRealtimeService : IAsyncDisposable
{
    public const string HubPath = "/hubs/notifications";
    public const string NotificationReceivedEvent = "NotificationReceived";
    public const string UnreadCountChangedEvent = "UnreadCountChanged";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAuthService _auth;
    private readonly ILogger<NotificationRealtimeService>? _logger;
    private HubConnection? _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>收到新通知（服务端推送）。参数：通知 + 该用户当前未读数。</summary>
    public event Action<Core.Dtos.NotificationDto, int>? NotificationReceived;

    /// <summary>未读数变化（已读/全部已读/新通知都会触发）。</summary>
    public event Action<int>? UnreadCountChanged;

    /// <summary>连接状态变化（上 UI 角标或调试用，可不订阅）。</summary>
    public event Action<bool>? ConnectionStateChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public NotificationRealtimeService(IAuthService auth, ILogger<NotificationRealtimeService>? logger = null)
    {
        _auth = auth;
        _logger = logger;
    }

    /// <summary>
    /// 已登录则确保连上；未登录则断开。登录成功、App 启动恢复会话、页面打开时都可调，幂等。
    /// </summary>
    public async Task EnsureConnectedAsync()
    {
        if (!_auth.IsLoggedIn || string.IsNullOrEmpty(_auth.Token))
        {
            await DisconnectAsync();
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_connection is not null && _connection.State != HubConnectionState.Disconnected)
                return; // Connecting / Connected —— 幂等返回

            // 先拆干净旧连接再建新的（token 可能已换号）
            if (_connection is not null)
            {
                try { await _connection.DisposeAsync(); } catch { /* 旧连接尽力释放 */ }
                _connection = null;
            }

            // ⚠️ 不设 BaseAddress（全局约束 6）：每次用当前 ApiConfig.BaseUrl 拼绝对地址，
            // 设置页改完地址后下一次 Ensure 即生效。
            var url = ApiConfig.Absolute(HubPath);
            if (string.IsNullOrEmpty(url)) return;

            var conn = new HubConnectionBuilder()
                .WithUrl(url, o =>
                {
                    // SignalR 标准：WebSocket 握手不带 Authorization 头，JWT 走查询串
                    o.AccessTokenProvider = () => Task.FromResult(_auth.Token);
                })
                .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
                .Build();

            conn.On<JsonElement>(NotificationReceivedEvent, raw =>
            {
                try
                {
                    var push = JsonSerializer.Deserialize<NotificationPush>(raw.GetRawText(), JsonOpts);
                    if (push is null) return;
                    NotificationReceived?.Invoke(push.Notification, push.UnreadCount);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[Notify] 解析 NotificationReceived 失败");
                }
            });

            conn.On<int>(UnreadCountChangedEvent, count =>
            {
                UnreadCountChanged?.Invoke(count);
            });

            conn.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            conn.Reconnected += async _ =>
            {
                ConnectionStateChanged?.Invoke(true);
                // TC-2.15-08：断线窗口里漏掉的实时事件用 REST 对齐（Hub 方法 + 页面列表）
                try
                {
                    var unread = await conn.InvokeAsync<int>("GetUnreadCount");
                    UnreadCountChanged?.Invoke(unread);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[Notify] 重连后拉取未读数失败");
                }
            };

            conn.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            await conn.StartAsync();
            _connection = conn;
            ConnectionStateChanged?.Invoke(true);

            try
            {
                var unread = await conn.InvokeAsync<int>("GetUnreadCount");
                UnreadCountChanged?.Invoke(unread);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Notify] 初始未读数拉取失败");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Notify] SignalR 连接失败（不阻塞业务，下次再试）");
            if (_connection is not null)
            {
                try { await _connection.DisposeAsync(); } catch { /* ignore */ }
                _connection = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>登出 / 关闭时断开。幂等。</summary>
    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_connection is null) return;
            try { await _connection.StopAsync(); } catch { /* 已断或服务端不可达 */ }
            try { await _connection.DisposeAsync(); } catch { /* ignore */ }
            _connection = null;
            ConnectionStateChanged?.Invoke(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _gate.Dispose();
    }
}
