// Axios 实例 + 拦截器：
//   - 附加 Bearer（从 Pinia 取）；401 清状态 + 跳 /login。
//   - 统一错误：读后端 { "message": "..." }，抛 ApiException 由页面统一展示。
//   - 幂等请求自动重试：见下方 RETRY_*，解决开发环境经 Vite 代理偶发的 ECONNRESET。

import axios, { AxiosError, type AxiosResponse } from 'axios';

export class ApiException extends Error {
  constructor(public status: number, public message: string, public payload?: unknown) {
    super(message);
  }
}

// 开发环境经 Vite 代理时，keep-alive 连接偶发被重置（代理日志 read ECONNRESET，
// 约 1/10 概率）。这是连接层竞态，不是业务错误 —— 重试一次即可成功，
// 否则用户会看到"列表偶尔加载不出来、刷新又好了"。
// 只重试幂等请求（GET/HEAD），且只重试连接类错误，不重试 4xx/5xx 业务响应。
const RETRY_METHODS = new Set(['get', 'head']);
const RETRYABLE_CODES = new Set(['ECONNRESET', 'ECONNABORTED', 'ETIMEDOUT', 'EPIPE']);
const MAX_RETRY = 2;

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASEURL ?? '',
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
});

// 拦截器：附加 token
http.interceptors.request.use((cfg) => {
  const token = readToken();
  if (token) cfg.headers.Authorization = `Bearer ${token}`;
  return cfg;
});

// 拦截器：连接类错误自动重试（仅幂等请求）
http.interceptors.response.use(
  (r) => r,
  async (err: AxiosError<any>) => {
    const cfg = err.config as (typeof err.config & { __retry?: number }) | undefined;
    const code = err.code ?? '';
    const method = (cfg?.method ?? 'get').toLowerCase();
    const noResponse = !err.response;

    if (
      cfg && noResponse && RETRY_METHODS.has(method) &&
      (RETRYABLE_CODES.has(code) || code === '') &&
      (cfg.__retry ?? 0) < MAX_RETRY
    ) {
      cfg.__retry = (cfg.__retry ?? 0) + 1;
      console.warn(`[http] 连接被重置，重试 ${cfg.__retry}/${MAX_RETRY}: ${cfg.url}`);
      return http.request(cfg);
    }
    return Promise.reject(err);
  },
);

// 拦截器：401 跳登录 + 统一抛 ApiException
http.interceptors.response.use(
  (r) => r,
  (err: AxiosError<any>) => {
    const status = err.response?.status ?? 0;
    const data = err.response?.data;
    const message =
      (data && typeof data === 'object' && 'message' in data ? (data as any).message : null) ??
      err.message ??
      '请求失败';

    if (status === 401) {
      clearAuthSide();
      // 用 location 强制跳，避免循环依赖 router
      if (!location.pathname.startsWith('/login') && !location.pathname.startsWith('/register')) {
        location.replace(`/login?redirect=${encodeURIComponent(location.pathname + location.search)}`);
      }
    }
    return Promise.reject(new ApiException(status, message, data));
  },
);

// 由 Pinia store 调用：让拦截器知道当前 token（避免循环 import）
let tokenReader: () => string | null = () => null;
let authClearer: () => void = () => {};
export function bindAuthSide(reader: () => string | null, clearer: () => void) {
  tokenReader = reader;
  authClearer = clearer;
}
function readToken() {
  return tokenReader();
}
function clearAuthSide() {
  authClearer();
}

// 小工具：解包响应（兼容 AxiosResponse 与直接数据）
export function unwrap<T>(p: Promise<AxiosResponse<T>>): Promise<T> {
  return p.then((r) => r.data);
}