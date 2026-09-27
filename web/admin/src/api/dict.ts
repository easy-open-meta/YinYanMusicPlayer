// 数据字典 API（V2.16）：
//   公开读：GET /api/dicts/{type} —— 返回某类型下启用的选项（歌手地区/类型下拉框用）
//   管理写：/api/dicts 下全部需要 admin 角色（后台「数据字典」页）

import { http, unwrap } from './http';

export interface DictItemDto {
  id: number;
  dictType: string;
  label: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface DictTypeDto {
  id: number;
  type: string;
  name: string;
  isEnabled: boolean;
  items: DictItemDto[];
}

export interface CreateDictTypeRequest {
  type: string;
  name: string;
  isEnabled?: boolean;
}

export interface UpdateDictTypeRequest {
  name?: string | null;
  isEnabled?: boolean | null;
}

export interface CreateDictItemRequest {
  label: string;
  sortOrder?: number;
  isEnabled?: boolean;
}

export interface UpdateDictItemRequest {
  label?: string | null;
  sortOrder?: number | null;
  isEnabled?: boolean | null;
}

export const dictApi = {
  /** GET /api/dicts/{type}（公开）：某类型启用的字典项 */
  enabledItems: (type: string) =>
    unwrap(http.get<DictItemDto[]>(`/api/dicts/${type}`)),

  /** GET /api/dicts（admin）：全部类型 + 每类型的全部项（含禁用） */
  all: () =>
    unwrap(http.get<DictTypeDto[]>('/api/dicts')),

  /** POST /api/dicts/types */
  createType: (body: CreateDictTypeRequest) =>
    unwrap(http.post<DictTypeDto>('/api/dicts/types', body)),

  /** PUT /api/dicts/types/{id} */
  updateType: (id: number, body: UpdateDictTypeRequest) =>
    unwrap(http.put<DictTypeDto>(`/api/dicts/types/${id}`, body)),

  /** DELETE /api/dicts/types/{id} */
  deleteType: (id: number) =>
    unwrap(http.delete(`/api/dicts/types/${id}`)),

  /** POST /api/dicts/{type}/items */
  createItem: (type: string, body: CreateDictItemRequest) =>
    unwrap(http.post<DictItemDto>(`/api/dicts/${type}/items`, body)),

  /** PUT /api/dicts/items/{id} */
  updateItem: (id: number, body: UpdateDictItemRequest) =>
    unwrap(http.put<DictItemDto>(`/api/dicts/items/${id}`, body)),

  /** DELETE /api/dicts/items/{id} */
  deleteItem: (id: number) =>
    unwrap(http.delete(`/api/dicts/items/${id}`)),
};