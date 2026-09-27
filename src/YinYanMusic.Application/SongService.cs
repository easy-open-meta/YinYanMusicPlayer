using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface ISongService
{
    Task<PagedResult<SongDto>> SearchAsync(string? keyword, long? artistId, long? albumId, int? categoryId, int page, int pageSize);
    Task<SongDto?> GetAsync(long id);
    Task<ServiceResult<SongDto>> CreateAsync(CreateSongRequest req);
    Task<ServiceResult<SongDto>> UpdateAsync(long id, UpdateSongRequest req);
    Task<ServiceResult<bool>> DeleteAsync(long id);
    Task RecordPlayAsync(long id);
    Task<IReadOnlyList<SongDto>> GetLikedAsync(long userId);
    Task<bool> LikeAsync(long userId, long songId);
    Task<bool> UnlikeAsync(long userId, long songId);
}

public class SongService(MusicDbContext db, AudioMetadataService metadata) : ISongService
{
    public async Task<PagedResult<SongDto>> SearchAsync(string? keyword, long? artistId, long? albumId, int? categoryId, int page, int pageSize)
    {
        var q = db.Songs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // ILIKE：大小写不敏感（搜 "ado" 命中 "Ado"）；pattern 已转义 %、_、\
            // 歌手名命中**任一**关联歌手（含联合创作者）：搜 EGOIST 也能找到 "Aimer;EGOIST" 那首；
            // 后面那条 Artist.Name 是兜底（万一某行没建关联，主歌手照样搜得到）。
            var pattern = LikePattern.Contains(keyword);
            q = q.Where(s => EF.Functions.ILike(s.Title, pattern, LikePattern.EscapeChar)
                || EF.Functions.ILike(s.Artist.Name, pattern, LikePattern.EscapeChar)
                || s.SongArtists.Any(sa => EF.Functions.ILike(sa.Artist.Name, pattern, LikePattern.EscapeChar))
                || (s.Album != null && EF.Functions.ILike(s.Album.Name, pattern, LikePattern.EscapeChar)));
        }
        // 按歌手列歌走关联表：联合创作的歌在**每位**歌手名下都能看到。
        // 仍保留 ArtistId 兜底（万一有历史行没建关联，也不会凭空消失）。
        if (artistId.HasValue) q = q.Where(s => s.ArtistId == artistId || s.SongArtists.Any(sa => sa.ArtistId == artistId));
        if (albumId.HasValue) q = q.Where(s => s.AlbumId == albumId);
        if (categoryId.HasValue) q = q.Where(s => s.CategoryId == categoryId);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(s => s.PlayCount).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SongDto
            {
                Id = s.Id, Title = s.Title, ArtistId = s.ArtistId, ArtistName = s.Artist.Name,
                Artists = s.SongArtists.OrderBy(sa => sa.Position).Select(sa => new SongArtistRef(sa.ArtistId, sa.Artist.Name)).ToList(),
                AlbumId = s.AlbumId, AlbumName = s.Album != null ? s.Album.Name : null,
                CategoryId = s.CategoryId, CategoryName = s.Category != null ? s.Category.Name : null,
                CoverUrl = s.CoverUrl ?? (s.Album != null ? s.Album.CoverUrl : null), AudioUrl = s.AudioUrl,
                LyricUrl = s.LyricUrl, DurationSeconds = s.DurationSeconds, PlayCount = s.PlayCount
            })
            .ToListAsync();
        await metadata.FillDurationsAsync(items);
        await metadata.FillCoversAsync(items);
        return new PagedResult<SongDto>(items, total, page, pageSize);
    }

    public async Task<SongDto?> GetAsync(long id)
    {
        var song = await db.Songs.AsNoTracking()
            .Include(s => s.Artist).Include(s => s.Album).Include(s => s.Category)
            // 关联表也要 Include：ToDto() 的 Artists 从 SongArtists 取（联合创作的全部歌手），
            // 漏了它就会静默退化成"只有主歌手"——列表接口给了两位、详情接口只给一位，前端选不出来。
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.Id == id);
        return song?.ToDto();
    }

    /// <summary>
    /// 把"主歌手"同步到关联表：保证 <c>SongArtists</c> 里有一条 Position = 0 且指向新的主歌手。
    /// 规则：新主歌手若已是合作者 → 把它提到 0、删掉旧的主歌手行；否则替换那条 0 行。
    /// 其余合作歌手一概不动。
    /// </summary>
    private async Task SyncPrimaryArtistLinkAsync(long songId, long newArtistId)
    {
        var links = await db.SongArtists.Where(sa => sa.SongId == songId).ToListAsync();
        var oldPrimary = links.FirstOrDefault(sa => sa.Position == 0);
        if (oldPrimary?.ArtistId == newArtistId) return;   // 没变

        var existing = links.FirstOrDefault(sa => sa.ArtistId == newArtistId);
        if (oldPrimary is not null) db.SongArtists.Remove(oldPrimary);

        if (existing is not null)
        {
            if (existing.Position != 0) existing.Position = 0;   // 从合作者提为主歌手
        }
        else
        {
            db.SongArtists.Add(new SongArtist { SongId = songId, ArtistId = newArtistId, Position = 0 });
        }
    }

    /// <summary>
    /// 整体重建歌曲的歌手关联（V2.12）：按传入顺序写 Position（0 = 主歌手）。
    /// 一把删掉旧行再插新行 —— 关联表只有 (SongId, ArtistId, Position) 三列，
    /// 这样最不容易出"旧主歌手残留 Position=0"这类隐性错位。
    /// </summary>
    private async Task ReplaceArtistLinksAsync(long songId, IReadOnlyList<long> artistIds)
    {
        var old = await db.SongArtists.Where(sa => sa.SongId == songId).ToListAsync();
        db.SongArtists.RemoveRange(old);

        for (var i = 0; i < artistIds.Count; i++)
        {
            db.SongArtists.Add(new SongArtist { SongId = songId, ArtistId = artistIds[i], Position = i });
        }
        await db.SaveChangesAsync();   // 先落库，避免映射时关联行还在"待删"状态
    }

    public async Task<ServiceResult<SongDto>> CreateAsync(CreateSongRequest req)
    {
        if (!await db.Artists.AnyAsync(a => a.Id == req.ArtistId))
            return ServiceResult<SongDto>.Fail("歌手不存在。");
        if (req.AlbumId.HasValue && !await db.Albums.AnyAsync(a => a.Id == req.AlbumId))
            return ServiceResult<SongDto>.Fail("专辑不存在。");

        var song = new Song
        {
            Title = req.Title.Trim(),
            ArtistId = req.ArtistId,
            AlbumId = req.AlbumId,
            CategoryId = req.CategoryId,
            AudioUrl = req.AudioUrl,
            LyricUrl = req.LyricUrl,
            DurationSeconds = req.DurationSeconds
        };
        // 关联表至少要有一条主歌手行（Position = 0）—— 按歌手列歌走的是它，
        // 只写 Song.ArtistId 会让这首歌在任何歌手页面上都看不到。
        song.SongArtists.Add(new SongArtist { ArtistId = req.ArtistId, Position = 0 });

        db.Songs.Add(song);
        await db.SaveChangesAsync();
        await db.Entry(song).Reference(s => s.Artist).LoadAsync();
        await db.Entry(song).Reference(s => s.Album).LoadAsync();
        await db.Entry(song).Reference(s => s.Category).LoadAsync();
        var dto = song.ToDto();
        await metadata.FillCoversAsync([dto]);
        return ServiceResult<SongDto>.Ok(dto);
    }

    public async Task<ServiceResult<SongDto>> UpdateAsync(long id, UpdateSongRequest req)
    {
        var song = await db.Songs.Include(s => s.Artist).Include(s => s.Album).Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (song is null) return ServiceResult<SongDto>.Fail("歌曲不存在。");

        // 只更新传进来的字段（partial update）。
        if (req.Title is not null) song.Title = req.Title.Trim();

        // 完整歌手列表（V2.12）：优先于 ArtistId —— 传了就整体重建关联（首位 = 主歌手）
        if (req.ArtistIds is { Count: > 0 })
        {
            var ids = req.ArtistIds.Distinct().ToList();
            var existingIds = await db.Artists.Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync();
            var missing = ids.Except(existingIds).ToList();
            if (missing.Count > 0)
                return ServiceResult<SongDto>.Fail($"歌手不存在：{string.Join(", ", missing)}");

            song.ArtistId = ids[0];   // 首位 = 主歌手
            await ReplaceArtistLinksAsync(song.Id, ids);
        }
        else if (req.ArtistId is long aid)
        {
            if (!await db.Artists.AnyAsync(a => a.Id == aid))
                return ServiceResult<SongDto>.Fail("歌手不存在。");
            song.ArtistId = aid;
            // 主歌手换了，关联表里的 Position = 0 那一行必须跟着换 ——
            // 否则这首歌在"新主歌手"的页面上反而看不到（按歌手列歌走的是关联表）。
            // 其余合作歌手（Position > 0）保留不动。
            await SyncPrimaryArtistLinkAsync(song.Id, aid);
        }
        // 可空外键（专辑 / 分区）都有"改动"和"清空"两种意图，而这一层是局部更新：
        // null 只表示"这次不改"，所以清空必须靠各自的 Clear* 标志显式表达
        // （否则前端把下拉清空后提交，字段会被当成"不改"，值又回来了）。
        if (req.ClearAlbum) song.AlbumId = null;
        else if (req.AlbumId is long alid)
        {
            if (!await db.Albums.AnyAsync(a => a.Id == alid))
                return ServiceResult<SongDto>.Fail("专辑不存在。");
            song.AlbumId = alid;
        }
        if (req.ClearCategory) song.CategoryId = null;
        else if (req.CategoryId is int cid) song.CategoryId = cid;
        if (req.AudioUrl is not null) song.AudioUrl = req.AudioUrl;
        if (req.LyricUrl is not null) song.LyricUrl = req.LyricUrl;
        if (req.DurationSeconds is int dur) song.DurationSeconds = dur;

        await db.SaveChangesAsync();
        await db.Entry(song).Reference(s => s.Artist).LoadAsync();
        await db.Entry(song).Reference(s => s.Album).LoadAsync();
        await db.Entry(song).Reference(s => s.Category).LoadAsync();
        // 换主歌手时 SyncPrimaryArtistLinkAsync 查过关联表，EF 关系修正会把行挂进 song.SongArtists
        // 但 Artist 导航是空的 —— 显式加载一遍，映射器才能拿到合作歌手的名字（否则映射器判空回退）
        await db.Entry(song).Collection(s => s.SongArtists).Query()
            .Include(sa => sa.Artist).LoadAsync();
        var dto = song.ToDto();
        await metadata.FillCoversAsync([dto]);
        return ServiceResult<SongDto>.Ok(dto);
    }

    /// <summary>
    /// 物理删除歌曲，并级联删除用户喜欢记录（防止外键悬挂）。
    /// 用户收藏的歌单不受影响（歌单是独立的 songIds 集合，由 PlaylistSongs 中间表维护）。
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(long id)
    {
        var song = await db.Songs.FirstOrDefaultAsync(s => s.Id == id);
        if (song is null) return ServiceResult<bool>.Fail("歌曲不存在。");

        // 一次性把喜欢记录 + 歌单中间表记录清掉，避免 FK 冲突。
        await db.LikedSongs.Where(l => l.SongId == id).ExecuteDeleteAsync();
        await db.PlaylistSongs.Where(p => p.SongId == id).ExecuteDeleteAsync();
        // 评论没有外键可依（TargetId 是多态的，见 CommentTargets），必须显式清 ——
        // 否则歌删了评论还留着，后台审核列表里会挂着一堆"（已被删除）"的孤儿记录（TC-2.9-10）。
        await db.Comments.Where(c => c.TargetType == CommentTargets.Song && c.TargetId == id).ExecuteDeleteAsync();
        db.Songs.Remove(song);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task RecordPlayAsync(long id)
    {
        await db.Songs.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.PlayCount, s => s.PlayCount + 1));
    }

    public async Task<IReadOnlyList<SongDto>> GetLikedAsync(long userId)
    {
        var songs = await db.LikedSongs.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new SongDto
            {
                Id = l.Song.Id, Title = l.Song.Title, ArtistId = l.Song.ArtistId, ArtistName = l.Song.Artist.Name,
                Artists = l.Song.SongArtists.OrderBy(sa => sa.Position).Select(sa => new SongArtistRef(sa.ArtistId, sa.Artist.Name)).ToList(),
                AlbumId = l.Song.AlbumId, AlbumName = l.Song.Album != null ? l.Song.Album.Name : null,
                CategoryId = l.Song.CategoryId, CategoryName = l.Song.Category != null ? l.Song.Category.Name : null,
                CoverUrl = l.Song.CoverUrl ?? (l.Song.Album != null ? l.Song.Album.CoverUrl : null),
                AudioUrl = l.Song.AudioUrl, LyricUrl = l.Song.LyricUrl,
                DurationSeconds = l.Song.DurationSeconds, PlayCount = l.Song.PlayCount
            })
            .ToListAsync();
        await metadata.FillDurationsAsync(songs);
        await metadata.FillCoversAsync(songs);
        return songs;
    }

    public async Task<bool> LikeAsync(long userId, long songId)
    {
        if (!await db.Songs.AnyAsync(s => s.Id == songId)) return false;
        if (await db.LikedSongs.AnyAsync(l => l.UserId == userId && l.SongId == songId)) return true;
        db.LikedSongs.Add(new LikedSong { UserId = userId, SongId = songId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnlikeAsync(long userId, long songId)
    {
        var count = await db.LikedSongs.Where(l => l.UserId == userId && l.SongId == songId).ExecuteDeleteAsync();
        return count > 0;
    }
}