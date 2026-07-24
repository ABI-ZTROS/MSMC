export function DashboardPage(): JSX.Element {
  return (
    <div className="p-6 animate-slide-in-up">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
          服务器管理
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          管理你的 Minecraft 服务器
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="card p-5">
          <div className="text-3xl mb-2">🎮</div>
          <div className="text-2xl font-bold text-slate-900 dark:text-slate-100">0</div>
          <div className="text-sm text-slate-500 dark:text-slate-400">已导入服务器</div>
        </div>
        <div className="card p-5">
          <div className="text-3xl mb-2">🟢</div>
          <div className="text-2xl font-bold text-green-600 dark:text-green-400">0</div>
          <div className="text-sm text-slate-500 dark:text-slate-400">运行中</div>
        </div>
        <div className="card p-5">
          <div className="text-3xl mb-2">🔴</div>
          <div className="text-2xl font-bold text-red-600 dark:text-red-400">0</div>
          <div className="text-sm text-slate-500 dark:text-slate-400">已停止</div>
        </div>
      </div>

      <div className="card p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
            快速开始
          </h2>
        </div>
        <div className="text-center py-12">
          <div className="text-5xl mb-4">📦</div>
          <p className="text-slate-500 dark:text-slate-400 mb-4">
            还没有导入任何服务器
          </p>
          <button className="btn btn-primary">
            导入服务器
          </button>
        </div>
      </div>
    </div>
  )
}
