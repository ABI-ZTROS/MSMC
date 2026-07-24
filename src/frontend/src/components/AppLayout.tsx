import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAppStore } from '@/stores/appStore'

export function AppLayout(): JSX.Element {
  const statusMessage = useAppStore((s) => s.statusMessage)
  const isAdmin = useAppStore((s) => s.isAdmin)

  return (
    <div className="h-full flex flex-col">
      <div className="flex-1 flex overflow-hidden">
        <Sidebar />
        <main className="flex-1 overflow-y-auto bg-slate-50 dark:bg-slate-900 animate-fade-in">
          <Outlet />
        </main>
      </div>

      <footer className="h-8 bg-primary-900 dark:bg-primary-950 text-white text-xs flex items-center px-4 gap-4 border-t border-slate-700">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse-slow" />
          <span className="text-primary-200">{statusMessage}</span>
        </div>
        <div className="flex-1" />
        <div className="flex items-center gap-2 text-primary-300">
          {isAdmin && (
            <>
              <span>🛡️</span>
              <span>管理员模式</span>
            </>
          )}
        </div>
      </footer>
    </div>
  )
}
