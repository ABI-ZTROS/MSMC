import { useState, useEffect, useRef } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'

interface LogEntry {
  id: number
  message: string
  type: 'default' | 'success' | 'error'
}

let logIdCounter = 0

export function StartupPage(): JSX.Element {
  const [progress, setProgress] = useState(0)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isFailed, setIsFailed] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [version, setVersion] = useState('v1.0.0')
  const [primaryColor, setPrimaryColor] = useState('#3B82F6')
  const logContainerRef = useRef<HTMLDivElement>(null)
  const bridgeReadyRef = useRef(false)

  useEffect(() => {
    function initBridge(): void {
      if (bridgeReadyRef.current) return
      if (!window.chrome?.webview) return

      bridgeReadyRef.current = true

      window.chrome.webview.addEventListener('message', (event) => {
        const data = event.data as {
          type?: string
          action?: string
          payload?: unknown
          id?: string
          success?: boolean
          error?: string
        }

        if (!data || !data.type) return

        const type = String(data.type).toLowerCase()
        const action = data.action || ''

        if (type === 'event') {
          switch (action) {
            case 'startup:progress': {
              const payload = data.payload as { percent: number; status: string }
              if (payload) {
                setProgress(Math.max(0, Math.min(100, payload.percent)))
              }
              break
            }
            case 'startup:log': {
              const payload = data.payload as { message: string; isError?: boolean; isSuccess?: boolean }
              if (payload) {
                const newEntry: LogEntry = {
                  id: ++logIdCounter,
                  message: payload.message,
                  type: payload.isError ? 'error' : payload.isSuccess ? 'success' : 'default',
                }
                setLogs((prev) => [...prev, newEntry])
              }
              break
            }
            case 'startup:completed': {
              setIsCompleted(true)
              setProgress(100)
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '✅ 初始化完成，正在启动主界面...'
              setLogs((prev) => [
                ...prev,
                { id: ++logIdCounter, message: msg, type: 'success' },
              ])
              break
            }
            case 'startup:failed': {
              setIsFailed(true)
              setProgress(100)
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '启动失败'
              setLogs((prev) => [
                ...prev,
                { id: ++logIdCounter, message: `❌ 启动失败：${msg}`, type: 'error' },
              ])
              break
            }
            case 'startup:init': {
              const payload = data.payload as { version?: string; primaryColor?: string }
              if (payload?.version) setVersion(`v${payload.version}`)
              if (payload?.primaryColor) setPrimaryColor(payload.primaryColor)
              break
            }
          }
        }
      })

      if (window.__msmc_bridge__) {
        window.__msmc_bridge__.sendEvent('startup:ready', { ts: Date.now() })
      }
    }

    if (document.readyState === 'complete') {
      initBridge()
    } else {
      window.addEventListener('load', initBridge, { once: true })
    }

    setTimeout(initBridge, 100)
    setTimeout(initBridge, 500)
    setTimeout(initBridge, 1000)
  }, [])

  useEffect(() => {
    if (logContainerRef.current) {
      logContainerRef.current.scrollTop = logContainerRef.current.scrollHeight
    }
  }, [logs])

  const handleClose = (): void => {
    if (isFailed && window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:shutdown', {})
    } else if (window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:close', {})
    }
  }

  const handleWindowDrag = (e: React.MouseEvent<HTMLDivElement>): void => {
    if (e.button !== 0) return
    if (window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:dragMove', {})
    }
  }

  const statusDotColor = isFailed
    ? 'var(--md-danger)'
    : isCompleted
      ? 'var(--md-gauge-green)'
      : 'var(--md-gauge-green)'

  const progressBarColor = isFailed ? 'var(--md-danger)' : 'var(--md-primary)'

  return (
    <div
      className="w-full h-full flex flex-col"
      style={{
        backgroundColor: 'var(--md-deep-background)',
        fontFamily: 'var(--md-font-family)',
        color: 'var(--md-body)',
      }}
    >
      <div
        className="flex-1 flex flex-col p-8 cursor-grab active:cursor-grabbing"
        onMouseDown={handleWindowDrag}
        style={{
          background: 'var(--md-deep-background)',
          border: '1px solid var(--md-subtle-border)',
        }}
      >
        <div className="flex items-start mb-6 mt-4">
          <div className="relative mr-5 flex-shrink-0" style={{ width: 64, height: 64 }}>
            <div
              className="absolute rounded-full animate-pulse"
              style={{
                width: 72,
                height: 72,
                left: -4,
                top: -4,
                backgroundColor: primaryColor,
                opacity: 0.08,
                animationDuration: '1.8s',
              }}
            />
            <div
              className="absolute rounded-full"
              style={{
                width: 66,
                height: 66,
                left: -1,
                top: -1,
                backgroundColor: primaryColor,
                opacity: 0.15,
              }}
            />
            <div
              className="absolute rounded-full flex items-center justify-center"
              style={{
                width: 64,
                height: 64,
                backgroundColor: primaryColor,
              }}
            >
              <FaIcon kind="ShieldHalvedSolid" size={32} className="text-white" />
            </div>
          </div>

          <div className="flex flex-col justify-center pt-1">
            <div
              className="font-bold"
              style={{
                fontSize: 24,
                color: 'var(--md-body)',
              }}
            >
              MSMC
            </div>
            <div
              className="mt-1"
              style={{
                fontSize: 13,
                color: 'var(--md-body-light)',
              }}
            >
              Minecraft 服务器管理控制台
            </div>
            <div
              className="mt-2"
              style={{
                fontSize: 11,
                color: 'var(--md-body-lighter)',
              }}
            >
              {version}
            </div>
          </div>
        </div>

        <div
          className="flex-1 flex flex-col overflow-hidden rounded-lg"
          style={{
            backgroundColor: 'var(--md-card-background)',
            border: '1px solid var(--md-subtle-border)',
          }}
        >
          <div
            className="flex items-center justify-between px-3 py-2"
            style={{
              backgroundColor: 'var(--md-deep-background)',
            }}
          >
            <div className="flex items-center gap-2">
              <FaIcon kind="TerminalSolid" size={14} style={{ color: primaryColor }} />
              <span
                className="font-semibold"
                style={{
                  fontSize: 11,
                  color: 'var(--md-body)',
                }}
              >
                启动日志
              </span>
            </div>
            <div
              className="rounded-full"
              style={{
                width: 8,
                height: 8,
                backgroundColor: statusDotColor,
                animation: !isFailed && !isCompleted ? 'mdStatusPulse 2s var(--md-ease-standard) infinite' : 'none',
              }}
            />
          </div>

          <div
            className="w-full"
            style={{
              height: 3,
              backgroundColor: 'var(--md-deep-background)',
            }}
          >
            <div
              className="h-full rounded-r"
              style={{
                width: `${progress}%`,
                backgroundColor: progressBarColor,
                transition: 'width 400ms cubic-bezier(0.33, 1, 0.68, 1)',
              }}
            />
          </div>

          <div
            ref={logContainerRef}
            className="flex-1 overflow-y-auto p-3"
            style={{
              scrollbarWidth: 'thin',
            }}
          >
            {logs.map((entry) => (
              <div
                key={entry.id}
                style={{
                  fontFamily: 'Consolas, monospace',
                  fontSize: 12,
                  lineHeight: 1.6,
                  color:
                    entry.type === 'error'
                      ? 'var(--md-error-text)'
                      : entry.type === 'success'
                        ? 'var(--md-gauge-green)'
                        : 'var(--md-body)',
                  wordBreak: 'break-all',
                }}
              >
                {entry.message}
              </div>
            ))}
          </div>
        </div>

        <div className="mt-7 flex justify-center">
          {isFailed ? (
            <button
              onClick={handleClose}
              className="px-6 py-2 text-white font-semibold rounded cursor-pointer border-none"
              style={{
                width: 100,
                height: 36,
                fontSize: 12,
                backgroundColor: 'var(--md-danger)',
                transition: 'filter 150ms var(--md-ease-standard)',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.filter = 'brightness(0.9)')}
              onMouseLeave={(e) => (e.currentTarget.style.filter = 'brightness(1)')}
              onMouseDown={(e) => (e.currentTarget.style.transform = 'scale(0.96)')}
              onMouseUp={(e) => (e.currentTarget.style.transform = 'scale(1)')}
            >
              退出
            </button>
          ) : (
            <div style={{ height: 36 }} />
          )}
        </div>
      </div>
    </div>
  )
}
