import { useEffect, useRef, useState, useCallback, useMemo } from 'react'
import { GaugeRing } from '@/components/ui'
import { bridge, getSystemMetrics, getSystemHistory, getSystemHistoryRange, getCpuInfo } from '@/utils/bridge'
import type { SystemMetrics, HistoryPoint, CpuInfo } from '@/types/bridge'

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

interface LineChartProps {
  data: { timestamp: string; value: number }[]
  color: string
  height?: number
  label: string
  /** 间隙阈值（秒），相邻点时间差超过此值则断开连线 */
  gapThresholdSec?: number
}

// 格式化时间戳为 HH:MM:SS
function formatTime(isoString: string): string {
  try {
    const d = new Date(isoString)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const ss = String(d.getSeconds()).padStart(2, '0')
    return `${hh}:${mm}:${ss}`
  } catch {
    return ''
  }
}

// 简单 SVG 折线图：网格 + 面积 + 曲线（支持间隙断开）+ 交互式 Tooltip
function SimpleLineChart({ data, color, height = 200, label, gapThresholdSec = 30 }: LineChartProps): JSX.Element {
  const width = 600
  const titleHeight = 28
  const padding = { top: 12, right: 12, bottom: 24, left: 36 }
  const chartHeight = height - titleHeight - padding.top - padding.bottom
  const chartWidth = width - padding.left - padding.right
  const yLabels = [0, 25, 50, 75, 100]

  const [hoverIndex, setHoverIndex] = useState<number | null>(null)
  const [tooltipPos, setTooltipPos] = useState({ x: 0, y: 0 })
  const svgRef = useRef<SVGSVGElement>(null)

  const points = useMemo(() => {
    return data.map((item, i) => {
      const safeVal = Math.max(0, Math.min(100, item.value))
      const x = padding.left + (data.length > 1 ? (i / (data.length - 1)) * chartWidth : chartWidth / 2)
      const y = padding.top + (1 - safeVal / 100) * chartHeight
      return { x, y }
    })
  }, [data, chartWidth, chartHeight, padding.left, padding.top])

  // 支持间隙的 path：相邻点时间差 > gapThresholdSec 时断开（MoveTo 而非 LineTo）
  const pathD = useMemo(() => {
    if (points.length === 0) return ''
    const parts: string[] = []
    for (let i = 0; i < points.length; i++) {
      const isGap = i > 0 && (() => {
        const prevTs = new Date(data[i - 1].timestamp).getTime()
        const currTs = new Date(data[i].timestamp).getTime()
        return (currTs - prevTs) / 1000 > gapThresholdSec
      })()
      const cmd = i === 0 || isGap ? 'M' : 'L'
      parts.push(`${cmd} ${points[i].x.toFixed(1)} ${points[i].y.toFixed(1)}`)
    }
    return parts.join(' ')
  }, [points, data, gapThresholdSec])

  // 面积路径：对于间隙，分别绘制每段面积
  const areaSegments = useMemo(() => {
    if (data.length === 0) return []
    // 按间隙分段
    const segments: { startIdx: number; endIdx: number }[] = []
    let segStart = 0
    for (let i = 1; i < data.length; i++) {
      const prevTs = new Date(data[i - 1].timestamp).getTime()
      const currTs = new Date(data[i].timestamp).getTime()
      if ((currTs - prevTs) / 1000 > gapThresholdSec) {
        segments.push({ startIdx: segStart, endIdx: i - 1 })
        segStart = i
      }
    }
    segments.push({ startIdx: segStart, endIdx: data.length - 1 })

    return segments.map(seg => {
      const segPoints = points.slice(seg.startIdx, seg.endIdx + 1)
      if (segPoints.length < 2) return ''
      const linePath = segPoints.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')
      return `${linePath} L ${segPoints[segPoints.length - 1].x.toFixed(1)} ${padding.top + chartHeight} L ${segPoints[0].x.toFixed(1)} ${padding.top + chartHeight} Z`
    })
  }, [points, data, gapThresholdSec, padding.top, chartHeight])

  const handleMouseMove = useCallback(
    (e: React.MouseEvent<SVGSVGElement>) => {
      if (points.length === 0 || !svgRef.current) return
      const rect = svgRef.current.getBoundingClientRect()
      const scaleX = width / rect.width
      const mouseX = (e.clientX - rect.left) * scaleX

      const relativeX = mouseX - padding.left
      if (relativeX < 0 || relativeX > chartWidth) {
        setHoverIndex(null)
        return
      }

      const ratio = Math.max(0, Math.min(1, relativeX / chartWidth))
      const index = Math.round(ratio * (points.length - 1))
      setHoverIndex(index)
      setTooltipPos({ x: e.clientX - rect.left, y: e.clientY - rect.top })
    },
    [points, chartWidth, width]
  )

  const handleMouseLeave = useCallback(() => {
    setHoverIndex(null)
  }, [])

  const hoverPoint = hoverIndex !== null ? points[hoverIndex] : null
  const hoverValue = hoverIndex !== null ? data[hoverIndex].value : 0
  const hoverTime = hoverIndex !== null ? formatTime(data[hoverIndex].timestamp) : ''

  return (
    <div className="w-full h-full flex flex-col" style={{ position: 'relative' }}>
      {/* 标题行 */}
      <div className="flex items-center" style={{ gap: 6, marginBottom: 8, height: titleHeight - 8 }}>
        <span style={{ fontSize: 16, color, display: 'inline-flex' }}>📈</span>
        <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>{label}</span>
      </div>

      <div className="flex-1 min-h-0" style={{ position: 'relative' }}>
        {data.length === 0 ? (
          <div className="md-empty-state" style={{ height: chartHeight + padding.top + padding.bottom }}>
            <div className="md-empty-state-icon">📈</div>
            <div className="md-empty-state-text">暂无趋势数据</div>
          </div>
        ) : (
          <>
            <svg
              ref={svgRef}
              width="100%"
              height={chartHeight + padding.top + padding.bottom}
              viewBox={`0 0 ${width} ${chartHeight + padding.top + padding.bottom}`}
              preserveAspectRatio="none"
              onMouseMove={handleMouseMove}
              onMouseLeave={handleMouseLeave}
              style={{ cursor: 'crosshair' }}
            >
              {/* Y 轴网格线和标签 */}
              {yLabels.map((val) => {
                const y = padding.top + (1 - val / 100) * chartHeight
                return (
                  <g key={val}>
                    <line
                      x1={padding.left}
                      y1={y}
                      x2={width - padding.right}
                      y2={y}
                      stroke="var(--md-subtle-border)"
                      strokeWidth="1"
                      strokeDasharray="3,3"
                      opacity="0.5"
                    />
                    <text
                      x={padding.left - 6}
                      y={y + 3}
                      fill="var(--md-body-lighter)"
                      fontSize="10"
                      textAnchor="end"
                    >
                      {val}%
                    </text>
                  </g>
                )
              })}

              {/* 面积填充（分段绘制，间隙处不填充） */}
              {areaSegments.map((d, i) => d && <path key={i} d={d} fill={color} opacity="0.15" />)}

              {/* 折线（间隙处自动断开） */}
              {pathD && (
                <path
                  d={pathD}
                  fill="none"
                  stroke={color}
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              )}

              {/* 悬停指示线 */}
              {hoverPoint && (
                <line
                  x1={hoverPoint.x}
                  y1={padding.top}
                  x2={hoverPoint.x}
                  y2={padding.top + chartHeight}
                  stroke={color}
                  strokeWidth="1"
                  strokeDasharray="4,3"
                  opacity="0.6"
                />
              )}

              {/* 悬停数据点 */}
              {hoverPoint && (
                <g>
                  <circle
                    cx={hoverPoint.x}
                    cy={hoverPoint.y}
                    r="6"
                    fill={color}
                    opacity="0.25"
                  />
                  <circle
                    cx={hoverPoint.x}
                    cy={hoverPoint.y}
                    r="4"
                    fill="var(--md-card-bg)"
                    stroke={color}
                    strokeWidth="2"
                  />
                </g>
              )}
            </svg>

            {/* Tooltip 浮层 */}
            {hoverPoint && hoverIndex !== null && (
              <div
                style={{
                  position: 'absolute',
                  left: tooltipPos.x + 12,
                  top: tooltipPos.y - 10,
                  transform: 'translateY(-100%)',
                  background: 'var(--md-card-bg)',
                  border: '1px solid var(--md-subtle-border)',
                  borderRadius: 8,
                  padding: '8px 12px',
                  fontSize: 12,
                  color: 'var(--md-body)',
                  boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
                  pointerEvents: 'none',
                  zIndex: 10,
                  whiteSpace: 'nowrap',
                  minWidth: 100,
                }}
              >
                <div style={{ fontWeight: 700, color, fontSize: 16, marginBottom: 4, fontVariantNumeric: 'tabular-nums' }}>
                  {hoverValue.toFixed(2)}%
                </div>
                {hoverTime && (
                  <div style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.7 }}>
                    ⏱ {hoverTime}
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}

interface CpuTopologyProps {
  cpuInfo: CpuInfo | null
  perCoreUsages: number[]
}

function CpuTopology({ cpuInfo, perCoreUsages }: CpuTopologyProps): JSX.Element {
  const coreCount = cpuInfo?.logicalCores ?? perCoreUsages.length ?? 0
  const columns = Math.min(8, Math.max(4, Math.ceil(Math.sqrt(coreCount))))

  const getCoreColor = (usage: number): string => {
    if (usage < 50) return 'var(--md-gauge-green)'
    if (usage < 80) return 'var(--md-gauge-yellow)'
    return 'var(--md-gauge-red)'
  }

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
    <div className="md-card" style={{ padding: 16 }}>
      <div className="flex items-center justify-between" style={{ marginBottom: 12 }}>
        <div className="flex items-center" style={{ gap: 8 }}>
          <span style={{ fontSize: 18 }}>🖥️</span>
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 物理拓扑
          </span>
        </div>
        <div style={{ fontSize: 12, color: 'var(--md-body-light)', opacity: 0.8 }}>
          {cpuInfo?.physicalCores} 物理核 / {cpuInfo?.logicalCores} 逻辑核
          {cpuInfo?.isHyperThreadingEnabled && ' · 超线程开启'}
        </div>
      </div>

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

  const handleStart = async () => {
    try {
      await bridge.invoke('systemMonitor:start')
      await fetchMetrics()
      await fetchHistory()
      await fetchCpuInfo()
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

    // 每 2 秒自动刷新指标，同时刷新当天历史
    intervalRef.current = window.setInterval(() => {
      fetchMetrics()
      // 仅在"今天"模式下实时追加历史数据
      if (historyDays <= 1) {
        fetchHistory(1)
      }
    }, 2000)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [historyDays])

  // 将历史数据转换为图表所需格式
  const cpuChartData = useMemo(() =>
    history.map(h => ({ timestamp: h.timestamp, value: h.cpuUsagePercent })),
    [history])
  const memChartData = useMemo(() =>
    history.map(h => ({ timestamp: h.timestamp, value: h.memoryUsagePercent })),
    [history])

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

      {/* ═══ CPU 物理拓扑 ═══ */}
      <div style={{ marginBottom: 12 }}>
        <CpuTopology
          cpuInfo={cpuInfo}
          perCoreUsages={metrics?.perCoreCpuUsages ?? []}
        />
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

      {/* ═══ 折线图区域：左右两栏 ═══ */}
      <div className="grid grid-cols-2" style={{ gap: 12 }}>
        <div className="md-card" style={{ padding: 16 }}>
          <SimpleLineChart
            data={cpuChartData}
            color="var(--md-gauge-green)"
            height={228}
            label="CPU 使用率趋势"
            gapThresholdSec={30}
          />
        </div>
        <div className="md-card" style={{ padding: 16 }}>
          <SimpleLineChart
            data={memChartData}
            color="var(--md-primary-hue-mid)"
            height={228}
            label="内存使用率趋势"
            gapThresholdSec={30}
          />
        </div>
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
