// 后台仪表盘统计 API（V2.16）：GET /api/admin/dashboard 一次返回全部统计数字。

import { http, unwrap } from './http';

export interface CategoryDistDto {
  categoryId?: number | null;
  categoryName?: string | null;
  songCount: number;
}

export interface RecentSongDto {
  id: number;
  title: string;
  artistName: string;
  albumName?: string | null;
  categoryName?: string | null;
  durationSeconds: number;
  playCount: number;
  createdAt: string;
}

export interface TopArtistDto {
  id: number;
  name: string;
  songCount: number;
  totalPlays: number;
}

export interface DashboardStatsDto {
  songs: number;
  albums: number;
  artists: number;
  playlists: number;
  users: number;
  totalPlays: number;
  categoryDistribution: CategoryDistDto[];
  recentSongs: RecentSongDto[];
  topArtists: TopArtistDto[];
}

export const dashboardApi = {
  /** GET /api/admin/dashboard */
  stats: () => unwrap(http.get<DashboardStatsDto>('/api/admin/dashboard')),
};