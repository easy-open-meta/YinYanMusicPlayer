import { createApp } from 'vue';
import { createPinia } from 'pinia';
import ElementPlus from 'element-plus';
import 'element-plus/dist/index.css';
import zhCn from 'element-plus/es/locale/lang/zh-cn';
import * as ElementPlusIconsVue from '@element-plus/icons-vue';

import App from './App.vue';
import { router } from './router';
import { useAuthStore } from './stores/auth';

import './styles/element-override.css';
import './styles/material-icons.css';

(async () => {
  // 根组件用 App.vue（SFC）：模板在构建期由 @vitejs/plugin-vue 编译成渲染函数。
  // ⚠️ 不要改回 createApp({ template: '<router-view />' })：Vue 的默认导出是
  // runtime-only 构建（不含模板编译器），运行时传 template 字符串会抛
  // "runtime compilation is not supported"，此时 mount 不会执行 —— 页面一片空白，
  // 只有打开控制台才看得到报错。
  const app = createApp(App);

  app.use(createPinia());
  app.use(ElementPlus, { locale: zhCn });

  // 全局注册 Element Plus 图标。
  // ⚠️ 必需：SideMenu / TopBar 等用 <component :is="item.icon" /> 按名字渲染图标
  // （'Odometer'、'List'…）。不注册的话 Vue 会去解析一个不存在的组件，
  // 每次渲染都走一遍「解析失败 + 警告」流程，侧边菜单点击出现明显延迟甚至无响应。
  for (const [name, comp] of Object.entries(ElementPlusIconsVue)) {
    app.component(name, comp);
  }

  app.use(router);

  // 启动时恢复登录态（刷新保持登录）。
  // 用 try/catch 包住：localStorage 里的 user JSON 若损坏（手工改过 / 旧版本残留），
  // restore 里的 JSON.parse 会抛错；不兜住的话 mount 永远不会执行 —— 又是白屏。
  // 恢复失败只当未登录处理，不能拖垮整个应用启动。
  try {
    await useAuthStore().restore();
  } catch (e) {
    console.error('[启动] 恢复登录态失败，按未登录处理：', e);
  }

  await router.isReady();
  app.mount('#app');
})();