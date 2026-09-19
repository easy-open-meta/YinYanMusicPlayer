# YinYan Music Player

A modern cross-platform music player for both Windows and Android, with a backend built on ASP.NET Core Web API + PostgreSQL.

> The predecessor was donated by Enrico. The original project was a .NET Framework 4.6.1 WinForms implementation and has since been upgraded to a .NET 10 MAUI cross-platform architecture.

## Tech Stack

| Layer | Technology |
|---|---|
| Client | .NET MAUI (net10.0-android, net10.0-windows10.0.19041.0) |
| Server | ASP.NET Core Web API (.NET 10) |
| Database | PostgreSQL (EF Core 10) |
| Auth | JWT Bearer Token |
| MVVM | CommunityToolkit.Mvvm |
| Media Playback | CommunityToolkit.Maui.MediaElement (Android uses ExoPlayer / system media session notification controls) |
| Audio Metadata | ATL (z440.atl.core, frame-by-frame duration parsing) + TagLibSharp (tags / cover art) |
| Package Management | Central Package Management (CPM, Directory.Packages.props) |

## Project Structure

```
YinYanMusic.slnx
├── src/
│   ├── YinYanMusic.Core/          # Shared class library (entities + DTOs + mapping extensions)
│   ├── YinYanMusic.Data/          # EF Core DbContext
│   ├── YinYanMusic.Application/   # Business services (songs / playlists / directories / accounts / metadata / duration detection)
│   ├── YinYanMusic.Api/           # ASP.NET Core Web API
│   ├── YinYanMusic.App/           # MAUI client (Windows + Android)
│   └── YinYanMusic.Scanner/       # Local music import CLI tool
├── tests/
│   └── YinYanMusic.Tests/         # xUnit unit tests
├── docs/                          # Design docs (e.g. category feature proposal)
├── Directory.Build.props          # Unified build properties
└── Directory.Packages.props       # Central package version management
```

## Build & Run

### Prerequisites

- .NET 10 SDK (10.0.300+)
- MAUI workload: `dotnet workload install maui`
- PostgreSQL 16+
- (Android build) Android SDK

### Build the server

```bash
dotnet build src/YinYanMusic.Api/YinYanMusic.Api.csproj
```

### Run the server

```bash
dotnet run --project src/YinYanMusic.Api/YinYanMusic.Api.csproj
```

The API listens on `http://localhost:5000` by default, and the Swagger docs are available at `/swagger`.

### Build the client (Windows)

```bash
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-windows10.0.19041.0
```

### Build the client (Android)

```bash
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-android
```

### Local music import (Scanner CLI)

```bash
dotnet run --project src/YinYanMusic.Scanner/YinYanMusic.Scanner.csproj -- --path "E:\CloudMusic" [--dry-run] [--force]
```

- Recursively scans MP3 / FLAC / M4A / WAV / OGG; TagLib reads tags and embedded cover art (auto-saved as `song-{id}.jpg`); matching `.lrc` files are associated as lyrics automatically
- Duration is parsed frame-by-frame from the audio data by ATL (rounded down); files that fail to parse are rejected, so corrupted audio never enters the library

### Run tests

```bash
dotnet test
```

## Configuration

### Server (`appsettings.json`, dev config in `appsettings.Development.json`)

- `ConnectionStrings:Default` — PostgreSQL connection string
- `Jwt:SecretKey` — JWT signing key (≥32 chars), `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes`
- `Media:MusicDirectory` — local music directory (static audio source for Scanner and Api)
- `Media:ImageDirectory` / `Media:BaseUrl` — cover art output directory and external-link prefix (optional)

> Database schema changes do not go through EF migrations: new databases are created via `EnsureCreated`, and existing databases are patched at `Program.cs` startup with idempotent raw SQL (add columns / tables / seed data). When you change the schema, remember to maintain the patches accordingly.

### Client (`Services/ApiConfig.cs`)

- `BaseUrl` — API service address (default `http://localhost:5000`)

## Features

### Accounts & Profile

- Register / login (JWT); "Me" page personal info card (avatar / nickname / gender)
- The follower / following / playlist three-item statistics are **clickable**, leading to the follower list, my playlist list, and following list respectively (followed objects include both users and artists)
- User profile: reachable after searching a user; view their info and public playlists, **follow / unfollow** that user

### Discovery Page & Music Categories

- Hot songs and featured playlists horizontal-scroll sections (system playlists are auto-excluded)
- **Music categories**: fully data-driven by the `Categories` table (Chinese专区, Cantonese专区, K-Pop专区 …), with a discovery-page horizontal card + full category grid page + category detail page; adding a category only requires inserting one row
- Each song / playlist can be attached to a category (`CategoryId`); both song and playlist search APIs support filtering by category

### Search

- Keyword fuzzy matching across **songs** (song name / artist name / album name), **albums**, and **users**, three-way parallel with sectioned results
- Clicking an album result plays the whole album; clicking a user result opens the user profile
- Search history (locally persisted, supports single-delete / clear-all)

### Playlists

- Create (with reserved playlist-name validation) / favorite / unfavorite / delete
- Songs inside a playlist support a "more" menu: play next / add to another playlist / like / view artist / follow artist

### Playback

- Play / pause / previous / next / seek / volume, with a persistent mini player bar
- Four playback modes: sequential / list loop / single loop / shuffle, **locally persisted** (retained after restart)
- Background playback continues; Android notification media control panel (play/pause/prev/next/progress, sharing the same duration as the player page)
- Player page: vinyl rotation animation, long-press cover preview & save, color acrylic background, audio-quality tag (FLAC / MP3 / M4A … auto-shown by file extension)
- Song duration is unified end-to-end: ATL frame-by-frame parsing + round-down (import rejects files that fail to parse → playlist doesn't render them → playback skips them automatically)
- Lyrics: matching `.lrc` files are associated automatically

### Likes & Follows

- Like songs: heart on the player page / menu item; "My Liked Music" personal playlist shown pinned at the top
- Follow artists: from the song menu / quick-follow on the player page; following list is shown by section
- Follow users: one-click follow from the user profile; follower count and following list sync in real time

### Themes

- Five theme colors (purple / red / orange / blue / pure white), switchable with one tap from the settings drawer, applied instantly and persisted locally
- Global DynamicResource theming: buttons / sliders / tabs / drawers / bottom navigation all follow; the player page's dark acrylic background stays unchanged, while emphasis elements such as the progress bar / play button / quality tag follow the theme

## Contributing

1. Fork the repository
2. Create a Feat_xxx branch
3. Commit your code
4. Create a Pull Request
