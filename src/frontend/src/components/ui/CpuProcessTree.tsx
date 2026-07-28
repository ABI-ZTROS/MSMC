import { useState } from 'react'
import type { CpuInfo, ProcessAffinityInfo } from '@/types/bridge'

interface CpuProcessTreeProps {
  cpuInfo: CpuInfo | null
  perCoreUsages: number[]
  processAffinities: ProcessAffinityInfo[]
  onKillProcess: (pid: number) => Promise<void>
}

// 字节数转 MB
function bytesToMB(bytes: number): number {
  if (!bytes || bytes <= 0) return 0
  return bytes / (1024 * 1024)
}

export function CpuProcessTree({
  cpuInfo,
  perCoreUsages,
  processAffinities,
  onKillProcess,
}: CpuProcessTreeProps): JSX.Element {
  const [collapsed, setCollapsed] = useState(false)
  const [expandedProcess, setExpandedProcess] = useState<number | null>(null)
  const [killing, setKilling] = useState<number | null>(null)

  const logicalCores = cpuInfo?.logicalCores ?? perCoreUsages.length ?? 0
  const physicalCores = cpuInfo?.physicalCores ?? 0
  const coreMap = cpuInfo?.logicalToPhysicalCoreMap ?? []

  // 构建 Minecraft 进程占用的核心集合
  const minecraftCores = new Set<number>()
  const minecraftProcesses: ProcessAffinityInfo[] = []
  for (const proc of processAffinities) {
    if (proc.isMinecraftServer) {
      minecraftProcesses.push(proc)
      for (const coreIdx of proc.allowedCoreIndices) {
        minecraftCores.add(coreIdx)
      }
    }
  }

  // 按物理核分组逻辑核
  const physicalGroups = new Map<number, number[]>()
  for (let i = 0; i < logicalCores; i++) {
    const physical = coreMap[i] ?? Math.floor(i / 2)
    if (!physicalGroups.has(physical)) {
      physicalGroups.set(physical, [])
    }
    physicalGroups.get(physical)!.push(i)
  }

  const getCoreColor = (usage: number): string => {
    if (usage < 50) return 'var(--md-gauge-green)'
    if (usage < 80) return 'var(--md-gauge-yellow)'
    return 'var(--md-gauge-red)'
  }

  const handleKill = async (pid: number) => {
    setKilling(pid)
    try {
      await onKillProcess(pid)
      setExpandedProcess(null)
    } finally {
      setKilling(null)
    }
  }

  if (logicalCores === 0) {
    return (
      <div className="md-card" style={{ padding: 16 }}>
        <div className="flex items-center" style={{ gap: 8, marginBottom: 12 }}>
          <span style={{ fontSize: 18 }}>🌲</span>
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 核心进程亲和性树
          </span>
        </div>
        <div className="md-empty-state" style={{ height: 80 }}>
          <div className="md-empty-state-text">正在获取 CPU 信息...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="md-card" style={{ padding: 16, overflow: 'hidden' }}>
      {/* 标题栏 */}
      <div
        className="flex items-center justify-between"
        style={{ marginBottom: collapsed ? 0 : 12, cursor: 'pointer', userSelect: 'none' }}
        onClick={() => setCollapsed(c => !c)}
      >
        <div className="flex items-center" style={{ gap: 8 }}>
          <span
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              transition: 'transform 0.25s ease',
              transform: collapsed ? 'rotate(-90deg)' : 'rotate(0deg)',
              display: 'inline-block',
              width: 12,
            }}
          >
            ▼
          </span>
          <span style={{ fontSize: 18 }}>🌲</span>
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 核心进程亲和性树
          </span>
          {minecraftProcesses.length > 0 && (
            <span
              style={{
                fontSize: 11,
                fontWeight: 600,
                color: '#fff',
                background: 'var(--md-gauge-red)',
                padding: '2px 8px',
                borderRadius: 10,
                marginLeft: 4,
              }}
            >
              Minecraft × {minecraftProcesses.length}
            </span>
          )}
        </div>
        {!collapsed && (
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', opacity: 0.8 }}>
            红色边框 = Minecraft 占用核心
          </div>
        )}
      </div>

      {/* 树形内容 */}
      <div
        style={{
          maxHeight: collapsed ? 0 : 4000,
          overflow: 'hidden',
          transition: 'max-height 0.3s ease, opacity 0.2s ease',
          opacity: collapsed ? 0 : 1,
        }}
      >
        {/* CPU 根节点 */}
        <div style={{ marginBottom: 8 }}>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 13,
              fontWeight: 700,
              color: 'var(--md-body)',
              padding: '4px 10px',
              background: 'var(--md-subtle-border)',
              borderRadius: 6,
            }}
          >
            <span>🖥️</span>
            <span>CPU</span>
            <span style={{ fontSize: 11, opacity: 0.7, fontWeight: 400 }}>
              {physicalCores}P / {logicalCores}L
            </span>
          </div>
        </div>

        {/* 物理核 → 逻辑核 树 */}
        <div style={{ marginLeft: 16 }}>
          {Array.from(physicalGroups.entries()).map(([physicalCore, logicalIndices]) => (
            <div key={physicalCore} style={{ marginBottom: 6 }}>
              {/* 物理核节点 */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 4,
                  fontSize: 12,
                  color: 'var(--md-body-light)',
                  padding: '2px 8px',
                  borderLeft: '2px solid var(--md-subtle-border)',
                  marginBottom: 4,
                }}
              >
                <span style={{ fontSize: 10 }}>├─</span>
                <span>物理核 {physicalCore}</span>
              </div>

              {/* 逻辑核节点 */}
              <div style={{ marginLeft: 20, display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {logicalIndices.map((logicalIdx) => {
                  const usage = perCoreUsages[logicalIdx] ?? 0
                  const isMinecraft = minecraftCores.has(logicalIdx)
                  const color = getCoreColor(usage)
                  const borderColor = isMinecraft ? 'var(--md-gauge-red)' : 'transparent'
                  const borderWidth = isMinecraft ? 2 : 0

                  // 查找关联的 Minecraft 进程
                  const relatedProcs = minecraftProcesses.filter(p =>
                    p.allowedCoreIndices.includes(logicalIdx)
                  )

                  return (
                    <div
                      key={logicalIdx}
                      title={`逻辑核 ${logicalIdx} · 物理核 ${physicalCore}\n负载: ${usage.toFixed(2)}%${isMinecraft ? `\nMinecraft: ${relatedProcs.map(p => p.displayName).join(', ')}` : ''}`}
                      style={{
                        padding: '6px 10px',
                        textAlign: 'center',
                        border: `${borderWidth}px solid ${borderColor}`,
                        borderRadius: 6,
                        background: isMinecraft
                          ? 'rgba(239, 68, 68, 0.08)'
                          : 'var(--md-card-bg)',
                        minWidth: 70,
                        transition: 'transform 0.15s ease, box-shadow 0.15s ease',
                        cursor: isMinecraft ? 'pointer' : 'default',
                        boxShadow: isMinecraft ? '0 0 8px rgba(239, 68, 68, 0.2)' : 'none',
                      }}
                      onMouseEnter={(e) => {
                        if (isMinecraft) e.currentTarget.style.transform = 'translateY(-2px)'
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.transform = 'translateY(0)'
                      }}
                      onClick={() => {
                        if (isMinecraft && relatedProcs.length > 0) {
                          setExpandedProcess(
                            expandedProcess === relatedProcs[0].processId
                              ? null
                              : relatedProcs[0].processId
                          )
                        }
                      }}
                    >
                      <div style={{ fontSize: 10, color: 'var(--md-body-light)', opacity: 0.7 }}>
                        L{logicalIdx}
                      </div>
                      <div
                        style={{
                          fontSize: 14,
                          fontWeight: 700,
                          color: isMinecraft ? 'var(--md-gauge-red)' : color,
                          fontVariantNumeric: 'tabular-nums',
                        }}
                      >
                        {usage.toFixed(0)}%
                      </div>
                      {isMinecraft && (
                        <div style={{ fontSize: 9, color: 'var(--md-gauge-red)', fontWeight: 600, marginTop: 2 }}>
                          MC
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            </div>
          ))}
        </div>

        {/* Minecraft 进程详情面板 */}
        {minecraftProcesses.length > 0 && (
          <div style={{ marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--md-subtle-border)' }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--md-body)', marginBottom: 8 }}>
              🎮 Minecraft 服务器进程
            </div>
            {minecraftProcesses.map((proc) => (
              <div key={proc.processId} style={{ marginBottom: 8 }}>
                <div
                  onClick={() =>
                    setExpandedProcess(expandedProcess === proc.processId ? null : proc.processId)
                  }
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '8px 12px',
                    background: 'rgba(239, 68, 68, 0.06)',
                    borderRadius: 6,
                    cursor: 'pointer',
                    border: '1px solid rgba(239, 68, 68, 0.2)',
                  }}
                >
                  <div className="flex items-center" style={{ gap: 8 }}>
                    <span style={{ fontSize: 12 }}>🎮</span>
                    <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                      {proc.displayName}
                    </span>
                  </div>
                  <div className="flex items-center" style={{ gap: 12, fontSize: 11, color: 'var(--md-body-light)' }}>
                    <span>CPU: <strong style={{ color: 'var(--md-gauge-red)' }}>{proc.cpuUsagePercent.toFixed(1)}%</strong></span>
                    <span>内存: {bytesToMB(proc.workingSetBytes).toFixed(0)}MB</span>
                    <span>线程: {proc.threadCount}</span>
                    <span>核心: {proc.allowedCoreIndices.length}</span>
                    <span style={{ fontSize: 10 }}>{expandedProcess === proc.processId ? '▲' : '▼'}</span>
                  </div>
                </div>

                {/* 展开的详情 */}
                {expandedProcess === proc.processId && (
                  <div
                    style={{
                      marginLeft: 12,
                      marginTop: 4,
                      padding: '10px 14px',
                      background: 'var(--md-subtle-border)',
                      borderRadius: 6,
                      fontSize: 12,
                    }}
                  >
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 16px', marginBottom: 8 }}>
                      <div><span style={{ opacity: 0.6 }}>PID:</span> <strong>{proc.processId}</strong></div>
                      <div><span style={{ opacity: 0.6 }}>优先级:</span> <strong>{proc.priorityClass || '未知'}</strong></div>
                      <div><span style={{ opacity: 0.6 }}>进程名:</span> <strong>{proc.processName}</strong></div>
                      <div><span style={{ opacity: 0.6 }}>亲和性掩码:</span> <strong>0x{proc.affinityMask.toString(16).toUpperCase()}</strong></div>
                    </div>
                    {proc.commandLine && (
                      <div style={{ marginBottom: 8, fontSize: 11, opacity: 0.7, wordBreak: 'break-all' }}>
                        <span style={{ opacity: 0.6 }}>路径:</span> {proc.commandLine}
                      </div>
                    )}
                    <div className="flex items-center" style={{ gap: 8, marginTop: 8 }}>
                      <button
                        onClick={(e) => {
                          e.stopPropagation()
                          handleKill(proc.processId)
                        }}
                        disabled={killing === proc.processId}
                        style={{
                          padding: '6px 16px',
                          fontSize: 12,
                          fontWeight: 600,
                          color: '#fff',
                          background: killing === proc.processId
                            ? 'var(--md-subtle-border)'
                            : 'var(--md-gauge-red)',
                          border: 'none',
                          borderRadius: 6,
                          cursor: killing === proc.processId ? 'not-allowed' : 'pointer',
                          opacity: killing === proc.processId ? 0.6 : 1,
                        }}
                      >
                        {killing === proc.processId ? '正在终止...' : '终止进程'}
                      </button>
                      <span style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.6 }}>
                        优雅停止 → 3s 超时 → 强杀
                      </span>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        {/* 无 Java 进程时的提示 */}
        {processAffinities.length === 0 && (
          <div style={{ marginTop: 12, fontSize: 12, color: 'var(--md-body-light)', opacity: 0.6, textAlign: 'center' }}>
            暂无 Java 进程运行
          </div>
        )}
      </div>
    </div>
  )
}
