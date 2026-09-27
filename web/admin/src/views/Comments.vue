<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import dayjs from 'dayjs';
import { adminCommentApi } from '@/api/comments';
import type { AdminCommentDto } from '@/api/comments';

const state = {
  items: ref<AdminCommentDto[]>([]),
  total: ref(0),
  loading: ref(false),
  keyword: ref(''),
  // 筛选值用 'all' 而不是 ''：el-select 把空串当作「未选中」从而显示 placeholder，
  // 「全部」这一项就点不出来了。
  targetType: ref<'all' | 'song' | 'playlist'>('all'),
  hidden: ref<'all' | 'normal' | 'hidden'>('all'),
  page: ref(1),
  pageSize: ref(20),
};

function fmtTime(iso: string | null | undefined): string {
  return iso ? dayjs(iso).format('YYYY-MM-DD HH:mm:ss') : '—';
}

async function fetch() {
  state.loading.value = true;
  try {
    const r = await adminCommentApi.list({
      keyword: state.keyword.value || undefined,
      targetType: state.targetType.value === 'all' ? undefined : state.targetType.value,
      isHidden: state.hidden.value === 'all' ? undefined : state.hidden.value === 'hidden',
      page: state.page.value,
      pageSize: state.pageSize.value,
    });
    state.items.value = r.items;
    state.total.value = r.total;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '加载失败');
  } finally {
    state.loading.value = false;
  }
}

async function handleToggleHidden(row: AdminCommentDto) {
  try {
    await adminCommentApi.setHidden(row.id, !row.isHidden);
    ElMessage.success(row.isHidden ? '已恢复' : '已隐藏');
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '操作失败');
  }
}

async function handleDelete(row: AdminCommentDto) {
  try {
    await ElMessageBox.confirm('确定要删除这条评论吗？此操作不可恢复。', '请确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
  } catch { return; }
  try {
    await adminCommentApi.remove(row.id);
    ElMessage.success('已删除');
    // 删掉本页最后一条后回退一页，避免停在空白页
    if (state.items.value.length === 1 && state.page.value > 1) state.page.value -= 1;
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '删除失败');
  }
}

function onSearch() { state.page.value = 1; void fetch(); }
function onPageChange(p: number) { state.page.value = p; void fetch(); }
function onPageSizeChange(s: number) { state.pageSize.value = s; state.page.value = 1; void fetch(); }

onMounted(() => void fetch());
</script>

<template>
  <div class="page">
    <div class="toolbar">
      <el-input v-model="state.keyword.value" placeholder="搜索评论内容" prefix-icon="Search" clearable
        style="width:260px" @keyup.enter="onSearch" @clear="onSearch" />
      <el-select v-model="state.targetType.value" style="width:130px" @change="onSearch">
        <el-option label="全部对象" value="all" />
        <el-option label="歌曲" value="song" />
        <el-option label="歌单" value="playlist" />
      </el-select>
      <el-select v-model="state.hidden.value" style="width:130px" @change="onSearch">
        <el-option label="全部状态" value="all" />
        <el-option label="正常" value="normal" />
        <el-option label="已隐藏" value="hidden" />
      </el-select>
    </div>

    <el-table :data="state.items.value" v-loading="state.loading.value" stripe>
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column label="目标" min-width="220">
        <template #default="{ row }">
          <div class="target-cell">
            <span class="target-title">{{ row.targetTitle }}</span>
            <el-tag size="small" :type="row.targetType === 'song' ? 'primary' : 'info'">
              {{ row.targetType === 'song' ? '歌曲' : '歌单' }} #{{ row.targetId }}
            </el-tag>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="作者" min-width="120">
        <template #default="{ row }">
          <div>{{ row.userName }}</div>
          <div class="sub-num">#{{ row.userId }}</div>
        </template>
      </el-table-column>
      <!-- 评论正文可能很长：只给 min-width + show-overflow-tooltip（el-table-column 没有
           max-width 属性），让列宽由表格按剩余空间分配，超长文本省略并悬浮显示全文。 -->
      <el-table-column prop="content" label="内容" min-width="260" show-overflow-tooltip />
      <el-table-column label="层级" width="90">
        <template #default="{ row }">
          <span class="stat-num">{{ row.depth === 0 ? '主评论' : `第 ${row.depth + 1} 层` }}</span>
        </template>
      </el-table-column>
      <el-table-column label="点赞" width="80">
        <template #default="{ row }"><span class="stat-num">{{ row.likeCount }}</span></template>
      </el-table-column>
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <!-- 作者自己删的占位楼：正文已清空、只剩层级结构，后台只需知道"这儿原来是条评论" -->
          <el-tag v-if="row.isDeleted" type="info" size="small">作者已删</el-tag>
          <el-tag v-else :type="row.isHidden ? 'danger' : 'success'" size="small">{{ row.isHidden ? '已隐藏' : '正常' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="时间" width="170">
        <template #default="{ row }">{{ fmtTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <!-- 占位楼没有可隐藏的内容，禁用掉避免误操作 -->
          <el-button link :type="row.isHidden ? 'success' : 'warning'" :disabled="row.isDeleted"
            @click="handleToggleHidden(row)">
            {{ row.isHidden ? '恢复' : '隐藏' }}
          </el-button>
          <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="state.total.value > 0"
      :total="state.total.value"
      :current-page="state.page.value"
      :page-size="state.pageSize.value"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next"
      style="margin-top:14px; justify-content:flex-end"
      @current-change="onPageChange"
      @size-change="onPageSizeChange"
    />
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
.target-cell { display: flex; align-items: center; gap: 8px; min-width: 0; }
.target-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sub-num { font-size: 12px; color: #8A8AA3; }
.stat-num { font-variant-numeric: tabular-nums; color: #8A8AA3; font-size: 13px; }
</style>
