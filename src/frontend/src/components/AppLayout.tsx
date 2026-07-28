import { Outlet, useLocation } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAppStore } from '@/stores/appStore'
import { useEffect, useState, useRef } from 'react'
import { FaShield } from 'react-icons/fa6'
import { ParticleField } from '@/components/ui/ParticleField'

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
      className="h-full flex flex-col overflow-hidden relative"
      style={{ backgroundColor: 'var(--md-paper)', color: 'var(--md-body)' }}
    >
      {/* 环境粒子层：极低密度，仅在应用底层营造"系统在呼吸"的氛围 */}
      <ParticleField
        density={0.35}
        color="var(--md-primary-hue-mid)"
        connect
        connectDistance={140}
        speed={0.18}
        radiusRange={[0.5, 1.4]}
        maxOpacity={0.32}
        style={{ opacity: 0.6 }}
      />

      {/* 顶部主色辉光：营造空间深度 */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          top: -120,
          left: '50%',
          transform: 'translateX(-50%)',
          width: '60%',
          height: 240,
          background:
            'radial-gradient(ellipse at center, var(--md-primary-subtle-background) 0%, transparent 70%)',
          opacity: 0.5,
          pointerEvents: 'none',
        }}
      />

      <div className="flex-1 flex overflow-hidden relative z-10">
        <Sidebar />

        <main className="flex-1 flex flex-col overflow-hidden">
          {isReady ? (
            <div key={pageKey} className="flex-1 overflow-y-auto md-page-enter">
              <Outlet />
            </div>
          ) : (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                {/* 双环品牌加载指示器 */}
                <div style={{ position: 'relative', width: 56, height: 56, margin: '0 auto 16px' }}>
                  <div
                    className="md-orbit"
                    style={{
                      position: 'absolute',
                      inset: 0,
                      borderRadius: '50%',
                      border: '2px solid transparent',
                      borderTopColor: 'var(--md-primary-hue-mid)',
                      borderRightColor: 'var(--md-primary-hue-light)',
                    }}
                  />
                  <div
                    className="md-orbit-reverse"
                    style={{
                      position: 'absolute',
                      inset: 8,
                      borderRadius: '50%',
                      border: '1.5px solid transparent',
                      borderBottomColor: 'var(--md-accent-text)',
                      borderLeftColor: 'var(--md-primary-hue-lighter)',
                    }}
                  />
                  <div
                    className="md-breathe"
                    style={{
                      position: 'absolute',
                      inset: 20,
                      borderRadius: '50%',
                      background: 'var(--md-primary-subtle-background)',
                    }}
                  />
                </div>
                <p style={{ color: 'var(--md-body-light)' }} className="text-sm">
                  正在加载...
                </p>
              </div>
            </div>
          )}
        </main>
      </div>

      <footer
        className="flex items-center px-4 gap-4 flex-shrink-0 relative z-10"
        style={{
          height: 'var(--status-bar-height)',
          backgroundColor: 'var(--md-primary-hue-mid)',
          color: 'white',
          fontSize: 11,
        }}
      >
        <div className="flex items-center gap-2">
          <FaShield size={11} className="md-breathe" style={{ opacity: 0.8 }} />
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
