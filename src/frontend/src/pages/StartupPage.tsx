import { useState, useEffect, useRef, useLayoutEffect, useCallback } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'
import { ParticleField } from '@/components/ui/ParticleField'

// ─────────────────────────────────────────────────────────────────────────────
// 类型定义
// ─────────────────────────────────────────────────────────────────────────────

interface LogEntry {
  id: number
  message: string
  type: 'default' | 'success' | 'error'
  tag: string
  timestamp: number
}

let logIdCounter = 0

function extractTag(message: string): string {
  const m = message.match(/^\s*\[([A-Z]+)\]/)
  return m ? m[1] : ''
}

const TAG_COLOR: Record<string, string> = {
  BOOT: '#60a5fa',
  BUILD: '#a78bfa',
  LOAD: '#fbbf24',
  OK: '#34d399',
  ERR: '#f87171',
  WARN: '#fb923c',
  TIME: '#22d3ee',
  DETECT: '#60a5fa',
  NET: '#38bdf8',
  SEC: '#f472b6',
  CFG: '#a78bfa',
  METRIC: '#4ade80',
  BASE: '#94a3b8',
  VM: '#c084fc',
}

// ─────────────────────────────────────────────────────────────────────────────
// CSS keyframes — 注入到 <style> 标签，Vite 不会处理内联样式
// ─────────────────────────────────────────────────────────────────────────────

const KEYFRAMES = `
@keyframes cyberScan {
  0% { transform: translateY(-100%); }
  100% { transform: translateY(100vh); }
}
@keyframes cyberGlitch {
  0%, 100% { clip-path: inset(0 0 0 0); transform: translate(0); }
  20% { clip-path: inset(20% 0 30% 0); transform: translate(-2px, 1px); }
  40% { clip-path: inset(50% 0 10% 0); transform: translate(2px, -1px); }
  60% { clip-path: inset(10% 0 60% 0); transform: translate(-1px, 2px); }
  80% { clip-path: inset(70% 0 5% 0); transform: translate(1px, -2px); }
}
@keyframes cyberPulse {
  0%, 100% { opacity: 0.4; transform: scale(1); }
  50% { opacity: 0.8; transform: scale(1.05); }
}
@keyframes cyberRing {
  0% { stroke-dashoffset: 283; }
  100% { stroke-dashoffset: 283; }
}
@keyframes cyberFlicker {
  0%, 100% { opacity: 1; }
  3% { opacity: 0.4; }
  6% { opacity: 1; }
  7% { opacity: 0.6; }
  8% { opacity: 1; }
}
@keyframes cyberBoot {
  0% { opacity: 0; filter: blur(20px); transform: scale(0.8); }
  50% { opacity: 0.5; filter: blur(8px); transform: scale(1.05); }
  100% { opacity: 1; filter: blur(0); transform: scale(1); }
}
@keyframes cyberGridMove {
  0% { background-position: 0 0; }
  100% { background-position: 40px 40px; }
}
@keyframes cyberNeonPulse {
  0%, 100% { box-shadow: 0 0 20px rgba(59,130,246,0.3), 0 0 40px rgba(59,130,246,0.1), inset 0 0 20px rgba(59,130,246,0.05); }
  50% { box-shadow: 0 0 40px rgba(59,130,246,0.5), 0 0 80px rgba(59,130,246,0.2), inset 0 0 30px rgba(59,130,246,0.1); }
}
@keyframes cyberTextGlow {
  0%, 100% { text-shadow: 0 0 10px rgba(96,165,250,0.5), 0 0 20px rgba(96,165,250,0.3); }
  50% { text-shadow: 0 0 20px rgba(96,165,250,0.8), 0 0 40px rgba(96,165,250,0.4); }
}
@keyframes cyberLogEntry {
  from { opacity: 0; transform: translateX(-8px); }
  to { opacity: 1; transform: translateX(0); }
}
`

// ─────────────────────────────────────────────────────────────────────────────
// 主组件
// ─────────────────────────────────────────────────────────────────────────────

export function StartupPage(): JSX.Element {
  const [progress, setProgress] = useState(0)
  const [currentStatus, setCurrentStatus] = useState('正在初始化...')
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isFailed, setIsFailed] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [version, setVersion] = useState('v1.0.0')
  const [primaryColor, setPrimaryColor] = useState('#3B82F6')
  const [bootDone, setBootDone] = useState(false)
  const logContainerRef = useRef<HTMLDivElement>(null)
  const bridgeReadyRef = useRef(false)
  const autoScrollRef = useRef(true)

  const appendLog = useCallback((message: string, type: LogEntry['type'] = 'default'): void => {
    setLogs((prev) => [
      ...prev,
      { id: ++logIdCounter, message, type, tag: extractTag(message), timestamp: Date.now() },
    ])
  }, [])

  const formatTime = (ts: number): string => {
    const d = new Date(ts)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const ss = String(d.getSeconds()).padStart(2, '0')
    return `${hh}:${mm}:${ss}`
  }

  // ── 桥接初始化 ──
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
              const msg = payload?.message || '[OK] 初始化完成，正在启动主界面...'
              appendLog(msg, 'success')
              break
            }
            case 'startup:failed': {
              setIsFailed(true)
              setProgress(100)
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '启动失败'
              setCurrentStatus(`启动失败：${msg}`)
              appendLog(`[ERR] 启动失败：${msg}`, 'error')
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

    // 开机动画完成
    const timer = setTimeout(() => setBootDone(true), 800)
    return () => clearTimeout(timer)
  }, [appendLog])

  // ── 自动滚动 ──
  useLayoutEffect(() => {
    const el = logContainerRef.current
    if (!el || !autoScrollRef.current) return
    requestAnimationFrame(() => {
      const el2 = logContainerRef.current
      if (!el2) return
      if (el2.scrollHeight - el2.clientHeight - el2.scrollTop < 24) {
        el2.scrollTo({ top: el2.scrollHeight, behavior: 'smooth' })
      }
    })
  }, [logs])

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

  const statusColor = isFailed ? '#f87171' : isCompleted ? '#34d399' : primaryColor
  const circumference = 2 * Math.PI * 45
  const strokeDashoffset = circumference - (progress / 100) * circumference

  return (
    <>
      <style>{KEYFRAMES}</style>
      <div
        className="w-full h-full flex flex-col min-h-0 relative overflow-hidden"
        style={{
          backgroundColor: '#020617',
          fontFamily: 'var(--md-font-family)',
          color: 'var(--md-body)',
        }}
      >
        {/* ═══════════════════════════════════════════════════════════════
            背景层：粒子 + 网格 + 扫描线 + 暗角
           ═══════════════════════════════════════════════════════════════ */}

        {/* 粒子场 */}
        <ParticleField
          density={1.2}
          color={primaryColor}
          connect
          connectDistance={100}
          speed={0.3}
          maxOpacity={0.4}
        />

        {/* 网格背景 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            backgroundImage: `
              linear-gradient(rgba(59,130,246,0.04) 1px, transparent 1px),
              linear-gradient(90deg, rgba(59,130,246,0.04) 1px, transparent 1px)
            `,
            backgroundSize: '40px 40px',
            animation: 'cyberGridMove 4s linear infinite',
            pointerEvents: 'none',
          }}
        />

        {/* CRT 扫描线 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            background: 'repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(0,0,0,0.15) 2px, rgba(0,0,0,0.15) 4px)',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
        {/* 移动扫描光带 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            left: 0,
            right: 0,
            height: 120,
            background: `linear-gradient(to bottom, transparent, ${primaryColor}11, transparent)`,
            animation: 'cyberScan 6s linear infinite',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />

        {/* 暗角 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            background: 'radial-gradient(ellipse at center, transparent 30%, rgba(2,6,23,0.6) 80%, rgba(2,6,23,0.95) 100%)',
            pointerEvents: 'none',
            zIndex: 2,
          }}
        />

        {/* ═══════════════════════════════════════════════════════════════
            主内容层：居中聚焦
           ═══════════════════════════════════════════════════════════════ */}
        <div
          className="flex-1 flex flex-col items-center justify-center px-8 relative"
          style={{ zIndex: 10 }}
          onMouseDown={handleWindowDrag}
        >
          {/* ── Logo + 环形进度 ── */}
          <div
            className="relative flex items-center justify-center mb-6"
            style={{
              width: 120,
              height: 120,
              animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out forwards',
            }}
          >
            {/* 外层霓虹脉冲环 */}
            <div
              style={{
                position: 'absolute',
                width: 130,
                height: 130,
                borderRadius: '50%',
                border: `1px solid ${primaryColor}40`,
                animation: 'cyberPulse 2.5s ease-in-out infinite',
              }}
            />
            {/* 第二层脉冲环（延迟） */}
            <div
              style={{
                position: 'absolute',
                width: 110,
                height: 110,
                borderRadius: '50%',
                border: `1px solid ${primaryColor}25`,
                animation: 'cyberPulse 2.5s ease-in-out infinite 0.5s',
              }}
            />

            {/* SVG 环形进度条 */}
            <svg width={120} height={120} style={{ position: 'absolute', transform: 'rotate(-90deg)' }}>
              {/* 背景圆环 */}
              <circle
                cx={60}
                cy={60}
                r={45}
                fill="none"
                stroke={`${primaryColor}15`}
                strokeWidth={2}
              />
              {/* 进度圆环 */}
              <circle
                cx={60}
                cy={60}
                r={45}
                fill="none"
                stroke={statusColor}
                strokeWidth={2.5}
                strokeLinecap="round"
                strokeDasharray={circumference}
                strokeDashoffset={strokeDashoffset}
                style={{
                  transition: 'stroke-dashoffset 400ms cubic-bezier(0.33, 1, 0.68, 1)',
                  filter: `drop-shadow(0 0 6px ${statusColor}80)`,
                }}
              />
            </svg>

            {/* 中心 Logo */}
            <div
              style={{
                width: 56,
                height: 56,
                borderRadius: '50%',
                background: `linear-gradient(135deg, ${primaryColor}, ${primaryColor}cc)`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                animation: 'cyberNeonPulse 2s ease-in-out infinite',
                position: 'relative',
              }}
            >
              <FaIcon kind="ShieldHalvedSolid" size={28} className="text-white" />
            </div>

            {/* 进度百分比 — 右下角小标 */}
            <div
              style={{
                position: 'absolute',
                bottom: -2,
                right: -8,
                fontSize: 10,
                fontFamily: 'Consolas, monospace',
                color: statusColor,
                fontWeight: 700,
                textShadow: `0 0 8px ${statusColor}80`,
                animation: 'cyberFlicker 3s linear infinite',
              }}
            >
              {Math.round(progress)}%
            </div>
          </div>

          {/* ── 标题 ── */}
          <div
            className="text-center mb-1"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.15s both' }}
          >
            <div
              style={{
                fontSize: 32,
                fontWeight: 900,
                letterSpacing: '0.15em',
                color: 'var(--md-body)',
                position: 'relative',
                display: 'inline-block',
                animation: 'cyberTextGlow 3s ease-in-out infinite',
              }}
            >
              MSMC
              {/* Glitch 副本 */}
              <span
                aria-hidden
                style={{
                  position: 'absolute',
                  inset: 0,
                  color: '#f87171',
                  opacity: 0.7,
                  animation: 'cyberGlitch 4s steps(1) infinite',
                  clipPath: 'inset(0 0 0 0)',
                }}
              >
                MSMC
              </span>
            </div>
          </div>

          {/* ── 副标题 + 版本 ── */}
          <div
            className="text-center mb-5"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.3s both' }}
          >
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', letterSpacing: '0.08em' }}>
              MINECRAFT SERVER MANAGEMENT CONSOLE
            </div>
            <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginTop: 4, fontFamily: 'Consolas, monospace' }}>
              {version} · {isFailed ? 'SYSTEM ERROR' : isCompleted ? 'READY' : 'BOOTING'}
            </div>
          </div>

          {/* ── 状态行 ── */}
          <div
            className="mb-4 flex items-center gap-2"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.45s both' }}
          >
            <div
              style={{
                width: 6,
                height: 6,
                borderRadius: '50%',
                backgroundColor: statusColor,
                boxShadow: `0 0 8px ${statusColor}`,
                animation: !isFailed && !isCompleted ? 'cyberPulse 1s ease-in-out infinite' : 'none',
              }}
            />
            <span
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                fontFamily: 'Consolas, monospace',
                maxWidth: 400,
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                textOverflow: 'ellipsis',
              }}
            >
              {currentStatus}
            </span>
          </div>

          {/* ═══════════════════════════════════════════════════════════════
              玻璃卡片日志区
             ═══════════════════════════════════════════════════════════════ */}
          <div
            className="w-full max-w-[520px] flex flex-col overflow-hidden rounded-xl"
            style={{
              animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.6s both',
              height: 180,
              background: 'rgba(15, 23, 42, 0.55)',
              backdropFilter: 'blur(12px)',
              WebkitBackdropFilter: 'blur(12px)',
              border: `1px solid ${primaryColor}25`,
              boxShadow: `0 4px 24px rgba(0,0,0,0.4), 0 0 40px ${primaryColor}08, inset 0 1px 0 rgba(255,255,255,0.05)`,
            }}
          >
            {/* 卡片标题栏 */}
            <div
              className="flex items-center justify-between px-3 py-1.5 flex-shrink-0"
              style={{
                borderBottom: `1px solid ${primaryColor}15`,
                background: 'rgba(2, 6, 23, 0.3)',
              }}
            >
              <div className="flex items-center gap-2">
                <FaIcon kind="TerminalSolid" size={12} style={{ color: primaryColor }} />
                <span style={{ fontSize: 10, fontWeight: 600, color: 'var(--md-body-light)', letterSpacing: '0.05em' }}>
                  SYSTEM LOG
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span style={{ fontSize: 9, color: 'var(--md-body-lighter)', fontFamily: 'Consolas, monospace' }}>
                  {logs.length} entries
                </span>
                <div
                  style={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    backgroundColor: statusColor,
                    boxShadow: `0 0 6px ${statusColor}`,
                    animation: !isFailed && !isCompleted ? 'cyberPulse 1.5s ease-in-out infinite' : 'none',
                  }}
                />
              </div>
            </div>

            {/* 日志列表 */}
            <div
              ref={logContainerRef}
              className="flex-1 overflow-y-auto min-h-0"
              style={{
                padding: '8px 12px',
                scrollbarWidth: 'thin',
              }}
            >
              {logs.length === 0 && (
                <div
                  style={{
                    fontSize: 11,
                    color: 'var(--md-body-lighter)',
                    opacity: 0.4,
                    textAlign: 'center',
                    padding: '20px 0',
                    fontFamily: 'Consolas, monospace',
                  }}
                >
                  awaiting system signals...
                </div>
              )}
              {logs.map((entry) => {
                const tagColor = TAG_COLOR[entry.tag] || 'var(--md-body)'
                const isError = entry.type === 'error'
                const isSuccess = entry.type === 'success'
                const tagMatch = entry.message.match(/^(\s*\[[A-Z]+\])(.*)$/s)
                const tagPart = tagMatch ? tagMatch[1] : ''
                const bodyPart = tagMatch ? tagMatch[2] : entry.message
                return (
                  <div
                    key={entry.id}
                    style={{
                      display: 'flex',
                      gap: 6,
                      alignItems: 'flex-start',
                      fontFamily: 'Consolas, "JetBrains Mono", "Cascadia Code", monospace',
                      fontSize: 11,
                      lineHeight: 1.7,
                      marginBottom: 1,
                      padding: '1px 4px',
                      borderRadius: 2,
                      animation: 'cyberLogEntry 0.2s ease-out',
                      backgroundColor: isError
                        ? 'rgba(239, 68, 68, 0.08)'
                        : isSuccess
                          ? 'rgba(52, 211, 153, 0.06)'
                          : 'transparent',
                      color: isError
                        ? '#f87171'
                        : isSuccess
                          ? '#34d399'
                          : 'var(--md-body)',
                      wordBreak: 'break-word',
                      whiteSpace: 'pre-wrap',
                      borderLeft: tagPart ? `2px solid ${tagColor}` : 'none',
                      paddingLeft: tagPart ? 6 : 4,
                    }}
                  >
                    <span
                      style={{
                        flexShrink: 0,
                        fontSize: 9,
                        color: 'var(--md-body-lighter)',
                        opacity: 0.5,
                        userSelect: 'none',
                      }}
                    >
                      {formatTime(entry.timestamp)}
                    </span>
                    {tagPart && (
                      <span
                        style={{
                          flexShrink: 0,
                          color: tagColor,
                          fontWeight: 700,
                          fontSize: 10,
                          userSelect: 'none',
                        }}
                      >
                        {tagPart}
                      </span>
                    )}
                    <span style={{ flex: 1 }}>{bodyPart}</span>
                  </div>
                )
              })}
            </div>
          </div>

          {/* ── 退出按钮（仅失败时显示） ── */}
          <div className="mt-5" style={{ height: 36 }}>
            {isFailed && (
              <button
                onClick={handleClose}
                className="px-6 py-2 text-white font-semibold rounded cursor-pointer border-none"
                style={{
                  width: 120,
                  height: 36,
                  fontSize: 12,
                  letterSpacing: '0.1em',
                  background: 'linear-gradient(135deg, #dc2626, #991b1b)',
                  boxShadow: '0 0 20px rgba(220,38,38,0.4), inset 0 1px 0 rgba(255,255,255,0.1)',
                  transition: 'all 150ms ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.boxShadow = '0 0 30px rgba(220,38,38,0.6), inset 0 1px 0 rgba(255,255,255,0.15)'
                  e.currentTarget.style.transform = 'scale(1.03)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.boxShadow = '0 0 20px rgba(220,38,38,0.4), inset 0 1px 0 rgba(255,255,255,0.1)'
                  e.currentTarget.style.transform = 'scale(1)'
                }}
              >
                SHUTDOWN
              </button>
            )}
          </div>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            底部水印
           ═══════════════════════════════════════════════════════════════ */}
        <div
          className="text-center pb-3 flex-shrink-0"
          style={{ zIndex: 10, pointerEvents: 'none' }}
        >
          <span
            style={{
              fontSize: 9,
              color: 'var(--md-body-lighter)',
              opacity: 0.3,
              fontFamily: 'Consolas, monospace',
              letterSpacing: '0.15em',
            }}
          >
            io.NET.ZTR_OS · SECURED · UTC+8
          </span>
        </div>
      </div>
    </>
  )
}
