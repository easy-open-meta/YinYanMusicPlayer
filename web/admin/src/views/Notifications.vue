<script setup lang="ts">
// 消息推送（V2.15）：新建通知 + 已发送列表。
// 手写 fetch/分页，不套 useCrudTable —— 「行」是发送记录（不可编辑、不可删除），
// 操作只有"发一条新的"，硬套 CRUD composable 会凭空长出编辑/删除按钮语义。

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { ElMessage } from 'element-plus';
import { notificationsApi } from '@/api/notifications';
import type {
  AdminNotificationDto,
  NotificationStatus,
  NotificationTargetType,
} from '@/api/notifications';
import { adminApi, catalogApi, playlistApi } from '@/api/catalog';
import type {
  AdminUserDto,
  AlbumDto,
  PagedAdminUserResult,
  PlaylistDto,
  SongDto,
} from '@/api/catalog';

const items = ref<AdminNotificationDto[]>([]);
const total = ref(0);
const loading = ref(false);
const keyword = ref('');
const page = ref(1);
const pageSize = ref(20);

// ── 新建通知弹窗 ────────────────────────────────────────────────────────────
const dialogVisible = ref(false);
const saving = ref(false);
const form = reactive({
  title: '',
  content: '',
  targetType: 'all' as NotificationTargetType,
  recipientUserIds: [] as number[],
  relatedSongId: null as number | null,
  relatedPlaylistId: null as number | null,
  relatedAlbumId: null as number | null,
});

// 关联对象三选一：通知点开只能落到一个地方。选了新的就把另外两项清掉 ——
// 只靠后端/App 端兜底不行，那样发出去的通知会跳到运营没预期的页面。
// 只在**选中**（值为真）时清别的：清空某一项不算"改了关联"，不该动另外两项。
watch(
  () => form.relatedSongId,
  (v) => {
    if (!v) return;
    form.relatedPlaylistId = null;
    form.relatedAlbumId = null;
  },
);
watch(
  () => form.relatedPlaylistId,
  (v) => {
    if (!v) return;
    form.relatedSongId = null;
    form.relatedAlbumId = null;
  },
);
watch(
  () => form.relatedAlbumId,
  (v) => {
    if (!v) return;
    form.relatedSongId = null;
    form.relatedPlaylistId = null;
  },
);

/**
 * el-select 在 multiple 开关之间给的**取值形状不同**：multiple=true 给数组，
 * multiple=false 给单个值。若直接 `v-model="form.recipientUserIds"`，
 * 切到「单个用户」后这个字段会被写成 number，而下游一律按数组用
 * （校验里的 `.length`、提交时当列表传给服务端）——
 * `(9).length === undefined`，于是"明明只选了一个"也被判成不合法，
 * 弹「单个推送只能选择一个接收用户」；就算放过这层校验，发出去的也是裸数字 9 而不是 [9]，
 * 服务端按 `IReadOnlyList<long>?` 反序列化会直接失败。
 * 所以存储统一是数组：**读**的时候按当前模式给 el-select 想要的形状，**写**的时候一律归一成数组。
 */
const recipientModel = computed(() =>
  form.targetType === 'partial' ? form.recipientUserIds : (form.recipientUserIds[0] ?? null),
);

function normalizeIds(value: unknown): number[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) return value.filter((v): v is number => typeof v === 'number');
  return typeof value === 'number' ? [value] : [];
}

function onRecipientsUpdate(value: unknown) {
  form.recipientUserIds = normalizeIds(value);
}

// 从「部分」切到「单个」而此前选了多人时清空，让运营重选：
// 静默只保留第一个会让人以为还是发给原来那批人。
watch(
  () => form.targetType,
  (t) => {
    if (t === 'single' && form.recipientUserIds.length > 1) form.recipientUserIds = [];
  },
);

// 部分/单个用户：远程搜索用户（全量拉一遍再本地过滤，用户量级是后台规模）
const userOptions = ref<AdminUserDto[]>([]);
const userKeyword = ref('');
const userLoading = ref(false);async function searchUsers(kw: string) {
  userKeyword.value = kw;
  if (!kw.trim()) {
    userOptions.value = [];
    return;
  }
  userLoading.value = true;
  try {
    const r: PagedAdminUserResult = await adminApi.users.list({
      keyword: kw.trim(),
      page: 1,
      pageSize: 20,
    });
    userOptions.value = r.items;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '用户搜索失败');
  } finally {
    userLoading.value = false;
  }
}

// 关联歌曲 / 关联歌单：同样是远程搜索下拉，避免让运营手填 Id。
// 两个接口都支持 keyword（服务端 ILike）：/api/songs 命中标题/歌手/专辑，/api/playlists 命中歌单名/创建者。
const songOptions = ref<SongDto[]>([]);
const songLoading = ref(false);

async function searchSongs(kw: string) {
  if (!kw.trim()) {
    songOptions.value = [];
    return;
  }
  songLoading.value = true;
  try {
    const r = await catalogApi.songs.list({ keyword: kw.trim(), page: 1, pageSize: 20 });
    songOptions.value = r.items;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '歌曲搜索失败');
  } finally {
    songLoading.value = false;
  }
}

const playlistOptions = ref<PlaylistDto[]>([]);
const playlistLoading = ref(false);

async function searchPlaylists(kw: string) {
  if (!kw.trim()) {
    playlistOptions.value = [];
    return;
  }
  playlistLoading.value = true;
  try {
    const r = await playlistApi.list({ keyword: kw.trim(), page: 1, pageSize: 20 });
    playlistOptions.value = r.items;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '歌单搜索失败');
  } finally {
    playlistLoading.value = false;
  }
}

const albumOptions = ref<AlbumDto[]>([]);
const albumLoading = ref(false);

async function searchAlbums(kw: string) {
  if (!kw.trim()) {
    albumOptions.value = [];
    return;
  }
  albumLoading.value = true;
  try {
    const r = await catalogApi.albums.list({ keyword: kw.trim(), page: 1, pageSize: 20 });
    albumOptions.value = r.items;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '专辑搜索失败');
  } finally {
    albumLoading.value = false;
  }
}

function openDialog() {
  form.title = '';
  form.content = '';
  form.targetType = 'all';
  form.recipientUserIds = [];
  form.relatedSongId = null;
  form.relatedPlaylistId = null;
  form.relatedAlbumId = null;
  userOptions.value = [];
  userKeyword.value = '';
  // 上一次搜索的候选也要清掉：否则重新打开弹窗会看到与当前输入无关的旧选项
  songOptions.value = [];
  playlistOptions.value = [];
  albumOptions.value = [];
  dialogVisible.value = true;
}

async function fetch() {
  loading.value = true;
  try {
    const r = await notificationsApi.list({
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

async function handleSend() {
  const title = form.title.trim();
  const content = form.content.trim();
  if (!title) {
    ElMessage.warning('标题不能为空');
    return;
  }
  if (!content) {
    ElMessage.warning('正文不能为空');
    return;
  }
  if (form.targetType !== 'all') {
    if (form.targetType === 'single' && form.recipientUserIds.length !== 1) {
      // 分开提示：这条报错原来两种情况共用一句「只能选择一个接收用户」，
      // 用户"明明只选了一个"却看到这句，会以为是系统坏了（其实是当时值被写成了裸数字）。
      ElMessage.warning(
        form.recipientUserIds.length === 0 ? '请选择一个接收用户' : '单个推送只能选择一个接收用户',
      );
      return;
    }
    if (form.targetType === 'partial' && form.recipientUserIds.length === 0) {
      ElMessage.warning('请至少选择一个接收用户');
      return;
    }
  }

  saving.value = true;
  try {
    await notificationsApi.send({
      title,
      content,
      targetType: form.targetType,
      recipientUserIds: form.targetType === 'all' ? undefined : form.recipientUserIds,
      // 三个关联字段都是搜索下拉（值要么是真实 Id，要么是清空后的 null/undefined），
      // 这里的 > 0 守卫只作兜底：0 不是合法外键，绝不能发到服务端。
      // （历史：早前是手填 Id 的 el-input-number，清空会落成 0，那会儿这道守卫是必需的。）
      relatedSongId: form.relatedSongId && form.relatedSongId > 0 ? form.relatedSongId : null,
      relatedPlaylistId:
        form.relatedPlaylistId && form.relatedPlaylistId > 0 ? form.relatedPlaylistId : null,
      relatedAlbumId:
        form.relatedAlbumId && form.relatedAlbumId > 0 ? form.relatedAlbumId : null,
    });
    ElMessage.success('已发送');
    dialogVisible.value = false;
    page.value = 1;
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '发送失败');
  } finally {
    saving.value = false;
  }
}

function onSearch() {
  page.value = 1;
  void fetch();
}
function onPageChange(p: number) {
  page.value = p;
  void fetch();
}
function onPageSizeChange(s: number) {
  pageSize.value = s;
  page.value = 1;
  void fetch();
}

function fmtDateTime(s?: string | null) {
  if (!s) return '—';
  // ISO → 本地可读（YYYY-MM-DD HH:mm）
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) return s;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

const targetLabels: Record<NotificationTargetType, string> = {
  all: '全部用户',
  partial: '部分用户',
  single: '单个用户',
};

type TagType = 'primary' | 'info' | 'warning' | 'success' | 'danger';

function statusOf(status: NotificationStatus): { text: string; type: TagType } {
  switch (status) {
    case 'completed':
      return { text: '已完成', type: 'success' };
    case 'sending':
      return { text: '发送中', type: 'warning' };
    case 'failed':
      return { text: '失败', type: 'danger' };
    default:
      return { text: status, type: 'info' };
  }
}

/**
 * 关联对象摘要。弹窗里已是三选一，这里仍按优先级取一个 ——
 * 与 App 端点击时的判定顺序保持一致（歌单 > 专辑 > 歌曲），
 * 这样列表上显示的就是用户实际会跳到的那个目标。
 */
function relatedText(row: AdminNotificationDto): string {
  if (row.relatedPlaylistId) return `歌单 #${row.relatedPlaylistId}`;
  if (row.relatedAlbumId) return `专辑 #${row.relatedAlbumId}`;
  if (row.relatedSongId) return `歌曲 #${row.relatedSongId}`;
  return '—';
}

onMounted(() => void fetch());
</script>

<template>
  <div class="page">
    <el-alert type="info" :closable="false" show-icon class="hint">
      <template #title>消息推送说明</template>
      <div class="hint-body">
        <div>发送后在线用户会实时收到（SignalR）；离线用户下次打开 App 也能在通知列表里看到。</div>
        <div>
          <b>全部用户</b>：按发送时的未停用账号生成接收记录；<b>部分/单个</b>：按选中的用户 ID 列表。
          可选关联歌曲或歌单，用户点击通知可跳转。
        </div>
      </div>
    </el-alert>

    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索标题 / 正文"
        prefix-icon="Search"
        clearable
        style="width: 220px"
        @keyup.enter="onSearch"
        @clear="onSearch"
      />
      <div class="spacer" />
      <el-button type="primary" @click="openDialog">新建通知</el-button>
    </div>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="title" label="标题" min-width="160" show-overflow-tooltip />
      <el-table-column prop="content" label="正文" min-width="220" show-overflow-tooltip />
      <el-table-column label="目标范围" width="100">
        <template #default="{ row }">{{ targetLabels[row.targetType as NotificationTargetType] ?? row.targetType }}</template>
      </el-table-column>
      <el-table-column label="接收人数" width="90">
        <template #default="{ row }"><span class="stat-num">{{ row.recipientCount }}</span></template>
      </el-table-column>
      <el-table-column label="已读" width="90">
        <template #default="{ row }"><span class="stat-num">{{ row.readCount }}</span></template>
      </el-table-column>
      <el-table-column label="关联" width="110">
        <template #default="{ row }">{{ relatedText(row) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="statusOf(row.status as NotificationStatus).type" size="small">
            {{ statusOf(row.status as NotificationStatus).text }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdByName" label="发送人" width="110" />
      <el-table-column label="发送时间" width="150">
        <template #default="{ row }">{{ fmtDateTime(row.sentAtUtc ?? row.createdAtUtc) }}</template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="total > 0"
      :total="total"
      :current-page="page"
      :page-size="pageSize"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next"
      style="margin-top: 14px; justify-content: flex-end"
      @current-change="onPageChange"
      @size-change="onPageSizeChange"
    />

    <el-dialog v-model="dialogVisible" width="520px" title="新建通知">
      <el-form label-width="90px">
        <el-form-item label="标题" required>
          <el-input v-model="form.title" maxlength="100" show-word-limit placeholder="1–100 字" />
        </el-form-item>
        <el-form-item label="正文" required>
          <el-input
            v-model="form.content"
            type="textarea"
            :rows="4"
            maxlength="500"
            show-word-limit
            placeholder="1–500 字，纯文本"
          />
        </el-form-item>
        <el-form-item label="接收范围">
          <el-radio-group v-model="form.targetType">
            <el-radio value="all">全部用户</el-radio>
            <el-radio value="partial">部分用户</el-radio>
            <el-radio value="single">单个用户</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="form.targetType !== 'all'" label="选择用户" required>
          <el-select
            :model-value="recipientModel"
            @update:model-value="onRecipientsUpdate"
            :multiple="form.targetType === 'partial'"
            :filterable="true"
            :remote="true"
            :remote-method="searchUsers"
            :loading="userLoading"
            placeholder="输入用户名 / 昵称搜索"
            style="width: 100%"
            clearable
          >
            <el-option
              v-for="u in userOptions"
              :key="u.id"
              :label="`${u.userName}（${u.displayName}）`"
              :value="u.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="关联歌曲">
          <div class="field">
            <!-- 远程搜索下拉（与上面的用户选择同一套写法）：运营不必知道歌曲 Id -->
            <el-select
              v-model="form.relatedSongId"
              :filterable="true"
              :remote="true"
              :remote-method="searchSongs"
              :loading="songLoading"
              placeholder="输入歌名 / 歌手 / 专辑搜索"
              style="width: 100%"
              clearable
            >
              <el-option
                v-for="s in songOptions"
                :key="s.id"
                :label="`${s.title} — ${s.artistName}`"
                :value="s.id"
              />
            </el-select>
            <div class="field-hint">
              可不填。选了之后用户点击通知会跳到该歌曲的播放页。
              三项关联对象（歌曲 / 歌单 / 专辑）<b>三选一</b>，选了新的会自动清掉另外两项。
            </div>
          </div>
        </el-form-item>
        <el-form-item label="关联歌单">
          <div class="field">
            <el-select
              v-model="form.relatedPlaylistId"
              :filterable="true"
              :remote="true"
              :remote-method="searchPlaylists"
              :loading="playlistLoading"
              placeholder="输入歌单名 / 创建者搜索"
              style="width: 100%"
              clearable
            >
              <el-option
                v-for="p in playlistOptions"
                :key="p.id"
                :label="`${p.name}（by ${p.ownerName}）`"
                :value="p.id"
              />
            </el-select>
            <div class="field-hint">可不填。用户点击通知会打开该歌单页。</div>
          </div>
        </el-form-item>
        <el-form-item label="关联专辑">
          <div class="field">
            <el-select
              v-model="form.relatedAlbumId"
              :filterable="true"
              :remote="true"
              :remote-method="searchAlbums"
              :loading="albumLoading"
              placeholder="输入专辑名 / 歌手搜索"
              style="width: 100%"
              clearable
            >
              <el-option
                v-for="a in albumOptions"
                :key="a.id"
                :label="`${a.name} — ${a.artist?.name ?? ''}`"
                :value="a.id"
              />
            </el-select>
            <div class="field-hint">可不填。用户点击通知会直接播放整张专辑。</div>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSend">发送</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page {
  padding: 8px;
}
.hint {
  margin-bottom: 12px;
}
.hint-body {
  font-size: 12px;
  line-height: 1.7;
}
.toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}
.spacer {
  flex: 1;
}
.stat-num {
  font-variant-numeric: tabular-nums;
  color: #8a8aa3;
  font-size: 13px;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 2px;
  width: 100%;
}
.field-hint {
  font-size: 12px;
  color: #8a8aa3;
  line-height: 1.5;
}
</style>
