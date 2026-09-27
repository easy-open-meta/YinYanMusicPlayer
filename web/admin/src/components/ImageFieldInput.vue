<script setup lang="ts">
// 图片字段输入：可以直接填外链 / 相对路径，也可以选本地图片 —— 在浏览器里压缩后转成
// base64（data URI）一起提交，后端按普通字符串存进 `text` 列（专辑封面 / 歌手头像都是）。
//
// 为什么必须先压缩：这两个字段会进入**每一行列表响应**（歌曲列表的封面还会拿专辑封面兜底），
// 一张几 MB 的原图 base64 能把一页列表撑到几十 MB。缩到 512px / JPEG 0.85 后约 30~60KB，
// 列表缩略图和播放页都够看。后端 ImageDataUri 的 2MB 上限是第二道闸，别只留一道。
import { computed, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { mediaUrl } from '@/api/media';

const props = withDefaults(defineProps<{ modelValue?: string | null; maxSide?: number }>(), {
  maxSide: 512,
});
const emit = defineEmits<{ (e: 'update:modelValue', v: string | null): void }>();

const QUALITY = 0.85;
/** 与后端 ImageDataUri.MaxDecodedBytes 对齐。 */
const MAX_DECODED_BYTES = 2 * 1024 * 1024;

const fileInput = ref<HTMLInputElement | null>(null);
const busy = ref(false);

const isDataUri = computed(() => (props.modelValue ?? '').startsWith('data:'));
const preview = computed(() => mediaUrl(props.modelValue));

/** base64 的体积（按解码后估算），只用于显示。 */
const storedSize = computed(() => {
  const v = props.modelValue ?? '';
  const comma = v.indexOf(',');
  if (!isDataUri.value || comma < 0) return '';
  const bytes = Math.floor(((v.length - comma - 1) * 3) / 4);
  return bytes >= 1024 ? `${Math.round(bytes / 1024)} KB` : `${bytes} B`;
});

function choose() {
  fileInput.value?.click();
}

function onInput(v: string) {
  emit('update:modelValue', v.trim() === '' ? null : v);
}

function clear() {
  emit('update:modelValue', null);
}

async function onFile(e: Event) {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = ''; // 清掉，才能连续选同一个文件
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    ElMessage.error('请选择图片文件（PNG / JPG / WebP / GIF）。');
    return;
  }

  busy.value = true;
  try {
    const dataUri = await shrinkToDataUri(file);
    const bytes = decodedBytes(dataUri);
    if (bytes > MAX_DECODED_BYTES) {
      ElMessage.error(`图片压缩后仍有 ${(bytes / 1024 / 1024).toFixed(1)}MB，请换一张更小的图。`);
      return;
    }
    emit('update:modelValue', dataUri);
    ElMessage.success(`已转为 base64（${Math.round(bytes / 1024)} KB），保存后即生效，不依赖图片文件部署`);
  } catch (err) {
    ElMessage.error(`图片处理失败：${(err as Error).message}`);
  } finally {
    busy.value = false;
  }
}

/**
 * 缩到 ≤ maxSide 并转 JPEG（白底铺满）。
 * 白底是刻意的：JPEG 没有透明通道，直接画会在透明区域出黑块（带透明通道的 PNG 常见）。
 */
async function shrinkToDataUri(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file);
  try {
    const longest = Math.max(bitmap.width, bitmap.height);
    const scale = Math.min(1, props.maxSide / longest);
    const w = Math.max(1, Math.round(bitmap.width * scale));
    const h = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('当前浏览器不支持 canvas');

    ctx.fillStyle = '#FFFFFF';
    ctx.fillRect(0, 0, w, h);
    ctx.drawImage(bitmap, 0, 0, w, h);

    return canvas.toDataURL('image/jpeg', QUALITY);
  } finally {
    bitmap.close?.();
  }
}

function decodedBytes(dataUri: string): number {
  const comma = dataUri.indexOf(',');
  return comma < 0 ? 0 : Math.floor(((dataUri.length - comma - 1) * 3) / 4);
}
</script>

<template>
  <div class="img-field">
    <!-- 已存 base64：不把几万字符塞进输入框，只给摘要 + 两个动作 -->
    <div v-if="isDataUri" class="img-field__stored">
      <el-tag type="success" size="small">已存 base64 图片（{{ storedSize }}）</el-tag>
      <el-button link type="primary" size="small" :loading="busy" @click="choose">重新选择</el-button>
      <el-button link type="danger" size="small" @click="clear">改为填 URL</el-button>
    </div>
    <el-input
      v-else
      :model-value="modelValue ?? ''"
      placeholder="https://… 或点右侧「选图片」自动转 base64"
      @update:model-value="onInput"
    >
      <template #append>
        <el-button :loading="busy" @click="choose">选图片</el-button>
      </template>
    </el-input>

    <div class="img-field__preview-row">
      <img v-if="preview" :src="preview" class="img-field__preview" alt="预览" />
      <span v-else class="img-field__empty">无图</span>
    </div>

    <!-- 隐藏的原生 file input：只用它的选择能力，选完立刻清空 -->
    <input ref="fileInput" type="file" accept="image/*" class="img-field__file" @change="onFile" />
  </div>
</template>

<style scoped>
.img-field { width: 100%; }
.img-field__stored { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.img-field__preview-row { margin-top: 8px; }
.img-field__preview {
  width: 64px; height: 64px; object-fit: cover;
  border-radius: 6px; border: 0.5px solid #ECECF2; background: #F7F7FB;
}
.img-field__empty { font-size: 12px; color: #A8ABB2; }
.img-field__file { display: none; }
</style>
