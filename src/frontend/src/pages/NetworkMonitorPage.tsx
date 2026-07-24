import { useState } from 'react'
import { StatCard, ChartPlaceholder, SectionHeader } from '@/components/ui'

interface PortBridge {
  id: string
  name: string
  externalPort: number
  internalPort: number
  protocol: 'TCP' | 'UDP' | 'BOTH'
  status: 'active' | 'inactive' | 'error'
  connections: number
}

const mockBridges: PortBridge[] = [
  {
    id: '1',
    name: '生存服务器',
    externalPort: 25565,
    internalPort: 25565,
    protocol: 'BOTH',
    status: 'active',
    connections: 12,
  },
  {
    id: '2',
    name: '创造服务器',
    externalPort: 25566,
    internalPort: 25565,
    protocol: 'TCP',
    status: 'inactive',
    connections: 0,
  },
  {
    id: '3',
    name: 'Web 管理面板',
    externalPort: 8080,
    internalPort: 8080,
    protocol: 'TCP',
    status: 'active',
    connections: 3,
  },
]

const statusConfig = {
  active: { label: '运行中', class: 'success', dot: 'status-dot-success' },
  inactive: { label: '已停止', class: 'muted', dot: 'status-dot-muted' },
  error: { label: '错误', class: 'danger', dot: 'status-dot-danger' },
}

export function NetworkMonitorPage(): JSX.Element {
  const [bridges] = useState(mockBridges)

  const activeCount = bridges.filter((b) => b.status === 'active').length
  const totalConnections = bridges.reduce((sum, b) => sum + b.connections, 0)

  return (
    <div className="p-6 pb-8">
      {/* Page Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-1">
          网络监控
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          监控网络流量与端口桥接状态
        </p>
      </div>

      {/* Stats Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard
          label="下载速度"
          value="1.2"
          unit="MB/s"
          icon="⬇️"
          color="success"
          trend={12}
          trendLabel="较1小时前"
        />
        <StatCard
          label="上传速度"
          value="0.8"
          unit="MB/s"
          icon="⬆️"
          color="primary"
          trend={5}
          trendLabel="较1小时前"
        />
        <StatCard
          label="活动桥接"
          value={activeCount}
          unit="个"
          icon="🔌"
          color="accent"
        />
        <StatCard
          label="活跃连接"
          value={totalConnections}
          unit="个"
          icon="🔗"
          color="warning"
        />
      </div>

      {/* Traffic Chart */}
      <div className="mb-8">
        <ChartPlaceholder title="网络流量 (24h)" height={220} type="area" />
      </div>

      {/* Port Bridges */}
      <SectionHeader
        title="端口桥接"
        subtitle={`共 ${bridges.length} 条桥接规则`}
        action={
          <button className="btn btn-primary">
            <span>+</span>
            <span>添加桥接</span>
          </button>
        }
      />

      <div className="space-y-3">
        {bridges.map((bridge, index) => {
          const status = statusConfig[bridge.status]
          return (
            <div
              key={bridge.id}
              className="card p-5 card-hover"
              style={{ animationDelay: `${index * 80}ms` }}
            >
              <div className="flex items-center gap-4">
                {/* Icon */}
                <div className="relative w-12 h-12 rounded-2xl bg-gradient-to-br from-primary-50 to-accent-50 dark:from-primary-500/10 dark:to-accent-500/10 flex items-center justify-center text-xl flex-shrink-0">
                  🔌
                  <span
                    className={`absolute -bottom-0.5 -right-0.5 status-dot ${status.dot}`}
                  />
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-semibold text-slate-900 dark:text-white truncate">
                      {bridge.name}
                    </h3>
                    <span className={`badge badge-${status.class}`}>
                      {status.label}
                    </span>
                    <span className="badge badge-muted">{bridge.protocol}</span>
                  </div>
                  <div className="flex items-center gap-4 text-sm text-slate-500 dark:text-slate-400">
                    <span>
                      外部: <span className="font-mono font-medium text-slate-700 dark:text-slate-300">{bridge.externalPort}</span>
                    </span>
                    <span>→</span>
                    <span>
                      内部: <span className="font-mono font-medium text-slate-700 dark:text-slate-300">{bridge.internalPort}</span>
                    </span>
                    <span>·</span>
                    <span>👥 {bridge.connections} 连接</span>
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-2 flex-shrink-0">
                  {bridge.status === 'active' ? (
                    <button className="btn btn-secondary btn-icon" title="停止">
                      ⏹️
                    </button>
                  ) : (
                    <button className="btn btn-success btn-icon" title="启动">
                      ▶️
                    </button>
                  )}
                  <button className="btn btn-ghost btn-icon" title="编辑">
                    ✏️
                  </button>
                </div>
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
