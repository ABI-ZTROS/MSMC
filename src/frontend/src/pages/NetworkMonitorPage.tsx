export function NetworkMonitorPage(): JSX.Element {
  return (
    <div className="p-6 animate-slide-in-up">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
          网络监控
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          监控网络流量与端口桥接
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <div className="card p-5">
          <div className="text-sm text-slate-500 dark:text-slate-400 mb-1">下载速度</div>
          <div className="text-3xl font-bold text-green-600 dark:text-green-400">
            0 KB/s
          </div>
        </div>
        <div className="card p-5">
          <div className="text-sm text-slate-500 dark:text-slate-400 mb-1">上传速度</div>
          <div className="text-3xl font-bold text-blue-600 dark:text-blue-400">
            0 KB/s
          </div>
        </div>
      </div>

      <div className="card p-6">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-4">
          端口桥接
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          端口桥接功能正在开发中...
        </p>
      </div>
    </div>
  )
}
