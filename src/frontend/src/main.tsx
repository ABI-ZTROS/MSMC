import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import './styles/globals.css'

// 通知诊断脚本：主入口已成功加载
;(window as any).__msmcMainScriptLoaded = true

// 上报错误到 C# 日志（通过桥接 API）
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
        .catch(() => {
          /* 静默失败，避免循环 */
        })
    }
  } catch {
    /* 静默 */
  }
}

// 全局错误捕获：同步运行时错误
window.addEventListener('error', (e) => {
  const msg =
    (e.message || '未知错误') +
    (e.filename ? ` @ ${e.filename}:${e.lineno || 0}:${e.colno || 0}` : '')
  console.error('[FE-ERR]', msg, e.error)
  reportToCsharp('Error', `[FE-ERR] ${msg}`, e.error?.stack)
})

// 全局错误捕获：未处理的 Promise 拒绝
window.addEventListener('unhandledrejection', (e) => {
  const reason = e.reason
  const msg = (reason && (reason.message || reason.toString())) || '未处理的 Promise 拒绝'
  console.error('[FE-ERR] Unhandled rejection:', reason)
  reportToCsharp('Error', `[FE-ERR] 未处理的 Promise 拒绝: ${msg}`, reason?.stack)
})

const rootEl = document.getElementById('root')
if (!rootEl) {
  reportToCsharp('Error', '[FE-ERR] #root 元素未找到，无法挂载 React')
} else {
  try {
    ReactDOM.createRoot(rootEl).render(
      <React.StrictMode>
        <App />
      </React.StrictMode>,
    )
    // ✅ React 挂载成功
    ;(window as any).__msmcReactMounted = true
    // React 挂载成功后，移除启动诊断层（延迟一点让渲染完成，避免闪屏）
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const bootDiag = document.getElementById('boot-diagnostics')
        if (bootDiag && bootDiag.parentNode) {
          bootDiag.parentNode.removeChild(bootDiag)
        }
      })
    })
  } catch (err) {
    const stack = err instanceof Error ? err.stack : String(err)
    reportToCsharp('Error', `[FE-ERR] React 渲染异常: ${String(err)}`, stack)
    // 保留诊断层，并在其中显示错误
    const bootLog = document.getElementById('boot-log')
    if (bootLog) {
      bootLog.textContent += `[FATAL] React 渲染失败: ${String(err)}\n${stack || ''}\n`
    }
  }
}
