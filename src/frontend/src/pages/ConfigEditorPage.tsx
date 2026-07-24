export function ConfigEditorPage(): JSX.Element {
  return (
    <div className="p-6 animate-slide-in-up">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
          配置编辑
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          编辑服务器配置文件
        </p>
      </div>

      <div className="card p-6">
        <div className="text-center py-12">
          <div className="text-5xl mb-4">⚙️</div>
          <p className="text-slate-500 dark:text-slate-400 mb-4">
            请先选择一个服务器
          </p>
          <p className="text-sm text-slate-400 dark:text-slate-500">
            配置编辑器正在开发中...
          </p>
        </div>
      </div>
    </div>
  )
}
