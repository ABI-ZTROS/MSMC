import { HashRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AppLayout } from '@/components/AppLayout'
import { ToastContainer } from '@/components/ui/Toast'
import { DashboardPage } from '@/pages/DashboardPage'
import { ConfigEditorPage } from '@/pages/ConfigEditorPage'
import { SystemMonitorPage } from '@/pages/SystemMonitorPage'
import { NetworkMonitorPage } from '@/pages/NetworkMonitorPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { useBridgeInit } from '@/hooks/useBridgeInit'
import { useAppStore } from '@/stores/appStore'

function App(): JSX.Element {
  useBridgeInit()
  const isReady = useAppStore((s) => s.isReady)

  if (!isReady) {
    return (
      <div className="h-full flex items-center justify-center bg-slate-900">
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4" />
          <p className="text-slate-400 text-sm">正在加载...</p>
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
