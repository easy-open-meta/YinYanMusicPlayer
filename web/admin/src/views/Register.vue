<script setup lang="ts">
// 首次部署用：库空时可达。注册成功 = 唯一超级管理员，注册页随后永久下线。
// ⚠️ 此页仅在「系统还没有 admin」时路由可达（见 router/index.ts）。

import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { getRegistrationOpen, register } from '@/api/auth';
import { ApiException } from '@/api/http';

const router = useRouter();
const form = ref({
userName: '',
password: '',
confirm: '',
displayName: '',
bootstrapCode: '',
});
const loading = ref(false);
const isOpen = ref(true);

onMounted(async () => {
  const { open } = await getRegistrationOpen('admin');
  isOpen.value = open;
  if (!open) router.replace('/login');
});

async function submit() {
  const f = form.value;
  if (!f.userName || f.userName.length < 3) return ElMessage.warning('账号至少 3 个字符');
  if (f.password.length < 6) return ElMessage.warning('密码至少 6 位');
  if (f.password !== f.confirm) return ElMessage.warning('两次密码不一致');
  if (!f.displayName) return ElMessage.warning('请输入显示名');
  if (!f.bootstrapCode) return ElMessage.warning('请输入引导码');

  loading.value = true;
  try {
    await register({
      userName: f.userName.trim(),
      password: f.password,
      displayName: f.displayName.trim(),
      source: 'admin',
      bootstrapCode: f.bootstrapCode.trim(),
    });
    ElMessage.success('超级管理员创建成功！请用该账号登录');
    router.replace('/login');
  } catch (e) {
    if (e instanceof ApiException) ElMessage.error(e.message);
    else ElMessage.error('注册失败');
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="register-page">
    <el-card class="register-card" shadow="hover">
      <template #header>
        <div class="reg-header">
          <div class="brand">创建超级管理员</div>
          <div class="hint">首次部署专用。注册成功后此页将永久关闭，后续账号由超管在后台「用户」页创建。</div>
        </div>
      </template>

      <el-alert v-if="isOpen" type="warning" :closable="false" class="warn">
        <strong>引导码</strong> 在 API 服务器的
        <code>%ProgramData%\YinYanMusic\bootstrap.txt</code>（Linux: <code>/var/lib/yinyan/bootstrap.txt</code>），
        或者 API 启动日志里。建超管成功后该文件自动销毁。
      </el-alert>

      <el-form :model="form" label-position="top" @keyup.enter="submit">
        <el-form-item label="引导码">
          <el-input v-model="form.bootstrapCode" placeholder="XXXX-XXXX-XXXX-XXXX-XXXX" clearable />
        </el-form-item>
        <el-form-item label="账号">
          <el-input v-model="form.userName" autocomplete="username" placeholder="superadmin" clearable />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.password" type="password" autocomplete="new-password" show-password />
        </el-form-item>
        <el-form-item label="再次输入">
          <el-input v-model="form.confirm" type="password" autocomplete="new-password" show-password />
        </el-form-item>
        <el-form-item label="显示名">
          <el-input v-model="form.displayName" placeholder="管理员" clearable />
        </el-form-item>

        <el-button type="primary" :loading="loading" class="submit" @click="submit">创建超级管理员</el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.register-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: linear-gradient(135deg, #FFF1DD 0%, #FAFAFC 60%);
}
.register-card { width: 420px; border-radius: 20px; }
.reg-header { display: flex; flex-direction: column; gap: 4px; }
.brand { font-weight: 600; font-size: 16px; }
.hint { font-size: 12px; color: #6E6E8A; line-height: 1.6; }
.warn { margin-bottom: 16px; }
.submit { width: 100%; margin-top: 8px; }
code { background: #F7F7FB; padding: 1px 6px; border-radius: 4px; font-size: 12px; }
</style>