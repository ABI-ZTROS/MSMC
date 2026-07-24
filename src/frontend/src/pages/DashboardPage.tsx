import { StatCard, SectionHeader } from '@/components/ui'

interface ServerItem {
  id: string
  name: string
  version: string
  status: 'running' | 'stopped' | 'starting' | 'error'
  players: number
  maxPlayers: number
  cpu: number
  memory: number
  uptime: string
}

const mockServers: ServerItem[] = [
  {
    id: '1',
    name: '生存服务器',
    version: '1.21.1',
    status: 'running',
    players: 12,
    maxPlayers: 30,
    cpu: 35,
    memory: 62,
    uptime: '3天 12小时',
  },
  {
    id: '2',
    name: '创造服务器',
    version: '1.21.1',
    status: 'stopped',
    players: 0,
    maxPlayers: 20,
    cpu: 0,
    memory: 0,
    uptime: '—',
  },
  {
    id: '3',
    name: '模组服务器',
    version: 'Forge 1.20.1',
    status: 'starting',
    players: 0,
    maxPlayers: 15,
    cpu: 45,
    memory: 28,
    uptime: '启动中...',
  },
]

const statusConfig = {
  running: { label: '运行中', class: 'success', dot: 'status-dot-success' },
  stopped: { label: '已停止', class: 'muted', dot: 'status-dot-muted' },
  starting: { label: '启动中', class: 'warning', dot: 'status-dot-warning' },
  error: { label: '错误', class: 'danger', dot: 'status-dot-danger' },
}

export function DashboardPage(): JSX.Element {
  const runningCount = mockServers.filter((s) => s.status === 'running').length
  const totalPlayers = mockServers.reduce((sum, s) => sum + s.players, 0)

  return (
    <div className="p-6 pb-8">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-1">
          服务器管理
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          管理和监控你的 Minecraft 服务器
        </p>
      </div>

      {/* Stats Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard
          label="服务器总数"
          value={mockServers.length}
          unit="台"
          icon="🖥️"
          color="primary"
          trend={12}
          trendLabel="较上周"
        />
        <StatCard
          label="运行中"
          value={runningCount}
          unit="台"
          icon="🟢"
          color="success"
        />
        <StatCard
          label="在线玩家"
          value={totalPlayers}
          unit="人"
          icon="👥"
          color="accent"
          trend={8}
          trendLabel="较1小时前"
        />
        <StatCard
          label="平均 CPU"
          value="27"
          unit="%"
          icon="⚡"
          color="warning"
          trend={-5}
          trendLabel="较1小时前"
        />
      </div>

      {/* Server List */}
      <SectionHeader
        title="服务器列表"
        subtitle={`共 ${mockServers.length} 台服务器`}
        action={
          <button className="btn btn-primary">
            <span>+</span>
            <span>添加服务器</span>
          </button>
        }
      />

      <div className="space-y-3">
        {mockServers.map((server, index) => {
          const status = statusConfig[server.status]
          return (
            <div
              key={server.id}
              className="card p-5 card-hover"
              style={{ animationDelay: `${index * 80}ms` }}
            >
              <div className="flex items-center gap-4">
                {/* Server Icon */}
                <div className="relative w-14 h-14 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-200 dark:from-slate-700 dark:to-slate-800 flex items-center justify-center text-2xl flex-shrink-0 shadow-inner">
                  <span>🎮</span>
                  <span
                    className={`absolute -bottom-1 -right-1 status-dot ${status.dot}`}
                  />
                </div>

                {/* Server Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-semibold text-slate-900 dark:text-white truncate">
                      {server.name}
                    </h3>
                    <span className={`badge badge-${status.class}`}>
                      {status.label}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 text-sm text-slate-500 dark:text-slate-400">
                    <span>{server.version}</span>
                    <span>·</span>
                    <span>👥 {server.players}/{server.maxPlayers}</span>
                    <span>·</span>
                    <span>⏱️ {server.uptime}</span>
                  </div>
                </div>

                {/* Resource Bars */}
                <div className="hidden md:flex items-center gap-6 flex-shrink-0">
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-slate-500 dark:text-slate-400 w-8">CPU</span>
                    <div className="w-24">
                      <div className="progress-bar h-1.5">
                        <div
                          className={`progress-fill ${
                            server.cpu > 80
                              ? 'progress-danger'
                              : server.cpu > 60
                              ? 'progress-warning'
                              : 'progress-primary'
                          }`}
                          style={{ width: `${server.cpu}%` }}
                        />
                      </div>
                    </div>
                    <span className="text-xs font-medium text-slate-600 dark:text-slate-300 w-10 text-right number-animate">
                      {server.cpu}%
                    </span>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-slate-500 dark:text-slate-400 w-8">内存</span>
                    <div className="w-24">
                      <div className="progress-bar h-1.5">
                        <div
                          className={`progress-fill ${
                            server.memory > 80
                              ? 'progress-danger'
                              : server.memory > 60
                              ? 'progress-warning'
                              : 'progress-success'
                          }`}
                          style={{ width: `${server.memory}%` }}
                        />
                      </div>
                    </div>
                    <span className="text-xs font-medium text-slate-600 dark:text-slate-300 w-10 text-right number-animate">
                      {server.memory}%
                    </span>
                  </div>
                </div>

                {/* Action Buttons */}
                <div className="flex items-center gap-2 flex-shrink-0">
                  {server.status === 'running' ? (
                    <>
                      <button className="btn btn-secondary btn-icon" title="重启">
                        🔄
                      </button>
                      <button className="btn btn-danger btn-icon" title="停止">
                        ⏹️
                      </button>
                    </>
                  ) : (
                    <button className="btn btn-success btn-icon" title="启动">
                      ▶️
                    </button>
                  )}
                  <button className="btn btn-ghost btn-icon" title="设置">
                    ⚙️
                  </button>
                </div>
              </div>
            </div>
          )
        })}
      </div>

      {/* Quick Actions */}
      <div className="mt-8">
        <SectionHeader title="快捷操作" />
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {[
            { icon: '📂', label: '导入服务器', desc: '从文件夹导入' },
            { icon: '⬇️', label: '下载服务端', desc: '官方/模组端' },
            { icon: '🔌', label: '端口桥接', desc: '外网访问' },
            { icon: '📊', label: '性能分析', desc: '查看报告' },
          ].map((action, i) => (
            <button
              key={i}
              className="card p-4 text-left card-hover group"
            >
              <div className="text-2xl mb-2 group-hover:scale-110 transition-transform duration-300">
                {action.icon}
              </div>
              <div className="text-sm font-semibold text-slate-800 dark:text-slate-200">
                {action.label}
              </div>
              <div className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                {action.desc}
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
