using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IAdminService
{
    Task<(int Total, int Imported, string? Error)> ImportAsync(string? dir);
    Task<int> DeleteSeedSongsAsync();
    Task<(int Total, int Updated, string? Error)> ScanDurationsAsync();
}

public class AdminService(MusicDbContext db, IConfiguration config) : IAdminService
{
    private static readonly string[] AudioExts = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];

    public async Task<(int Total, int Imported, string? Error)> ImportAsync(string? dir)
    {
        try
        {
            var musicDir = dir ?? config["Media:MusicDirectory"];
            if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
                return (0, 0, $"目录不存在: {musicDir}");

            var files = Directory.GetFiles(musicDir)
                .Where(f => AudioExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            var existingUrls = await db.Songs.Select(s => s.AudioUrl).ToListAsync();
            var existingSet = new HashSet<string>(existingUrls);
            var artistCache = await db.Artists.ToDictionaryAsync(a => a.Name);
            var imported = 0;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var urlPath = $"/media/audio/{Uri.EscapeDataString(fileName)}";
                if (existingSet.Contains(urlPath)) continue;

                var (artistName, title) = ParseFileName(fileName);
                if (artistName.Length > 64) artistName = artistName[..64];
                if (title.Length > 128) title = title[..128];

                if (!artistCache.TryGetValue(artistName, out var artist))
                {
                    artist = new Artist { Name = artistName };
                    db.Artists.Add(artist);
                    artistCache[artistName] = artist;
                }

                db.Songs.Add(new Song
                {
                    Title = title,
                    Artist = artist,
                    AudioUrl = urlPath,
                    DurationSeconds = 0
                });
                existingSet.Add(urlPath);
                imported++;
            }

            await db.SaveChangesAsync();
            return (files.Count, imported, null);
        }
        catch (Exception ex)
        {
            return (0, 0, $"{ex.Message} | {ex.InnerException?.Message}");
        }
    }

    public async Task<int> DeleteSeedSongsAsync()
    {
        var seedSongs = await db.Songs
            .Where(s => s.AudioUrl.StartsWith("/media/audio/sample_"))
            .ToListAsync();
        var count = seedSongs.Count;
        db.Songs.RemoveRange(seedSongs);
        await db.SaveChangesAsync();
        return count;
    }

    public async Task<(int Total, int Updated, string? Error)> ScanDurationsAsync()
    {
        var musicDir = config["Media:MusicDirectory"];
        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
            return (0, 0, $"目录不存在: {musicDir}");

        var songs = await db.Songs.ToListAsync();
        var updated = 0;
        foreach (var song in songs)
        {
            var fileName = Uri.UnescapeDataString(song.AudioUrl.Replace("/media/audio/", ""));
            var filePath = Path.Combine(musicDir, fileName);
            if (!File.Exists(filePath)) continue;
            var duration = Mp3DurationReader.GetDuration(filePath);
            if (duration > 0)
            {
                song.DurationSeconds = (int)Math.Round(duration);
                updated++;
            }
        }
        await db.SaveChangesAsync();
        return (songs.Count, updated, null);
    }

    private static (string artist, string title) ParseFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var idx = name.IndexOf(" - ");
        if (idx > 0 && idx < name.Length - 3)
        {
            var artist = name[..idx].Replace(';', ',').Trim();
            var title = name[(idx + 3)..].Trim();
            return (artist, title);
        }
        return ("未知艺术家", name.Trim());
    }
}