// 后台「消息推送」API（V2.15）：发送通知 + 已发送列表。
// 独立成文件：资源族与 catalog/recommend 都不同（发完即写入、状态机是 sending/completed/failed）。

import { http, unwrap } from './http';
import type { PagedResult } from './catalog';

/** 目标范围：all = 全部用户；partial / single 见 recipientUserIds。 */
export type NotificationTargetType = 'all' | 'partial' | 'single';

/** 发送状态：sending / completed / failed。 */
export type NotificationStatus = 'sending' | 'completed' | 'failed';

export interface AdminNotificationDto {
  id: number;
  title: string;
  content: string;
  targetType: NotificationTargetType;
  /** partial / single 时的目标用户 Id 列表；all 时为空数组。 */
  recipientUserIds: number[];
  relatedSongId?: number | null;
  relatedPlaylistId?: number | null;
  relatedAlbumId?: number | null;
  status: NotificationStatus;
  createdById: number;
  createdByName: string;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  recipientCount: number;
  readCount: number;
}

export interface SendNotificationRequest {
  title: string;
  content: string;
  targetType: NotificationTargetType;
  /** partial/single 必填且非空；all 忽略。 */
  recipientUserIds?: number[];
  /** 关联对象三选一（同时给多个时 App 端按 歌单 > 专辑 > 歌曲 取第一个）。 */
  relatedSongId?: number | null;
  relatedPlaylistId?: number | null;
  relatedAlbumId?: number | null;
}

export const notificationsApi = {
  /** POST /api/admin/notifications */
  send: (body: SendNotificationRequest) =>
    unwrap(http.post<AdminNotificationDto>('/api/admin/notifications', body)),

  /** GET /api/admin/notifications（分页，按时间倒序） */
  list: (params?: { keyword?: string; page?: number; pageSize?: number }) =>
    unwrap(http.get<PagedResult<AdminNotificationDto>>('/api/admin/notifications', { params })),
};
