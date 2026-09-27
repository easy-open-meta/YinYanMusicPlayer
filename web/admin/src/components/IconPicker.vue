<script setup lang="ts">
// 图标选择器：下拉网格选择 + 实时预览。
// 值为 Material Icons 的 Unicode 字符（码点），与 MAUI 客户端共用同一语义。
//
// ⚠️ 用显式 props + emit 而不是 defineModel：
// 这个组件被 v-model="table.editing.iconGlyph" 绑定，而 table.editing 来自
// useCrudTable 的 getter（shallowRef 包装的对象）。defineModel 的更新链路在这种
// 「父级对象来自 getter + 子组件内容被 el-popover teleport 到 body」的组合下
// 不可靠（实测点击后值写不回去）。显式 emit 更直接。
import { computed, ref } from 'vue';
import { ICON_OPTIONS, iconNameOf, type IconOption } from '@/api/icons';

const props = defineProps<{ modelValue?: string | null }>();
const emit = defineEmits<{ (e: 'update:modelValue', v: string | null): void }>();

const keyword = ref('');
const visible = ref(false);

const current = computed(() => props.modelValue ?? null);

/** 过滤后的图标（按名称/中文说明/码点搜索）。 */
const filtered = computed(() => {
  const k = keyword.value.trim().toLowerCase();
  if (!k) return ICON_OPTIONS;
  return ICON_OPTIONS.filter(
    (o) =>
      o.name.includes(k) ||
      o.label.includes(keyword.value.trim()) ||
      o.code.toLowerCase().includes(k),
  );
});

const selected = computed(() => iconNameOf(current.value));

function pick(o: IconOption) {
  emit('update:modelValue', o.glyph);
  visible.value = false;
}

function clear() {
  emit('update:modelValue', null);
}
</script>

<template>
  <div class="icon-picker">
    <el-popover
      v-model:visible="visible"
      :width="380"
      trigger="click"
      placement="bottom-start"
      popper-class="icon-picker-popper"
    >
      <template #reference>
        <div class="trigger" :class="{ empty: !current }">
          <!-- 预览：有值时显示图标，无值时给占位提示 -->
          <span v-if="current" class="material-icon preview">{{ current }}</span>
          <span v-else class="placeholder">选择图标</span>
          <span class="meta">
            <template v-if="selected">{{ selected }}</template>
            <template v-else-if="current">自定义 U+{{ current.codePointAt(0)?.toString(16).toUpperCase() }}</template>
            <template v-else>未选</template>
          </span>
          <span class="arrow">▾</span>
        </div>
      </template>

      <div class="panel">
        <div class="panel-head">
          <el-input
            v-model="keyword"
            size="small"
            placeholder="搜索：音符 / music / E405"
            clearable
          />
        </div>

        <div class="grid">
          <button
            v-for="o in filtered"
            :key="o.code"
            type="button"
            class="cell"
            :class="{ active: current === o.glyph }"
            :title="`${o.name} · ${o.label} · U+${o.code}`"
            @click="pick(o)"
          >
            <span class="material-icon">{{ o.glyph }}</span>
            <span class="cell-label">{{ o.label }}</span>
          </button>
          <div v-if="!filtered.length" class="empty-tip">没有匹配的图标</div>
        </div>

        <div class="panel-foot">
          <el-button size="small" text @click="clear">清除图标</el-button>
          <span class="hint">共 {{ filtered.length }} / {{ ICON_OPTIONS.length }} 个</span>
        </div>
      </div>
    </el-popover>
  </div>
</template>

<style scoped>
.icon-picker { width: 100%; }

.trigger {
  display: flex; align-items: center; gap: 10px;
  min-height: 40px; padding: 0 12px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base, 12px);
  cursor: pointer; background: #fff;
  transition: border-color 0.15s;
}
.trigger:hover { border-color: var(--el-color-primary); }
.trigger.empty .placeholder { color: #A8ABB2; }
.preview { font-size: 22px; color: #1F1F2E; }
.placeholder { font-size: 13px; }
.meta { flex: 1; font-size: 12px; color: #8A8AA3; }
.arrow { color: #A8ABB2; font-size: 12px; }

.panel-head { margin-bottom: 10px; }
.grid {
  display: grid; grid-template-columns: repeat(6, 1fr);
  gap: 6px; max-height: 260px; overflow-y: auto; padding: 2px;
}
.cell {
  display: flex; flex-direction: column; align-items: center; gap: 2px;
  padding: 6px 2px; border: 1px solid transparent; border-radius: 8px;
  background: #FAFAFC; cursor: pointer; transition: all 0.12s;
}
.cell:hover { background: #EEEDFE; border-color: var(--el-color-primary); }
.cell.active { background: #EEEDFE; border-color: var(--el-color-primary); }
.cell .material-icon { font-size: 20px; color: #1F1F2E; }
.cell-label { font-size: 10px; color: #8A8AA3; white-space: nowrap; }
.empty-tip { grid-column: 1 / -1; text-align: center; color: #A8ABB2; font-size: 12px; padding: 16px 0; }

.panel-foot {
  display: flex; align-items: center; justify-content: space-between;
  margin-top: 10px; padding-top: 8px; border-top: 1px solid #F2F2F7;
}
.hint { font-size: 11px; color: #A8ABB2; }
</style>
