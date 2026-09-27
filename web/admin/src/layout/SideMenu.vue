<script setup lang="ts">
import { computed, type Component } from 'vue';
import { useRouter, useRoute } from 'vue-router';
// 显式导入图标组件（而非靠字符串名 + 全局注册）：类型安全，且不依赖 main.ts 的注册顺序。
import {
  Odometer, List, Files, User, Menu as MenuIcon,
  Collection, UserFilled, Refresh, Notebook, ChatDotRound, MagicStick,
  Bell,
} from '@element-plus/icons-vue';

const router = useRouter();
const route = useRoute();

interface NavItem { path: string; label: string; icon: Component; soon?: boolean }

const groups: { title: string; items: NavItem[] }[] = [
  {
    title: '',
    items: [
      { path: '/dashboard', label: '仪表盘', icon: Odometer },
    ],
  },
  {
    title: '曲库',
    items: [
      { path: '/songs',    label: '歌曲',    icon: List },
      { path: '/albums',   label: '专辑',    icon: Files },
      { path: '/artists',  label: '歌手',    icon: User },
      { path: '/categories', label: '分区',   icon: MenuIcon },
    ],
  },
  {
    title: '内容',
    items: [
      { path: '/playlists', label: '歌单', icon: Collection },
      { path: '/recommended', label: '推荐位管理', icon: MagicStick },
      { path: '/comments',  label: '评论管理', icon: ChatDotRound },
      { path: '/users',     label: '用户', icon: UserFilled },
      { path: '/notifications', label: '消息推送', icon: Bell },
    ],
  },
  {
    title: '系统',
    items: [
      { path: '/system/tasks', label: '导入与扫描', icon: Refresh },
      { path: '/system/dicts', label: '数据字典', icon: Notebook },
    ],
  },
];

const currentPath = computed(() => route.path);
function go(item: NavItem) { if (!item.soon) void router.push(item.path); }
</script>

<template>
  <div class="side-menu">
    <div class="brand">
      <img src="../assets/icon/logo.svg" alt="Logo" width="32" height="32" />
      <div class="name">音言音乐-后台管理端</div>
    </div>
    <div v-for="(g, gi) in groups" :key="gi" class="group">
      <div v-if="g.title" class="group-title">{{ g.title }}</div>
      <div
        v-for="item in g.items"
        :key="item.path"
        class="item"
        :class="{ active: currentPath === item.path, soon: item.soon }"
        @click="go(item)"
      >
        <el-icon><component :is="item.icon" /></el-icon>
        <span class="label">{{ item.label }}</span>
        <span v-if="item.soon" class="badge">M3+</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.side-menu { display: flex; flex-direction: column; gap: 14px; }
.brand { display: flex; align-items: center; gap: 8px; padding: 0 4px 8px; }
.name { font-weight: 500; font-size: 14px; }
.group-title { font-size: 11px; color: #8A8AA3; padding: 6px 10px 4px; }
.item {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 12px; border-radius: 8px;
  color: #404040; font-size: 13px;
  cursor: pointer; user-select: none;
}
.item:hover { background: #F7F7FB; }
.item.active { background: #EEEDFE; color: #4F46E5; font-weight: 500; }
.item.soon { opacity: 0.55; cursor: not-allowed; }
.item.soon:hover { background: transparent; }
.label { flex: 1; }
.badge {
  font-size: 10px; color: #8A8AA3;
  border: 0.5px solid #ECECF2; border-radius: 4px;
  padding: 0 4px; line-height: 14px; height: 14px;
}
</style>