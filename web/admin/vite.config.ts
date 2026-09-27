import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import AutoImport from 'unplugin-auto-import/vite';
import Components from 'unplugin-vue-components/vite';
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers';
import path from 'node:path';

// 后端地址不在这里配置 —— 见 .env.development 的 VITE_API_BASEURL / VITE_MEDIA_BASEURL。
// 前端直连后端，不走 Vite 代理（原因见下方 server 段注释）。

export default defineConfig({
  plugins: [
    vue(),
    AutoImport({ resolvers: [ElementPlusResolver()] }),
    Components({ resolvers: [ElementPlusResolver()] }),
  ],
  // ⚠️ optimizeDeps.include 是必需的，不是性能优化。
  // Element Plus 按需引入（unplugin-vue-components）的样式是「用到哪个组件才 import 哪个 css」，
  // Vite 预构建只在首次启动时扫描到已存在的入口 —— 于是每进一个新页面（首次出现某组件），
  // 它就发现新依赖 → 重新预构建 → 触发「optimized dependencies changed. reloading」
  // 整页重载。用户点击落在重载窗口里就被丢弃，表现为「点菜单没反应、要点好几次」。
  // 这里把全部用到的组件样式预先登记，启动时一次性预构建完，运行期不再重载。
  optimizeDeps: {
    include: [
      'element-plus/es',
      // 按需样式：与 unplugin 实际解析出的路径一致（见 vite 日志的 new dependencies optimized）
      'element-plus/es/components/base/style/css',
      'element-plus/es/components/alert/style/css',
      'element-plus/es/components/avatar/style/css',
      'element-plus/es/components/button/style/css',
      'element-plus/es/components/card/style/css',
      'element-plus/es/components/checkbox/style/css',
      'element-plus/es/components/color-picker/style/css',
      'element-plus/es/components/date-picker/style/css',
      'element-plus/es/components/dialog/style/css',
      'element-plus/es/components/form/style/css',
      'element-plus/es/components/form-item/style/css',
      'element-plus/es/components/icon/style/css',
      'element-plus/es/components/image/style/css',
      'element-plus/es/components/input/style/css',
      'element-plus/es/components/input-number/style/css',
      'element-plus/es/components/loading/style/css',
      'element-plus/es/components/message/style/css',
      'element-plus/es/components/message-box/style/css',
      'element-plus/es/components/option/style/css',
      'element-plus/es/components/pagination/style/css',
      'element-plus/es/components/select/style/css',
      'element-plus/es/components/switch/style/css',
      'element-plus/es/components/table/style/css',
      'element-plus/es/components/table-column/style/css',
      'element-plus/es/components/tag/style/css',
      // 常用运行时依赖也一并预构建，避免首访页面时抖动
      'axios', 'pinia', 'vue-router',
      '@element-plus/icons-vue',
    ],
  },
  resolve: {
    alias: [
      { find: /^@layout\/(.*)$/, replacement: path.resolve(__dirname, 'src/layout') + '/$1' },
      { find: /^@api\/(.*)$/,    replacement: path.resolve(__dirname, 'src/api')    + '/$1' },
      { find: /^@stores\/(.*)$/, replacement: path.resolve(__dirname, 'src/stores') + '/$1' },
      { find: '@',              replacement: path.resolve(__dirname, 'src') },
    ],
  },
  server: {
    port: 5173,
    host: '0.0.0.0',
    // ⚠️ 这里刻意不配置 /api 与 /media 代理，全部由前端直连后端。
    //
    // 原因（已实测确认）：Vite dev server 的 http-proxy 在连续/并发请求下与 Kestrel
    // 存在 TCP 连接竞态，约 1/4 请求失败 —— 代理日志 `read ECONNRESET`，前端收到
    // 502 或空 body 的 500。典型表现：「切换列表分页报 500」「列表偶尔加载不出来」。
    //   对照实验（同一后端同一接口，各 25 次翻页）：
    //     经 Vite 代理 → 失败 7 次；直连后端 → 失败 0 次
    // 试过 agent:false + Connection:close，Vite 6 的内置 http-proxy 下仍会复现，
    // 因此改为彻底绕开代理：
    //   - API   走 .env.development 的 VITE_API_BASEURL（axios baseURL）
    //   - 静态资源走 VITE_MEDIA_BASEURL（见 src/api/media.ts）
    // 后端 CORS 全开，跨端口直连无障碍。生产形态由 Nginx 反代，与此无关。
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
    chunkSizeWarningLimit: 800,
  },
});