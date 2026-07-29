#!/usr/bin/env node
// ---------------------------------------------------------------------------
// 用途：方案A · Emoji → [TAG] 批量替换脚本（后端 .cs/.xaml + 前端 .tsx/.ts/.html）
// 用法：node tools/remove-emoji.mjs [dry-run]
//       带 dry-run 参数时只打印改动，不写文件
// 规则：按 brainstorm Step4 第 1 部分映射表严格替换（一个 emoji 只对应一个 TAG）
// ---------------------------------------------------------------------------
import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(__dirname, '..')          // /workspace/src
const DRY_RUN = process.argv.includes('dry-run')

// ============= Emoji → TAG 映射表 =============
// 注意：匹配顺序按「字符长度倒序」（多码位 emoji 先匹配），避免误切
const RAW_MAP = [
  // ===== 双码位（带 VAR-16 / ZWJ）先匹配 =====
  ['⚠️', '[WARN]'], ['‼️', '[WARN]'], ['❗', '[WARN]'],
  ['ℹ️', '[INFO]'],
  ['⚙️', '[CFG]'], ['🛠', '[CFG]'],
  ['⏰', '[TIME]'], ['⏱️', '[TIME]'], ['⏲️', '[TIME]'],
  ['☑️', '[OK]'],   ['✔️', '[OK]'],
  ['🔐', '[SEC]'],
  ['🪟', '[UI]'],
  ['🗂️', '[DUMP]'],
  ['✏️', '[EDIT]'],

  // ===== 启动/初始化 =====
  ['🚀', '[BOOT]'],
  ['🏗️', '[BUILD]'],
  ['🏭', '[FACTORY]'],
  ['🧩', '[ASSEMBLE]'],
  ['🌉', '[BRDG]'],
  ['🛡', '[SEC]'],
  ['🏠', '[HOME]'],
  ['🏎️', '[SPEED]'],

  // ===== 成功/OK =====
  ['✅', '[OK]'],
  ['☑', '[OK]'],
  ['✔️', '[OK]'],
  ['🟢', '[OK]'],

  // ===== 错误/失败 =====
  ['❌', '[ERR]'], ['❎', '[ERR]'], ['🚫', '[ERR]'],
  ['💥', '[FATAL]'],['💀', '[FATAL]'],['🔥', '[FATAL]'],['🆘', '[FATAL]'],['💣', '[FATAL]'],
  ['🚨', '[WARN]'],
  ['🔴', '[ERR]'],
  ['⚫', '[STOP]'],

  // ===== 警告/注意 =====
  ['⚠', '[WARN]'],
  ['🟡', '[WARN]'],
  ['🟠', '[WARN]'],

  // ===== UI / 窗口 / 提示 =====
  ['📜', '[LOG]'],
  ['💬', '[MSG]'],
  ['📢', '[MSG]'],
  ['🏷️', '[LABEL]'],
  ['🎮', '[CTRL]'],
  ['🎛', '[CTRL]'],
  ['🔘', '[CTRL]'],
  ['💖', '[HEART]'],
  ['💞', '[HEART]'],
  ['🖱️', '[MOUSE]'],
  ['🖱', '[MOUSE]'],

  // ===== 日志/文档/清单 =====
  ['📝', '[LOG]'],  ['📋', '[LOG]'],  ['📄', '[LOG]'],  ['📃', '[LOG]'],
  ['📚', '[DOC]'],
  ['📑', '[DOC]'],
  ['📏', '[SIZE]'],
  ['🔝', '[TOP]'],
  ['📐', '[LAYOUT]'],
  ['🃏', '[CARD]'],
  ['✨', '[FX]'],
  ['🌈', '[FX]'],
  ['🆕', '[NEW]'],
  ['🆔', '[ID]'],
  ['🔑', '[KEY]'],

  // ===== API / 桥接 / 端口 =====
  ['🔌', '[API]'],
  ['🔗', '[LINK]'],
  ['🧭', '[NAV]'],
  ['📡', '[BRDG]'],
  ['🔁', '[RETRY]'],
  ['🔄', '[REFRESH]'],
  ['♻️', '[CACHE]'],
  ['🔬', '[SCAN]'],
  ['🕵️', '[SCAN]'],
  ['📏', '[PARSE]'],

  // ===== 消息/事件 =====
  ['📨', '[MSG]'],  ['📥', '[MSG]'],  ['📤', '[MSG]'],
  ['🏓', '[PING]'],
  ['🎯', '[DONE]'],

  // ===== 退出/销毁/停止 =====
  ['👋', '[EXIT]'],
  ['💽', '[DUMP]'],
  ['🛑', '[STOP]'], ['⛔', '[STOP]'],
  ['🔫', '[KILL]'],

  // ===== 资源/打包/存储 =====
  ['📦', '[PKG]'],
  ['💾', '[SAVE]'],
  ['📀', '[DISK]'],
  ['📁', '[FS]'],   ['📂', '[FS]'],
  ['🧵', '[THREAD]'],
  ['🔢', '[NUM]'],

  // ===== 指标/图表/监控 =====
  ['📊', '[METRIC]'],['📈', '[METRIC]'],['📉', '[METRIC]'],['⚡', '[METRIC]'],['🧮', '[METRIC]'],
  ['🧠', '[CPU]'],
  ['🌿', '[CLEAN]'],

  // ===== 快照/计时 =====
  ['📸', '[SNAP]'],
  ['🏁', '[FIN]'],
  ['⏩', '[FAST]'],

  // ===== 提示/建议 =====
  ['💡', '[HINT]'],

  // ===== 安全/权限/加密 =====
  ['🔒', '[SEC]'],
  ['🔓', '[OPEN]'],

  // ===== 标记/固定 =====
  ['📌', '[NOTE]'],
  ['⭐', '[STAR]'],
  ['🌟', '[STAR]'],

  // ===== 主题/样式/颜色 =====
  ['🎨', '[THEME]'],
  ['🟦', '[THEME]'],
  ['🧡', '[THEME]'],

  // ===== 清理/GC/内存 =====
  ['🧹', '[CLEAN]'],
  ['🗺️', '[MAP]'],

  // ===== 替代/兜底路径 =====
  ['📎', '[ALT]'],

  // ===== 网络 =====
  ['🌐', '[NET]'],  ['🌍', '[NET]'],  ['🖧', '[NET]'],

  // ===== 配置/设置 =====
  ['⚙', '[CFG]'],  ['🔧', '[CFG]'],

  // ===== 查找/扫描 =====
  ['🔍', '[FIND]'], ['🔎', '[FIND]'],

  // ===== 主机/进程/机器信息 =====
  ['🖥️', '[HOST]'], ['💻', '[HOST]'],
  ['🕹', '[GAME]'],

  // ===== Toast 通知 =====
  ['🔔', '[TOAST]'],['🔕', '[TOAST]'],

  // ===== 成功/庆祝/情绪类 =====
  ['🎉', '[OK]'],
  ['😢', ''],   // 直接删情绪 emoji
  ['😔', ''],
  ['😶', ''],
  ['🫠', ''],
  ['🩺', ''],

  // ===== JAVA/咖啡/启动 JAR =====
  ['☕', '[JAVA]'],
  ['🎬', '[CMD]'],

  // ===== 日历/时间 =====
  ['📅', '[TIME]'],

  // ===== 其他 =====
  ['❓', '[INFO]'],
  ['➕', '[ADD]'],
  ['➖', '[DEL]'],
  ['🗑', '[TRASH]'],
  ['🎲', '[MISC]'],

  // ===== 注释常见 emoji（清理掉不留 TAG）=====
  ['🎢', ''],
  ['💋', ''],
]


// 按字符串长度倒序（长的先匹配，避免多码位被拆成单码位）
const MAP = RAW_MAP
  .map(([emoji, tag]) => ({ emoji, tag, len: [...emoji].length }))
  .sort((a, b) => b.len - a.len || b.emoji.length - a.emoji.length)

// 构建正则：一次性 replace，效率高（用 Array.from 转码点数组后 join，保证 ZWJ/emoji modifier 完整）
const UNION = MAP.map(({ emoji }) =>
  [...emoji].map(cp => `\\u{${cp.codePointAt(0).toString(16).toUpperCase().padStart(4,'0')}}`).join('')
).join('|')
const RE = new RegExp(UNION, 'gu')
console.log(`[remove-emoji] 规则数: ${MAP.length}, dry-run=${DRY_RUN}`)

// ============= 扫描目录 =============
const TARGET_DIRS = [
  join(ROOT, 'MSMC'),                             // 后端 C# / XAML
  join(ROOT, 'frontend', 'src'),                  // 前端源码（不含 dist/node_modules）
  join(ROOT, 'frontend'),                         // 前端根（index.html / startup.html）
]
const EXCLUDE_DIR = new Set(['bin', 'obj', 'node_modules', 'dist', '.git', '.vs'])
const ALLOW_EXT = new Set(['.cs', '.xaml', '.tsx', '.ts', '.html'])

let totalFiles = 0
let changedFiles = 0
let totalReplaces = 0

function walk(dir) {
  const entries = readdirSync(dir, { withFileTypes: true })
  for (const e of entries) {
    if (e.isDirectory()) {
      if (EXCLUDE_DIR.has(e.name)) continue
      walk(join(dir, e.name))
    } else if (e.isFile()) {
      const ext = e.name.slice(e.name.lastIndexOf('.')).toLowerCase()
      if (!ALLOW_EXT.has(ext)) continue
      handleFile(join(dir, e.name))
    }
  }
}

function handleFile(filePath) {
  totalFiles++
  const src = readFileSync(filePath, 'utf8')
  if (!RE.test(src)) return

  let hits = 0
  const seenEmoji = new Map()
  const out = src.replace(RE, (match) => {
    hits++
    const rule = MAP.find(r => r.emoji === match)
    const tag = rule ? rule.tag : ''
    if (rule) seenEmoji.set(match, (seenEmoji.get(match) || 0) + 1)
    return tag
  })

  if (hits === 0) return

  changedFiles++
  totalReplaces += hits
  const rel = filePath.slice(ROOT.length)
  const details = [...seenEmoji.entries()].slice(0, 5).map(([e, n]) => `${e}x${n}`).join(',')
  console.log(`  [${String(hits).padStart(4)}] ${rel}  ${details}${seenEmoji.size > 5 ? ` ...+${seenEmoji.size - 5}` : ''}`)

  if (!DRY_RUN) writeFileSync(filePath, out, 'utf8')
}

for (const d of TARGET_DIRS) {
  try { statSync(d); walk(d) } catch { /* 忽略不存在目录 */ }
}

console.log('\n================ 汇总 ================')
console.log(`扫描文件: ${totalFiles}`)
console.log(`命中文件: ${changedFiles}`)
console.log(`替换次数: ${totalReplaces}`)
if (DRY_RUN) console.log('DRY-RUN 模式，未写入文件。重新运行不带 dry-run 参数即可落地。')
