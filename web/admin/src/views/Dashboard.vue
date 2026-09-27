<script setup lang="ts">
// 仪表盘（V2.16）：接入 /api/admin/dashboard 展示曲库与用户数据统计。
// 统计卡片 + 分区歌曲分布（CSS 条形图）+ 歌手 Top 8 + 最近新增歌曲。
import { computed, onMounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { dashboardApi, type DashboardStatsDto } from '@/api/dashboard';

const auth = useAuthStore();
const loading = ref(false);
const stats = ref<DashboardStatsDto | null>(null);

const cards = computed(() => {
  const s = stats.value;
  return [
    { key: 'songs',     label: '歌曲',   value: s?.songs ?? 0 },
    { key: 'albums',    label: '专辑',   value: s?.albums ?? 0 },
    { key: 'artists',   label: '歌手',   value: s?.artists ?? 0 },
    { key: 'playlists', label: '歌单',   value: s?.playlists ?? 0 },
    { key: 'users',     label: '用户',   value: s?.users ?? 0 },
    { key: 'plays',     label: '总播放', value: s?.totalPlays ?? 0 },
  ];
});

const maxDist = computed(() =>
  Math.max(1, ...(stats.value?.categoryDistribution.map((d) => d.songCount) ?? [1])));

function barWidth(count: number): string {
  return `${Math.round((count / maxDist.value) * 100)}%`;
}

function fmtNum(n: number): string {
  return (n ?? 0).toLocaleString();
}

function fmtDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const mm = h > 0 ? String(m).padStart(2, '0') : String(m);
  const ss = String(s).padStart(2, '0');
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
}

function fmtDate(iso: string): string {
  const d = new Date(iso);
  const pad = (x: number) => String(x).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

async function fetchStats() {
  loading.value = true;
  try {
    stats.value = await dashboardApi.stats();
  } finally {
    loading.value = false;
  }
}

onMounted(() => void fetchStats());
</script>

<template>
  <div class="dashboard" v-loading="loading">
    <h2 class="title">仪表盘</h2>
    <p class="subtitle">欢迎，{{ auth.user?.displayName }}。这里是曲库与用户数据总览。</p>

    <!-- 统计卡片 -->
    <div class="grid">
      <el-card v-for="s in cards" :key="s.key" class="metric" shadow="never">
        <div class="label">{{ s.label }}</div>
        <div class="value numeric">{{ fmtNum(s.value) }}</div>
      </el-card>
    </div>

    <!-- 中部：分区分布 + Top 歌手 -->
    <div class="cols">
      <el-card class="panel" shadow="never">
        <template #header><span class="panel-title">分区歌曲分布</span></template>
        <div v-if="stats?.categoryDistribution.length" class="dist-list">
          <div v-for="d in stats.categoryDistribution" :key="d.categoryId ?? 'none'" class="dist-row">
            <span class="dist-name" :title="d.categoryName ?? ''">{{ d.categoryName ?? '未分类' }}</span>
            <div class="dist-bar-wrap">
              <div class="dist-bar" :style="{ width: barWidth(d.songCount) }" />
            </div>
            <span class="dist-count">{{ d.songCount }}</span>
          </div>
        </div>
        <el-empty v-else description="暂无歌曲" :image-size="60" />
      </el-card>

      <el-card class="panel" shadow="never">
        <template #header><span class="panel-title">歌手 Top 8</span></template>
        <div v-if="stats?.topArtists.length" class="artist-list">
          <div v-for="(a, i) in stats.topArtists" :key="a.id" class="artist-row">
            <span class="rank" :class="{ top: i < 3 }">{{ i + 1 }}</span>
            <span class="artist-name">{{ a.name }}</span>
            <span class="artist-meta">{{ a.songCount }} 首 · {{ fmtNum(a.totalPlays) }} 播放</span>
          </div>
        </div>
        <el-empty v-else description="暂无歌手" :image-size="60" />
      </el-card>
    </div>

    <!-- 最近新增歌曲 -->
    <el-card class="panel" shadow="never">
      <template #header><span class="panel-title">最近新增歌曲</span></template>
      <el-table :data="stats?.recentSongs ?? []" stripe size="small">
        <el-table-column prop="title"       label="歌曲" min-width="160" show-overflow-tooltip />
        <el-table-column prop="artistName"  label="歌手" width="120" show-overflow-tooltip />
        <el-table-column label="专辑" width="140" show-overflow-tooltip>
          <template #default="{ row }">{{ row.albumName ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="分区" width="110">
          <template #default="{ row }">{{ row.categoryName ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="时长" width="90">
          <template #default="{ row }">{{ fmtDuration(row.durationSeconds) }}</template>
        </el-table-column>
        <el-table-column label="播放" width="90" align="right">
          <template #default="{ row }">{{ fmtNum(row.playCount) }}</template>
        </el-table-column>
        <el-table-column label="入库时间" width="160">
          <template #default="{ row }">{{ fmtDate(row.createdAt) }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.dashboard { padding: 8px; }
.title { margin: 0 0 4px; font-size: 20px; font-weight: 600; }
.subtitle { margin: 0 0 20px; color: #6E6E8A; font-size: 13px; }
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.metric { border: 0.5px solid #ECECF2; }
.label { color: #6E6E8A; font-size: 12px; }
.value { font-size: 28px; font-weight: 500; margin-top: 4px; }
.cols {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 16px;
}
.panel { border: 0.5px solid #ECECF2; }
.panel-title { font-weight: 600; font-size: 14px; }

.dist-list { display: flex; flex-direction: column; gap: 10px; }
.dist-row { display: flex; align-items: center; gap: 10px; }
.dist-name { width: 96px; flex-shrink: 0; font-size: 13px; color: #404040; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.dist-bar-wrap { flex: 1; height: 10px; background: #F2F2F8; border-radius: 6px; overflow: hidden; }
.dist-bar { height: 100%; background: #4F46E5; border-radius: 6px; }
.dist-count { width: 44px; text-align: right; font-size: 13px; font-variant-numeric: tabular-nums; color: #6E6E8A; }

.artist-list { display: flex; flex-direction: column; gap: 2px; }
.artist-row { display: flex; align-items: center; gap: 10px; padding: 6px 4px; border-radius: 8px; }
.artist-row:hover { background: #F7F7FB; }
.rank {
  width: 20px; height: 20px; border-radius: 50%;
  background: #F2F2F8; color: #8A8AA3;
  display: grid; place-items: center;
  font-size: 12px; font-weight: 600; flex-shrink: 0;
}
.rank.top { background: #EEEDFE; color: #4F46E5; }
.artist-name { flex: 1; font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.artist-meta { color: #8A8AA3; font-size: 12px; flex-shrink: 0; }

@media (max-width: 900px) {
  .cols { grid-template-columns: 1fr; }
}
</style>