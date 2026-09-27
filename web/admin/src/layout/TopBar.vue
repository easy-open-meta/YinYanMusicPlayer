<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router';
import { ElMessageBox, ElMessage } from 'element-plus';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
const router = useRouter();
const route = useRoute();

async function logout() {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '退出', {
      confirmButtonText: '退出',
      cancelButtonText: '取消',
      type: 'warning',
    });
  } catch { return; }
  await auth.logout();
  ElMessage.success('已退出');
  router.replace('/login');
}
</script>

<template>
  <header class="top-bar">
    <div class="breadcrumb">{{ route.meta.title || '首页' }}</div>
    <div class="right">
      <div class="user">
        <div class="avatar">{{ auth.user?.displayName?.charAt(0) || '?' }}</div>
        <div class="info">
          <div class="name">{{ auth.user?.displayName }}</div>
          <div class="role">超级管理员</div>
        </div>
      </div>
      <el-button text type="danger" @click="logout">退出</el-button>
    </div>
  </header>
</template>

<style scoped>
.top-bar {
  display: flex; align-items: center; justify-content: space-between;
  height: 56px;
  padding: 0 24px;
  background: #FFFFFF;
  border-bottom: 0.5px solid #ECECF2;
}
.breadcrumb { font-size: 14px; color: #404040; }
.right { display: flex; align-items: center; gap: 16px; }
.user { display: flex; align-items: center; gap: 10px; }
.avatar {
  width: 32px; height: 32px; border-radius: 50%;
  background: #EEEDFE; color: #4F46E5;
  display: grid; place-items: center; font-weight: 600; font-size: 13px;
}
.info { display: flex; flex-direction: column; gap: 2px; }
.name { font-size: 13px; font-weight: 500; }
.role { font-size: 11px; color: #8A8AA3; }
</style>