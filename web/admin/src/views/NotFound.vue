<script setup lang="ts">
import { useRouter } from 'vue-router';
const router = useRouter();
</script>

<template>
  <div class="not-found">
    <h1>404</h1>
    <p>页面不存在</p>
    <el-button type="primary" @click="router.replace('/')">回首页</el-button>
  </div>
</template>

<style scoped>
.not-found {
  min-height: 100vh; display: grid; place-items: center; text-align: center;
}
h1 { font-size: 64px; margin: 0 0 8px; color: #4F46E5; }
p { color: #6E6E8A; margin-bottom: 16px; }
</style>