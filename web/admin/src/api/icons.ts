// 分区图标目录（Material Icons）。
//
// ⚠️ 为什么用码点而不是图标名：
// Categories.IconGlyph 在数据库里存的是 Material Icons 的 Unicode 字符（码点），
// MAUI 客户端用 <Label FontFamily="MaterialIcons" Text="{Binding IconGlyph}" /> 渲染。
// 后台前端必须沿用同一套码点语义，否则改完客户端图标就废了。
//
// 本目录的每个码点都已用 fontTools 校验存在于 MaterialIcons-Regular.ttf 中
// （见提交记录；字体与 MAUI 项目 Resources/Fonts 下的是同一份文件）。
// 新增条目时请先确认字体里有该码点，否则会显示成豆腐块。

export interface IconOption {
  /** Material Icons 图标名（仅用于搜索/展示，不存库）。 */
  name: string;
  /** 中文说明，便于检索。 */
  label: string;
  /** 存入 IconGlyph 的 Unicode 字符。 */
  glyph: string;
  /** 码点，便于调试与搜索（如 "E405"）。 */
  code: string;
}

function icon(name: string, label: string, codePoint: number): IconOption {
  return {
    name,
    label,
    glyph: String.fromCodePoint(codePoint),
    code: codePoint.toString(16).toUpperCase(),
  };
}

/** 音乐 / 内容相关图标（已逐个校验字体可用）。 */
export const ICON_OPTIONS: IconOption[] = [
  // ── 音乐 ──
  icon('music_note',            '音符',     0xE405),
  icon('library_music',         '音乐库',   0xE8E2),
  icon('queue_music',           '播放列表', 0xE03D),
  icon('audiotrack',            '音轨',     0xE3A1),
  icon('album',                 '专辑',     0xE019),
  icon('headphones',            '耳机',     0xF01F),
  icon('speaker',               '音箱',     0xE32D),
  icon('mic',                   '麦克风',   0xE029),
  icon('radio',                 '电台',     0xE03E),
  icon('piano',                 '钢琴',     0xE521),
  // ── 音效 ──
  icon('graphic_eq',            '均衡器',   0xE1B8),
  icon('equalizer',             '调音',     0xE1BD),
  icon('surround_sound',        '环绕声',   0xE049),
  icon('spatial_audio',         '空间音频', 0xEBEB),
  icon('hearing',               '听觉',     0xE023),
  // ── 播放 ──
  icon('play_circle',           '播放',     0xE1C4),
  icon('podcasts',              '播客',     0xEF6C),
  icon('cast',                  '投送',     0xE307),
  icon('movie',                 '影视',     0xE02C),
  icon('tv',                    '电视',     0xE333),
  // ── 收藏 / 热度 ──
  icon('favorite',              '喜欢',     0xE87D),
  icon('star',                  '收藏',     0xE838),
  icon('bookmark',              '书签',     0xE866),
  icon('local_fire_department', '热门',     0xEF55),
  icon('whatshot',              '热榜',     0xE80B),
  // ── 主题 / 推荐 ──
  icon('celebration',           '庆祝',     0xEA65),
  icon('nightlife',             '夜生活',   0xEA62),
  icon('rocket_launch',         '新歌',     0xEB9B),
  icon('auto_awesome',          '推荐',     0xE65F),
  icon('emoji_events',          '榜单',     0xEA23),
  icon('public',                '环球',     0xE80B),
  icon('language',              '语言',     0xE894),
];

/** 码点 → 图标名，用于把库里的存量值显示成可读名称。 */
const CODE_TO_NAME = new Map(ICON_OPTIONS.map((o) => [o.code, o.name]));

/** 查某个 glyph 对应的图标名（不在目录内返回 null）。 */
export function iconNameOf(glyph?: string | null): string | null {
  if (!glyph) return null;
  const cp = glyph.codePointAt(0);
  if (cp === undefined) return null;
  return CODE_TO_NAME.get(cp.toString(16).toUpperCase()) ?? null;
}
