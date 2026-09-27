// 后端认证相关 API。
// 类型先手写——`npm run gen:api` 跑过一次后用 openapi-typescript 覆盖这里即可。

import { http, unwrap } from './http';

export type Role = 'user' | 'admin';

export interface UserDto {
  id: number;
  userName: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  gender: string;
  createdAt: string;
  role: Role;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface RegisterRequest {
  userName: string;
  password: string;
  displayName: string;
  gender?: string | null;
  birthday?: string | null;
  /** 通道："admin"（后台注册页，创建超管）/ "app"（MAUI 客户端，普通用户）。默认 "app"。 */
  source?: 'admin' | 'app' | null;
  /** 后台注册页必填：服务端比对 bootstrap.txt。 */
  bootstrapCode?: string | null;
}

/** 仅后台注册页关心：open=true 时可以创建超管。 */
export async function getRegistrationOpen(source: 'admin' | 'app' = 'admin'): Promise<{ open: boolean }> {
  return unwrap(http.get('/api/auth/registration-open', { params: { source } }));
}

/** App 端普通登录（不校验 role，超管也能登）。 */
export async function login(req: LoginRequest): Promise<AuthResponse> {
  return unwrap(http.post('/api/auth/login', req));
}

/** 后台管理登录：必须是 admin 角色，8 小时令牌。 */
export async function adminLogin(req: LoginRequest): Promise<AuthResponse> {
  return unwrap(http.post('/api/admin/login', req));
}

/** 注册。App 默认 source=app；后台注册页传 source=admin + bootstrapCode。 */
export async function register(req: RegisterRequest): Promise<AuthResponse> {
  return unwrap(http.post('/api/auth/register', req));
}

/** 当前登录用户。 */
export async function me(): Promise<UserDto> {
  return unwrap(http.get('/api/auth/me'));
}