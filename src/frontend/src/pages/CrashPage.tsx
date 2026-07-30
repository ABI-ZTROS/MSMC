import { useState, useEffect } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'

/**
 * 灾难性故障信息（从 C# 通过桥接推送）
 */
interface CrashFrame {
  /** 故障发生的方法/类全名，如 App.OnStartup */
  location: string
  /** 文件名:行号 */
  source?: string
  /** 该帧的具体原因/消息 */
  reason: string
}

interface InnerException {
  type: string
  message: string
  stack?: string
}

interface CrashReport {
  /** 顶层异常类型 */
  type: string
  /** 顶层异常消息 */
  message: string
  /** 顶层异常堆栈（完整文本） */
  stack: string
  /** 故障点链：从最外层到最内层 */
  frames: CrashFrame[]
  /** 内部异常链 */
  inner?: InnerException[]
  /** 系统环境 */
  env: {
    os: string
    net: string
    x64: boolean
    cpu: number
    pid: number
    time: string
    version: string
    baseDir: string
  }
  /** 已生成的崩溃转储路径 */
  crashDumpPath?: string
  /** 强制死日志路径 */
  forceLogPath?: string
  /** 当前 Serilog 日志路径 */
  serilogLogPath?: string
}

export function CrashPage(): JSX.Element {
  const [report, setReport] = useState<CrashReport | null>(null)
  const [copied, setCopied] = useState(false)
  const [expandedFrames, setExpandedFrames] = useState<Set<number>>(new Set([0]))

  useEffect(() => {
    function initBridge(): void {
      if (!window.chrome?.webview) return
      window.chrome.webview.addEventListener('message', (event) => {
        const data = event.data as { type?: string; action?: string; payload?: unknown }
        if (!data || data.type !== 'event') return
        if (data.action === 'crash:report') {
          const payload = data.payload as CrashReport
          if (payload) setReport(payload)
        }
      })
      // 通知 C# 已就绪
      if (window.__msmc_bridge__) {
        window.__msmc_bridge__.sendEvent('crash:ready', {})
      }
    }
    if (document.readyState === 'complete') initBridge()
    else window.addEventListener('load', initBridge, { once: true })
    setTimeout(initBridge, 100)
    setTimeout(initBridge, 500)
  }, [])

  const handleCopy = (): void => {
    if (!report) return
    const text = [
      '=== MSMC 灾难性故障报告 ===',
      `时间: ${report.env.time}`,
      `版本: ${report.env.version}`,
      `OS:   ${report.env.os}`,
      `.NET: ${report.env.net}  x64=${report.env.x64}  CPU=${report.env.cpu}  PID=${report.env.pid}`,
      '',
      '--- 顶层异常 ---',
      `Type:    ${report.type}`,
      `Message: ${report.message}`,
      '',
      '--- Stack ---',
      report.stack,
      '',
      '--- 故障点链 ---',
      ...report.frames.map((f, i) => `[${i}] ${f.location}${f.source ? ` (${f.source})` : ''}: ${f.reason}`),
      '',
      '--- 内部异常链 ---',
      ...(report.inner ?? []).map((e, i) => `[${i}] ${e.type}: ${e.message}${e.stack ? '\n' + e.stack : ''}`),
      '',
      '--- 日志文件 ---',
      `Serilog: ${report.serilogLogPath ?? '(未初始化)'}`,
      `ForceLog: ${report.forceLogPath ?? '(未生成)'}`,
      `CrashDump: ${report.crashDumpPath ?? '(未生成)'}`,
    ].join('\n')
    try {
      navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      /* 忽略 */
    }
  }

  const handleExit = (): void => {
    if (window.__msmc_bridge__) window.__msmc_bridge__.sendEvent('crash:exit', {})
  }

  const handleRestart = (): void => {
    if (window.__msmc_bridge__) window.__msmc_bridge__.sendEvent('crash:restart', {})
  }

  const toggleFrame = (idx: number): void => {
    setExpandedFrames((prev) => {
      const next = new Set(prev)
      if (next.has(idx)) next.delete(idx)
      else next.add(idx)
      return next
    })
  }

  if (!report) {
    return (
      <div style={{ padding: 32, fontFamily: 'Consolas, monospace', color: '#94a3b8', fontSize: 12 }}>
        <div style={{ color: '#f87171', fontWeight: 700, marginBottom: 8 }}>⚠ 等待故障报告数据...</div>
        <div>如果停留超过 3 秒，说明桥接通道也已损坏，请直接查看：</div>
        <div style={{ marginTop: 8 }}>logs/crashes/crash-*.log</div>
        <div>logs/force-boot-*.log</div>
      </div>
    )
  }

  return (
    <div
      style={{
        width: '100%',
        height: '100%',
        overflow: 'auto',
        backgroundColor: '#0a0f1e',
        fontFamily: '"Segoe UI", "Microsoft YaHei UI", sans-serif',
        color: '#e2e8f0',
        padding: 24,
        boxSizing: 'border-box',
      }}
    >
      {/* ── 顶部标题区 ── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 16,
          marginBottom: 20,
          paddingBottom: 16,
          borderBottom: '1px solid #334155',
        }}
      >
        <div
          style={{
            width: 48,
            height: 48,
            borderRadius: 8,
            backgroundColor: '#dc2626',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <FaIcon kind="TriangleExclamationSolid" size={24} className="text-white" />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 20, fontWeight: 700, color: '#fca5a5' }}>灾难性故障</div>
          <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 2 }}>
            MSMC 启动或运行过程中发生了不可恢复的错误。下方是详细的故障定位信息。
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            onClick={handleCopy}
            style={{
              padding: '8px 16px',
              fontSize: 12,
              backgroundColor: copied ? '#16a34a' : '#334155',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
            }}
          >
            {copied ? '已复制 ✓' : '复制报告'}
          </button>
          <button
            onClick={handleRestart}
            style={{
              padding: '8px 16px',
              fontSize: 12,
              backgroundColor: '#2563eb',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
            }}
          >
            重启程序
          </button>
          <button
            onClick={handleExit}
            style={{
              padding: '8px 16px',
              fontSize: 12,
              backgroundColor: '#475569',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
            }}
          >
            退出
          </button>
        </div>
      </div>

      {/* ── 顶层异常卡片 ── */}
      <section style={{ marginBottom: 16 }}>
        <SectionHeader icon="BugSolid" title="顶层异常" accent="#f87171" />
        <div
          style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderLeft: '3px solid #f87171',
            borderRadius: 6,
            padding: 14,
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '6px 12px', fontSize: 12 }}>
            <KeyLabel>异常类型</KeyLabel>
            <MonoText color="#fbbf24">{report.type}</MonoText>
            <KeyLabel>异常消息</KeyLabel>
            <MonoText color="#fca5a5">{report.message}</MonoText>
          </div>
        </div>
      </section>

      {/* ── 故障点链 ── */}
      <section style={{ marginBottom: 16 }}>
        <SectionHeader icon="ListOlSolid" title={`故障点链（${report.frames.length} 帧）`} accent="#a78bfa" />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {report.frames.map((f, i) => {
            const expanded = expandedFrames.has(i)
            const isRoot = i === report.frames.length - 1
            return (
              <div
                key={i}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #334155',
                  borderLeft: `3px solid ${isRoot ? '#dc2626' : '#64748b'}`,
                  borderRadius: 6,
                  overflow: 'hidden',
                }}
              >
                <button
                  onClick={() => toggleFrame(i)}
                  style={{
                    width: '100%',
                    padding: '10px 14px',
                    backgroundColor: 'transparent',
                    border: 'none',
                    color: '#e2e8f0',
                    textAlign: 'left',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 10,
                    fontSize: 12,
                  }}
                >
                  <span style={{ color: '#64748b', fontFamily: 'monospace', minWidth: 32 }}>
                    #{String(i).padStart(2, '0')}
                  </span>
                  <span style={{ color: isRoot ? '#f87171' : '#94a3b8', fontWeight: 600, minWidth: 16 }}>
                    {isRoot ? '◉' : '○'}
                  </span>
                  <span style={{ flex: 1, fontFamily: 'Consolas, monospace', color: '#cbd5e1' }}>
                    {f.location}
                  </span>
                  {f.source && (
                    <span style={{ color: '#64748b', fontSize: 11, fontFamily: 'monospace' }}>{f.source}</span>
                  )}
                  <span style={{ color: '#475569' }}>{expanded ? '▲' : '▼'}</span>
                </button>
                {expanded && (
                  <div
                    style={{
                      padding: '8px 14px 12px 56px',
                      borderTop: '1px solid #1e293b',
                      fontSize: 12,
                      color: '#94a3b8',
                      fontFamily: 'Consolas, monospace',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-word',
                    }}
                  >
                    <span style={{ color: '#64748b' }}>原因：</span>
                    <span style={{ color: '#fbbf24' }}>{f.reason}</span>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      </section>

      {/* ── 完整堆栈 ── */}
      <section style={{ marginBottom: 16 }}>
        <SectionHeader icon="CodeSolid" title="完整堆栈" accent="#22d3ee" />
        <div
          style={{
            backgroundColor: '#020617',
            border: '1px solid #334155',
            borderRadius: 6,
            padding: 14,
            fontFamily: 'Consolas, "JetBrains Mono", monospace',
            fontSize: 11,
            lineHeight: 1.7,
            color: '#94a3b8',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            maxHeight: 320,
            overflow: 'auto',
          }}
        >
          {report.stack || '(无堆栈)'}
        </div>
      </section>

      {/* ── 内部异常链 ── */}
      {report.inner && report.inner.length > 0 && (
        <section style={{ marginBottom: 16 }}>
          <SectionHeader icon="LayerGroupSolid" title={`内部异常链（${report.inner.length} 层）`} accent="#fb923c" />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {report.inner.map((e, i) => (
              <div
                key={i}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #334155',
                  borderLeft: '3px solid #fb923c',
                  borderRadius: 6,
                  padding: 12,
                  fontSize: 12,
                }}
              >
                <div style={{ marginBottom: 4 }}>
                  <span style={{ color: '#64748b', fontFamily: 'monospace' }}>[{i}] </span>
                  <span style={{ color: '#fbbf24', fontFamily: 'monospace', fontWeight: 600 }}>{e.type}</span>
                </div>
                <div style={{ color: '#fca5a5', marginBottom: 6 }}>{e.message}</div>
                {e.stack && (
                  <div
                    style={{
                      fontFamily: 'Consolas, monospace',
                      fontSize: 10,
                      color: '#64748b',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-word',
                      maxHeight: 120,
                      overflow: 'auto',
                      backgroundColor: '#020617',
                      padding: 8,
                      borderRadius: 4,
                    }}
                  >
                    {e.stack}
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ── 系统环境 ── */}
      <section style={{ marginBottom: 16 }}>
        <SectionHeader icon="MicrochipSolid" title="系统环境" accent="#4ade80" />
        <div
          style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderRadius: 6,
            padding: 14,
            display: 'grid',
            gridTemplateColumns: 'repeat(2, 1fr)',
            gap: '6px 24px',
            fontSize: 12,
          }}
        >
          <EnvRow label="操作系统" value={report.env.os} />
          <EnvRow label=".NET 运行时" value={report.env.net} />
          <EnvRow label="进程位数" value={report.env.x64 ? 'x64' : 'x86'} />
          <EnvRow label="CPU 核心数" value={String(report.env.cpu)} />
          <EnvRow label="进程 ID" value={String(report.env.pid)} />
          <EnvRow label="故障时间" value={report.env.time} />
          <EnvRow label="程序版本" value={report.env.version} />
          <EnvRow label="工作目录" value={report.env.baseDir} />
        </div>
      </section>

      {/* ── 日志文件 ── */}
      <section style={{ marginBottom: 16 }}>
        <SectionHeader icon="FileLinesSolid" title="日志与转储" accent="#94a3b8" />
        <div
          style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderRadius: 6,
            padding: 14,
            fontSize: 12,
          }}
        >
          <LogRow label="Serilog 日志" path={report.serilogLogPath} />
          <LogRow label="强制死日志" path={report.forceLogPath} />
          <LogRow label="崩溃转储" path={report.crashDumpPath} />
        </div>
        <div style={{ marginTop: 8, fontSize: 11, color: '#64748b' }}>
          把上述文件打包发给开发者，可以加速问题定位。
        </div>
      </section>
    </div>
  )
}

function SectionHeader(props: { icon: string; title: string; accent: string }): JSX.Element {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
      <FaIcon kind={props.icon as any} size={14} style={{ color: props.accent }} />
      <span style={{ fontSize: 13, fontWeight: 600, color: '#e2e8f0' }}>{props.title}</span>
      <div style={{ flex: 1, height: 1, backgroundColor: '#1e293b', marginLeft: 8 }} />
    </div>
  )
}

function KeyLabel(props: { children: React.ReactNode }): JSX.Element {
  return <span style={{ color: '#64748b', fontSize: 11, alignSelf: 'center' }}>{props.children}</span>
}

function MonoText(props: { children: React.ReactNode; color?: string }): JSX.Element {
  return (
    <span
      style={{
        fontFamily: 'Consolas, "JetBrains Mono", monospace',
        color: props.color ?? '#e2e8f0',
        wordBreak: 'break-word',
      }}
    >
      {props.children}
    </span>
  )
}

function EnvRow(props: { label: string; value: string }): JSX.Element {
  return (
    <div style={{ display: 'flex', gap: 8 }}>
      <span style={{ color: '#64748b', minWidth: 80 }}>{props.label}</span>
      <span style={{ color: '#cbd5e1', fontFamily: 'Consolas, monospace', wordBreak: 'break-word' }}>
        {props.value}
      </span>
    </div>
  )
}

function LogRow(props: { label: string; path?: string }): JSX.Element {
  return (
    <div style={{ display: 'flex', gap: 8, padding: '4px 0', borderBottom: '1px dashed #1e293b' }}>
      <span style={{ color: '#64748b', minWidth: 96 }}>{props.label}</span>
      <span
        style={{
          color: props.path ? '#cbd5e1' : '#475569',
          fontFamily: 'Consolas, monospace',
          fontSize: 11,
          wordBreak: 'break-all',
          flex: 1,
        }}
      >
        {props.path || '(未生成)'}
      </span>
    </div>
  )
}
