import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAppStore } from '@/stores/appStore'

export function AppLayout(): JSX.Element {
  const statusMessage = useAppStore((s) => s.statusMessage)
  const isAdmin = useAppStore((s) => s.isAdmin)
  const isReady = useAppStore((s) => s.isReady)

  return (
    <div className="h-full flex flex-col bg-slate-50 dark:bg-slate-950 relative overflow-hidden">
      <div className="absolute inset-0 pointer-events-none overflow-hidden">
        <div className="absolute -top-40 -left-40 w-96 h-96 bg-primary-500/5 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -right-40 w-96 h-96 bg-accent-500/5 rounded-full blur-3xl" />
        <div className="absolute inset-0 bg-grid opacity-[0.03] dark:opacity-[0.02]" />
      </div>

      <div className="relative z-10 flex-1 flex overflow-hidden">
        <Sidebar />

        <main className="flex-1 overflow-y-auto overflow-x-hidden">
          {isReady ? (
            <div className="page-container min-h-full">
              <Outlet />
            </div>
          ) : (
            <div className="h-full flex items-center justify-center">
              <div className="text-center">
                <div className="relative w-16 h-16 mx-auto mb-5">
                  <div className="absolute inset-0 rounded-2xl bg-gradient-to-br from-primary-400 via-primary-500 to-accent-500 animate-pulse-glow" />
                  <div className="absolute inset-1 rounded-xl bg-slate-50 dark:bg-slate-900 flex items-center justify-center">
                    <span className="text-xl font-bold text-gradient-primary">M</span>
                  </div>
                </div>
                <p className="text-slate-500 dark:text-slate-400 text-sm animate-pulse">
                  正在初始化...
                </p>
              </div>
            </div>
          )}
        </main>
      </div>

      <footer className="relative z-10 h-8 glass-strong border-t border-white/20 dark:border-slate-700/40 flex items-center px-5 gap-4 text-xs">
        <div className="flex items-center gap-2.5">
          <span className="status-dot status-dot-success" />
          <span className="text-slate-600 dark:text-slate-300 font-medium">
            {statusMessage}
          </span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-4">
          {isAdmin && (
            <div className="flex items-center gap-1.5 text-primary-600 dark:text-primary-400">
              <span>🛡️</span>
              <span className="font-medium">管理员</span>
            </div>
          )}
          <div className="flex items-center gap-1.5 text-slate-500 dark:text-slate-400">
            <span>🌐</span>
            <span className="font-medium">Web UI</span>
          </div>
        </div>
      </footer>
    </div>
  )
}
