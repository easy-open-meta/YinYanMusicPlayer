// 路由 + 守卫：
//   /register       仅 registration-open?source=admin 为 true 时可达（创建超管后永远消失）
//   /login          登录页
//   /               AdminLayout（要登录 + admin）
//     /dashboard    仪表盘
//     /songs        歌曲
//     /albums       专辑
//     /artists      歌手
//     /categories   分区
//     /playlists    歌单
//     /recommended  推荐位管理（V2.12）
//     其他 M2+ 页面（M3 歌单/用户 M4 系统任务）

import { createRouter, createWebHistory } from 'vue-router';
import { ElMessage } from 'element-plus';
import { useAuthStore } from '@/stores/auth';
import { ApiException } from '@/api/http';

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/dashboard' },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/Login.vue'),
      meta: { public: true, title: '登录' },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/Register.vue'),
      meta: { public: true, title: '创建超级管理员' },
      // 仅当还没有超管时可达；守卫内异步查接口决定。
      // ⚠️ 接口失败必须兜住：让异常冒出去会中断导航，用户点链接表现为"没反应"。
      // 这里失败时给出明确提示并回登录页，而不是静默卡住。
      beforeEnter: async () => {
        try {
          const { getRegistrationOpen } = await import('@/api/auth');
          const { open } = await getRegistrationOpen('admin');
          if (open) return true;
          ElMessage.warning('已存在超级管理员，无需再注册。请直接登录。');
          return { name: 'login' };
        } catch (e) {
          const msg = e instanceof ApiException ? e.message : '无法连接后端服务';
          ElMessage.error(`无法确认注册状态：${msg}`);
          return { name: 'login' };
        }
      },
    },
    {
      path: '/',
      component: () => import('@/layout/AdminLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        { path: 'dashboard',   name: 'dashboard',   component: () => import('@/views/Dashboard.vue'),   meta: { title: '仪表盘' } },
        { path: 'songs',       name: 'songs',       component: () => import('@/views/Songs.vue'),       meta: { title: '歌曲' } },
        { path: 'albums',      name: 'albums',      component: () => import('@/views/Albums.vue'),      meta: { title: '专辑' } },
        { path: 'artists',     name: 'artists',     component: () => import('@/views/Artists.vue'),     meta: { title: '歌手' } },
        { path: 'categories',  name: 'categories',  component: () => import('@/views/Categories.vue'),  meta: { title: '分区' } },
        { path: 'playlists',   name: 'playlists',   component: () => import('@/views/Playlists.vue'),   meta: { title: '歌单' } },
        { path: 'users',       name: 'users',       component: () => import('@/views/Users.vue'),       meta: { title: '用户' } },
        { path: 'comments',    name: 'comments',    component: () => import('@/views/Comments.vue'),    meta: { title: '评论管理' } },
        { path: 'recommended', name: 'recommended', component: () => import('@/views/Recommended.vue'), meta: { title: '推荐位管理' } },
        { path: 'notifications', name: 'notifications', component: () => import('@/views/Notifications.vue'), meta: { title: '消息推送' } },
        { path: 'system/tasks', name: 'system-tasks', component: () => import('@/views/SystemTasks.vue'), meta: { title: '导入与扫描' } },
        { path: 'system/dicts', name: 'system-dicts', component: () => import('@/views/Dicts.vue'), meta: { title: '数据字典' } },
      ],
    },
    { path: '/:pathMatch(.*)*', name: 'notFound', component: () => import('@/views/NotFound.vue'), meta: { public: true } },
  ],
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();
  if (!auth.token) await auth.restore();

  if (to.meta.requiresAuth) {
    if (!auth.isLoggedIn) return { name: 'login', query: { redirect: to.fullPath } };
    if (!auth.isAdmin) return { name: 'login' };
  }
  return true;
});

router.afterEach((to) => {
  const base = '音言音乐 · 后台管理';
  document.title = to.meta.title ? `${to.meta.title} · ${base}` : base;
});