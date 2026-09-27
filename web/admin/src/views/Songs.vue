<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { catalogApi, type SongDto, type CreateSongRequest } from '@/api/catalog';
import { mediaUrl } from '@/api/media';
import { useCrudTable } from '@/composables/useCrudTable';

const artists = ref<{ id: number; name: string }[]>([]);
const albums = ref<{ id: number; name: string }[]>([]);
const categories = ref<{ id: number; name: string }[]>([]);
// 歌手多选（V2.12 联合创作）：[主歌手, ...合作歌手]，保存时整体提交 artistIds。
// 独立于 editing 状态：打开编辑对话框时回填当前歌曲的歌手列表，切换/关闭时重置。
// ⚠️ 回填的 watch 必须放在 table 声明之后（回调里引用了 table）——放这里会 TDZ 报错。
const selectedArtistIds = ref<number[]>([]);

async function loadLookups() {
  const [ar, al, cat] = await Promise.all([
    catalogApi.artists.list({ page: 1, pageSize: 500 }),
    catalogApi.albums.list({ page: 1, pageSize: 500 }),
    catalogApi.categories.list(),
  ]);
  artists.value = ar.items.map((a) => ({ id: a.id, name: a.name }));
  albums.value = al.items.map((a) => ({ id: a.id, name: a.name }));
  categories.value = cat.map((c) => ({ id: c.id, name: c.name }));
}

// 编辑载荷：在"可选字段"之上补两个 clear 标志（见 dialog 里保存处的说明）。
type SongUpdateBody = Partial<CreateSongRequest> & { clearCategory?: boolean; clearAlbum?: boolean };

const table = useCrudTable<SongDto, CreateSongRequest, SongUpdateBody>({
  list: (p) => catalogApi.songs.list({ keyword: p.keyword, page: p.page, pageSize: p.pageSize }),
  create: (body) => catalogApi.songs.create(body),
  update: (id, body) => catalogApi.songs.update(id, body),
  remove: (id) => catalogApi.songs.delete(id),
}, {
  confirmMessage: (i) => `确定要删除歌曲「${i.title}」吗？此操作会同时删除用户喜欢记录与歌单中间表关联。`,
  // 新建时的表单骨架：表单用 v-if="table.editing" 包着，必须有初始值才渲染得出来
  emptyDraft: () => ({
    id: 0,
    title: '',
    artistId: undefined as unknown as number,
    albumId: null,
    categoryId: null,
    coverUrl: null,
    audioUrl: '',
    lyricUrl: null,
    durationSeconds: 0,
    playCount: 0,
  } as unknown as SongDto),
});

// 回填多选歌手：table.handleEdit 打开对话框前不会通知我们，用 watch 监听对话框开关。
// 新建：空列表；编辑：回填当前歌手（后端 artists 按顺序，首位 = 主歌手）；关闭：清空。
watch(() => table.dialogOpen, (open) => {
  if (!open) { selectedArtistIds.value = []; return; }
  const d = table.editing;
  if (!d) { selectedArtistIds.value = []; return; }
  selectedArtistIds.value = (d.artists && d.artists.length > 0)
    ? d.artists.map((a) => a.id)
    : (d.artistId ? [d.artistId] : []);
});

// 时长 mm:ss 显示
function fmtDuration(s: number) {
  if (!s || s <= 0) return '—';
  const m = Math.floor(s / 60);
  const ss = s % 60;
  return `${m}:${ss.toString().padStart(2, '0')}`;
}

// 歌手列展示：全部关联歌手用 " / " 连接（首位 = 主歌手）；没有关联列表时退回主歌手名。
function displayArtists(row: SongDto) {
  if (row.artists && row.artists.length > 0) return row.artists.map((a) => a.name).join(' / ');
  return row.artistName || '—';
}

onMounted(async () => {
  // ⚠️ 下拉数据（歌手/专辑/分区）是列表的附属信息，加载失败不能拖累主列表。
  // 原来写成 `await loadLookups(); await table.fetch();` 串行：loadLookups 一抛错，
  // table.fetch() 就永远不执行 —— 表现为"进了页面但列表一直空白、无报错提示"。
  // 这里让两者互不阻塞，主列表始终会加载。
  void loadLookups().catch((e) => {
    console.warn('[歌曲页] 下拉数据加载失败，不影响列表：', e);
  });
  await table.fetch();
});
</script>

<template>
  <div class="page">
    <div class="toolbar">
      <el-input
        v-model="table.keyword"
        placeholder="搜索歌曲 / 歌手 / 专辑"
        prefix-icon="Search"
        clearable
        style="width: 280px"
        @keyup.enter="table.onSearch"
        @clear="table.onSearch"
      />
      <el-button type="primary" @click="table.handleNew">+ 新建歌曲</el-button>
    </div>

    <el-table :data="table.items" v-loading="table.loading" stripe>
      <el-table-column prop="id"       label="ID"     width="80" />
      <el-table-column label="封面"    width="70">
        <template #default="{ row }">
          <!-- lazy：只加载进入视口的封面。列表一页 20 行，不加 lazy 会一次性并发
               20 个图片请求，把 dev 代理的连接打满并触发 ECONNRESET 风暴。 -->
          <el-image
            v-if="row.coverUrl"
            :src="mediaUrl(row.coverUrl)"
            lazy
            fit="cover"
            style="width: 40px; height: 40px; border-radius: 6px"
          />
          <div v-else class="cover-placeholder">音</div>
        </template>
      </el-table-column>
      <el-table-column prop="title"    label="歌曲名"  min-width="160" />
      <!-- 歌手列（V2.12 联合创作）：显示全部关联歌手（后端已按 Position 排好，首位 = 主歌手），
           之间用 " / " 连接；与 App 端 ArtistsDisplay 同一口径。artists 为空时退回主歌手名。 -->
      <el-table-column label="歌手"   min-width="140">
        <template #default="{ row }">{{ displayArtists(row) }}</template>
      </el-table-column>
      <el-table-column label="专辑"   min-width="120">
        <template #default="{ row }">{{ row.albumName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="时长"   width="80">
        <template #default="{ row }">{{ fmtDuration(row.durationSeconds) }}</template>
      </el-table-column>
      <el-table-column label="播放"   width="90">
        <template #default="{ row }">
          <span class="stat-num">{{ row.playCount }}</span>
        </template>
      </el-table-column>
      <el-table-column label="音频 URL" min-width="200" show-overflow-tooltip>
        <template #default="{ row }">
          <code class="audio-url">{{ row.audioUrl }}</code>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="table.handleEdit(row)">编辑</el-button>
          <el-button link type="danger"   @click="table.handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="table.total > 0"
      :total="table.total"
      :current-page="table.page"
      :page-size="table.pageSize"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next"
      style="margin-top: 14px; justify-content: flex-end"
      @current-change="table.onPageChange"
      @size-change="table.onPageSizeChange"
    />

    <el-dialog
      v-model="table.dialogOpen"
      :title="table.editing?.id ? '编辑歌曲' : '新建歌曲'"
      width="540px"
      :close-on-click-modal="false"
    >
      <el-form v-if="table.editing" label-width="92">
        <el-form-item label="标题" required>
          <el-input v-model="table.editing.title" placeholder="歌曲标题" maxlength="120" />
        </el-form-item>
        <!-- 歌手多选（V2.12 联合创作）：按选择顺序决定 Position —— 第一个 = 主歌手，
             其余 = 合作歌手。el-select multiple 自带 tag 删除，拖拽排序不做（场景低频）。 -->
        <el-form-item label="歌手" required>
          <el-select
            v-model="selectedArtistIds"
            placeholder="选择歌手（可多选，第一个为主歌手）"
            filterable
            multiple
            style="width: 100%"
          >
            <el-option v-for="a in artists" :key="a.id" :label="a.name" :value="a.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="专辑">
          <el-select v-model="table.editing.albumId" placeholder="可选" clearable filterable style="width: 100%">
            <el-option
              v-for="a in albums"
              :key="a.id"
              :label="a.name"
              :value="a.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="分类">
          <el-select
            v-model="table.editing.categoryId"
            placeholder="可选"
            clearable
            filterable
            style="width: 100%"
          >
            <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="音频 URL" required>
          <el-input v-model="table.editing.audioUrl" placeholder="/media/music/xxx.mp3 或 https://..." />
        </el-form-item>
        <el-form-item label="歌词 URL">
          <el-input v-model="table.editing.lyricUrl" placeholder="可选，LRC 文件" />
        </el-form-item>
        <el-form-item label="时长(秒)" required>
          <el-input-number v-model="table.editing.durationSeconds" :min="0" :max="99999" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="table.closeDialog">取消</el-button>
        <el-button
          type="primary"
          :loading="table.saving"
          @click="table.handleSave(() => {
            const d = table.editing!;
            // 专辑 / 分类是可空外键：后端把 null 当「这次不改」，所以「框里为空」要额外用
            // clear* 标志表达 —— 不这样，下拉自带的 × 点了也白点。表单整份提交，空即清空。
            const albumId = d.albumId ?? null;
            const categoryId = d.categoryId ?? null;
            // 歌手（V2.12）：selectedArtistIds[0] = 主歌手；传 artistIds 让后端整体重建关联。
            // 兼容兜底：多选框意外为空时退回原 artistId，避免空列表把关联清没。
            // （模板表达式里 ref 自动解包，不要写 .value）
            const artistIds = selectedArtistIds.length > 0
              ? [...selectedArtistIds]
              : [d.artistId!];
            const body = {
              title: d.title!,
              artistId: artistIds[0],
              artistIds,
              albumId,
              categoryId,
              audioUrl: d.audioUrl!,
              lyricUrl: d.lyricUrl ?? null,
              durationSeconds: d.durationSeconds ?? 0,
            };
            return d.id
              ? { id: d.id, update: { ...body, clearAlbum: albumId === null, clearCategory: categoryId === null } }
              : { create: body };
          })"
        >保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
.cover-placeholder {
  width: 40px; height: 40px; border-radius: 6px;
  background: #EEEDFE; color: #4F46E5;
  display: grid; place-items: center; font-size: 12px;
}
.stat-num { font-variant-numeric: tabular-nums; color: #8A8AA3; font-size: 13px; }
.audio-url {
  font-size: 12px; color: #8A8AA3;
  background: #F7F7FB; padding: 1px 6px; border-radius: 4px;
}
</style>
