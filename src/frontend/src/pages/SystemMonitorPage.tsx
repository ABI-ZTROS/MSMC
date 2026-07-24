export function SystemMonitorPage(): JSX.Element {
  return (
    <div className="p-6 animate-slide-in-up">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
          系统监控
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          实时监控系统资源使用情况
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <span className="text-sm font-medium text-slate-600 dark:text-slate-300">CPU</span>
            <span className="text-2xl font-bold text-blue-600 dark:text-blue-400">0%</span>
          </div>
          <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 rounded-full transition-all duration-500"
              style={{ width: '0%' }}
            />
          </div>
        </div>

        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <span className="text-sm font-medium text-slate-600 dark:text-slate-300">内存</span>
            <span className="text-2xl font-bold text-green-600 dark:text-green-400">0%</span>
          </div>
          <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 rounded-full transition-all duration-500"
              style={{ width: '0%' }}
            />
          </div>
        </div>

        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <span className="text-sm font-medium text-slate-600 dark:text-slate-300">磁盘</span>
            <span className="text-2xl font-bold text-purple-600 dark:text-purple-400">0%</span>
          </div>
          <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-purple-500 rounded-full transition-all duration-500"
              style={{ width: '0%' }}
            />
          </div>
        </div>
      </div>

      <div className="card p-6">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-4">
          系统信息
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          图表组件正在开发中...
        </p>
      </div>
    </div>
  )
}
