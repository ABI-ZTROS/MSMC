import { useState, useEffect, useRef, useLayoutEffect } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'

interface LogEntry {
  id: number
  message: string
  type: 'default' | 'success' | 'error'
  /** 日志输出的毫秒时间戳，用于显示时分秒 */
  timestamp: number
}

let logIdCounter = 0

export function StartupPage(): JSX.Element {
  const [progress, setProgress] = useState(0)
  const [currentStatus, setCurrentStatus] = useState('正在初始化...')
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isFailed, setIsFailed] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [version, setVersion] = useState('v1.0.0')
  const [primaryColor, setPrimaryColor] = useState('#3B82F6')
  const logContainerRef = useRef<HTMLDivElement>(null)
  const bridgeReadyRef = useRef(false)
  /** 用户手动滚动后禁用自动滚到底部，只有拉回最底部才恢复 */
  const autoScrollRef = useRef(true)
  /** 用于 setLogs 之外追加日志时保证时间戳一致 */
  const appendLog = (message: string, type: LogEntry['type'] = 'default'): void => {
    setLogs((prev) => [
      ...prev,
      { id: ++logIdCounter, message, type, timestamp: Date.now() },
    ])
  }

  // 格式化日志时间戳 → HH:mm:ss
  const formatTime = (ts: number): string => {
    const d = new Date(ts)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const ss = String(d.getSeconds()).padStart(2, '0')
    return `${hh}:${mm}:${ss}`
  }

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
              const payload = data.payload as { percent: number; status?: string }
              if (payload) {
                setProgress(Math.max(0, Math.min(100, payload.percent)))
                // ✅ 修复问题 2：之前完全忽略了 status 字段，现在同步展示
                if (typeof payload.status === 'string' && payload.status.length > 0) {
                  setCurrentStatus(payload.status)
                }
              }
              break
            }
            case 'startup:log': {
              const payload = data.payload as { message: string; isError?: boolean; isSuccess?: boolean }
              if (payload) {
                const entryType: LogEntry['type'] = payload.isError
                  ? 'error'
                  : payload.isSuccess
                    ? 'success'
                    : 'default'
                appendLog(payload.message, entryType)
              }
              break
            }
            case 'startup:completed': {
              setIsCompleted(true)
              setProgress(100)
              setCurrentStatus('初始化完成')
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '✅ 初始化完成，正在启动主界面...'
              appendLog(msg, 'success')
              break
            }
            case 'startup:failed': {
              setIsFailed(true)
              setProgress(100)
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '启动失败'
              setCurrentStatus(`启动失败：${msg}`)
              appendLog(`❌ 启动失败：${msg}`, 'error')
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

  // ── 自动滚到底部（修复问题 1：原来用 useEffect + scrollTop，受布局时机影响，
  //    现在改用 useLayoutEffect 保证在 commit 后下一帧之前 DOM 高度已确定；
  //    并且只有当用户没有手动向上滚动时才执行 autoscroll）─────────
  useLayoutEffect(() => {
    const el = logContainerRef.current
    if (!el) return
    if (!autoScrollRef.current) return
    // 用 requestAnimationFrame 确保布局引擎已经把新内容合并到 scrollHeight
    requestAnimationFrame(() => {
      const el2 = logContainerRef.current
      if (!el2) return
      // 距离底部 < 24px 视为仍在看最新日志，才允许自动滚
      if (el2.scrollHeight - el2.clientHeight - el2.scrollTop < 24) {
        el2.scrollTo({ top: el2.scrollHeight, behavior: 'smooth' })
      }
    })
  }, [logs])

  // 用户手动滚动：如果滚离了底部就暂停自动滚；滚回底部就恢复
  useEffect(() => {
    const el = logContainerRef.current
    if (!el) return
    const onScroll = (): void => {
      const distanceFromBottom = el.scrollHeight - el.clientHeight - el.scrollTop
      autoScrollRef.current = distanceFromBottom < 24
    }
    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [])

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
            // ✅ 修复问题 3：给整个日志卡片加上阴影，让它和启动页背景有明显边界
            boxShadow: '0 4px 24px rgba(0,0,0,0.25), inset 0 1px 0 rgba(255,255,255,0.03)',
          }}
        >
          {/* 标题栏 */}
          <div
            className="flex items-center justify-between px-3 py-2"
            style={{
              backgroundColor: 'var(--md-deep-background)',
              // 标题栏和下方日志内容之间加一条细分割线，有边界感
              borderBottom: '1px solid var(--md-subtle-border)',
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
              {/* ✅ 修复问题 2：进度条上方展示当前阶段的加载细节（status 字段） */}
              <span
                style={{
                  fontSize: 10,
                  color: 'var(--md-body-lighter)',
                  padding: '2px 6px',
                  backgroundColor: 'var(--md-card-background)',
                  borderRadius: 4,
                  maxWidth: 220,
                  overflow: 'hidden',
                  whiteSpace: 'nowrap',
                  textOverflow: 'ellipsis',
                  border: '1px solid var(--md-subtle-border)',
                  opacity: 0.85,
                }}
                title={currentStatus}
              >
                {currentStatus}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <span
                style={{
                  fontSize: 10,
                  color: 'var(--md-body-lighter)',
                  opacity: 0.7,
                }}
              >
                {logs.length} 条
              </span>
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
          </div>

          {/* 进度条 */}
          <div
            className="w-full"
            style={{
              height: 3,
              backgroundColor: 'var(--md-deep-background)',
              borderBottom: '1px solid var(--md-subtle-border)',
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

          {/* 日志滚动区 */}
          <div
            ref={logContainerRef}
            className="flex-1 overflow-y-auto"
            style={{
              // ✅ 修复问题 3：给日志区加内边距 + 上下 padding 不对称，让它看起来像终端
              padding: '12px 14px 16px 14px',
              // 内边界：深色填充，和顶部标题栏视觉上分隔
              backgroundColor: 'rgba(0, 0, 0, 0.22)',
              scrollbarWidth: 'thin',
              // 明确最大高度（父 flex-1 会撑开，但加上 min/max-height 让滚动条在任何分辨率下都有）
              minHeight: 200,
              maxHeight: '100%',
            }}
          >
            {logs.length === 0 && (
              <div
                style={{
                  fontSize: 11,
                  color: 'var(--md-body-lighter)',
                  opacity: 0.5,
                  textAlign: 'center',
                  padding: '24px 0',
                }}
              >
                等待启动日志...
              </div>
            )}
            {logs.map((entry) => (
              <div
                key={entry.id}
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'flex-start',
                  fontFamily: 'Consolas, "JetBrains Mono", "Cascadia Code", monospace',
                  fontSize: 12,
                  lineHeight: 1.75,
                  // ✅ 修复问题 3：每条日志一个轻微背景色卡片 + 底边距，不再是散在的纯文本
                  marginBottom: 3,
                  padding: '2px 6px',
                  borderRadius: 3,
                  backgroundColor: entry.type === 'error'
                    ? 'rgba(239, 68, 68, 0.06)'
                    : entry.type === 'success'
                      ? 'rgba(34, 197, 94, 0.05)'
                      : 'transparent',
                  color:
                    entry.type === 'error'
                      ? 'var(--md-error-text)'
                      : entry.type === 'success'
                        ? 'var(--md-gauge-green)'
                        : 'var(--md-body)',
                  wordBreak: 'break-word',
                  whiteSpace: 'pre-wrap',
                }}
              >
                {/* 左侧时间戳：淡色 + 等宽，对齐整齐 */}
                <span
                  style={{
                    flexShrink: 0,
                    fontSize: 10,
                    color: 'var(--md-body-lighter)',
                    opacity: 0.55,
                    userSelect: 'none',
                  }}
                >
                  [{formatTime(entry.timestamp)}]
                </span>
                <span style={{ flex: 1 }}>{entry.message}</span>
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
