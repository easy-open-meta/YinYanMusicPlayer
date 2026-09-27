<script setup lang="ts">
import { onMounted } from 'vue';
import { catalogApi } from '@/api/catalog';
import { iconNameOf } from '@/api/icons';
import { useCrudTable } from '@/composables/useCrudTable';
import IconPicker from '@/components/IconPicker.vue';
import type { CategoryDto } from '@/api/catalog';

const table = useCrudTable<CategoryDto, Omit<CategoryDto, 'id'>, Omit<CategoryDto, 'id'>>({
  list: ({ keyword, page, pageSize }) =>
    catalogApi.categories.list().then((cats) => {
      // 分区数量通常很少，客户端搜索即可。如果将来上千再上服务端。
      // 与其它列表页同一套语义：名称 / 宣传语 任一命中即可。比较前统一转小写 ——
      // 服务端走的是 ILIKE（大小写不敏感），前端若用 includes 会变成区分大小写，
      // 搜 "k-pop" 就匹配不到「K-Pop专区」，两边语义会分叉。
      const kw = keyword?.trim().toLowerCase();
      const filtered = kw
        ? cats.filter((c) => c.name.toLowerCase().includes(kw) || (c.slogan ?? '').toLowerCase().includes(kw))
        : cats;
      const start = (page - 1) * pageSize;
      return { items: filtered.slice(start, start + pageSize), total: filtered.length, page, pageSize };
    }),
  create: (body) => catalogApi.categories.create(body),
  update: (id, body) => catalogApi.categories.update(id, body),
  remove: (id) => catalogApi.categories.delete(id),
}, {
  // 新建时的表单骨架：表单用 v-if="table.editing" 包着，必须有初始值才渲染得出来
  // contentMode 给默认值 both：这样保存时总是送出明确取值，后端无需猜"没传是什么意思"
  emptyDraft: () => ({ id: 0, name: '', slogan: null, colorHex: null, iconGlyph: null, contentMode: 'both' } as CategoryDto),
});

onMounted(() => void table.fetch());

// 专区内容类型的中文名（与后端 CategoryContentModes 的取值一一对应）
const MODE_LABEL: Record<string, string> = {
  both: '歌曲+歌单',
  songs: '仅歌曲',
  playlists: '仅歌单',
};
const modeLabel = (mode?: string | null) => MODE_LABEL[mode ?? 'both'] ?? mode ?? '歌曲+歌单';
</script>

<template>
  <div class="page">
    <!-- 头部工具栏 -->
    <div class="toolbar">
      <el-input
        v-model="table.keyword"
        placeholder="搜索分区 / 宣传语"
        prefix-icon="Search"
        clearable
        style="width: 220px"
        @keyup.enter="table.onSearch"
        @clear="table.onSearch"
      />
      <el-button type="primary" @click="table.handleNew">+ 新建分区</el-button>
    </div>

    <!-- 列表 -->
    <el-table :data="table.items" v-loading="table.loading" stripe>
      <el-table-column prop="id"      label="ID"       width="80"  />
      <el-table-column prop="name"    label="名称"     min-width="160" />
      <el-table-column label="内容"   width="110">
        <template #default="{ row }">{{ modeLabel(row.contentMode) }}</template>
      </el-table-column>
      <el-table-column prop="slogan"  label="标语"     min-width="200" show-overflow-tooltip />
      <el-table-column label="颜色"   width="120">
        <template #default="{ row }">
          <span
            v-if="row.colorHex"
            class="color-dot"
            :style="{ background: row.colorHex }"
          />
          <code v-if="row.colorHex" class="color-hex">{{ row.colorHex }}</code>
        </template>
      </el-table-column>
      <el-table-column label="图标" width="110">
        <template #default="{ row }">
          <!-- ⚠️ 不要用 <el-icon> 包：el-icon 是 SVG 图标系统，不认 IconGlyph 里的
               Material Icons 私有区码点（U+E405 等），会显示成豆腐块。
               这里用 Material Icons 字体直接渲染，与 MAUI 客户端同一套字形。 -->
          <span v-if="row.iconGlyph" class="icon-cell" :title="iconNameOf(row.iconGlyph) ?? row.iconGlyph">
            <span class="material-icon">{{ row.iconGlyph }}</span>
            <span class="icon-name">{{ iconNameOf(row.iconGlyph) ?? '自定义' }}</span>
          </span>
          <span v-else class="icon-empty">—</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="table.handleEdit(row)">编辑</el-button>
          <el-button link type="danger"   @click="table.handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新建 / 编辑 Dialog -->
    <el-dialog
      v-model="table.dialogOpen"
      :title="table.editing?.id ? '编辑分区' : '新建分区'"
      width="420px"
      :close-on-click-modal="false"
    >
      <el-form
        v-if="table.editing"
        label-width="72"
        @submit.prevent="() => {}"
      >
        <el-form-item label="名称" required>
          <el-input v-model="table.editing.name" placeholder="如：华语、欧美" maxlength="32" />
        </el-form-item>
        <el-form-item label="内容">
          <!-- 决定 App 专区详情页展示哪几段。「仅歌曲」= 纯歌曲专区（不出现歌单段） -->
          <el-select v-model="table.editing.contentMode" style="width: 100%">
            <el-option label="歌曲 + 歌单（默认）" value="both" />
            <el-option label="仅歌曲" value="songs" />
            <el-option label="仅歌单" value="playlists" />
          </el-select>
        </el-form-item>
        <el-form-item label="标语">
          <el-input v-model="table.editing.slogan" placeholder="一句话描述" maxlength="64" />
        </el-form-item>
        <el-form-item label="颜色">
          <el-color-picker v-model="table.editing.colorHex" />
          <span class="color-hint">{{ table.editing.colorHex || '未选' }}</span>
        </el-form-item>
        <el-form-item label="图标">
          <!-- 图标选择器：下拉网格 + 实时预览。值为 Material Icons 码点，
               与 MAUI 客户端共用同一语义（不能改成 Element Plus 组件名）。
               这里用 @update:modelValue + syncEditing 显式同步一次草稿，而不是 v-model：
               它与绑 v-model 的普通字段效果一致，只是把"这个字段是子组件回填的"写在明面上。 -->
          <IconPicker
            :model-value="table.editing.iconGlyph"
            @update:model-value="(v) => table.syncEditing('iconGlyph', v)"
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
              slogan: d.slogan ?? null,
              colorHex: d.colorHex ?? null,
              iconGlyph: d.iconGlyph ?? null,
              // 总是送出明确取值（骨架里默认 both）—— 后端把 null 当「这次不改」
              contentMode: d.contentMode ?? 'both',
            };
            return d.id ? { id: d.id, update: body } : { create: body };
          })"
        >保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.toolbar {
  display: flex; align-items: center; gap: 10px;
  margin-bottom: 14px;
}
.color-dot {
  display: inline-block; width: 14px; height: 14px;
  border-radius: 50%; vertical-align: middle; margin-right: 6px;
  border: 0.5px solid rgba(0,0,0,0.1);
}
.color-hex { font-size: 12px; color: #8A8AA3; }
.color-hint { margin-left: 10px; font-size: 12px; color: #8A8AA3; }
.icon-empty { color: #C0C4CC; }
.icon-cell { display: inline-flex; align-items: center; gap: 6px; }
.icon-name { font-size: 11px; color: #8A8AA3; }
</style>
