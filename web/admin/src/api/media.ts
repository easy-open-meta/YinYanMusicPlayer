// 静态资源 URL 解析。
//
// ⚠️ 为什么需要它（这是「点左侧菜单没反应」的根治点）：
// 后端返回的封面/音频是相对路径（`/media/image/xxx.jpg`）。开发环境下若让它们走
// Vite 的 `/media` 代理，列表页一页并发请求 20+ 张封面（单张最大 400KB+），
// 会把浏览器同源 HTTP/1.1 的 6 条并发连接全部占满 —— 此时路由懒加载 chunk
// （`import('@/views/Xxx.vue')`）的请求只能排队，Vue Router 的导航因此永久 pending
// （既不 resolve 也不 reject），表现为「点菜单毫无反应、要点好几次」。
//   对照实验：屏蔽 /media → 9/9 次导航成功；不屏蔽 → 4/9 成功。
//
// 修法：开发环境把 `/media/...` 解析成后端的绝对地址，让浏览器直连后端取资源，
// 不再占用应用同源连接。
//
// ⚠️ 生产环境兼容（V2.15）：Nginx 通常只反代 `/api`，不反代 `/media` ——
// 因此这里把 `/media/...` 统一重写为 `/api/media/...`（服务端 `api/media` 端点
// 从音乐/图片目录返回文件），走 `/api` 反代即可，不再依赖额外反代 `/media`。

/** 后端资源基址。开发环境默认指向本机 API；生产留空（走 Nginx 同源反代）。 */
const MEDIA_BASE = (import.meta.env.VITE_MEDIA_BASEURL ?? '').replace(/\/+$/, '');

/**
 * 把后端返回的资源路径解析成可直接用于 <img src> 的地址。
 * - 绝对地址（http/https/data:）原样返回；
 * - 相对 `/media/...` 先重写为 `/api/media/...`，再在配置了基址时加上基址前缀；
 * - 其余（空值等）原样返回。
 */
export function mediaUrl(url?: string | null): string {
  if (!url) return '';
  if (/^(https?:|data:|blob:)/i.test(url)) return url;
  if (url.startsWith('/media/')) url = '/api' + url;
  if (MEDIA_BASE && url.startsWith('/')) return MEDIA_BASE + url;
  return url;
}
