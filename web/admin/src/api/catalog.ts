// 曲库 CRUD API（歌曲/专辑/歌手/分区）。
// 类型与后端 DTO 对应。

import { http, unwrap } from './http';

// ── 共享分页 ────────────────────────────────────────────────────────────────

export interface PageReq {
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ── 分区 Category ────────────────────────────────────────────────────────────

export interface CategoryDto {
  id: number;
  name: string;
  slogan?: string | null;
  colorHex?: string | null;
  iconGlyph?: string | null;
  /** 专区内容类型：both（歌曲+歌单，默认）/ songs（仅歌曲）/ playlists（仅歌单）。读接口必返回；写接口留空表示不改。 */
  contentMode?: string | null;
}

export const catalogApi = {
  categories: {
    /** GET /api/catalog/categories */
    list: () => unwrap(http.get<CategoryDto[]>('/api/catalog/categories')),

    /** POST /api/catalog/categories */
    create: (body: Omit<CategoryDto, 'id'>) =>
      unwrap(http.post<CategoryDto>('/api/catalog/categories', body)),

    /** PUT /api/catalog/categories/{id} */
    update: (id: number, body: Partial<Omit<CategoryDto, 'id'>>) =>
      unwrap(http.put<CategoryDto>(`/api/catalog/categories/${id}`, body)),

    /** DELETE /api/catalog/categories/{id} */
    delete: (id: number) => unwrap(http.delete(`/api/catalog/categories/${id}`)),
  },

  // ── 歌手 Artist ─────────────────────────────────────────────────────────

  artists: {
    /** GET /api/catalog/artists */
    list: (params?: { keyword?: string; page?: number; pageSize?: number }) =>
      unwrap(http.get<PagedResult<ArtistDto>>('/api/catalog/artists', { params })),

    /** GET /api/catalog/artists/{id} */
    get: (id: number) =>
      unwrap(http.get<ArtistDto>(`/api/catalog/artists/${id}`)),

    /** POST /api/catalog/artists */
    create: (body: CreateArtistRequest) =>
      unwrap(http.post<ArtistDto>('/api/catalog/artists', body)),

    /** PUT /api/catalog/artists/{id} */
    update: (id: number, body: Partial<CreateArtistRequest> & { clearAvatar?: boolean }) =>
      unwrap(http.put<ArtistDto>(`/api/catalog/artists/${id}`, body)),

    /** DELETE /api/catalog/artists/{id} */
    delete: (id: number) =>
      unwrap(http.delete(`/api/catalog/artists/${id}`)),
  },

  // ── 专辑 Album ──────────────────────────────────────────────────────────

  albums: {
    /** GET /api/catalog/albums */
    list: (params?: { artistId?: number; keyword?: string; page?: number; pageSize?: number }) =>
      unwrap(http.get<PagedResult<AlbumDto>>('/api/catalog/albums', { params })),

    /** GET /api/catalog/albums/{id} */
    get: (id: number) =>
      unwrap(http.get<AlbumDto>(`/api/catalog/albums/${id}`)),

    /** POST /api/catalog/albums */
    create: (body: CreateAlbumRequest) =>
      unwrap(http.post<AlbumDto>('/api/catalog/albums', body)),

    /** PUT /api/catalog/albums/{id}。clear* 用于表达"清空可空字段"——null 只表示"不改"。
     *  artistIds（V2.12 联合创作）：完整歌手列表、按顺序，首位 = 主歌手；传了就整体重建关联。 */
    update: (id: number, body: Partial<CreateAlbumRequest> & {
      clearReleaseDate?: boolean;
      clearCover?: boolean;
      artistIds?: number[];
    }) =>
      unwrap(http.put<AlbumDto>(`/api/catalog/albums/${id}`, body)),

    /** DELETE /api/catalog/albums/{id} */
    delete: (id: number) =>
      unwrap(http.delete(`/api/catalog/albums/${id}`)),
  },

  // ── 歌曲 Song ────────────────────────────────────────────────────────────

  songs: {
    /** GET /api/songs */
    list: (params?: {
      keyword?: string;
      artistId?: number;
      albumId?: number;
      categoryId?: number;
      page?: number;
      pageSize?: number;
    }) =>
      unwrap(http.get<PagedResult<SongDto>>('/api/songs', { params })),

    /** GET /api/songs/{id} */
    get: (id: number) =>
      unwrap(http.get<SongDto>(`/api/songs/${id}`)),

    /** POST /api/songs */
    create: (body: CreateSongRequest) =>
      unwrap(http.post<SongDto>('/api/songs', body)),

    /** PUT /api/songs/{id}。clear* 用于表达"把可空外键（专辑/分类）清空"—— null 只表示"不改"。
     *  artistIds（V2.12 联合创作）：完整歌手列表、按顺序，首位 = 主歌手；传了就整体重建关联。 */
    update: (id: number, body: Partial<CreateSongRequest> & {
      clearCategory?: boolean;
      clearAlbum?: boolean;
      artistIds?: number[];
    }) =>
      unwrap(http.put<SongDto>(`/api/songs/${id}`, body)),

    /** DELETE /api/songs/{id} */
    delete: (id: number) =>
      unwrap(http.delete(`/api/songs/${id}`)),
  },
};

// ── 请求类型 ────────────────────────────────────────────────────────────────

export interface CreateArtistRequest {
  name: string;
  region?: string | null;
  kind?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
}

export interface CreateAlbumRequest {
  artistId: number;
  name: string;
  releaseDate?: string | null;   // ISO date string "YYYY-MM-DD"
  description?: string | null;
  coverUrl?: string | null;
}

export interface CreateSongRequest {
  title: string;
  artistId: number;
  albumId?: number | null;
  categoryId?: number | null;
  audioUrl: string;
  lyricUrl?: string | null;
  durationSeconds: number;
}

// ── 响应类型 ────────────────────────────────────────────────────────────────

export interface ArtistDto {
  id: number;
  name: string;
  region?: string | null;
  kind?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
  followerCount: number;
  songCount: number;
  albumCount: number;
}

export interface AlbumDto {
  id: number;
  name: string;
  coverUrl?: string | null;
  releaseDate?: string;   // ISO date "YYYY-MM-DD"
  description?: string | null;
  artist: ArtistDto;
  /** 全部歌手（V2.12 联合创作专辑）：按顺序，首位 = 主歌手。读接口返回。 */
  artists?: { id: number; name: string }[] | null;
  /** 专辑下有几首歌。**只有读接口有值**（创建/更新的响应里是 0）。 */
  trackCount?: number;
}

// ── M3: 歌单 Playlist ───────────────────────────────────────────────────────

export interface PlaylistDto {
  id: number;
  name: string;
  description?: string | null;
  coverUrl?: string | null;
  categoryId?: number | null;
  categoryName?: string | null;
  ownerId: number;
  ownerName: string;
  trackCount: number;
  collectorCount: number;
  createdAt: string;
  isSystem: boolean;
}

export const playlistApi = {
  /** GET /api/playlists */
  list: (params?: { keyword?: string; categoryId?: number; ownerId?: number; page?: number; pageSize?: number }) =>
    unwrap(http.get<PagedResult<PlaylistDto>>('/api/playlists', { params })),

  /** DELETE /api/playlists/{id}/admin */
  adminDelete: (id: number) => unwrap(http.delete(`/api/playlists/${id}/admin`)),
};

// ── M3: 后台用户管理 ────────────────────────────────────────────────────────

export interface AdminUserDto {
  id: number;
  userName: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  gender: string;
  createdAt: string;
  isDisabled: boolean;
}

export interface PagedAdminUserResult {
  items: AdminUserDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CreateAdminUserRequest {
  userName: string;
  password: string;
  displayName: string;
  gender?: string | null;
}

export interface UpdateAdminUserRequest {
  displayName?: string | null;
  bio?: string | null;
  gender?: string | null;
  avatarUrl?: string | null;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export const adminApi = {
  users: {
    /** GET /api/admin/users */
    list: (params?: { keyword?: string; page?: number; pageSize?: number }) =>
      unwrap(http.get<PagedAdminUserResult>('/api/admin/users', { params })),

    /** POST /api/admin/users */
    create: (body: CreateAdminUserRequest) =>
      unwrap(http.post<AdminUserDto>('/api/admin/users', body)),

    /** PUT /api/admin/users/{id} */
    update: (id: number, body: UpdateAdminUserRequest) =>
      unwrap(http.put<AdminUserDto>(`/api/admin/users/${id}`, body)),

    /** PUT /api/admin/users/{id}/disabled?disabled=true/false */
    setDisabled: (id: number, disabled: boolean) =>
      unwrap(http.put<void>(`/api/admin/users/${id}/disabled`, null, { params: { disabled } })),

    /** PUT /api/admin/users/{id}/password */
    resetPassword: (id: number, body: ResetPasswordRequest) =>
      unwrap(http.put<void>(`/api/admin/users/${id}/password`, body)),
  },
};

export interface SongDto {
  id: number;
  title: string;
  artistId: number;
  artistName: string;
  /**
   * 全部歌手（联合创作时不止一个，第一位是主歌手）。读接口才返回；本地歌为 null。
   * 带 id 是为了"点歌手名进详情页 / 关注某一位"——只有名字时会认错同名歌手。
   * 索引里没有歌手 ID 的场景（离线缓存）id 为 0，表示"知道有这位，但跳不过去"。
   */
  artists?: { id: number; name: string }[] | null;
  albumId?: number | null;
  albumName?: string | null;
  categoryId?: number | null;
  categoryName?: string | null;
  coverUrl?: string | null;
  audioUrl: string;
  lyricUrl?: string | null;
  durationSeconds: number;
  playCount: number;
}
