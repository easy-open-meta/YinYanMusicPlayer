<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { adminApi } from '@/api/catalog';
import type { AdminUserDto, CreateAdminUserRequest, UpdateAdminUserRequest } from '@/api/catalog';

const state = {
  items: ref<AdminUserDto[]>([]),
  total: ref(0),
  loading: ref(false),
  keyword: ref(''),
  page: ref(1),
  pageSize: ref(20),
  dialogOpen: ref(false),
  saving: ref(false),
  editing: ref<AdminUserDto | null>(null),
};

const editingDraft = ref<AdminUserDto | null>(null);
const newUserPassword = ref('');
const resetPwdDraft = ref({ password: '' });
const resetPwdOpen = ref(false);

function openNew() {
  newUserPassword.value = '';
  editingDraft.value = { id: 0, userName: '', displayName: '', bio: null, avatarUrl: null, gender: '保密', createdAt: '', isDisabled: false };
  state.dialogOpen.value = true;
}

function openEdit(item: AdminUserDto) {
  editingDraft.value = { ...item };
  state.dialogOpen.value = true;
}

async function fetch() {
  state.loading.value = true;
  try {
    const r = await adminApi.users.list({ keyword: state.keyword.value || undefined, page: state.page.value, pageSize: state.pageSize.value });
    state.items.value = r.items;
    state.total.value = r.total;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '加载失败');
  } finally {
    state.loading.value = false;
  }
}

async function handleSave() {
  const d = editingDraft.value!;
  state.saving.value = true;
  try {
    if (d.id === 0) {
      if (!newUserPassword.value || newUserPassword.value.length < 6) {
        ElMessage.warning('密码至少 6 位');
        state.saving.value = false;
        return;
      }
      const body: CreateAdminUserRequest = {
        userName: d.userName!,
        password: newUserPassword.value,
        displayName: d.displayName!,
        gender: d.gender ?? null,
      };
      await adminApi.users.create(body);
      ElMessage.success('用户已创建');
    } else {
      const body: UpdateAdminUserRequest = { displayName: d.displayName!, bio: d.bio ?? null, gender: d.gender ?? null, avatarUrl: d.avatarUrl ?? null };
      await adminApi.users.update(d.id, body);
      ElMessage.success('已保存');
    }
    state.dialogOpen.value = false;
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '保存失败');
  } finally {
    state.saving.value = false;
  }
}

async function handleToggleDisabled(item: AdminUserDto) {
  try {
    await adminApi.users.setDisabled(item.id, !item.isDisabled);
    ElMessage.success(item.isDisabled ? '已启用' : '已禁用');
    await fetch();
  } catch (e: any) {
    ElMessage.error(e?.message ?? '操作失败');
  }
}

function openResetPwd(item: AdminUserDto) {
  editingDraft.value = item;
  resetPwdDraft.value.password = '';
  resetPwdOpen.value = true;
}

async function handleResetPwd() {
  if (!resetPwdDraft.value.password || resetPwdDraft.value.password.length < 6) {
    ElMessage.warning('密码至少 6 位');
    return;
  }
  try {
    await adminApi.users.resetPassword(editingDraft.value!.id, { newPassword: resetPwdDraft.value.password });
    ElMessage.success('密码已重置');
    resetPwdOpen.value = false;
  } catch (e: any) {
    ElMessage.error(e?.message ?? '重置失败');
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
      <el-input v-model="state.keyword.value" placeholder="搜索用户名 / 昵称" prefix-icon="Search" clearable
        style="width:260px" @keyup.enter="onSearch" @clear="onSearch" />
      <el-button type="primary" @click="openNew">+ 新建账号</el-button>
    </div>

    <el-table :data="state.items.value" v-loading="state.loading.value" stripe>
      <el-table-column prop="id"        label="ID"       width="70"  />
      <el-table-column label="头像"      width="64">
        <template #default="{ row }">
          <el-avatar v-if="row.avatarUrl" :src="row.avatarUrl" :size="36" />
          <div v-else class="avatar-ph">{{ row.displayName?.charAt(0) }}</div>
        </template>
      </el-table-column>
      <el-table-column prop="userName"   label="用户名"    min-width="120" />
      <el-table-column prop="displayName" label="昵称"      min-width="120" />
      <el-table-column prop="gender"    label="性别"      width="70" />
      <el-table-column label="状态"      width="80">
        <template #default="{ row }">
          <el-tag :type="row.isDisabled ? 'danger' : 'success'" size="small">{{ row.isDisabled ? '已禁用' : '正常' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="注册时间" width="160">
        <template #default="{ row }">{{ row.createdAt?.substring(0, 10) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link :type="row.isDisabled ? 'success' : 'warning'" @click="handleToggleDisabled(row)">
            {{ row.isDisabled ? '启用' : '禁用' }}
          </el-button>
          <el-button link type="primary" @click="openResetPwd(row)">重置密码</el-button>
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

    <!-- 新建/编辑 Dialog -->
    <!-- ⚠️ 必须绑 state.dialogOpen.value，不能写 state.dialogOpen：
         后者绑的是 ref 对象本身，v-model 写回时不生效，el-dialog 内外状态脱节，
         遮罩（.el-overlay）会残留成 display:block 并盖住整页 —— 表现为
         「这一页所有真实鼠标点击都失效（左侧菜单点不动）」，而程序化 router.push 却正常。 -->
    <el-dialog v-model="state.dialogOpen.value"
      :title="editingDraft?.id === 0 ? '新建账号' : '编辑用户'" width="460px" :close-on-click-modal="false">
      <el-form v-if="editingDraft" label-width="80">
        <el-form-item label="用户名" required>
          <el-input v-model="editingDraft.userName" :disabled="editingDraft.id !== 0" placeholder="登录账号名" maxlength="32" />
        </el-form-item>
        <el-form-item v-if="editingDraft.id === 0" label="初始密码" required>
          <el-input v-model="newUserPassword" type="password" placeholder="请输入初始密码（至少 6 位）" show-password maxlength="64" />
        </el-form-item>
        <el-form-item label="昵称" required>
          <el-input v-model="editingDraft.displayName" placeholder="显示名称" maxlength="64" />
        </el-form-item>
        <el-form-item label="性别">
          <el-input v-model="editingDraft.gender" placeholder="男/女/保密" maxlength="16" />
        </el-form-item>
        <el-form-item label="头像 URL">
          <el-input v-model="editingDraft.avatarUrl" placeholder="https://..." />
        </el-form-item>
        <el-form-item label="简介">
          <el-input v-model="editingDraft.bio" type="textarea" :rows="2" placeholder="个人简介" maxlength="200" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="state.dialogOpen.value = false">取消</el-button>
        <el-button type="primary" :loading="state.saving.value" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <!-- 重置密码 Dialog -->
    <el-dialog v-model="resetPwdOpen" title="重置密码" width="380px" :close-on-click-modal="false">
      <p style="margin-bottom:12px;color:#6E6E8A;font-size:13px">
        为用户 <strong>{{ editingDraft?.displayName }}</strong> 重置密码。
      </p>
      <el-form label-width="80">
        <el-form-item label="新密码" required>
          <el-input v-model="resetPwdDraft.password" type="password" placeholder="至少 6 位" show-password maxlength="64" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetPwdOpen = false">取消</el-button>
        <el-button type="primary" @click="handleResetPwd">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page { padding: 8px; }
.toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
.avatar-ph {
  width: 36px; height: 36px; border-radius: 50%;
  background: #EEEDFE; color: #4F46E5;
  display: grid; place-items: center; font-weight: 600; font-size: 14px;
}
.form-tip { font-size: 11px; color: #8A8AA3; margin-top: 4px; line-height: 1.4; }
</style>
