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
            q = q.Where(s => s.Title.Contains(keyword) || s.Artist.Name.Contains(keyword)
                || (s.Album != null && s.Album.Name.Contains(keyword)));
        if (artistId.HasValue) q = q.Where(s => s.ArtistId == artistId);
        if (albumId.HasValue) q = q.Where(s => s.AlbumId == albumId);
        if (categoryId.HasValue) q = q.Where(s => s.CategoryId == categoryId);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(s => s.PlayCount).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SongDto
            {
                Id = s.Id, Title = s.Title, ArtistId = s.ArtistId, ArtistName = s.Artist.Name,
                AlbumId = s.AlbumId, AlbumName = s.Album != null ? s.Album.Name : null,
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
            .Include(s => s.Artist).Include(s => s.Album)
            .FirstOrDefaultAsync(s => s.Id == id);
        return song?.ToDto();
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
        db.Songs.Add(song);
        await db.SaveChangesAsync();
        await db.Entry(song).Reference(s => s.Artist).LoadAsync();
        await db.Entry(song).Reference(s => s.Album).LoadAsync();
        var dto = song.ToDto();
        await metadata.FillCoversAsync([dto]);
        return ServiceResult<SongDto>.Ok(dto);
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
                AlbumId = l.Song.AlbumId, AlbumName = l.Song.Album != null ? l.Song.Album.Name : null,
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