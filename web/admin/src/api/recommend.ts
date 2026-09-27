// 后台「推荐位管理」API（V2.12）：查全量歌单 + 各自的人工干预状态、设置推荐位、移除干预。
//
// 为什么不并进 catalog.ts 的 playlistApi：这里的「行」是【歌单 + 干预状态】的合成视图，
// 底层资源也是独立的一族（PUT 整行覆盖、DELETE 删干预行，主键是 PlaylistId 而不是自增 id）。
// 混进 playlistApi 会让两个资源族的语义互相污染，调用方也分不清 path 该带哪个 id。

import { http, unwrap } from './http';
import type { PagedResult } from './catalog';

export interface AdminRecommendedDto {
  playlistId: number;
  name: string;
  coverUrl: string | null;
  categoryName: string | null;
  ownerName: string;
  trackCount: number;
  collectorCount: number;
  /** 歌单播放量 = 歌单内全部歌曲播放次数之和，与 C 端列表同一口径。 */
  playCount: number;
  createdAt: string;
  /** 系统歌单（如「我喜欢的音乐」）。后台列表照常展示，但规则排序永远不会推荐它 —— 运营需要知道这一点。 */
  isSystem: boolean;
  /** 人工置顶序号：0 = 不置顶，>=1 = 置顶（数字小的排前面）。 */
  sortOrder: number;
  /** 人工加权分：加到规则分上，可为负数（用来把歌单往后压）。 */
  weight: number;
  /** 已下线：从全部推荐场景里摘掉，但歌单本身仍正常可见、可听。 */
  isHidden: boolean;
  /**
   * 是否**存在**人工干预记录。
   * ⚠️ 不能用「三个干预字段都等于 0/false」反推「没被干预」—— 运营可以显式设成 0，
   * 那种行是"干预成了默认值"，与"从未干预"在业务上不同（决定要不要显示「移除干预」）。
   * 因此前端一切"有没有干预过"的判断都只看这个字段。
   */
  hasOverride: boolean;
}

/**
 * 设置推荐位请求体。三个字段全部必需：后端按整行覆盖处理，
 * 传 0/false 就是"把该项改回默认"，而不是"不动该项"。
 */
export interface UpdateRecommendedRequest {
  sortOrder: number;
  weight: number;
  isHidden: boolean;
}

export const recommendApi = {
  /** GET /api/admin/recommended。keyword 由后端按「歌单名 / 创建者名」模糊匹配，前端不再自己过滤（否则会漏掉分页外的匹配项）。 */
  list: (params?: { keyword?: string; page?: number; pageSize?: number }) =>
    unwrap(http.get<PagedResult<AdminRecommendedDto>>('/api/admin/recommended', { params })),

  /** PUT /api/admin/recommended/{playlistId}（204，无响应体）。幂等 upsert：重复提交是覆盖，不会插出第二行。 */
  set: (playlistId: number, body: UpdateRecommendedRequest) =>
    unwrap(http.put<void>(`/api/admin/recommended/${playlistId}`, body)),

  /**
   * DELETE /api/admin/recommended/{playlistId}（204）。
   * 该歌单没有干预记录时后端返回 404 —— 这是正常业务回流（可能别人刚移除过），不是异常，
   * 调用方把 message 原样提示即可，不要自己改写成「系统错误」。
   */
  remove: (playlistId: number) =>
    unwrap(http.delete<void>(`/api/admin/recommended/${playlistId}`)),
};
