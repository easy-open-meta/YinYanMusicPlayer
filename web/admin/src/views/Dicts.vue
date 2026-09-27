<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { dictApi, type DictItemDto, type DictTypeDto } from '@/api/dict';

// 数据字典管理（V2.16）：左侧是字典类型，右侧维护该类型下的选项。
// 歌手页的地区/类型下拉框选项就来自 artist_region / artist_kind 两个类型。

const types = ref<DictTypeDto[]>([]);
const loading = ref(false);
const currentTypeId = ref<number | null>(null);

const currentType = computed(() => types.value.find((t) => t.id === currentTypeId.value) ?? null);

async function fetchAll() {
  loading.value = true;
  try {
    types.value = await dictApi.all();
    // 默认选中第一个；若当前选中的类型已被删除，则回落到第一个
    if (types.value.length > 0 && !types.value.some((t) => t.id === currentTypeId.value)) {
      currentTypeId.value = types.value[0].id;
    } else if (types.value.length === 0) {
      currentTypeId.value = null;
    }
  } finally {
    loading.value = false;
  }
}

// ── 类型 CRUD ──────────────────────────────────────────────────────────────

const typeDialogOpen = ref(false);
const savingType = ref(false);
const typeForm = ref<{ id: number; type: string; name: string; isEnabled: boolean }>({
  id: 0, type: '', name: '', isEnabled: true,
});

function openAddType() {
  typeForm.value = { id: 0, type: '', name: '', isEnabled: true };
  typeDialogOpen.value = true;
}

function openEditType(t: DictTypeDto) {
  typeForm.value = { id: t.id, type: t.type, name: t.name, isEnabled: t.isEnabled };
  typeDialogOpen.value = true;
}

async function saveType() {
  const f = typeForm.value;
  if (!f.name.trim()) { ElMessage.warning('请输入字典类型名称'); return; }
  if (!f.id && !f.type.trim()) { ElMessage.warning('请输入类型编码'); return; }
  savingType.value = true;
  try {
    if (f.id) {
      await dictApi.updateType(f.id, { name: f.name, isEnabled: f.isEnabled });
    } else {
      await dictApi.createType({ type: f.type, name: f.name, isEnabled: f.isEnabled });
    }
    typeDialogOpen.value = false;
    await fetchAll();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '保存失败');
  } finally {
    savingType.value = false;
  }
}

async function removeType(t: DictTypeDto) {
  try {
    await ElMessageBox.confirm(
      `确定要删除字典类型「${t.name}」吗？其下所有选项将一并删除。`,
      '删除确认',
      { type: 'warning' },
    );
  } catch { return; }
  try {
    await dictApi.deleteType(t.id);
    ElMessage.success('已删除');
    await fetchAll();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '删除失败');
  }
}

// ── 选项 CRUD ──────────────────────────────────────────────────────────────

const itemDialogOpen = ref(false);
const savingItem = ref(false);
const itemForm = ref<{ id: number; label: string; sortOrder: number; isEnabled: boolean }>({
  id: 0, label: '', sortOrder: 0, isEnabled: true,
});

function openAddItem() {
  itemForm.value = { id: 0, label: '', sortOrder: 0, isEnabled: true };
  itemDialogOpen.value = true;
}

function openEditItem(i: DictItemDto) {
  itemForm.value = { id: i.id, label: i.label, sortOrder: i.sortOrder, isEnabled: i.isEnabled };
  itemDialogOpen.value = true;
}

async function saveItem() {
  const f = itemForm.value;
  if (!currentType.value) return;
  if (!f.label.trim()) { ElMessage.warning('请输入选项名称'); return; }
  savingItem.value = true;
  try {
    if (f.id) {
      await dictApi.updateItem(f.id, { label: f.label, sortOrder: f.sortOrder, isEnabled: f.isEnabled });
    } else {
      await dictApi.createItem(currentType.value.type, {
        label: f.label,
        sortOrder: f.sortOrder,
        isEnabled: f.isEnabled,
      });
    }
    itemDialogOpen.value = false;
    await fetchAll();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '保存失败');
  } finally {
    savingItem.value = false;
  }
}

async function removeItem(i: DictItemDto) {
  try {
    await ElMessageBox.confirm(`确定要删除选项「${i.label}」吗？`, '删除确认', { type: 'warning' });
  } catch { return; }
  try {
    await dictApi.deleteItem(i.id);
    ElMessage.success('已删除');
    await fetchAll();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '删除失败');
  }
}

onMounted(() => void fetchAll());
</script>

<template>
  <div class="page dicts-page">
    <div class="dict-layout">
      <!-- 左侧：类型列表 -->
      <div class="type-panel">
        <div class="toolbar">
          <span class="panel-title">字典类型</span>
          <el-button type="primary" size="small" @click="openAddType">+ 新增</el-button>
        </div>
        <div v-loading="loading" class="type-list">
          <div
            v-for="t in types"
            :key="t.id"
            class="type-item"
            :class="{ active: t.id === currentTypeId }"
            @click="currentTypeId = t.id"
          >
            <div class="type-name">
              {{ t.name }}
              <el-tag v-if="!t.isEnabled" size="small" type="info">停用</el-tag>
            </div>
            <div class="type-code">{{ t.type }}</div>
          </div>
          <el-empty v-if="!loading && types.length === 0" description="暂无字典类型" :image-size="60" />
        </div>
      </div>

      <!-- 右侧：选项管理 -->
      <div class="item-panel" v-loading="loading">
        <template v-if="currentType">
          <div class="item-header">
            <div>
              <div class="panel-title">{{ currentType.name }}</div>
              <div class="type-code">编码：{{ currentType.type }} · 共 {{ currentType.items.length }} 个选项</div>
            </div>
            <div class="item-header-actions">
              <el-button size="small" @click="openEditType(currentType)">编辑类型</el-button>
              <el-button size="small" type="danger" plain @click="removeType(currentType)">删除类型</el-button>
              <el-button size="small" type="primary" @click="openAddItem">+ 新增选项</el-button>
            </div>
          </div>

          <el-table v-if="currentType.items.length > 0" :data="currentType.items" stripe size="small">
            <el-table-column prop="id"        label="ID"       width="70" />
            <el-table-column prop="label"     label="选项名称" min-width="160" />
            <el-table-column prop="sortOrder" label="排序"     width="80" />
            <el-table-column label="启用" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                  {{ row.isEnabled ? '启用' : '停用' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="130" fixed="right">
              <template #default="{ row }">
                <el-button link type="primary" size="small" @click="openEditItem(row)">编辑</el-button>
                <el-button link type="danger"   size="small" @click="removeItem(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-else description="该类型下暂无选项，点击右上角新增" :image-size="70" />
        </template>
        <el-empty v-else description="请选择或新增一个字典类型" :image-size="90" />
      </div>
    </div>

    <!-- 类型 Dialog -->
    <el-dialog
      v-model="typeDialogOpen"
      :title="typeForm.id ? '编辑字典类型' : '新增字典类型'"
      width="420px"
      :close-on-click-modal="false"
    >
      <el-form label-width="80">
        <el-form-item v-if="!typeForm.id" label="类型编码" required>
          <el-input v-model="typeForm.type" placeholder="如 artist_region（唯一）" maxlength="32" />
        </el-form-item>
        <el-form-item label="类型名称" required>
          <el-input v-model="typeForm.name" placeholder="如 歌手地区" maxlength="32" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="typeForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="typeDialogOpen = false">取消</el-button>
        <el-button type="primary" :loading="savingType" @click="saveType">保存</el-button>
      </template>
    </el-dialog>

    <!-- 选项 Dialog -->
    <el-dialog
      v-model="itemDialogOpen"
      :title="itemForm.id ? '编辑选项' : '新增选项'"
      width="420px"
      :close-on-click-modal="false"
    >
      <el-form label-width="80">
        <el-form-item label="选项名称" required>
          <el-input v-model="itemForm.label" placeholder="如 华语" maxlength="32" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="itemForm.sortOrder" :min="0" :max="9999" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="itemForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogOpen = false">取消</el-button>
        <el-button type="primary" :loading="savingItem" @click="saveItem">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.dict-layout { display: flex; gap: 16px; align-items: flex-start; }
.type-panel {
  width: 260px; flex-shrink: 0;
  border: 0.5px solid #ECECF2; border-radius: 10px;
  padding: 12px;
}
.item-panel {
  flex: 1; min-height: 320px;
  border: 0.5px solid #ECECF2; border-radius: 10px;
  padding: 12px;
}
.toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.panel-title { font-weight: 600; font-size: 14px; }
.type-list { display: flex; flex-direction: column; gap: 6px; min-height: 120px; }
.type-item {
  padding: 8px 10px; border-radius: 8px; cursor: pointer;
  border: 0.5px solid transparent;
}
.type-item:hover { background: #F7F7FB; }
.type-item.active { background: #EEEDFE; border-color: #C7C4F6; }
.type-name { font-size: 13px; font-weight: 500; display: flex; align-items: center; gap: 6px; }
.type-code { font-size: 11px; color: #8A8AA3; margin-top: 2px; }
.item-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.item-header-actions { display: flex; gap: 8px; }
</style>