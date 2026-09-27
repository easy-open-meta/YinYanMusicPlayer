<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage } from 'element-plus';
import { adminLogin, login as userLogin, getRegistrationOpen } from '@/api/auth';
import { ApiException } from '@/api/http';
import { useAuthStore } from '@/stores/auth';

const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const form = ref({ userName: '', password: '' });
const loading = ref(false);
// 注册入口仅在"还没有超管"时显示 —— 否则点进去也会被守卫弹回来，白费一次点击。
// 查询失败（后端没起/代理不通）时保持隐藏，不阻塞登录这条主链路。
const showRegister = ref(false);

onMounted(async () => {
  try {
    const { open } = await getRegistrationOpen('admin');
    showRegister.value = open;
  } catch {
    showRegister.value = false;
  }
});

async function submit() {
  if (!form.value.userName || !form.value.password) {
    ElMessage.warning('请输入账号和密码');
    return;
  }
  loading.value = true;
  try {
    // 后台必须 admin 角色，api/admin/login 会校验
    const resp = await adminLogin(form.value);
    if (resp.user.role !== 'admin') {
      ElMessage.error('该账号不是管理员');
      loading.value = false;
      return;
    }
    auth.setSession(resp.accessToken, resp.user);
    ElMessage.success(`欢迎，${resp.user.displayName}`);
    const redirect = (route.query.redirect as string) || '/dashboard';
    router.replace(redirect);
  } catch (e) {
    if (e instanceof ApiException) ElMessage.error(e.message);
    else ElMessage.error('登录失败');
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="login-page">
    <el-card class="login-card" shadow="hover">
      <template #header>
        <div class="login-header">
          <div class="brand">音言音乐 · 后台管理</div>
          <div class="hint">请使用超级管理员账号登录</div>
        </div>
      </template>

      <el-form :model="form" label-position="top" @keyup.enter="submit">
        <el-form-item label="账号">
          <el-input v-model="form.userName" autocomplete="username" placeholder="superadmin" clearable />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.password" type="password" autocomplete="current-password" show-password placeholder="••••••" />
        </el-form-item>
        <el-button type="primary" :loading="loading" class="submit" @click="submit">登录</el-button>
      </el-form>

      <div class="footer-hint">
        <template v-if="showRegister">
          后台令牌有效期 8 小时；首次部署请先在
          <router-link to="/register" class="link">注册页</router-link>
          创建超级管理员。
        </template>
        <template v-else>
          后台令牌有效期 8 小时。忘记密码请联系其他超级管理员重置。
        </template>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: linear-gradient(135deg, #EEEDFE 0%, #FAFAFC 60%);
}
.login-card { width: 360px; border-radius: 20px; }
.login-header { display: flex; flex-direction: column; gap: 4px; }
.brand { font-weight: 600; font-size: 16px; }
.hint { font-size: 12px; color: #6E6E8A; }
.submit { width: 100%; margin-top: 8px; }
.footer-hint { margin-top: 16px; font-size: 12px; color: #8A8AA3; line-height: 1.6; }
.link { color: #4F46E5; text-decoration: none; }
.link:hover { text-decoration: underline; }
</style>