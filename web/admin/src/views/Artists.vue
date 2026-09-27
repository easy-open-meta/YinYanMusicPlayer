<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { catalogApi } from '@/api/catalog';
import { dictApi } from '@/api/dict';
import { useCrudTable } from '@/composables/useCrudTable';
import ImageFieldInput from '@/components/ImageFieldInput.vue';
import type { ArtistDto, CreateArtistRequest } from '@/api/catalog';

const table = useCrudTable<ArtistDto, CreateArtistRequest, Partial<CreateArtistRequest>>({
  list: (p) => catalogApi.artists.list({ keyword: p.keyword, page: p.page, pageSize: p.pageSize }),
  create: (body) => catalogApi.artists.create(body),
  update: (id, body) => catalogApi.artists.update(id, body),
  remove: (id) => catalogApi.artists.delete(id),
}, {
  confirmMessage: (i) => `确定要删除歌手「${i.name}」吗？删除后会一并删除其下所有专辑与歌曲。`,
  // 新建时的表单骨架：表单用 v-if="table.editing" 包着，必须有初始值才渲染得出来
  emptyDraft: () => ({
    id: 0,
    name: '',
    region: null,
    kind: null,
    avatarUrl: null,
    bio: null,
    followerCount: 0,
    songCount: 0,
    albumCount: 0,
  }),
});

onMounted(() => {
  void table.fetch();
  void loadDictOptions();
});

// ── 地区 / 类型多选（V2.16）：选项来自数据字典（数据库），后台「数据字典」页可维护 ──
// 之前是写死在前端的预设数组，加选项要改代码；现在选项存数据库，这里只负责拉取。
const regionOptions = ref<string[]>([]);
const kindOptions = ref<string[]>([]);

async function loadDictOptions() {
  const [regions, kinds] = await Promise.all([
    dictApi.enabledItems('artist_region'),
    dictApi.enabledItems('artist_kind'),
  ]);
  regionOptions.value = regions.map((i) => i.label);
  kindOptions.value = kinds.map((i) => i.label);
}

const splitTags = (s: string | null | undefined): string[] =>
  (s ?? '').split(',').map((x) => x.trim()).filter(Boolean);

const joinTags = (arr: string[]): string | null => {
  const t = arr.map((x) => x.trim()).filter(Boolean);
  return t.length > 0 ? t.join(',') : null;
};
</script>

<template>
  <div class="page">
    <div class="toolbar">
      <el-input
        v-model="table.keyword"
        placeholder="搜索歌手 / 地区 / 类型"
        prefix-icon="Search"
        clearable
        style="width: 220px"
        @keyup.enter="table.onSearch"
        @clear="table.onSearch"
      />
      <el-button type="primary" @click="table.handleNew">+ 新建歌手</el-button>
    </div>

    <el-table :data="table.items" v-loading="table.loading" stripe>
      <el-table-column prop="id"           label="ID"         width="80"  />
      <el-table-column prop="name"         label="歌手名"     min-width="140" />
      <el-table-column label="地区" width="140">
        <template #default="{ row }">
          <el-tag v-for="r in splitTags(row.region)" :key="r" size="small" class="tag-item">{{ r }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="类型" width="140">
        <template #default="{ row }">
          <el-tag v-for="k in splitTags(row.kind)" :key="k" size="small" type="success" class="tag-item">{{ k }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="头像"        width="70">
        <template #default="{ row }">
          <el-avatar v-if="row.avatarUrl" :src="row.avatarUrl" :size="32" />
          <div v-else class="avatar-placeholder">{{ row.name?.charAt(0) }}</div>
        </template>
      </el-table-column>
      <el-table-column label="歌曲" width="90">
        <template #default="{ row }">
          <span class="stat-num">{{ row.songCount }}</span>
        </template>
      </el-table-column>
      <el-table-column label="专辑" width="90">
        <template #default="{ row }">
          <span class="stat-num">{{ row.albumCount }}</span>
        </template>
      </el-table-column>
      <el-table-column label="粉丝" width="90">
        <template #default="{ row }">
          <span class="stat-num">{{ row.followerCount }}</span>
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

    <!-- 新建 / 编辑 Dialog -->
    <el-dialog
      v-model="table.dialogOpen"
      :title="table.editing?.id ? '编辑歌手' : '新建歌手'"
      width="480px"
      :close-on-click-modal="false"
    >
      <el-form v-if="table.editing" label-width="80">
        <el-form-item label="名称" required>
          <el-input v-model="table.editing.name" placeholder="歌手名称" maxlength="64" />
        </el-form-item>
        <el-form-item label="地区">
          <el-select
            :model-value="splitTags(table.editing.region)"
            @update:model-value="(v: string[]) => { if (table.editing) table.editing.region = joinTags(v); }"
            multiple filterable allow-create default-first-option clearable
            placeholder="选择或输入地区，可多选"
            style="width: 100%"
          >
            <el-option v-for="r in regionOptions" :key="r" :label="r" :value="r" />
          </el-select>
        </el-form-item>
        <el-form-item label="类型">
          <el-select
            :model-value="splitTags(table.editing.kind)"
            @update:model-value="(v: string[]) => { if (table.editing) table.editing.kind = joinTags(v); }"
            multiple filterable allow-create default-first-option clearable
            placeholder="选择或输入类型，可多选"
            style="width: 100%"
          >
            <el-option v-for="k in kindOptions" :key="k" :label="k" :value="k" />
          </el-select>
        </el-form-item>
        <el-form-item label="头像">
          <!-- 外链 / 相对路径 / 选本地图片（自动压缩转 base64）都行 -->
          <ImageFieldInput v-model="table.editing.avatarUrl" />
        </el-form-item>
        <el-form-item label="简介">
          <el-input
            v-model="table.editing.bio"
            type="textarea"
            :rows="3"
            placeholder="歌手简介"
            maxlength="500"
            show-word-limit
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
            const body = {
              name: d.name!,
              region: d.region ?? null,
              kind: d.kind ?? null,
              avatarUrl: d.avatarUrl ?? null,
              bio: d.bio ?? null,
            };
            return d.id
              ? { id: d.id, update: { ...body, clearAvatar: !body.avatarUrl } }
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
.avatar-placeholder {
  width: 32px; height: 32px; border-radius: 50%;
  background: #EEEDFE; color: #4F46E5;
  display: grid; place-items: center; font-weight: 600; font-size: 14px;
}
.stat-num { font-variant-numeric: tabular-nums; color: #8A8AA3; font-size: 13px; }
.tag-item { margin-right: 4px; margin-bottom: 2px; }
</style>
