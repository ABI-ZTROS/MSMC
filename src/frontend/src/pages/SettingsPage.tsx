export function SettingsPage(): JSX.Element {
  return (
    <div className="p-6 animate-slide-in-up">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
          设置
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          自定义应用程序设置
        </p>
      </div>

      <div className="space-y-4">
        <div className="card p-5">
          <h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-4">
            外观
          </h3>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-600 dark:text-slate-300">深色模式</span>
              <button className="btn btn-secondary text-xs">切换</button>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-600 dark:text-slate-300">主题色</span>
              <div className="flex gap-2">
                <div className="w-6 h-6 rounded-full bg-blue-500 cursor-pointer ring-2 ring-blue-300" />
                <div className="w-6 h-6 rounded-full bg-purple-500 cursor-pointer" />
                <div className="w-6 h-6 rounded-full bg-pink-500 cursor-pointer" />
                <div className="w-6 h-6 rounded-full bg-green-500 cursor-pointer" />
              </div>
            </div>
          </div>
        </div>

        <div className="card p-5">
          <h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-4">
            动画
          </h3>
          <div className="flex items-center justify-between">
            <span className="text-sm text-slate-600 dark:text-slate-300">启用动画</span>
            <button className="btn btn-secondary text-xs">切换</button>
          </div>
        </div>

        <div className="card p-5">
          <h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-4">
            关于
          </h3>
          <div className="text-sm text-slate-500 dark:text-slate-400">
            <p>MSMC - Minecraft Server Management Console</p>
            <p className="mt-1">版本 0.1.0 (Web UI Preview)</p>
          </div>
        </div>
      </div>
    </div>
  )
}
