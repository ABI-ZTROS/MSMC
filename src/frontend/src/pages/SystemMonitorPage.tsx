import { useEffect, useRef, useState } from 'react'
import { GaugeRing } from '@/components/ui'
import { DualLineChart } from '@/components/ui/DualLineChart'
import { CpuProcessTree } from '@/components/ui/CpuProcessTree'
import {
  bridge,
  getSystemMetrics,
  getSystemHistory,
  getSystemHistoryRange,
  getCpuInfo,
  getProcessAffinities,
  killProcessById,
} from '@/utils/bridge'
import type { SystemMetrics, HistoryPoint, CpuInfo, ProcessAffinityInfo } from '@/types/bridge'

// 字节数转 GB
function bytesToGB(bytes: number): number {
  if (!bytes || bytes <= 0) return 0
  return bytes / (1024 * 1024 * 1024)
}

// 格式化容量明细文本（已用 / 总共 GB）
function formatCapacityInfo(usedBytes: number, totalBytes: number): string {
  const total = bytesToGB(totalBytes)
  const used = bytesToGB(usedBytes)
  if (total <= 0) return ''
  return `${used.toFixed(1)} / ${total.toFixed(1)} GB`
}

interface CpuTopologyProps {
  cpuInfo: CpuInfo | null
  perCoreUsages: number[]
}

function CpuTopology({ cpuInfo, perCoreUsages }: CpuTopologyProps): JSX.Element {
  const [collapsed, setCollapsed] = useState(false)
  const coreCount = cpuInfo?.logicalCores ?? perCoreUsages.length ?? 0
  const columns = Math.min(8, Math.max(4, Math.ceil(Math.sqrt(coreCount))))

  const getCoreColor = (usage: number): string => {
    if (usage < 50) return 'var(--md-gauge-green)'
    if (usage < 80) return 'var(--md-gauge-yellow)'
    return 'var(--md-gauge-red)'
  }

  const toggleCollapse = (): void => setCollapsed(c => !c)

  if (coreCount === 0) {
    return (
      <div className="md-card" style={{ padding: 16 }}>
        <div className="flex items-center" style={{ gap: 8, marginBottom: 12 }}>
          <span style={{ fontSize: 18 }}>🖥️</span>
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 物理拓扑
          </span>
        </div>
        <div className="md-empty-state" style={{ height: 120 }}>
          <div className="md-empty-state-text">正在获取 CPU 信息...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="md-card" style={{ padding: 16, overflow: 'hidden' }}>
      <div
        className="flex items-center justify-between"
        style={{ marginBottom: collapsed ? 0 : 12, cursor: 'pointer', userSelect: 'none' }}
        onClick={toggleCollapse}
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
          <span style={{ fontSize: 18 }}>🖥️</span>
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 物理拓扑
          </span>
          {collapsed && (
            <span style={{ fontSize: 12, color: 'var(--md-body-light)', opacity: 0.8, marginLeft: 8 }}>
              {cpuInfo?.physicalCores} 物理核 / {cpuInfo?.logicalCores} 逻辑核
            </span>
          )}
        </div>
        {!collapsed && (
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', opacity: 0.8 }}>
            {cpuInfo?.physicalCores} 物理核 / {cpuInfo?.logicalCores} 逻辑核
            {cpuInfo?.isHyperThreadingEnabled && ' · 超线程开启'}
          </div>
        )}
      </div>

      <div
        style={{
          maxHeight: collapsed ? 0 : 2000,
          overflow: 'hidden',
          transition: 'max-height 0.3s ease, opacity 0.2s ease',
          opacity: collapsed ? 0 : 1,
        }}
      >
        {cpuInfo?.modelName && (
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 12, opacity: 0.7 }}>
            {cpuInfo.modelName}
          </div>
        )}

        <div
          className="grid"
          style={{
            gridTemplateColumns: `repeat(${columns}, 1fr)`,
            gap: 8,
          }}
        >
          {Array.from({ length: coreCount }).map((_, i) => {
            const usage = perCoreUsages[i] ?? 0
            const physicalCore = cpuInfo?.logicalToPhysicalCoreMap?.[i]
            const color = getCoreColor(usage)

            return (
              <div
                key={i}
                className="md-card"
                title={`逻辑核 ${i}${physicalCore !== undefined ? ` · 物理核 ${physicalCore}` : ''}\n${usage.toFixed(2)}%`}
                style={{
                  padding: 10,
                  textAlign: 'center',
                  cursor: 'default',
                  borderLeft: `3px solid ${color}`,
                  transition: 'transform 0.15s ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)'
                }}
              >
                <div
                  style={{
                    fontSize: 11,
                    color: 'var(--md-body-light)',
                    marginBottom: 4,
                    opacity: 0.7,
                  }}
                >
                  Core {i}
                  {physicalCore !== undefined && (
                    <span style={{ opacity: 0.5 }}> · P{physicalCore}</span>
                  )}
                </div>
                <div
                  style={{
                    fontSize: 18,
                    fontWeight: 700,
                    color,
                    fontVariantNumeric: 'tabular-nums',
                    lineHeight: 1.2,
                  }}
                >
                  {usage.toFixed(1)}%
                </div>
                <div
                  style={{
                    marginTop: 6,
                    height: 4,
                    borderRadius: 2,
                    background: 'var(--md-subtle-border)',
                    overflow: 'hidden',
                  }}
                >
                  <div
                    style={{
                      width: `${Math.min(100, usage)}%`,
                      height: '100%',
                      background: color,
                      transition: 'width 0.3s ease',
                    }}
                  />
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}

// 历史范围选项
const HISTORY_RANGE_OPTIONS = [
  { label: '今天', days: 1 },
  { label: '近 3 天', days: 3 },
  { label: '近 7 天', days: 7 },
  { label: '近 30 天', days: 30 },
] as const

export function SystemMonitorPage(): JSX.Element {
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null)
  const [history, setHistory] = useState<HistoryPoint[]>([])
  const [cpuInfo, setCpuInfo] = useState<CpuInfo | null>(null)
  const [processAffinities, setProcessAffinities] = useState<ProcessAffinityInfo[]>([])
  const [loadError, setLoadError] = useState(false)
  const [historyDays, setHistoryDays] = useState(1)
  const intervalRef = useRef<number | null>(null)

  // 拉取系统指标（仅更新当前快照，不追加到历史数组——历史由持久化数据驱动）
  const fetchMetrics = async () => {
    try {
      const data = await getSystemMetrics()
      setMetrics(data)
      setLoadError(false)
    } catch (e) {
      console.error('获取系统指标失败:', e)
      setLoadError(true)
    }
  }

  // 拉取历史数据（从持久化文件加载）
  const fetchHistory = async (days: number = historyDays) => {
    try {
      if (days <= 1) {
        const data = await getSystemHistory()
        setHistory(data)
      } else {
        const result = await getSystemHistoryRange(days)
        setHistory(result.points)
      }
    } catch (e) {
      console.error('获取历史数据失败:', e)
    }
  }

  // 拉取 CPU 拓扑信息
  const fetchCpuInfo = async () => {
    try {
      const data = await getCpuInfo()
      setCpuInfo(data)
    } catch (e) {
      console.error('获取 CPU 信息失败:', e)
    }
  }

  // 拉取 Java 进程亲和性信息
  const fetchProcessAffinities = async () => {
    try {
      const data = await getProcessAffinities()
      setProcessAffinities(data ?? [])
    } catch (e) {
      console.error('获取进程亲和性信息失败:', e)
    }
  }

  // 终止进程回调：优雅停止 → 3s 超时 → 强杀，成功后刷新列表
  const handleKillProcess = async (pid: number) => {
    try {
      const result = await killProcessById(pid)
      if (result.success) {
        await fetchProcessAffinities()
      } else {
        console.error('杀进程失败:', result.error)
      }
    } catch (e) {
      console.error('杀进程失败:', e)
    }
  }

  const handleStart = async () => {
    try {
      await bridge.invoke('systemMonitor:start')
      await fetchMetrics()
      await fetchHistory()
      await fetchCpuInfo()
      await fetchProcessAffinities()
    } catch (e) {
      console.error('启动监控失败:', e)
    }
  }

  const handleStop = async () => {
    try {
      await bridge.invoke('systemMonitor:stop')
      await fetchMetrics()
    } catch (e) {
      console.error('停止监控失败:', e)
    }
  }

  const handleRangeChange = (days: number) => {
    setHistoryDays(days)
    fetchHistory(days)
  }

  useEffect(() => {
    // 初始拉取
    fetchMetrics()
    fetchHistory()
    fetchCpuInfo()
    fetchProcessAffinities()

    // 每 2 秒自动刷新指标，同时刷新当天历史
    intervalRef.current = window.setInterval(() => {
      fetchMetrics()
      // 仅在"今天"模式下实时追加历史数据
      if (historyDays <= 1) {
        fetchHistory(1)
      }
      // 同步刷新进程亲和性（杀进程后/进程退出后能及时反映）
      fetchProcessAffinities()
    }, 2000)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [historyDays])

  // 将历史数据转换为图表所需格式
  const cpu = metrics?.cpuUsagePercent ?? 0
  const mem = metrics?.memoryUsagePercent ?? 0
  const disk = metrics?.diskUsagePercent ?? 0
  const threads = metrics?.totalThreadCount ?? 0

  return (
    <div className="md-page-enter h-full overflow-auto" style={{ padding: 8 }}>
      {/* ═══ 控制按钮：开始 / 停止监控 ═══ */}
      <div className="flex items-center" style={{ gap: 8, marginBottom: 12 }}>
        <button
          onClick={handleStart}
          className="md-btn md-btn-primary"
          style={{ minHeight: 36, padding: '8px 16px' }}
        >
          开始监控
        </button>
        <button
          onClick={handleStop}
          className="md-btn md-btn-outlined"
          style={{ minHeight: 36, padding: '8px 16px' }}
        >
          停止监控
        </button>
        {metrics?.isMonitoring && (
          <span
            className="flex items-center"
            style={{ marginLeft: 8, gap: 6, fontSize: 13, color: 'var(--md-gauge-green)' }}
          >
            <span
              className="md-status-dot md-status-dot-green md-status-pulse"
            />
            监控中
          </span>
        )}
      </div>

      {/* ═══ 4 列仪表盘卡片（CPU / 内存 / 磁盘 / 线程） ═══ */}
      <div className="grid grid-cols-4" style={{ gap: 8, marginBottom: 12 }}>
        {/* CPU 圆环 */}
        <div
          className="md-card"
          style={{ padding: 8, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
        >
          <GaugeRing value={cpu} label="CPU" size={120} arcThickness={8} />
        </div>

        {/* 内存圆环 + 容量明细 */}
        <div
          className="md-card"
          style={{
            padding: 8,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <GaugeRing value={mem} label="内存" size={120} arcThickness={8} />
          <div
            style={{
              marginTop: 4,
              fontSize: 11,
              opacity: 0.7,
              color: 'var(--md-body-light)',
              textAlign: 'center',
            }}
          >
            {metrics ? formatCapacityInfo(metrics.usedMemoryBytes, metrics.totalMemoryBytes) : ''}
          </div>
        </div>

        {/* 磁盘圆环 + 容量明细 */}
        <div
          className="md-card"
          style={{
            padding: 8,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <GaugeRing value={disk} label="磁盘" size={120} arcThickness={8} />
          <div
            style={{
              marginTop: 4,
              fontSize: 11,
              opacity: 0.7,
              color: 'var(--md-body-light)',
              textAlign: 'center',
            }}
          >
            {metrics ? formatCapacityInfo(metrics.diskUsedBytes, metrics.diskTotalBytes) : ''}
          </div>
        </div>

        {/* 线程数：图标 + 大号数字 */}
        <div
          className="md-card"
          style={{
            padding: 8,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <div
            style={{
              fontSize: 32,
              color: 'var(--md-gauge-green)',
              marginBottom: 4,
              marginTop: 8,
            }}
          >
            ⚡
          </div>
          <div
            style={{
              fontSize: 13,
              opacity: 0.7,
              marginBottom: 8,
              color: 'var(--md-body-light)',
            }}
          >
            线程
          </div>
          <div
            style={{
              fontSize: 40,
              fontWeight: 700,
              color: 'var(--md-gauge-green)',
              fontVariantNumeric: 'tabular-nums',
              lineHeight: 1,
            }}
          >
            {threads}
          </div>
        </div>
      </div>

      {/* ═══ 历史范围选择 ═══ */}
      <div className="flex items-center" style={{ gap: 6, marginBottom: 8 }}>
        <span style={{ fontSize: 12, color: 'var(--md-body-light)', opacity: 0.7, marginRight: 4 }}>📅</span>
        {HISTORY_RANGE_OPTIONS.map(opt => (
          <button
            key={opt.days}
            onClick={() => handleRangeChange(opt.days)}
            className="md-btn"
            style={{
              minHeight: 28,
              padding: '4px 12px',
              fontSize: 12,
              background: historyDays === opt.days ? 'var(--md-primary-hue-mid)' : 'var(--md-subtle-border)',
              color: historyDays === opt.days ? '#fff' : 'var(--md-body)',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
            }}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* ═══ 合并折线图：CPU + 内存 ═══ */}
      <div className="md-card" style={{ padding: 16, marginBottom: 12 }}>
        <DualLineChart
          data={history}
          height={280}
          label="使用率趋势"
          gapThresholdSec={30}
        />
      </div>

      {/* ═══ CPU 物理拓扑 ═══ */}
      <div style={{ marginBottom: 12 }}>
        <CpuTopology
          cpuInfo={cpuInfo}
          perCoreUsages={metrics?.perCoreCpuUsages ?? []}
        />
      </div>

      {/* ═══ CPU 核心进程亲和性树（Minecraft 高亮 + 杀进程） ═══ */}
      <div style={{ marginBottom: 12 }}>
        <CpuProcessTree
          cpuInfo={cpuInfo}
          perCoreUsages={metrics?.perCoreCpuUsages ?? []}
          processAffinities={processAffinities}
          onKillProcess={handleKillProcess}
        />
      </div>

      {/* ═══ 空状态：完全无数据时显示 ═══ */}
      {!metrics && !loadError && (
        <div className="md-empty-state">
          <div className="md-empty-state-icon">📊</div>
          <div className="md-empty-state-text">正在加载监控数据...</div>
        </div>
      )}
      {loadError && !metrics && (
        <div className="md-empty-state">
          <div className="md-empty-state-icon">⚠</div>
          <div className="md-empty-state-text">无法获取监控数据，请检查桥接连接</div>
        </div>
      )}
    </div>
  )
}
