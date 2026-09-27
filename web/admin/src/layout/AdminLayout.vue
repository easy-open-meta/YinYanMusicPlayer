<script setup lang="ts">
import SideMenu from '@layout/SideMenu.vue';
import TopBar from '@layout/TopBar.vue';
</script>

<template>
  <div class="admin-layout">
    <aside class="side">
      <SideMenu />
    </aside>
    <div class="main">
      <TopBar />
      <div class="content">
        <router-view />
      </div>
    </div>
  </div>
</template>

<style scoped>
.admin-layout { display: flex; min-height: 100vh; background: #FAFAFC; }
.side {
  width: 224px;
  background: #FFFFFF;
  border-right: 0.5px solid #ECECF2;
  padding: 14px 12px;
}
.main { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.content { padding: 20px 24px 32px; max-width: 1440px; width: 100%; }
</style>