<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { playlistApi } from '@/api/catalog';
import { mediaUrl } from '@/api/media';

const items = ref<any[]>([]);
const total = ref(0);
const loading = ref(false);
const keyword = ref('');
const page = ref(1);
const pageSize = ref(20);

async function fetch() {
  loading.value = true;
  try {
    const r = await playlistApi.list({ keyword: keyword.value || undefined, page: page.value, pageSize: pageSize.value });
    items.value = r.items;
    total.value = r.total;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '加载失败');
  } finally {
    loading.value = false;
  }
}

async function handleDelete(row: any) {
  try {
    await ElMessageBox.confirm(`确定要删除歌单「${row.name}」吗？`, '请确认', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' });
  } catch { return; }
  try {
    await playlistApi.adminDelete(row.id);
    ElMessage.success('已删除');
    if (items.value.length === 1 && page.value > 1) page.value -= 1;
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '删除失败');
  }
}

function onSearch() { page.value = 1; void fetch(); }
function onPageChange(p: number) { page.value = p; void fetch(); }
function onPageSizeChange(s: number) { pageSize.value = s; page.value = 1; void fetch(); }

function fmtDate(s: string) { return s?.substring(0, 10) ?? '—'; }

onMounted(() => void fetch());
</script>

<template>
  <div class="page">
    <div class="toolbar">
      <el-input v-model="keyword" placeholder="搜索歌单 / 创建者" prefix-icon="Search" clearable
        style="width:220px" @keyup.enter="onSearch" @clear="onSearch" />
    </div>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="id"            label="ID"           width="70"  />
      <el-table-column label="封面"          width="64">
        <template #default="{ row }">
          <el-image v-if="row.coverUrl" :src="mediaUrl(row.coverUrl)" lazy fit="cover"
            style="width:40px;height:40px;border-radius:6px" />
          <div v-else class="cover-ph">歌</div>
        </template>
      </el-table-column>
      <el-table-column prop="name"          label="歌单名"       min-width="160" />
      <el-table-column prop="ownerName"      label="创建者"       width="100" />
      <el-table-column label="分类"          width="100">
        <template #default="{ row }">{{ row.categoryName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="歌曲数"        width="80">
        <template #default="{ row }"><span class="stat-num">{{ row.trackCount }}</span></template>
      </el-table-column>
      <el-table-column label="收藏数"        width="80">
        <template #default="{ row }"><span class="stat-num">{{ row.collectorCount }}</span></template>
      </el-table-column>
      <el-table-column label="创建时间"      width="140">
        <template #default="{ row }">{{ fmtDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作"          width="100" fixed="right">
        <template #default="{ row }">
          <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
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
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
.cover-ph {
  width: 40px; height: 40px; border-radius: 6px;
  background: #F2F2F7; color: #8A8AA3;
  display: grid; place-items: center; font-size: 12px;
}
.stat-num { font-variant-numeric: tabular-nums; color: #8A8AA3; font-size: 13px; }
</style>
