// -----------------------------------------------------------------------------
// troubleshootingNodes.ts — 二叉树症状导航硬编码常量
// 设计约束: 只改这个文件就能扩展导航树；所有问题 → 关联后端 CheckId（checkReference）
// -----------------------------------------------------------------------------
import type { TreeNode } from '@/types/bridge'

/** 症状入口根节点 — 从左轨进入二叉树对话 */
export const troubleshootingNodes: TreeNode[] = [
  // ═══ 根节点 ═══
  {
    id: 'root',
    category: 'System',
    question: '你的服务器遇到什么问题？',
    options: [
      { label: '启动失败 / 秒崩', next: 'server.boot.fail', severity: 4 },
      { label: '运行中卡顿 / 假死', next: 'server.lag', severity: 3 },
      { label: '无法连接', next: 'server.connection', severity: 3 },
      { label: '物品 / 地图异常', next: 'server.world', severity: 2 },
      { label: '插件问题', next: 'server.plugin', severity: 2 },
      { label: '让 AI 帮我分析', next: 'ai.hint', severity: 2 },
    ],
  },

  // ═══ 启动失败分支 ═══
  {
    id: 'server.boot.fail',
    category: 'System',
    question: '服务器启动后几秒就崩了吗？',
    checkReference: 'log.startup.failure',
    options: [
      { label: '没崩，但卡在 Preparing spawn area', next: 'boot.stuck.spawn', severity: 3 },
      { label: '崩了，有 crash-reports', next: 'boot.crash.with-report', severity: 4 },
      { label: '崩了，但没 crash-reports', next: 'boot.crash.no-report', severity: 4 },
    ],
  },
  {
    id: 'boot.crash.with-report',
    category: 'System',
    question: 'crash-reports 里有没有 OutOfMemoryError？',
    checkReference: 'log.outmemory',
    options: [
      { label: '有 OOM', next: 'fix.java.heap', severity: 4 },
      { label: '有，但是其他 Java 异常', next: 'fix.check.log', severity: 3 },
    ],
  },
  {
    id: 'boot.crash.no-report',
    category: 'System',
    question: 'java 进程是否被安全软件杀掉了？',
    options: [
      { label: '是（360/火绒/Defender）', next: 'fix.disable.av', severity: 2 },
      { label: '不确定', next: 'fix.run.scan', severity: 3 },
    ],
  },
  {
    id: 'boot.stuck.spawn',
    category: 'Map',
    question: 'spawn 区块生成是否卡住？',
    checkReference: 'region.entity.stack',
    options: [
      { label: '是，CPU 100% / 磁盘狂响', next: 'fix.region.regen', severity: 3 },
      { label: '是，但 CPU 正常', next: 'fix.check.java', severity: 2 },
    ],
  },

  // ═══ 卡顿分支 ═══
  {
    id: 'server.lag',
    category: 'System',
    question: '卡顿是什么表现？',
    options: [
      { label: 'TPS < 10，全员橡胶棒', next: 'fix.check.tps', severity: 3 },
      { label: '只有部分区域卡（区块问题）', next: 'fix.region.regen', severity: 3 },
      { label: '登录玩家越多越卡', next: 'fix.check.plugins', severity: 2 },
    ],
  },

  // ═══ 连接分支 ═══
  {
    id: 'server.connection',
    category: 'Network',
    question: '连接问题是什么表现？',
    checkReference: 'port.availability',
    options: [
      { label: 'Connection refused', next: 'fix.check.port', severity: 4 },
      { label: 'Timeout（连接超时）', next: 'fix.check.firewall', severity: 3 },
      { label: '登录后立刻 Kick', next: 'fix.check.plugins', severity: 2 },
    ],
  },

  // ═══ 地图分支 ═══
  {
    id: 'server.world',
    category: 'Map',
    question: '物品/地图异常是什么？',
    options: [
      { label: '物品 damage 异常大', next: 'fix.player.reset.damage', severity: 2 },
      { label: '区块里怪物堆叠', next: 'fix.region.clean.entities', severity: 3 },
      { label: '掉落物满地都是', next: 'fix.region.clean.entities', severity: 3 },
    ],
  },

  // ═══ 插件分支 ═══
  {
    id: 'server.plugin',
    category: 'Plugin',
    question: '插件问题是什么表现？',
    options: [
      { label: '启动时插件报错', next: 'fix.check.log', severity: 2 },
      { label: '装新插件后崩了', next: 'fix.disable.av', severity: 3 },
      { label: '不确定哪个插件', next: 'fix.run.scan', severity: 2 },
    ],
  },

  // ═══ AI 提示分支 ═══
  {
    id: 'ai.hint',
    category: 'System',
    question: 'AI 引导式诊断即将推出（P1）。当前建议：先运行一键体检，结果出来后再点「💬 追问AI」',
    aiHint: '把用户的自然语言描述喂给 AI，AI 判断下一个节点',
    options: [
      { label: '好的，先跑体检', next: 'fix.run.scan', severity: 1 },
    ],
  },

  // ═══ 修复锚点节点（跳到这个就可以直接触发 fixId）═══
  {
    id: 'fix.java.heap',
    category: 'System',
    question: '建议 FixAction: java.switch.version 或增加 -Xmx。要不要执行一键体检让系统生成准确修复方案？',
    options: [
      { label: '好，跑体检', next: 'fix.run.scan', severity: 2 },
      { label: '我自己改', next: 'root', severity: 0 },
    ],
  },
  {
    id: 'fix.check.log',
    category: 'System',
    question: '建议先跑一键体检，让系统扫描日志文件定位具体 ERROR/WARN。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 0 }],
  },
  {
    id: 'fix.disable.av',
    category: 'System',
    question: '建议：白名单 server.jar 所在目录，暂时关闭 Windows Defender 实时保护测试。',
    options: [{ label: '好的', next: 'root', severity: 0 }],
  },
  {
    id: 'fix.run.scan',
    category: 'System',
    question: '好的，返回页面点「🔍 开始一键体检」就能跑快速扫描。',
    options: [{ label: '好的', next: 'root', severity: 0 }],
  },
  {
    id: 'fix.region.regen',
    category: 'Map',
    question: '可以执行 region.clean.entities 清理异常区块，也可以直接删除 .mca 让服务器重新生成。建议先备份！',
    options: [
      { label: '先备份', next: 'backup.world', severity: 2 },
      { label: '直接清区块', next: 'region.clean.entities', severity: 3 },
    ],
  },
  {
    id: 'fix.check.java',
    category: 'Java',
    question: '建议用 MSMC 「Java 管理」切换到匹配的 Java 版本。要先跑体检确认吗？',
    options: [{ label: '跑体检', next: 'fix.run.scan', severity: 0 }],
  },
  {
    id: 'fix.check.port',
    category: 'Network',
    question: '端口被占用。建议 fixId: port.kill.process —— 杀掉占用端口的进程。',
    options: [{ label: '好，跑体检', next: 'fix.run.scan', severity: 2 }],
  },
  {
    id: 'fix.check.firewall',
    category: 'Network',
    question: '建议 fixId: port.firewall（需要管理员权限添加 netsh 入站规则）。先跑体检确认。',
    options: [{ label: '跑体检', next: 'fix.run.scan', severity: 2 }],
  },
  {
    id: 'fix.check.tps',
    category: 'System',
    question: 'TPS 低原因很多（插件/硬件/配置）。建议先跑体检拿到完整问题列表再决策。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 2 }],
  },
  {
    id: 'fix.check.plugins',
    category: 'Plugin',
    question: '建议先 disable 最近新加的插件试试。先跑体检拿到 ERROR/WARN 定位信息。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 2 }],
  },
  {
    id: 'backup.world',
    category: 'System',
    question: '备份是第一步也是最重要的一步。点「🔍 开始一键体检」，结果出来后在修复面板选 backup.world。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 0 }],
  },
  {
    id: 'region.clean.entities',
    category: 'Map',
    question: '这个修复会删除 .mca 让服务器重新生成区块。建议先备份！跑体检 → 选修复。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 0 }],
  },
  {
    id: 'fix.player.reset.damage',
    category: 'Player',
    question: '重置玩家物品 damage 需要读 .dat NBT。先跑体检。',
    options: [{ label: '好的', next: 'fix.run.scan', severity: 0 }],
  },
]

/** nodeMap — 通过 id 快速取节点 */
export const nodeMap: Record<string, TreeNode> = Object.fromEntries(
  troubleshootingNodes.map(n => [n.id, n])
)
