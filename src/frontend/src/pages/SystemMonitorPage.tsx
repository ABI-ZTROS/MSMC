import { useEffect, useRef, useState } from 'react'
import { GaugeRing } from '@/components/ui'
import { bridge, getSystemMetrics, getSystemHistory } from '@/utils/bridge'
import type { SystemMetrics, HistoryPoint } from '@/types/bridge'

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
  data: number[]
  color: string
  height?: number
  label: string
}

// 简单 SVG 折线图：网格 + 面积 + 曲线，颜色全部走 CSS 变量
function SimpleLineChart({ data, color, height = 200, label }: LineChartProps): JSX.Element {
  const width = 600
  const titleHeight = 28
  const padding = { top: 12, right: 12, bottom: 24, left: 36 }
  const chartHeight = height - titleHeight - padding.top - padding.bottom
  const chartWidth = width - padding.left - padding.right
  const yLabels = [0, 25, 50, 75, 100]

  const points = data.map((val, i) => {
    const safeVal = Math.max(0, Math.min(100, val))
    const x = padding.left + (data.length > 1 ? (i / (data.length - 1)) * chartWidth : chartWidth / 2)
    const y = padding.top + (1 - safeVal / 100) * chartHeight
    return { x, y }
  })

  const pathD = points.length > 0
    ? points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')
    : ''

  const areaD = points.length > 0
    ? `${pathD} L ${points[points.length - 1].x.toFixed(1)} ${padding.top + chartHeight} L ${points[0].x.toFixed(1)} ${padding.top + chartHeight} Z`
    : ''

  return (
    <div className="w-full h-full flex flex-col">
      {/* 标题行 */}
      <div className="flex items-center" style={{ gap: 6, marginBottom: 8, height: titleHeight - 8 }}>
        <span style={{ fontSize: 16, color, display: 'inline-flex' }}>📈</span>
        <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>{label}</span>
      </div>

      <div className="flex-1 min-h-0">
        {data.length === 0 ? (
          <div className="md-empty-state" style={{ height: chartHeight + padding.top + padding.bottom }}>
            <div className="md-empty-state-icon">📈</div>
            <div className="md-empty-state-text">暂无趋势数据</div>
          </div>
        ) : (
          <svg
            width="100%"
            height={chartHeight + padding.top + padding.bottom}
            viewBox={`0 0 ${width} ${chartHeight + padding.top + padding.bottom}`}
            preserveAspectRatio="none"
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

            {/* 面积填充 */}
            {areaD && <path d={areaD} fill={color} opacity="0.15" />}

            {/* 折线 */}
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
          </svg>
        )}
      </div>
    </div>
  )
}

export function SystemMonitorPage(): JSX.Element {
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null)
  const [history, setHistory] = useState<HistoryPoint[]>([])
  const [loadError, setLoadError] = useState(false)
  const intervalRef = useRef<number | null>(null)

  // 拉取系统指标
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

  // 拉取历史数据（用于折线图）
  const fetchHistory = async () => {
    try {
      const data = await getSystemHistory()
      setHistory(data)
    } catch (e) {
      console.error('获取历史数据失败:', e)
    }
  }

  const handleStart = async () => {
    try {
      await bridge.invoke('systemMonitor:start')
      await fetchMetrics()
      await fetchHistory()
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

  useEffect(() => {
    // 初始拉取
    fetchMetrics()
    fetchHistory()

    // 每 2 秒自动刷新指标，与 WPF 版一致
    intervalRef.current = window.setInterval(() => {
      fetchMetrics()
      fetchHistory()
    }, 2000)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [])

  // 从历史数据提取 CPU 和内存曲线
  const cpuHistory = history.map(h => h.cpuUsagePercent)
  const memHistory = history.map(h => h.memoryUsagePercent)

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
          style={{ padding: '10px 16px', fontSize: 13 }}
        >
          开始监控
        </button>
        <button
          onClick={handleStop}
          className="md-btn md-btn-outlined"
          style={{ padding: '10px 16px', fontSize: 13 }}
        >
          停止监控
        </button>
        {metrics?.isMonitoring && (
          <span
            className="flex items-center"
            style={{ marginLeft: 8, gap: 6, fontSize: 13, color: 'var(--md-gauge-green)' }}
          >
            <span
              className="md-status-dot md-status-dot-green"
              style={{ animation: 'mdSpin 1.5s linear infinite' }}
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

      {/* ═══ 折线图区域：左右两栏 ═══ */}
      <div className="grid grid-cols-2" style={{ gap: 12 }}>
        <div className="md-card" style={{ padding: 16 }}>
          <SimpleLineChart
            data={cpuHistory}
            color="var(--md-gauge-green)"
            height={228}
            label="CPU 使用率趋势"
          />
        </div>
        <div className="md-card" style={{ padding: 16 }}>
          <SimpleLineChart
            data={memHistory}
            color="var(--md-primary-hue-mid)"
            height={228}
            label="内存使用率趋势"
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
