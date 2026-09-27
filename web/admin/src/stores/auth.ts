// 鉴权状态：token + 当前管理员。从 localStorage 恢复（刷新保持登录）。

import { defineStore } from 'pinia';
import { bindAuthSide } from '@/api/http';
import * as authApi from '@/api/auth';

const TOKEN_KEY = 'yinyan.admin.token';
const USER_KEY = 'yinyan.admin.user';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: null as string | null,
    user: null as authApi.UserDto | null,
  }),
  getters: {
    isLoggedIn: (s) => !!s.token && !!s.user,
    isAdmin:   (s) => s.user?.role === 'admin',
  },
  actions: {
    async restore() {
      const token = localStorage.getItem(TOKEN_KEY);
      const userJson = localStorage.getItem(USER_KEY);
      if (!token || !userJson) return false;
      // 缓存可能被手工改坏或是旧版本残留：解析失败就当未登录，清掉脏数据重来。
      try {
        this.user = JSON.parse(userJson) as authApi.UserDto;
      } catch {
        await this.logout();
        return false;
      }
      this.token = token;
      // 校验过期
      if (isJwtExpired(token)) {
        await this.logout();
        return false;
      }
      // 顺手把拦截器挂上
      bindAuthSide(() => this.token, () => this.logout());
      return true;
    },

    setSession(token: string, user: authApi.UserDto) {
      this.token = token;
      this.user = user;
      localStorage.setItem(TOKEN_KEY, token);
      localStorage.setItem(USER_KEY, JSON.stringify(user));
      bindAuthSide(() => this.token, () => this.logout());
    },

    async logout() {
      this.token = null;
      this.user = null;
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    },
  },
});

function isJwtExpired(token: string): boolean {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = payload + '='.repeat((-payload.length) % 4);
    const json = JSON.parse(atob(padded)) as { exp?: number };
    return !json.exp || json.exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}