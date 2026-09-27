using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Tests.Application;

/// <summary>
/// 站内通知（V2.15）的发送范围物化、已读语义与广播调用。
/// 用 SQLite in-memory 跑真实 EF 管线：这一版的风险集中在"接收行到底给谁建了、
/// 已读计数算不算得对"，mock 掉 DbContext 就白测了。
///
/// <para>注意：后台关键字搜索走 PostgreSQL 的 ILike，SQLite 无法翻译，因此不在这里测
/// （与 RecommendServiceTests 同口径，改由本地 API + PostgreSQL 端到端验证）。</para>
/// </summary>
public class NotificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MusicDbContext _db;
    private readonly RecordingBroadcaster _broadcaster = new();
    private readonly NotificationService _service;

    private long _adminId;
    private long _userA;
    private long _userB;
    private long _disabledUser;

    public NotificationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>().UseSqlite(_connection).Options;
        _db = new MusicDbContext(options);
        _db.Database.EnsureCreated();

        Seed();
        _service = new NotificationService(_db, _broadcaster);
    }

    private void Seed()
    {
        _db.Users.Add(new User { UserName = "admin", DisplayName = "管理员", PasswordHash = "x", Role = "admin" });
        _db.Users.Add(new User { UserName = "ua", DisplayName = "用户甲", PasswordHash = "x", Role = "user" });
        _db.Users.Add(new User { UserName = "ub", DisplayName = "用户乙", PasswordHash = "x", Role = "user" });
        _db.Users.Add(new User { UserName = "uc", DisplayName = "停用者", PasswordHash = "x", Role = "user", IsDisabled = true });
        _db.SaveChanges();

        _adminId = _db.Users.First(u => u.UserName == "admin").Id;
        _userA = _db.Users.First(u => u.UserName == "ua").Id;
        _userB = _db.Users.First(u => u.UserName == "ub").Id;
        _disabledUser = _db.Users.First(u => u.UserName == "uc").Id;
    }

    private sealed class RecordingBroadcaster : INotificationBroadcaster
    {
        public List<(long UserId, string Title, int Unread)> Pushes { get; } = [];
        public List<(long UserId, int Unread)> Counts { get; } = [];

        public Task PushNotificationAsync(long userId, NotificationDto notification, int unreadCount)
        {
            Pushes.Add((userId, notification.Title, unreadCount));
            return Task.CompletedTask;
        }

        public Task PushUnreadCountAsync(long userId, int unreadCount)
        {
            Counts.Add((userId, unreadCount));
            return Task.CompletedTask;
        }
    }

    private static SendNotificationRequest Req(
        string title, string content, string target, params long[] recipients) =>
        new(title, content, target, recipients.Length == 0 ? null : recipients);

    [Fact]
    public async Task AdminSend_All_ReachesOnlyEnabledUsers()
    {
        var result = await _service.AdminSendAsync(_adminId,
            Req("系统维护", "今晚 23:00 维护", NotificationTargets.All));

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.RecipientCount);   // admin + ua + ub，停用者排除

        var rows = await _db.NotificationRecipients.Select(r => r.UserId).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.DoesNotContain(_disabledUser, rows);
    }

    [Fact]
    public async Task AdminSend_Single_OnlyTargetGetsRow()
    {
        var result = await _service.AdminSendAsync(_adminId,
            Req("私信", "给你单独看", NotificationTargets.Single, _userA));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.RecipientCount);

        var rows = await _db.NotificationRecipients.ToListAsync();
        Assert.Single(rows);
        Assert.Equal(_userA, rows[0].UserId);
    }

    [Fact]
    public async Task AdminSend_Single_WithMultipleIds_Rejected()
    {
        var result = await _service.AdminSendAsync(_adminId,
            Req("私信", "给你单独看", NotificationTargets.Single, _userA, _userB));

        Assert.False(result.Success);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task AdminSend_Partial_ReachesExactlyGivenUsers()
    {
        var result = await _service.AdminSendAsync(_adminId,
            Req("回归测试", "请两位参考", NotificationTargets.Partial, _userA, _userB));

        Assert.True(result.Success);
        var rows = await _db.NotificationRecipients.Select(r => r.UserId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(_userA, rows);
        Assert.Contains(_userB, rows);
    }

    [Fact]
    public async Task AdminSend_DisabledRecipient_RejectedAsNoValidTarget()
    {
        var result = await _service.AdminSendAsync(_adminId,
            Req("定向", "给停用者", NotificationTargets.Single, _disabledUser));

        // 停用账号不可达 → 视为"没有有效接收人"
        Assert.False(result.Success);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task AdminSend_EmptyTitleOrContent_Rejected()
    {
        var noTitle = await _service.AdminSendAsync(_adminId, Req("   ", "正文", NotificationTargets.All));
        Assert.False(noTitle.Success);

        var noContent = await _service.AdminSendAsync(_adminId, Req("标题", "", NotificationTargets.All));
        Assert.False(noContent.Success);

        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task AdminSend_OverlongTitleOrContent_Rejected()
    {
        var longTitle = await _service.AdminSendAsync(_adminId,
            Req(new string('标', NotificationService.MaxTitleLength + 1), "正文", NotificationTargets.All));
        Assert.False(longTitle.Success);

        var longContent = await _service.AdminSendAsync(_adminId,
            Req("标题", new string('文', NotificationService.MaxContentLength + 1), NotificationTargets.All));
        Assert.False(longContent.Success);

        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task AdminSend_InvalidTarget_Rejected()
    {
        var result = await _service.AdminSendAsync(_adminId, Req("标题", "正文", "everyone"));

        Assert.False(result.Success);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task AdminSend_PushesToEveryRecipient_WithUnreadCount()
    {
        _broadcaster.Pushes.Clear();
        await _service.AdminSendAsync(_adminId,
            Req("推送", "实时收", NotificationTargets.Partial, _userA, _userB));

        Assert.Equal(2, _broadcaster.Pushes.Count);
        Assert.All(_broadcaster.Pushes, p => Assert.Equal(1, p.Unread));
        Assert.Contains(_broadcaster.Pushes, p => p.UserId == _userA);
        Assert.Contains(_broadcaster.Pushes, p => p.UserId == _userB);
    }

    [Fact]
    public async Task List_UnreadFirst_ThenNewerFirst()
    {
        await _service.AdminSendAsync(_adminId, Req("第一条", "m1", NotificationTargets.Single, _userA));
        await _service.AdminSendAsync(_adminId, Req("第二条", "m2", NotificationTargets.Single, _userA));

        var first = await _db.Notifications.OrderBy(n => n.Id).FirstAsync();
        var second = await _db.Notifications.OrderBy(n => n.Id).Skip(1).FirstAsync();

        // 把"第一条"标已读 → 未读的"第二条"必须排前面
        await _service.MarkReadAsync(_userA, first.Id);

        var page = await _service.ListAsync(_userA, 1, 20);
        Assert.True(page.Success);
        Assert.Equal(2, page.Data!.Total);
        Assert.Equal(second.Id, page.Data.Items[0].Id);
        Assert.False(page.Data.Items[0].IsRead);
        Assert.Equal(first.Id, page.Data.Items[1].Id);
        Assert.True(page.Data.Items[1].IsRead);
    }

    [Fact]
    public async Task List_OnlyOwnRows()
    {
        await _service.AdminSendAsync(_adminId, Req("只给甲", "x", NotificationTargets.Single, _userA));

        var page = await _service.ListAsync(_userB, 1, 20);
        Assert.True(page.Success);
        Assert.Equal(0, page.Data!.Total);
        Assert.Empty(page.Data.Items);
    }

    [Fact]
    public async Task UnreadCount_DropsAfterRead_AndMarkAllZeroesIt()
    {
        await _service.AdminSendAsync(_adminId, Req("A", "a", NotificationTargets.Single, _userA));
        await _service.AdminSendAsync(_adminId, Req("B", "b", NotificationTargets.Single, _userA));

        Assert.Equal(2, (await _service.UnreadCountAsync(_userA)).Data);

        var ids = await _db.Notifications.OrderBy(n => n.Id).Select(n => n.Id).ToListAsync();
        await _service.MarkReadAsync(_userA, ids[0]);
        Assert.Equal(1, (await _service.UnreadCountAsync(_userA)).Data);

        var all = await _service.MarkAllReadAsync(_userA);
        Assert.True(all.Success);
        Assert.Equal(1, all.Data);                     // 本次标已读 1 条
        Assert.Equal(0, (await _service.UnreadCountAsync(_userA)).Data);

        // 其他用户不受影响（各自一行、各自已读状态）
        Assert.Equal(0, (await _service.UnreadCountAsync(_userB)).Data);
    }

    [Fact]
    public async Task MarkRead_OtherUsersRow_IsNotFound()
    {
        await _service.AdminSendAsync(_adminId, Req("只给甲", "x", NotificationTargets.Single, _userA));
        var id = _db.Notifications.Single().Id;

        // 乙去标甲的接收行 → 必须 NotFound（不泄露"这条通知存在"）
        var result = await _service.MarkReadAsync(_userB, id);
        Assert.False(result.Success);
        Assert.Equal(NotificationService.NotificationNotFound, result.Error);
    }

    [Fact]
    public async Task MarkRead_Idempotent_AndPushesCount()
    {
        await _service.AdminSendAsync(_adminId, Req("一次", "x", NotificationTargets.Single, _userA));
        var id = _db.Notifications.Single().Id;

        _broadcaster.Counts.Clear();
        await _service.MarkReadAsync(_userA, id);
        await _service.MarkReadAsync(_userA, id);

        Assert.Equal(0, (await _service.UnreadCountAsync(_userA)).Data);
        // 两次都推未读数（第二次推 0）；重复标记不改变结果
        Assert.Equal(2, _broadcaster.Counts.Count);
        Assert.All(_broadcaster.Counts, c => Assert.Equal(0, c.Unread));
    }

    [Fact]
    public async Task AdminList_ReturnsRecipientAndReadCounts()
    {
        await _service.AdminSendAsync(_adminId, Req("统计", "看人数", NotificationTargets.Partial, _userA, _userB));
        var id = _db.Notifications.Single().Id;
        await _service.MarkReadAsync(_userA, id);

        var list = await _service.AdminListAsync(null, 1, 20);
        Assert.True(list.Success);
        var row = list.Data!.Items.Single();
        Assert.Equal(2, row.RecipientCount);
        Assert.Equal(1, row.ReadCount);
        Assert.Equal("管理员", row.CreatedByName);
        Assert.Equal(NotificationTargets.Partial, row.TargetType);
        Assert.Equal(NotificationStatuses.Completed, row.Status);
        Assert.NotNull(row.SentAtUtc);
    }

    [Fact]
    public async Task AdminList_NewestFirst_AndPaged()
    {
        await _service.AdminSendAsync(_adminId, Req("旧", "1", NotificationTargets.Single, _userA));
        await _service.AdminSendAsync(_adminId, Req("新", "2", NotificationTargets.Single, _userA));

        var page = await _service.AdminListAsync(null, 1, 1);
        Assert.True(page.Success);
        Assert.Equal(2, page.Data!.Total);
        Assert.Single(page.Data.Items);
        Assert.Equal("新", page.Data.Items[0].Title);
    }

    [Fact]
    public async Task AdminSend_WithAlbum_AlbumIdRoundTripsThroughBothViews()
    {
        var result = await _service.AdminSendAsync(_adminId, new SendNotificationRequest(
            "新专辑上线", "整张专辑已上架", NotificationTargets.Single, [_userA], RelatedAlbumId: 77));

        Assert.True(result.Success);

        // 实体落库
        var entity = await _db.Notifications.SingleAsync();
        Assert.Equal(77, entity.RelatedAlbumId);

        // 用户侧列表带出去（App 靠它决定点通知时播哪张专辑），并让行尾箭头显示
        var page = await _service.ListAsync(_userA, 1, 20);
        var item = page.Data!.Items.Single();
        Assert.Equal(77, item.RelatedAlbumId);
        Assert.True(item.HasRelatedTarget);

        // 后台列表也带出去
        var admin = await _service.AdminListAsync(null, 1, 20);
        Assert.Equal(77, admin.Data!.Items.Single().RelatedAlbumId);
    }

    [Fact]
    public async Task AdminSend_OnlyAlbum_LeavesOtherRelationsNull()
    {
        await _service.AdminSendAsync(_adminId, new SendNotificationRequest(
            "只关联专辑", "x", NotificationTargets.Single, [_userA], RelatedAlbumId: 5));

        var entity = await _db.Notifications.SingleAsync();
        Assert.Null(entity.RelatedSongId);
        Assert.Null(entity.RelatedPlaylistId);
        Assert.Equal(5, entity.RelatedAlbumId);
    }

    [Fact]
    public async Task AdminSend_AllThreeRelations_EachPersistedIndependently()
    {
        // 服务端**不**强制三选一 —— 那是后台弹窗的交互约束（选了新的会清掉另外两项）。
        // 这里只保证三个字段都能独立存取：直接调接口写入的历史/异常数据也要能正常读回。
        var result = await _service.AdminSendAsync(_adminId, new SendNotificationRequest(
            "三个都填", "x", NotificationTargets.Single, [_userA],
            RelatedSongId: 11, RelatedPlaylistId: 22, RelatedAlbumId: 33));

        Assert.True(result.Success);

        var entity = await _db.Notifications.SingleAsync();
        Assert.Equal(11, entity.RelatedSongId);
        Assert.Equal(22, entity.RelatedPlaylistId);
        Assert.Equal(33, entity.RelatedAlbumId);

        var row = (await _service.AdminListAsync(null, 1, 20)).Data!.Items.Single();
        Assert.Equal(11, row.RelatedSongId);
        Assert.Equal(22, row.RelatedPlaylistId);
        Assert.Equal(33, row.RelatedAlbumId);
    }

    [Fact]
    public async Task DeleteUser_CascadesRecipientRows()
    {
        await _service.AdminSendAsync(_adminId, Req("级联", "x", NotificationTargets.Single, _userA));

        var user = await _db.Users.FirstAsync(u => u.Id == _userA);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        Assert.Empty(_db.NotificationRecipients);
    }

    [Fact]
    public async Task Notification_NoForeignKeyToUser_SurvivesSenderDeletion()
    {
        // CreatedById 只是审计字段：发送人（管理员）被删不该连带删掉通知本身
        await _service.AdminSendAsync(_adminId, Req("留存", "x", NotificationTargets.Single, _userA));
        var admin = await _db.Users.FirstAsync(u => u.Id == _adminId);
        _db.Users.Remove(admin);
        await _db.SaveChangesAsync();   // 若 Notification→User 有外键，这里会抛

        Assert.Single(_db.Notifications);
        // 已删管理员的通知仍在，列表里 CreatedByName 退化成 Id 字符串而非崩掉
        var list = await _service.AdminListAsync(null, 1, 20);
        Assert.True(list.Success);
        Assert.Equal(_adminId.ToString(), list.Data!.Items[0].CreatedByName);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
