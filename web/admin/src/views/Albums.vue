<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { catalogApi } from '@/api/catalog';
import { mediaUrl } from '@/api/media';
import { useCrudTable } from '@/composables/useCrudTable';
import ImageFieldInput from '@/components/ImageFieldInput.vue';
import type { AlbumDto } from '@/api/catalog';

// API 返回的 AlbumDto 现在带 artists: SongArtistRef[]（V2.12 联合创作，首位 = 主歌手）。
// 用本地编辑草稿类型，包含所有需要编辑的字段 + 表格展示用的 artist 引用。
interface AlbumEditDraft {
  /** 编辑行有 id；新建时为 undefined，由 handleSave 内部判断走 create/update。 */
  id?: number;
  artistId: number;
  name: string;
  releaseDate?: string | null;
  description?: string | null;
  coverUrl?: string | null;
  /** 表格展示用：保存时不上送。 */
  artist?: AlbumDto['artist'];
  /** 全部歌手（V2.12）：按顺序，首位 = 主歌手；读接口返回。 */
  artists?: { id: number; name: string }[] | null;
  clearReleaseDate?: boolean;
}

function dtoToDraft(d: AlbumDto): AlbumEditDraft {
  return {
    id: d.id,
    artistId: d.artist?.id ?? 0,
    name: d.name,
    releaseDate: d.releaseDate ?? null,
    description: d.description ?? null,
    coverUrl: d.coverUrl ?? null,
    artist: d.artist,
    artists: d.artists ?? (d.artist ? [{ id: d.artist.id, name: d.artist.name }] : []),
  };
}

function draftToCreate(d: AlbumEditDraft) {
  const artistIds = selectedArtistIds.value.length > 0 ? [...selectedArtistIds.value] : [d.artistId];
  return { artistId: artistIds[0], artistIds, name: d.name, releaseDate: d.releaseDate ?? null, description: d.description ?? null, coverUrl: d.coverUrl ?? null };
}
function draftToUpdate(d: AlbumEditDraft) {
  const artistIds = selectedArtistIds.value.length > 0 ? [...selectedArtistIds.value] : [d.artistId];
  return { name: d.name, releaseDate: d.releaseDate ?? null, description: d.description ?? null, coverUrl: d.coverUrl ?? null, clearReleaseDate: d.clearReleaseDate ?? false, clearCover: !d.coverUrl, artistIds };
}

const artists = ref<{ id: number; name: string }[]>([]);
async function loadArtists() {
  const r = await catalogApi.artists.list({ page: 1, pageSize: 500 });
  artists.value = r.items.map((a) => ({ id: a.id, name: a.name }));
}

const table = useCrudTable<AlbumEditDraft, ReturnType<typeof draftToCreate>, ReturnType<typeof draftToUpdate>>({
  list: (p) => catalogApi.albums.list({ keyword: p.keyword, page: p.page, pageSize: p.pageSize })
    .then(r => {
      const items = r.items.map(dtoToDraft);
      return { items, total: r.total, page: r.page, pageSize: r.pageSize };
    }),
  create: async (body) => dtoToDraft(await catalogApi.albums.create(body)),
  update: async (id, body) => dtoToDraft(await catalogApi.albums.update(id, body)),
  remove: (id) => catalogApi.albums.delete(id),
}, {
  confirmMessage: (i) => `确定要删除专辑「${i.name}」吗？`,
  // 新建时的表单骨架：表单用 v-if="table.editing" 包着，必须有初始值才渲染得出来
  emptyDraft: () => ({
    id: 0,
    artistId: undefined as unknown as number,
    name: '',
    releaseDate: null,
    description: null,
    coverUrl: null,
  }),
});

onMounted(async () => {
  // 同 Songs.vue：歌手下拉是附属信息，失败不能阻塞专辑列表加载。
  void loadArtists().catch((e) => {
    console.warn('[专辑页] 歌手下拉加载失败，不影响列表：', e);
  });
  await table.fetch();
});

const clearRelease = ref(false);
watch(() => table.editing, () => { clearRelease.value = false; });
const isClearRelease = () => clearRelease.value;

// 歌手列展示：全部关联歌手用 " / " 连接（首位 = 主歌手）；关联列表缺失时退回主歌手名。
function displayArtists(row: AlbumEditDraft) {
  if (row.artists && row.artists.length > 0) return row.artists.map((a) => a.name).join(' / ');
  return row.artist?.name ?? '—';
}

// 歌手多选（V2.12 联合创作专辑）：[主歌手, ...合作歌手]，保存时整体提交 artistIds。
// 打开编辑对话框时回填当前专辑的歌手列表；关闭/新建时重置。
const selectedArtistIds = ref<number[]>([]);
watch(() => table.dialogOpen, (open) => {
  if (!open) { selectedArtistIds.value = []; return; }
  const d = table.editing;
  if (!d) { selectedArtistIds.value = []; return; }
  selectedArtistIds.value = (d.artists && d.artists.length > 0)
    ? d.artists.map((a) => a.id)
    : (d.artistId ? [d.artistId] : []);
});
</script>

<template>
  <div class="page">
    <div class="toolbar">
      <el-input
        v-model="table.keyword"
        placeholder="搜索专辑 / 歌手"
        prefix-icon="Search"
        clearable
        style="width: 220px"
        @keyup.enter="table.onSearch"
        @clear="table.onSearch"
      />
      <el-button type="primary" @click="table.handleNew">+ 新建专辑</el-button>
    </div>

    <el-table :data="table.items" v-loading="table.loading" stripe>
      <el-table-column prop="id"      label="ID"     width="80" />
      <el-table-column label="封面"   width="70">
        <template #default="{ row }">
          <!-- ⚠️ 不要给表格里的 el-image 传 :preview-src-list。
               它在每行渲染时都创建新数组，触发 Element Plus 的 el-image-viewer
               全屏预览组件反复挂载/卸载；该组件的卸载会阻塞 Vue Router 的导航，
               表现为「在专辑页点任何菜单都没反应，要离开这一页才能点动」。
               需要预览就放到详情弹窗里做，表格只显示缩略图。 -->
          <el-image
            v-if="row.coverUrl"
            :src="mediaUrl(row.coverUrl)"
            lazy
            fit="cover"
            style="width: 40px; height: 40px; border-radius: 6px"
          />
          <div v-else class="cover-placeholder">封</div>
        </template>
      </el-table-column>
      <el-table-column prop="name"    label="专辑名"   min-width="160" />
      <!-- 歌手列（V2.12 联合创作）：显示全部关联歌手（后端按 Position 排，首位 = 主歌手），
           之间用 " / " 连接；artists 缺失时退回主歌手名。 -->
      <el-table-column label="歌手" min-width="140">
        <template #default="{ row }">{{ displayArtists(row) }}</template>
      </el-table-column>
      <el-table-column prop="releaseDate" label="发行日期" width="120" />
      <el-table-column prop="description" label="简介"     min-width="200" show-overflow-tooltip />
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
      :title="table.editing?.id ? '编辑专辑' : '新建专辑'"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-form v-if="table.editing" label-width="84">
        <el-form-item label="专辑名" required>
          <el-input v-model="table.editing.name" placeholder="专辑名称" maxlength="120" />
        </el-form-item>
        <!-- 歌手多选（V2.12 联合创作专辑）：选择顺序决定 Position —— 第一个 = 主歌手 -->
        <el-form-item label="所属歌手" required>
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
        <el-form-item label="发行日期">
          <div class="date-row">
            <el-date-picker
              v-model="table.editing.releaseDate"
              type="date"
              value-format="YYYY-MM-DD"
              placeholder="YYYY-MM-DD"
              :disabled="isClearRelease()"
              style="flex: 1"
            />
            <el-checkbox v-model="clearRelease" style="margin-left: 12px">清空</el-checkbox>
          </div>
        </el-form-item>
        <el-form-item label="封面">
          <!-- 外链 / 相对路径 / 选本地图片（自动压缩转 base64）都行 -->
          <ImageFieldInput v-model="table.editing.coverUrl" />
        </el-form-item>
        <el-form-item label="简介">
          <el-input
            v-model="table.editing.description"
            type="textarea"
            :rows="3"
            maxlength="500"
            show-word-limit
            placeholder="专辑简介"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="table.closeDialog">取消</el-button>
        <el-button
          type="primary"
          :loading="table.saving"
          @click="table.handleSave(() => {
            const d = table.editing!;
            return d.id
              ? { id: d.id, update: draftToUpdate({ ...d, clearReleaseDate: isClearRelease() }) }
              : { create: draftToCreate(d) };
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
  background: #F2F2F7; color: #8A8AA3;
  display: grid; place-items: center; font-size: 12px;
}
.date-row { display: flex; align-items: center; width: 100%; }
</style>
