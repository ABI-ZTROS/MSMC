import React from 'react'
import ReactDOM from 'react-dom/client'
import { StartupPage } from './pages/StartupPage'
import './styles/globals.css'
;(window as any).__msmcStartupScriptLoaded = true

function reportToCsharp(level: string, message: string, stack?: string): void {
  try {
    const bridge = (window as any).__msmc_bridge__
    if (bridge && typeof bridge.invoke === 'function') {
      bridge
        .invoke('log:write', {
          level,
          message,
          stack: stack || '',
          url: location.href,
          ua: navigator.userAgent,
        })
        .catch(() => {})
    }
  } catch {}
}

window.addEventListener('error', (e) => {
  const msg =
    (e.message || '未知错误') +
    (e.filename ? ` @ ${e.filename}:${e.lineno || 0}:${e.colno || 0}` : '')
  console.error('[STARTUP-ERR]', msg, e.error)
  reportToCsharp('Error', `[STARTUP-ERR] ${msg}`, e.error?.stack)
})

window.addEventListener('unhandledrejection', (e) => {
  const reason = e.reason
  const msg = (reason && (reason.message || reason.toString())) || '未处理的 Promise 拒绝'
  console.error('[STARTUP-ERR] Unhandled rejection:', reason)
  reportToCsharp('Error', `[STARTUP-ERR] 未处理的 Promise 拒绝: ${msg}`, reason?.stack)
})

const rootEl = document.getElementById('root')
if (rootEl) {
  try {
    ReactDOM.createRoot(rootEl).render(
      <React.StrictMode>
        <StartupPage />
      </React.StrictMode>,
    )
    const bootDiag = document.getElementById('boot-diagnostics')
    if (bootDiag && bootDiag.parentNode) {
      bootDiag.parentNode.removeChild(bootDiag)
    }
  } catch (err) {
    const stack = err instanceof Error ? err.stack : String(err)
    reportToCsharp('Error', `[STARTUP-ERR] React 渲染异常: ${String(err)}`, stack)
    const bootLog = document.getElementById('boot-log')
    if (bootLog) {
      bootLog.textContent += `[FATAL] React 渲染失败: ${String(err)}\n${stack || ''}\n`
    }
  }
}
