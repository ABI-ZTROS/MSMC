import { HashRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AppLayout } from '@/components/AppLayout'
import { ToastContainer } from '@/components/ui/Toast'
import { ParticleField } from '@/components/ui/ParticleField'
import { DashboardPage } from '@/pages/DashboardPage'
import { ConfigEditorPage } from '@/pages/ConfigEditorPage'
import { SystemMonitorPage } from '@/pages/SystemMonitorPage'
import { NetworkMonitorPage } from '@/pages/NetworkMonitorPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { useBridgeInit } from '@/hooks/useBridgeInit'
import { useAppStore } from '@/stores/appStore'
import { FaShield } from 'react-icons/fa6'

function App(): JSX.Element {
  useBridgeInit()
  const isReady = useAppStore((s) => s.isReady)

  if (!isReady) {
    return (
      <div
        className="h-full flex items-center justify-center relative overflow-hidden"
        style={{ backgroundColor: 'var(--md-deep-background)' }}
      >
        {/* 品牌加载屏粒子场：较高密度营造仪式感 */}
        <ParticleField
          density={1.2}
          color="var(--md-primary-hue-mid)"
          connect
          connectDistance={130}
          speed={0.3}
          radiusRange={[0.6, 1.8]}
          maxOpacity={0.55}
        />

        {/* 中央辉光 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            width: 400,
            height: 400,
            background:
              'radial-gradient(circle at center, var(--md-primary-subtle-background) 0%, transparent 60%)',
            opacity: 0.6,
            pointerEvents: 'none',
          }}
        />

        <div className="text-center relative z-10">
          {/* 品牌徽标：盾牌 + 双轨道环 */}
          <div style={{ position: 'relative', width: 96, height: 96, margin: '0 auto 28px' }}>
            {/* 外环 */}
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
            {/* 内环 */}
            <div
              className="md-orbit-reverse"
              style={{
                position: 'absolute',
                inset: 10,
                borderRadius: '50%',
                border: '1.5px solid transparent',
                borderBottomColor: 'var(--md-accent-text)',
                borderLeftColor: 'var(--md-primary-hue-lighter)',
              }}
            />
            {/* 中央盾牌 */}
            <div
              className="md-brand-pulse"
              style={{
                position: 'absolute',
                inset: 28,
                borderRadius: '50%',
                background: 'var(--md-primary-subtle-background)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                border: '1px solid var(--md-primary-subtle-border)',
              }}
            >
              <FaShield
                size={22}
                style={{ color: 'var(--md-primary-hue-light)' }}
              />
            </div>
          </div>

          {/* 品牌名 */}
          <div
            className="text-gradient"
            style={{
              fontSize: 32,
              fontWeight: 800,
              letterSpacing: '0.08em',
              marginBottom: 6,
            }}
          >
            MSMC
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              letterSpacing: '0.15em',
              textTransform: 'uppercase',
            }}
          >
            Minecraft Server Guard
          </div>

          {/* 加载进度条 */}
          <div
            style={{
              marginTop: 28,
              width: 180,
              height: 2,
              margin: '28px auto 0',
              background: 'var(--md-card-hover)',
              borderRadius: 1,
              overflow: 'hidden',
            }}
          >
            <div
              className="md-flow"
              style={{
                width: '40%',
                height: '100%',
                background:
                  'linear-gradient(90deg, transparent, var(--md-primary-hue-mid), transparent)',
                borderRadius: 1,
              }}
            />
          </div>
          <p
            className="md-breathe"
            style={{
              fontSize: 11,
              color: 'var(--md-body-lighter)',
              marginTop: 12,
            }}
          >
            正在初始化系统...
          </p>
        </div>
      </div>
    )
  }

  return (
    <>
      <HashRouter>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/config" element={<ConfigEditorPage />} />
            <Route path="/system" element={<SystemMonitorPage />} />
            <Route path="/network" element={<NetworkMonitorPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </HashRouter>
      <ToastContainer />
    </>
  )
}

export default App
