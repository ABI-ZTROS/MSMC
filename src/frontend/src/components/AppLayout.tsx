import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAppStore } from '@/stores/appStore'
import { useEffect, useState } from 'react'

export function AppLayout(): JSX.Element {
  const statusMessage = useAppStore((s) => s.statusMessage)
  const isReady = useAppStore((s) => s.isReady)
  const [currentTime, setCurrentTime] = useState('')

  // 时间更新（和 WPF 版一致）
  useEffect(() => {
    const update = () => {
      const now = new Date()
      setCurrentTime(now.toLocaleString('zh-CN', { hour12: false }))
    }
    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [])

  return (
    <div className="h-full flex flex-col bg-slate-950 text-slate-100 overflow-hidden">
      <div className="flex-1 flex overflow-hidden">
        <Sidebar />

        <main className="flex-1 flex flex-col overflow-hidden">
          {isReady ? (
            <div className="flex-1 overflow-y-auto">
              <Outlet />
            </div>
          ) : (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <div className="w-10 h-10 border-3 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
                <p className="text-slate-400 text-sm">正在加载...</p>
              </div>
            </div>
          )}
        </main>
      </div>

      {/* 底部状态栏 —— 复刻 WPF PrimaryDark 风格 */}
      <footer className="h-8 bg-blue-900/40 border-t border-blue-800/30 flex items-center px-4 gap-4 text-xs">
        <div className="flex items-center gap-2">
          <span className="text-blue-400">ℹ️</span>
          <span className="text-slate-300">{statusMessage || '就绪'}</span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 text-emerald-400">
            <span className="text-[10px]">🛡️</span>
            <span className="font-medium text-[11px]">Web UI Mode</span>
          </div>
          <div className="text-slate-400">{currentTime}</div>
        </div>
      </footer>
    </div>
  )
}
