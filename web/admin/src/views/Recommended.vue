<script setup lang="ts">
// 推荐位管理（V2.12）。
// 手写 fetch/onSearch/onPageChange，不套 @/composables/useCrudTable：
// 那个 composable 假设「行 = 实体」（新建/编辑/删除都作用在同一实体上），
// 而这里的「行 = 歌单 + 干预状态」—— 主键是 playlistId、操作是给歌单挂/摘一条干预记录，
// 没有"新建歌单"这件事，硬套只会到处写 if 绕开它的约定。

import { onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { recommendApi } from '@/api/recommend';
import type { AdminRecommendedDto } from '@/api/recommend';
import { mediaUrl } from '@/api/media';

const items = ref<AdminRecommendedDto[]>([]);
const total = ref(0);
const loading = ref(false);
const keyword = ref('');
const page = ref(1);
const pageSize = ref(20);

// ── 推荐设置弹窗 ────────────────────────────────────────────────────────────
const dialogVisible = ref(false);
const saving = ref(false);
const editing = ref<AdminRecommendedDto | null>(null);
const form = reactive({ sortOrder: 0, weight: 0, isHidden: false });

async function fetch() {
  loading.value = true;
  try {
    const r = await recommendApi.list({
      keyword: keyword.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    items.value = r.items;
    total.value = r.total;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '加载失败');
  } finally {
    loading.value = false;
  }
}

function openDialog(row: AdminRecommendedDto) {
  editing.value = row;
  // 必须把后端当前值整体回填：PUT 是整行覆盖语义，
  // 只回填被点击的那一项会让「只想改下线」顺手把置顶/加权清零。
  form.sortOrder = row.sortOrder;
  form.weight = row.weight;
  form.isHidden = row.isHidden;
  dialogVisible.value = true;
}

async function handleSave() {
  const row = editing.value;
  if (!row) return;
  saving.value = true;
  try {
    await recommendApi.set(row.playlistId, {
      sortOrder: form.sortOrder,
      weight: form.weight,
      isHidden: form.isHidden,
    });
    ElMessage.success('推荐设置已保存');
    dialogVisible.value = false;
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '保存失败');
  } finally {
    saving.value = false;
  }
}

async function handleRemoveOverride(row: AdminRecommendedDto) {
  try {
    await ElMessageBox.confirm(
      `确定要移除歌单「${row.name}」的人工干预吗？移除后它回到纯规则排序。`,
      '请确认',
      { type: 'warning', confirmButtonText: '移除干预', cancelButtonText: '取消' },
    );
  } catch { return; }
  try {
    await recommendApi.remove(row.playlistId);
    ElMessage.success('已移除人工干预');
    await fetch();
  } catch (e: any) {
    // 无干预记录时后端 404 —— 正常业务回流（可能别人刚移除过），原样提示后端 message，
    // 前端不自己改写成人话，免得掩盖真实原因。
    ElMessage.error(e?.message ?? '移除失败');
  }
}

function onSearch() { page.value = 1; void fetch(); }
function onPageChange(p: number) { page.value = p; void fetch(); }
function onPageSizeChange(s: number) { pageSize.value = s; page.value = 1; void fetch(); }

function fmtDate(s: string) { return s?.substring(0, 10) ?? '—'; }

type TagType = 'primary' | 'info' | 'warning' | 'danger';

/**
 * 推荐状态标签。一行只给一个标签，按优先级取最高的那个（下线 > 置顶 > 加权）。
 * hasOverride 判在最前面，因为它是「有没有干预记录」的唯一权威来源：
 * 拿 sortOrder/weight/isHidden 是否等于默认值来反推，会把"运营显式设成 0"误判成"从未干预"。
 */
function statusOf(row: AdminRecommendedDto): { text: string; type: TagType } {
  if (!row.hasOverride) return { text: '未干预', type: 'info' };
  if (row.isHidden) return { text: '已下线', type: 'danger' };
  if (row.sortOrder > 0) return { text: `置顶 #${row.sortOrder}`, type: 'primary' };
  if (row.weight !== 0) return { text: `已加权 ${row.weight}`, type: 'warning' };
  return { text: '未干预', type: 'info' };
}

onMounted(() => void fetch());
</script>

<template>
  <div class="page">
    <!-- 三个干预字段的语义只有运营自己需要知道，用常驻说明代替 tooltip：
         置顶/加权/下线的区别（尤其"下线 ≠ 删除"）是这一页最容易搞错的地方。 -->
    <el-alert type="info" :closable="false" show-icon class="hint">
      <template #title>推荐位说明</template>
      <div class="hint-body">
        <div><b>置顶序号</b>：0 = 不置顶；≥1 = 置顶，数字小的排前面。</div>
        <div><b>加权分</b>：额外加到规则分上的分数，可填负数（把歌单往后压）。</div>
        <div><b>下线</b>：从全部推荐场景里摘掉；歌单本身仍正常可见、可听，不是删除。</div>
        <div>保存会整行覆盖这三项；「移除干预」则清空全部人工干预，让它回到纯规则排序。</div>
      </div>
    </el-alert>

    <div class="toolbar">
      <el-input v-model="keyword" placeholder="搜索歌单 / 创建者" prefix-icon="Search" clearable
        style="width:220px" @keyup.enter="onSearch" @clear="onSearch" />
    </div>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column label="封面" width="64">
        <template #default="{ row }">
          <!-- ⚠️ 不要给表格里的 el-image 传 :preview-src-list。
               它在每行渲染时都创建新数组，触发 Element Plus 的 el-image-viewer
               全屏预览组件反复挂载/卸载；该组件的卸载会阻塞 Vue Router 的导航，
               表现为「在这一页点任何菜单都没反应，要离开这一页才能点动」。
               需要预览就放到详情弹窗里做，表格只显示缩略图。 -->
          <el-image v-if="row.coverUrl" :src="mediaUrl(row.coverUrl)" lazy fit="cover"
            style="width:40px;height:40px;border-radius:6px" />
          <div v-else class="cover-ph">歌</div>
        </template>
      </el-table-column>
      <el-table-column label="歌单名" min-width="180">
        <template #default="{ row }">
          <span class="name">{{ row.name }}</span>
          <!-- 系统歌单（「我喜欢的音乐」这类）规则排序永远不会推荐它，
               标出来省得运营反复给系统歌单设推荐后又疑惑"怎么没生效"。 -->
          <el-tag v-if="row.isSystem" type="info" size="small" class="sys-tag">系统</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="ownerName" label="创建者" width="110" />
      <el-table-column label="分区" width="110">
        <template #default="{ row }">{{ row.categoryName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="歌曲数" width="80">
        <template #default="{ row }"><span class="stat-num">{{ row.trackCount }}</span></template>
      </el-table-column>
      <el-table-column label="收藏数" width="80">
        <template #default="{ row }"><span class="stat-num">{{ row.collectorCount }}</span></template>
      </el-table-column>
      <el-table-column label="播放量" width="90">
        <template #default="{ row }"><span class="stat-num">{{ row.playCount }}</span></template>
      </el-table-column>
      <el-table-column label="推荐状态" width="120">
        <template #default="{ row }">
          <el-tag :type="statusOf(row).type" size="small">{{ statusOf(row).text }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="140">
        <template #default="{ row }">{{ fmtDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDialog(row)">推荐设置</el-button>
          <!-- 没有干预记录时不给「移除干预」：点了必然是 404，徒增一次无意义的报错。 -->
          <el-button v-if="row.hasOverride" link type="danger" @click="handleRemoveOverride(row)">移除干预</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="total > 0"
      :total="total"
      :current-page="page"
      :page-size="pageSize"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next"
      style="margin-top:14px; justify-content:flex-end"
      @current-change="onPageChange"
      @size-change="onPageSizeChange"
    />

    <el-dialog v-model="dialogVisible" width="460px" :title="`推荐设置 · ${editing?.name ?? ''}`">
      <el-form label-width="86px">
        <el-form-item label="置顶序号">
          <div class="field">
            <el-input-number v-model="form.sortOrder" :min="0" :step="1" />
            <div class="field-hint">0 = 不置顶；≥1 = 置顶，数字小的排前面。</div>
          </div>
        </el-form-item>
        <el-form-item label="加权分">
          <div class="field">
            <el-input-number v-model="form.weight" :step="1" />
            <div class="field-hint">加到规则分上的额外分，可填负数。</div>
          </div>
        </el-form-item>
        <el-form-item label="下线">
          <div class="field">
            <el-switch v-model="form.isHidden" />
            <div class="field-hint">从全部推荐场景摘掉；歌单本身仍正常可见、可听。</div>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.hint { margin-bottom: 12px; }
.hint-body { font-size: 12px; line-height: 1.7; }
.toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
.name { margin-right: 6px; }
.sys-tag { vertical-align: middle; }
.cover-ph {
  width: 40px; height: 40px; border-radius: 6px;
  background: #F2F2F7; color: #8A8AA3;
  display: grid; place-items: center; font-size: 12px;
}
.stat-num { font-variant-numeric: tabular-nums; color: #8A8AA3; font-size: 13px; }
.field { display: flex; flex-direction: column; gap: 2px; }
.field-hint { font-size: 12px; color: #8A8AA3; line-height: 1.5; }
</style>
