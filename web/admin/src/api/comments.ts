// 评论管理 API（V2.9）：后台审核用，全部接口需 admin 角色（令牌由 http.ts 拦截器附加）。

import { http, unwrap } from './http';

export interface AdminCommentDto {
  id: number;
  targetType: 'song' | 'playlist';
  targetId: number;
  /** 目标歌曲/歌单已被删除时后端返回字串「（已被删除）」 */
  targetTitle: string;
  userId: number;
  userName: string;
  parentId: number | null;
  content: string;
  likeCount: number;
  isHidden: boolean;
  createdAt: string;
  /** 层级：主评论 0，逐层 +1（最多 4，即第 5 层） */
  depth: number;
  /** 作者自己删掉的占位楼：正文在库里已清空，接口统一返回「该评论已删除」 */
  isDeleted: boolean;
}

export interface PagedCommentResult {
  items: AdminCommentDto[];
  total: number;
  page: number;
  pageSize: number;
}

export const adminCommentApi = {
  /** GET /api/admin/comments。keyword / targetType / isHidden 不传即「全部」。 */
  list: (params?: {
    keyword?: string;
    targetType?: 'song' | 'playlist';
    isHidden?: boolean;
    page?: number;
    pageSize?: number;
  }) => unwrap(http.get<PagedCommentResult>('/api/admin/comments', { params })),

  /** PUT /api/admin/comments/{id}/hidden?hidden=true/false（204，无响应体） */
  setHidden: (id: number, hidden: boolean) =>
    unwrap(http.put<void>(`/api/admin/comments/${id}/hidden`, null, { params: { hidden } })),

  /** DELETE /api/admin/comments/{id}（204，无响应体） */
  remove: (id: number) =>
    unwrap(http.delete(`/api/admin/comments/${id}`)),
};
