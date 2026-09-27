// 通用 CRUD 表组合式函数。
// 把「列表 + 分页 + 搜索 + 新建/编辑 Dialog + 删除二次确认 + 错误处理」打包好，
// 让 Songs/Albums/Artists/Categories 页面只关注字段。
//
// 用法：
//   const table = useCrudTable({
//     list: (params) => catalogApi.songs.list(params),
//     create: (body) => catalogApi.songs.create(body),
//     update: (id, body) => catalogApi.songs.update(id, body),
//     remove: (id) => catalogApi.songs.delete(id),
//   });
//   await table.fetch();
//   // table.items / table.loading / table.keyword / table.page / table.pageSize /
//   // table.dialogOpen / table.editing (null=关闭，{id,...}=当前编辑对象)
//   // table.handleNew() / handleEdit(row) / handleDelete(row) / handleSave(...)

import { ElMessage, ElMessageBox } from 'element-plus';
import { reactive, ref, shallowRef } from 'vue';
import { ApiException } from '@/api/http';

export interface CrudApi<T, TCreate, TUpdate> {
  list: (params: { keyword?: string; page: number; pageSize: number }) => Promise<{
    items: T[];
    total: number;
    page: number;
    pageSize: number;
  }>;
  create: (body: TCreate) => Promise<T>;
  update: (id: number, body: TUpdate) => Promise<T>;
  remove: (id: number) => Promise<void>;
}

export interface CrudTableReturn<T> {
  /** 列表数据。 */
  items: T[];
  /** 总数。 */
  total: number;
  /** 加载中。 */
  loading: boolean;
  /** 搜索关键词。 */
  keyword: string;
  /** 当前页。 */
  page: number;
  /** 每页大小。 */
  pageSize: number;
  /** 对话框是否打开。 */
  dialogOpen: boolean;
  /** 保存中。 */
  saving: boolean;
  /**
   * 当前正在编辑的对象（对话框打开时是深拷贝，关时为 null）。
   * 在模板里直接修改它的字段，保存时从这里读取。
   */
  editing: T | null;
  /**
   * 子组件 emit 新值后同步表单字段并触发响应式更新。
   * 用于表单里嵌了「自己管状态的子组件」（如 IconPicker）的场景 ——
   * editing 是 shallowRef，直接改属性不会触发依赖它的子组件重算。
   */
  syncEditing<K extends keyof T>(key: K, value: T[K]): void;
  /** 加载数据。 */
  fetch(): Promise<void>;
  /** 触发搜索（重置 page=1）。 */
  onSearch(): void;
  /** 页码变化。 */
  onPageChange(p: number): void;
  /** 每页条数变化。 */
  onPageSizeChange(s: number): void;
  /** 打开新建对话框。 */
  handleNew(): void;
  /** 打开编辑对话框（传入行数据）。 */
  handleEdit(item: T): void;
  /** 删除（带二次确认）。 */
  handleDelete(item: T): Promise<void>;
  /**
   * 保存（新建或更新）。
   * @param buildBody 返回 { id?, create?, update? } — 由页面构造请求体。
   */
  handleSave(buildBody: () => { id?: number; create?: unknown; update?: unknown }): Promise<void>;
  /** 关闭对话框。 */
  closeDialog(): void;
}

export function useCrudTable<T extends object, TCreate, TUpdate>(
  api: CrudApi<T, TCreate, TUpdate>,
  options?: {
    defaultPageSize?: number;
    confirmDelete?: boolean;
    confirmMessage?: (item: T) => string;
    /**
     * 新建时表单的初始骨架。
     *
     * ⚠️ 必传，否则「新建」对话框是空白的。
     * 原因：各页面用 `<el-form v-if="table.editing">` 包裹表单（为了在编辑时安全地
     * 绑定 `table.editing.xxx`），而 handleNew 原先把 editing 置为 null，
     * 导致新建时整个表单不渲染 —— 对话框只剩标题和按钮，无法录入。
     * 传入骨架后 editing 始终是对象，表单正常渲染。
     * 例：{ name: '', artistId: undefined, description: null }
     */
    emptyDraft?: () => T;
  },
): CrudTableReturn<T> {
  const pageSize = options?.defaultPageSize ?? 20;
  const doConfirm = options?.confirmDelete ?? true;
  const confirmMsg = options?.confirmMessage ?? ((i: T) => `确定要删除 #${(i as any).id} 吗？`);

  // 用 shallowRef 不用 ref/reactive，避免 Vue UnwrapRef 泛型干扰。
  // T 是业务类型，含 id?/artist? 等可选字段；用普通 ref 会触发 UnwrapRef 类型递归。
  const items = shallowRef<T[]>([]);
  const total = ref(0);
  const loading = ref(false);
  const keyword = ref('');
  const page = ref(1);
  const ps = ref(pageSize);
  const dialogOpen = ref(false);
  const saving = ref(false);
  // ⚠️ 编辑草稿必须是**深响应**对象（openDialog 里用 reactive() 包一层）。
  // 表单里 `v-model="table.editing.xxx"` 改的是对象属性：若只把普通对象塞进 shallowRef，
  // 属性写入不被追踪 → 父组件不重渲染 → 子组件拿到的 props 一直是旧值，
  // 于是 el-select 选完值框里还显示旧值、el-input-number / el-color-picker 同理。
  // 值其实已经改了（保存也生效），只是界面不刷新 —— 最容易被当成"根本没选中"。
  const editing = shallowRef<T | null>(null);

  /**
   * 子组件 emit 新值后写入草稿。
   * 用法：`@update:modelValue="v => table.syncEditing('iconGlyph', v)"`
   * （绑了 `v-model` 的普通字段不需要它 —— 草稿是 reactive 的，属性写入自己会触发更新。）
   */
  function syncEditing<K extends keyof T>(key: K, value: T[K]) {
    const cur = editing.value;
    if (!cur) return;
    // 这里直接写属性，不要写成 `editing.value = { ...cur, [key]: value }`：
    // 展开会把 reactive 代理摊回普通对象，响应性随之丢失，后续 v-model 写入又刷不出来。
    cur[key] = value;
  }

  async function fetch() {
    loading.value = true;
    try {
      const r = await api.list({ keyword: keyword.value || undefined, page: page.value, pageSize: ps.value });
      items.value = r.items as T[];
      total.value = r.total;
      page.value = r.page;
      ps.value = r.pageSize;
    } catch (e) {
      items.value = [];
      total.value = 0;
      ElMessage.error(toMsg(e));
    } finally {
      loading.value = false;
    }
  }

  function openDialog(item: T | null) {
    // 新建（item=null）时用 emptyDraft 造一个空骨架，而不是 null ——
    // 页面用 `v-if="table.editing"` 包表单，置 null 会让新建对话框变成空白。
    const draft = item
      ? deepClone(item)
      : options?.emptyDraft
        ? options.emptyDraft()
        : null;
    // reactive() 让草稿的属性写入可被追踪（原因见 editing 声明处）。
    // 外层仍是 shallowRef：替换引用的语义不变，只补上"属性写入"这一层。
    // `as T` 是绕开 UnwrapRef 泛型干扰的收窄（reactive(纯对象) 的运行时类型就是它本身）。
    editing.value = draft ? (reactive(draft) as T) : null;
    dialogOpen.value = true;
  }

  function closeDialog() {
    dialogOpen.value = false;
    editing.value = null;
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
    ps.value = s;
    page.value = 1;
    void fetch();
  }

  function handleNew() { openDialog(null); }
  function handleEdit(item: T) { openDialog(item); }

  async function handleDelete(item: T) {
    if (doConfirm) {
      try {
        await ElMessageBox.confirm(confirmMsg(item), '请确认', {
          type: 'warning',
          confirmButtonText: '删除',
          cancelButtonText: '取消',
        });
      } catch {
        return;
      }
    }
    try {
      await api.remove((item as any).id);
      ElMessage.success('已删除');
      if (items.value.length === 1 && page.value > 1) page.value -= 1;
      await fetch();
    } catch (e) {
      ElMessage.error(toMsg(e));
    }
  }

  async function handleSave(buildBody: () => { id?: number; create?: unknown; update?: unknown }) {
    saving.value = true;
    try {
      const { id, create, update } = buildBody();
      if (id && update) await api.update(id, update as TUpdate);
      else if (create) await api.create(create as TCreate);
      closeDialog();
      ElMessage.success('已保存');
      await fetch();
    } catch (e) {
      ElMessage.error(toMsg(e));
    } finally {
      saving.value = false;
    }
  }

  return {
    get items() { return items.value; },
    get total() { return total.value; },
    get loading() { return loading.value; },
    get keyword() { return keyword.value; },
    set keyword(v: string) { keyword.value = v; },
    get page() { return page.value; },
    set page(v: number) { page.value = v; },
    get pageSize() { return ps.value; },
    set pageSize(v: number) { ps.value = v; },
    get dialogOpen() { return dialogOpen.value; },
    set dialogOpen(v: boolean) { dialogOpen.value = v; },
    get saving() { return saving.value; },
    get editing() { return editing.value; },
    // setter：让 `v-model` 类绑定与子组件的 update:modelValue 能写回。
    // 缺了它，`table.editing = x` 会静默失败（Vue 只读 getter）。
    set editing(v: T | null) { editing.value = v; },
    fetch,
    onSearch,
    onPageChange,
    onPageSizeChange,
    handleNew,
    handleEdit,
    handleDelete,
    handleSave,
    closeDialog,
    syncEditing,
  };
}

function toMsg(e: unknown): string {
  if (e instanceof ApiException) return e.message;
  if (e instanceof Error) return e.message;
  return '请求失败';
}

function deepClone<T>(x: T): T {
  return JSON.parse(JSON.stringify(x));
}
