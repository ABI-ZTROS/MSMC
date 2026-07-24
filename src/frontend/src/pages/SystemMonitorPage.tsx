import { useEffect, useState } from 'react'
import { GaugeRing, ChartPlaceholder } from '@/components/ui'

interface SystemMetrics {
  cpu: number
  memory: number
  disk: number
  gpu: number
  cpuTemp: number
  gpuTemp: number
  networkIn: number
  networkOut: number
  totalMemory: number
  usedMemory: number
  totalDisk: number
  usedDisk: number
}

function getRandomVariation(base: number, range: number): number {
  return Math.max(0, Math.min(100, base + (Math.random() - 0.5) * range))
}

export function SystemMonitorPage(): JSX.Element {
  const [metrics, setMetrics] = useState<SystemMetrics>({
    cpu: 32,
    memory: 58,
    disk: 45,
    gpu: 28,
    cpuTemp: 52,
    gpuTemp: 48,
    networkIn: 1.2,
    networkOut: 0.8,
    totalMemory: 32,
    usedMemory: 18.6,
    totalDisk: 1024,
    usedDisk: 460.8,
  })

  useEffect(() => {
    const interval = setInterval(() => {
      setMetrics((prev) => ({
        ...prev,
        cpu: getRandomVariation(prev.cpu, 8),
        memory: getRandomVariation(prev.memory, 3),
        gpu: getRandomVariation(prev.gpu, 10),
        cpuTemp: getRandomVariation(prev.cpuTemp, 4),
        gpuTemp: getRandomVariation(prev.gpuTemp, 3),
        networkIn: Math.max(0, prev.networkIn + (Math.random() - 0.5) * 0.5),
        networkOut: Math.max(0, prev.networkOut + (Math.random() - 0.5) * 0.3),
      }))
    }, 1500)

    return () => clearInterval(interval)
  }, [])

  const getColor = (value: number): 'success' | 'warning' | 'danger' | 'primary' => {
    if (value >= 90) return 'danger'
    if (value >= 70) return 'warning'
    if (value >= 30) return 'primary'
    return 'success'
  }

  return (
    <div className="p-6 pb-8">
      {/* Page Header */}
      <div className="mb-6">
        <div className="flex items-center gap-3 mb-1">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
            系统监控
          </h1>
          <span className="badge badge-success flex items-center gap-1">
            <span className="status-dot status-dot-success" />
            实时
          </span>
        </div>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          实时监控系统资源使用情况
        </p>
      </div>

      {/* Gauge Rings Row */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <div className="card p-6 flex flex-col items-center">
          <GaugeRing
            value={metrics.cpu}
            label="CPU"
            sublabel={`${metrics.cpuTemp.toFixed(0)}°C`}
            color={getColor(metrics.cpu)}
            size={140}
            strokeWidth={9}
          />
        </div>
        <div className="card p-6 flex flex-col items-center">
          <GaugeRing
            value={metrics.memory}
            label="内存"
            sublabel={`${metrics.usedMemory.toFixed(1)}/${metrics.totalMemory} GB`}
            color={getColor(metrics.memory)}
            size={140}
            strokeWidth={9}
          />
        </div>
        <div className="card p-6 flex flex-col items-center">
          <GaugeRing
            value={metrics.gpu}
            label="GPU"
            sublabel={`${metrics.gpuTemp.toFixed(0)}°C`}
            color={getColor(metrics.gpu)}
            size={140}
            strokeWidth={9}
          />
        </div>
        <div className="card p-6 flex flex-col items-center">
          <GaugeRing
            value={metrics.disk}
            label="磁盘"
            sublabel={`${metrics.usedDisk.toFixed(0)}/${metrics.totalDisk} GB`}
            color={getColor(metrics.disk)}
            size={140}
            strokeWidth={9}
          />
        </div>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 mb-8">
        <ChartPlaceholder title="CPU 使用率 (24h)" height={200} type="area" />
        <ChartPlaceholder title="内存使用率 (24h)" height={200} type="area" />
      </div>

      {/* Network Stats + Details */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Network Card */}
        <div className="card p-5">
          <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-4">
            网络速度
          </h3>
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-success-50 dark:bg-success-500/10 flex items-center justify-center text-lg">
                  ↓
                </div>
                <div>
                  <div className="text-xs text-slate-500 dark:text-slate-400">下载</div>
                  <div className="text-lg font-bold text-success-600 dark:text-success-400 number-animate">
                    {metrics.networkIn.toFixed(1)}
                    <span className="text-xs font-medium ml-0.5">MB/s</span>
                  </div>
                </div>
              </div>
            </div>
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-primary-50 dark:bg-primary-500/10 flex items-center justify-center text-lg">
                  ↑
                </div>
                <div>
                  <div className="text-xs text-slate-500 dark:text-slate-400">上传</div>
                  <div className="text-lg font-bold text-primary-600 dark:text-primary-400 number-animate">
                    {metrics.networkOut.toFixed(1)}
                    <span className="text-xs font-medium ml-0.5">MB/s</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Process List */}
        <div className="card p-5 lg:col-span-2">
          <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-4">
            进程资源占用 Top 5
          </h3>
          <div className="space-y-3">
            {[
              { name: 'java.exe (Minecraft Server)', cpu: 28.5, mem: 12.4, color: 'primary' },
              { name: 'chrome.exe', cpu: 5.2, mem: 3.1, color: 'success' },
              { name: 'Code.exe', cpu: 3.8, mem: 2.8, color: 'success' },
              { name: 'System', cpu: 1.5, mem: 0.9, color: 'muted' },
              { name: 'explorer.exe', cpu: 0.8, mem: 0.6, color: 'muted' },
            ].map((process, i) => (
              <div key={i} className="flex items-center gap-3">
                <span className="w-5 text-xs font-medium text-slate-400 dark:text-slate-500 text-right">
                  {i + 1}
                </span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">
                      {process.name}
                    </span>
                    <span className="text-xs font-medium text-slate-500 dark:text-slate-400 flex-shrink-0 ml-2">
                      {process.cpu.toFixed(1)}% CPU · {process.mem.toFixed(1)}% MEM
                    </span>
                  </div>
                  <div className="progress-bar h-1">
                    <div
                      className={`progress-fill progress-${process.color}`}
                      style={{ width: `${Math.min(process.cpu, 100)}%` }}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
