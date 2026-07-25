import { Outlet, useLocation } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAppStore } from '@/stores/appStore'
import { useEffect, useState, useRef } from 'react'
import { FaShield } from 'react-icons/fa6'

export function AppLayout(): JSX.Element {
  const statusMessage = useAppStore((s) => s.statusMessage)
  const isReady = useAppStore((s) => s.isReady)
  const [currentTime, setCurrentTime] = useState('')
  const location = useLocation()
  const [pageKey, setPageKey] = useState(location.pathname)
  const prevPathRef = useRef(location.pathname)

  useEffect(() => {
    if (location.pathname !== prevPathRef.current) {
      prevPathRef.current = location.pathname
      setPageKey(location.pathname)
    }
  }, [location.pathname])

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
    <div
      className="h-full flex flex-col overflow-hidden"
      style={{ backgroundColor: 'var(--md-paper)', color: 'var(--md-body)' }}
    >
      <div className="flex-1 flex overflow-hidden">
        <Sidebar />

        <main className="flex-1 flex flex-col overflow-hidden">
          {isReady ? (
            <div key={pageKey} className="flex-1 overflow-y-auto md-page-enter">
              <Outlet />
            </div>
          ) : (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <div
                  className="w-10 h-10 border-3 border-t-transparent rounded-full animate-spin mx-auto mb-3"
                  style={{
                    borderColor: 'var(--md-primary-hue-mid)',
                    borderTopColor: 'transparent',
                    borderWidth: 3,
                  }}
                />
                <p style={{ color: 'var(--md-body-light)' }} className="text-sm">
                  正在加载...
                </p>
              </div>
            </div>
          )}
        </main>
      </div>

      <footer
        className="flex items-center px-4 gap-4 flex-shrink-0"
        style={{
          height: 'var(--status-bar-height)',
          backgroundColor: 'var(--md-primary-hue-mid)',
          color: 'white',
          fontSize: 11,
        }}
      >
        <div className="flex items-center gap-2">
          <FaShield size={11} style={{ opacity: 0.8 }} />
          <span style={{ opacity: 0.9 }}>{statusMessage || '就绪'}</span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5" style={{ opacity: 0.85 }}>
            <FaShield size={10} />
            <span className="font-medium text-[11px]">MSMC</span>
          </div>
          <div style={{ opacity: 0.8 }}>{currentTime}</div>
        </div>
      </footer>
    </div>
  )
}
