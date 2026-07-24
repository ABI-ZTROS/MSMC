import { useEffect, useState, useRef } from 'react'
import { GaugeRing } from '@/components/ui'
import { bridge } from '@/utils/bridge'

interface SystemMetrics {
  cpuUsagePercent: number
  memoryUsagePercent: number
  diskUsagePercent: number
  totalMemoryBytes: number
  usedMemoryBytes: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskName: string
  totalThreadCount: number
  javaCpuUsagePercent: number
  javaWorkingSetBytes: number
  javaThreadCount: number
  isMonitoring: boolean
  memoryInfoText: string
  diskInfoText: string
}

interface HistoryPoint {
  timestamp: string
  cpuUsagePercent: number
  memoryUsagePercent: number
}

function formatBytes(bytes: number): string {
  if (bytes >= 1 << 30) return `${(bytes / (1 << 30)).toFixed(1)} GB`
  if (bytes >= 1 << 20) return `${(bytes / (1 << 20)).toFixed(1)} MB`
  if (bytes >= 1 << 10) return `${(bytes / (1 << 10)).toFixed(1)} KB`
  return `${bytes} B`
}

function getColor(value: number): 'success' | 'warning' | 'danger' | 'primary' {
  if (value >= 90) return 'danger'
  if (value >= 70) return 'warning'
  if (value >= 30) return 'primary'
  return 'success'
}

// 简单的折线图组件（用 SVG 手绘，避免引入大依赖）
function SimpleLineChart({
  data,
  color,
  height = 160,
  label,
}: {
  data: number[]
  color: string
  height?: number
  label: string
}): JSX.Element {
  const width = 600
  const padding = { top: 10, right: 10, bottom: 20, left: 40 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom

  const maxVal = 100
  const minVal = 0

  const points = data.map((val, i) => {
    const x = padding.left + (i / Math.max(data.length - 1, 1)) * chartWidth
    const y = padding.top + (1 - (val - minVal) / (maxVal - minVal)) * chartHeight
    return { x, y }
  })

  const pathD = points.length > 0
    ? points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')
    : ''

  const areaD = points.length > 0
    ? `${pathD} L ${points[points.length - 1].x.toFixed(1)} ${padding.top + chartHeight} L ${padding.left} ${padding.top + chartHeight} Z`
    : ''

  const yLabels = [0, 25, 50, 75, 100]

  return (
    <div className="w-full">
      <div className="text-sm font-medium text-slate-300 mb-2 px-2">{label}</div>
      <svg width="100%" height={height} viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
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
                stroke="#334155"
                strokeWidth="1"
                strokeDasharray="3,3"
                opacity="0.5"
              />
              <text
                x={padding.left - 8}
                y={y + 4}
                fill="#64748b"
                fontSize="10"
                textAnchor="end"
              >
                {val}%
              </text>
            </g>
          )
        })}

        {/* 面积填充 */}
        {areaD && (
          <path
            d={areaD}
            fill={color}
            opacity="0.15"
          />
        )}

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
    </div>
  )
}

export function SystemMonitorPage(): JSX.Element {
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null)
  const [cpuHistory, setCpuHistory] = useState<number[]>([])
  const [memHistory, setMemHistory] = useState<number[]>([])
  const intervalRef = useRef<number | null>(null)

  // 拉取数据
  const fetchMetrics = async () => {
    try {
      const data = await bridge.invoke<SystemMetrics>('systemMonitor:getMetrics')
      setMetrics(data)
    } catch (e) {
      console.error('获取系统指标失败:', e)
    }
  }

  const fetchHistory = async () => {
    try {
      const data = await bridge.invoke<HistoryPoint[]>('systemMonitor:getHistory')
      setCpuHistory(data.map(d => d.cpuUsagePercent))
      setMemHistory(data.map(d => d.memoryUsagePercent))
    } catch (e) {
      console.error('获取历史数据失败:', e)
    }
  }

  const handleStart = async () => {
    try {
      await bridge.invoke('systemMonitor:start')
    } catch (e) {
      console.error('启动监控失败:', e)
    }
  }

  const handleStop = async () => {
    try {
      await bridge.invoke('systemMonitor:stop')
    } catch (e) {
      console.error('停止监控失败:', e)
    }
  }

  useEffect(() => {
    // 初始拉取
    fetchMetrics()
    fetchHistory()

    // 定时刷新（2 秒一次，和 WPF 版一致）
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

  const cpu = metrics?.cpuUsagePercent ?? 0
  const mem = metrics?.memoryUsagePercent ?? 0
  const disk = metrics?.diskUsagePercent ?? 0
  const threads = metrics?.totalThreadCount ?? 0

  return (
    <div className="p-3 h-full flex flex-col">
      {/* 顶部控制按钮 */}
      <div className="flex items-center gap-2 mb-3">
        <button
          onClick={handleStart}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium rounded-md transition-colors"
        >
          开始监控
        </button>
        <button
          onClick={handleStop}
          className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-slate-200 text-sm font-medium rounded-md border border-slate-600 transition-colors"
        >
          停止监控
        </button>
        {metrics?.isMonitoring && (
          <span className="flex items-center gap-1.5 text-emerald-400 text-sm ml-2">
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            监控中
          </span>
        )}
      </div>

      {/* 圆环仪表盘行 —— 4 列，和 WPF 版一致 */}
      <div className="grid grid-cols-4 gap-2 mb-3">
        {/* CPU */}
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="flex items-center justify-center">
            <GaugeRing
              value={cpu}
              label="CPU"
              color={getColor(cpu)}
              size={120}
              strokeWidth={8}
            />
          </div>
        </div>

        {/* 内存 */}
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="flex items-center justify-center">
            <GaugeRing
              value={mem}
              label="内存"
              sublabel={metrics?.memoryInfoText || ''}
              color={getColor(mem)}
              size={120}
              strokeWidth={8}
            />
          </div>
        </div>

        {/* 磁盘 */}
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="flex items-center justify-center">
            <GaugeRing
              value={disk}
              label="磁盘"
              sublabel={metrics?.diskInfoText || ''}
              color={getColor(disk)}
              size={120}
              strokeWidth={8}
            />
          </div>
        </div>

        {/* 线程数（用大号数字展示，和 WPF 版一致） */}
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="flex flex-col items-center justify-center h-full py-4">
            <div className="text-4xl font-bold text-slate-100 number-animate">
              {threads}
            </div>
            <div className="text-sm text-slate-400 mt-2">系统线程数</div>
            {metrics?.javaThreadCount ? (
              <div className="text-xs text-slate-500 mt-1">
                Java: {metrics.javaThreadCount}
              </div>
            ) : null}
          </div>
        </div>
      </div>

      {/* 折线图表区 */}
      <div className="flex-1 min-h-0 grid grid-cols-2 gap-3">
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3 overflow-hidden">
          <SimpleLineChart
            data={cpuHistory}
            color="#22c55e"
            height={180}
            label="CPU 使用率趋势"
          />
        </div>
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3 overflow-hidden">
          <SimpleLineChart
            data={memHistory}
            color="#3b82f6"
            height={180}
            label="内存使用率趋势"
          />
        </div>
      </div>

      {/* 详细数据区 */}
      <div className="mt-3 grid grid-cols-3 gap-3">
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="text-xs text-slate-500 mb-1">Java CPU 使用率</div>
          <div className="text-xl font-bold text-slate-200">
            {metrics?.javaCpuUsagePercent?.toFixed(1) || '0.0'}%
          </div>
        </div>
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="text-xs text-slate-500 mb-1">Java 内存占用</div>
          <div className="text-xl font-bold text-slate-200">
            {formatBytes(metrics?.javaWorkingSetBytes || 0)}
          </div>
        </div>
        <div className="bg-slate-900 rounded-md border border-slate-700/50 p-3">
          <div className="text-xs text-slate-500 mb-1">磁盘名称</div>
          <div className="text-xl font-bold text-slate-200">
            {metrics?.diskName || '-'}
          </div>
        </div>
      </div>
    </div>
  )
}
