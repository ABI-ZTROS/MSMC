// ─────────────────────────────────────────────────────────────────────
// Wizard 相关类型
// ─────────────────────────────────────────────────────────────────────

export type CoreType =
  | 'paper'
  | 'purpur'
  | 'vanilla'
  | 'folia'
  | 'leafmc'
  | 'fabric'
  | 'forge'
  | 'neoforge'
  | 'velocity'
  | 'waterfall'

export type CoreTag = '推荐' | '性能' | '模组' | '代理' | '原版'

export interface CoreMeta {
  key: CoreType
  name: string
  logo: string
  desc: string
  tag: CoreTag
}

export const CORE_CATALOG: CoreMeta[] = [
  { key: 'paper',    name: 'Paper',    logo: '📜', desc: '插件服务器标准，性能+稳定平衡',     tag: '推荐' },
  { key: 'purpur',   name: 'Purpur',   logo: '🌸', desc: 'Paper 的超集，更多可自定义项',       tag: '性能' },
  { key: 'folia',    name: 'Folia',    logo: '⚡', desc: 'Paper 分支，多线程大区服',           tag: '性能' },
  { key: 'vanilla',  name: 'Vanilla',  logo: '⛏️', desc: 'Mojang 原版，无插件支持',             tag: '原版' },
  { key: 'leafmc',   name: 'LeafMC',   logo: '🍃', desc: 'Paper 下游，轻量优化分支',           tag: '性能' },
  { key: 'fabric',   name: 'Fabric',   logo: '🧵', desc: '轻量 Mod 加载器',                     tag: '模组' },
  { key: 'forge',    name: 'Forge',    logo: '🔨', desc: '老牌 Mod 加载器，模组生态最广',       tag: '模组' },
  { key: 'neoforge', name: 'NeoForge', logo: '🛡️', desc: 'Forge 现代分支，更新积极',           tag: '模组' },
  { key: 'velocity', name: 'Velocity', logo: '🚄', desc: '高性能跨服代理',                       tag: '代理' },
  { key: 'waterfall',name: 'Waterfall',logo: '🌊', desc: 'BungeeCord 下游（和 Paper 配套）',     tag: '代理' },
]
