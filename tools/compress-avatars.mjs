// 压缩 App 内置头像（就地覆盖 Resources/Raw/avatars/*.png）。
//
// 为什么需要这一步：这批头像是带渐变/描边的插画，直接存 PNG 单张 130KB 左右；
// 而头像是 data URI 存库的，会**随每条评论下发** —— 一页 20 条评论最多多出约 3.5MB 响应。
// 转成 128 色调色板 PNG 后单张约 11~14KB，同屏对比看不出差别，且保留透明通道。
//
// 用法（需要 sharp，一次性工具，不进项目依赖）：
//     NODE_PATH=<带 sharp 的 node_modules> node tools/compress-avatars.mjs
//
// ⚠️ 原地覆盖，**不保留原图** —— 需要原图请从出图源重新导出。
// 幂等：只在结果更小时才写回，重复跑不会越压越糊。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const dir = path.join(import.meta.dirname, '..', 'src', 'YinYanMusic.App', 'Resources', 'Raw', 'avatars');
const files = fs.readdirSync(dir).filter((f) => f.toLowerCase().endsWith('.png')).sort();

for (const file of files) {
  const full = path.join(dir, file);
  const before = fs.statSync(full).size;
  const compressed = await sharp(full).png({ palette: true, colours: 128, compressionLevel: 9 }).toBuffer();
  if (compressed.length < before) fs.writeFileSync(full, compressed);

  const after = fs.statSync(full).size;
  console.log(`${file}  ${(before / 1024).toFixed(0)}KB → ${(after / 1024).toFixed(0)}KB`);
}
