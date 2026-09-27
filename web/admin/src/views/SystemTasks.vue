<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import dayjs from 'dayjs';
import { http, unwrap, ApiException } from '@/api/http';

const importing = ref(false);
const scanning = ref(false);
const seeding = ref(false);
const lastResult = ref<{ type: 'success' | 'error'; msg: string } | null>(null);
const importDir = ref('');

// ── V2.8 目录定时扫描 ────────────────────────────────────────────────────────
// 状态只在 API 进程内存里（重启归零），所以这里不做本地缓存，每次都以后端为准。
interface ScanStatus {
  enabled: boolean;
  intervalMinutes: number;
  isRunning: boolean;
  nextRunAtUtc: string | null;
  runCount: number;
  lastRunAtUtc: string | null;
  lastStartedAtUtc: string | null;
  lastTrigger: string | null;
  lastSucceeded: boolean | null;
  lastDurationMs: number;
  lastTotal: number;
  lastImported: number;
  lastUpdated: number;
  lastFailed: number;
  lastSkipped: number;
  lastError: string | null;
}

const scan = ref<ScanStatus | null>(null);
const scanLoading = ref(false);
const toggling = ref(false);
let pollTimer: ReturnType<typeof setInterval> | null = null;

const TRIGGER_LABEL: Record<string, string> = {
  auto: '定时任务',
  manual: '后台手动',
  startup: '启动首轮',
};

function fmtTime(iso: string | null | undefined): string {
  return iso ? dayjs(iso).format('YYYY-MM-DD HH:mm:ss') : '—';
}

function fmtDuration(ms: number): string {
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)} 秒` : `${ms} 毫秒`;
}

async function loadScanStatus() {
  try {
    scan.value = await unwrap(http.get('/api/admin/scan/status')) as unknown as ScanStatus;
  } catch (e) {
    // 状态拉取失败不弹提示（10s 轮询会刷屏），页面上的数字保持上一次的值即可
    console.warn('[scan] 状态查询失败', e);
  }
}

async function toggleScan(next: boolean) {
  toggling.value = true;
  try {
    await unwrap(http.put(`/api/admin/scan/enabled?enabled=${next}`));
    ElMessage.success(next ? '定时扫描已启用。' : '定时扫描已关闭。');
    await loadScanStatus();
  } catch (e) {
    ElMessage.error(e instanceof ApiException ? e.message : '切换失败');
    await loadScanStatus(); // 回滚开关显示
  } finally {
    toggling.value = false;
  }
}

async function runScanNow() {
  scanLoading.value = true;
  lastResult.value = null;
  try {
    const data: any = await unwrap(http.post('/api/admin/scan/run'));
    if (data.error) {
      lastResult.value = { type: 'error', msg: `扫描未完成：${data.error}` };
    } else {
      lastResult.value = {
        type: 'success',
        msg: `扫描完成：共扫描 ${data.total} 个文件，新增 ${data.imported} 首，补时长 ${data.updated} 首，` +
             `跳过 ${data.skipped} 个，失败 ${data.failed} 个，耗时 ${fmtDuration(data.durationMs)}。`,
      };
    }
  } catch (e) {
    lastResult.value = { type: 'error', msg: e instanceof ApiException ? e.message : '扫描失败' };
  } finally {
    scanLoading.value = false;
    await loadScanStatus();
  }
}

onMounted(() => {
  loadScanStatus();
  // 定时任务在服务端自己跑，页面开着时轮询能看到它自己完成的那一轮
  pollTimer = setInterval(loadScanStatus, 10_000);
});

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer);
});

// ── 手动任务（V2.3 既有能力，保持不变）────────────────────────────────────────

async function runImport() {
  importing.value = true;
  lastResult.value = null;
  try {
    const params = importDir.value.trim() ? `?dir=${encodeURIComponent(importDir.value.trim())}` : '';
    const data: any = await unwrap(http.post(`/api/admin/import${params}`));
    lastResult.value = {
      type: 'success',
      msg: `导入完成：共扫描 ${data.total} 首，导入了 ${data.imported} 首，跳过 ${data.skipped} 首。`,
    };
  } catch (e) {
    lastResult.value = {
      type: 'error',
      msg: e instanceof ApiException ? e.message : '导入失败',
    };
  } finally {
    importing.value = false;
  }
}

async function runScan() {
  scanning.value = true;
  lastResult.value = null;
  try {
    const data: any = await unwrap(http.post('/api/admin/scan-durations'));
    lastResult.value = {
      type: 'success',
      msg: `扫描完成：共 ${data.total} 首，更新了 ${data.updated} 首时长。`,
    };
  } catch (e) {
    lastResult.value = {
      type: 'error',
      msg: e instanceof ApiException ? e.message : '扫描失败',
    };
  } finally {
    scanning.value = false;
  }
}

async function runDeleteSeed() {
  try {
    await ElMessageBox.confirm(
      '此操作将永久删除所有标记为种子的歌曲记录（实际文件不受影响）。是否继续？',
      '确认删除种子歌曲',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' },
    );
  } catch {
    return; // 用户取消
  }

  seeding.value = true;
  lastResult.value = null;
  try {
    const data: any = await unwrap(http.delete('/api/admin/seed-songs'));
    lastResult.value = {
      type: 'success',
      msg: `已删除 ${data.deleted} 首种子歌曲记录。`,
    };
  } catch (e) {
    lastResult.value = {
      type: 'error',
      msg: e instanceof ApiException ? e.message : '删除失败',
    };
  } finally {
    seeding.value = false;
  }
}
</script>

<template>
  <div class="page">
    <h2 class="title">系统任务</h2>
    <p class="subtitle">
      定时扫描与后台音乐导入。注意：大规模导入可能需要较长时间，请耐心等待。
    </p>

    <!-- V2.8 目录定时扫描 -->
    <el-card v-if="scan" class="scan-card" shadow="never">
      <div class="scan-head">
        <div class="scan-title-wrap">
          <div class="task-name">目录定时扫描</div>
          <div class="task-desc">
            按固定间隔扫描服务器音乐目录，自动把新歌入库（增量：已存在的不重复入库，并补齐老数据的时长）。
            间隔由服务端配置 <code>Media:ScanIntervalMinutes</code> 决定。
          </div>
        </div>
        <div class="scan-switch">
          <el-tag :type="scan.isRunning ? 'warning' : scan.enabled ? 'success' : 'info'" size="small">
            {{ scan.isRunning ? '扫描中' : scan.enabled ? '已启用' : '已关闭' }}
          </el-tag>
          <el-switch
            :model-value="scan.enabled"
            :loading="toggling"
            :disabled="toggling"
            active-text="定时"
            @change="(v: any) => toggleScan(Boolean(v))"
          />
        </div>
      </div>

      <div class="scan-meta">
        <span>间隔：{{ scan.intervalMinutes > 0 ? `${scan.intervalMinutes} 分钟` : '未设置' }}</span>
        <span>下次运行：{{ scan.enabled ? fmtTime(scan.nextRunAtUtc) : '—（已关闭）' }}</span>
        <span>
          上次运行：{{ fmtTime(scan.lastRunAtUtc) }}
          <template v-if="scan.lastTrigger">（{{ TRIGGER_LABEL[scan.lastTrigger] ?? scan.lastTrigger }}）</template>
        </span>
        <span>本进程已完成：{{ scan.runCount }} 轮</span>
      </div>

      <el-alert
        v-if="scan.lastRunAtUtc"
        :type="scan.lastError ? 'error' : scan.lastFailed > 0 ? 'warning' : 'success'"
        :title="scan.lastError
          ? `上次扫描失败：${scan.lastError}`
          : `上次扫描：扫描 ${scan.lastTotal} 个文件，新增 ${scan.lastImported} 首，补时长 ${scan.lastUpdated} 首，` +
            `跳过 ${scan.lastSkipped} 个，失败 ${scan.lastFailed} 个，耗时 ${fmtDuration(scan.lastDurationMs)}`"
        :closable="false"
        class="scan-alert"
      />

      <div class="scan-actions">
        <el-button type="primary" :loading="scanLoading || scan.isRunning" @click="runScanNow">
          {{ scan.isRunning ? '扫描中…' : '立即扫描' }}
        </el-button>
        <el-button @click="loadScanStatus">刷新状态</el-button>
        <span class="scan-hint">开关只对本次运行生效，服务重启后恢复配置文件的值。</span>
      </div>
    </el-card>

    <div class="task-grid">
      <!-- 导入音乐 -->
      <el-card class="task-card" shadow="never">
        <div class="task-icon">📁</div>
        <div class="task-info">
          <div class="task-name">导入音乐</div>
          <div class="task-desc">
            从音乐目录扫描并导入歌曲到曲库，与定时扫描同一条路径：读文件标签（标题 / 歌手 / 专辑 / 发行年份）、
            时长由 ATL 逐帧解析（解析失败的文件拒绝入库）、递归子目录、按相对路径去重。
          </div>
          <el-input
            v-model="importDir"
            placeholder="留空则使用配置文件中的默认音乐目录"
            clearable
            style="margin-top:10px"
          />
          <el-button type="primary" :loading="importing" style="margin-top:8px;width:100%" @click="runImport">
            {{ importing ? '导入中…' : '执行导入' }}
          </el-button>
        </div>
      </el-card>

      <!-- 扫描时长 -->
      <el-card class="task-card" shadow="never">
        <div class="task-icon">⏱️</div>
        <div class="task-info">
          <div class="task-name">扫描时长</div>
          <div class="task-desc">
            读取每首歌曲的实际播放时长，补充到数据库（ATL 解析失败的歌曲会跳过）。
          </div>
          <el-button :loading="scanning" style="margin-top:36px;width:100%" @click="runScan">
            {{ scanning ? '扫描中…' : '执行扫描' }}
          </el-button>
        </div>
      </el-card>

      <!-- 删除种子歌曲 -->
      <el-card class="task-card" shadow="never">
        <div class="task-icon">🗑️</div>
        <div class="task-info">
          <div class="task-name">删除种子歌曲</div>
          <div class="task-desc">
            清理标记为"种子"的歌曲记录（由导入流程产生，供审核前保留原始文件）。
            <span class="warning-text">此操作不可恢复。</span>
          </div>
          <el-button type="danger" :loading="seeding" style="margin-top:36px;width:100%" @click="runDeleteSeed">
            {{ seeding ? '删除中…' : '执行删除' }}
          </el-button>
        </div>
      </el-card>
    </div>

    <el-alert
      v-if="lastResult"
      :type="lastResult.type === 'success' ? 'success' : 'error'"
      :title="lastResult.msg"
      :closable="false"
      style="margin-top:20px"
    />
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.title { margin: 0 0 4px; font-size: 20px; font-weight: 600; }
.subtitle { margin: 0 0 20px; color: #6E6E8A; font-size: 13px; }
.scan-card {
  border: 0.5px solid #ECECF2;
  margin-bottom: 16px;
}
.scan-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.scan-title-wrap { flex: 1; min-width: 0; }
.scan-switch {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}
.scan-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 24px;
  margin: 12px 0;
  font-size: 13px;
  color: #4A4A66;
}
.scan-alert { margin-bottom: 12px; }
.scan-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
.scan-hint { font-size: 12px; color: #9A9AB0; }
.task-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 16px;
}
.task-card {
  border: 0.5px solid #ECECF2;
  padding: 8px;
}
/* 图标在文字上方竖排。⚠️ 别在这儿写「图标在左、文字在右」的 flex 行：
   el-card 会给内容套一层 .el-card__body，图标与文字是那层 body 的子元素，
   卡片自身的 display:flex / gap 根本作用不到它们身上，写了也是白写。
   两者之间的距离只能靠图标自己的 margin 给（下面那条）。 */
.task-icon { font-size: 36px; line-height: 1; margin-bottom: 8px; }
.task-info { min-width: 0; }
.task-name { font-size: 15px; font-weight: 600; margin-bottom: 4px; }
.task-desc { font-size: 13px; color: #6E6E8A; line-height: 1.5; }
.warning-text { color: #F56C6C; font-size: 12px; display: block; margin-top: 4px; }
</style>
