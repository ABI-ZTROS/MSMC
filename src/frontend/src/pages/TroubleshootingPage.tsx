import { useEffect, useState, useCallback } from 'react'
import { FaWrench } from 'react-icons/fa6'
import type { DiagnosticReport, DiagnosticIssue, DiagnosticAiAnalysis, KnownServerInfo } from '@/types/bridge'
import { runDiagnostic, runDeepScan, checkServerRunning, killServerAndScan, getAiStatus, setApiKey, askAI } from '@/utils/bridge'
import { DiagnosticReportCard } from '@/components/DiagnosticReportCard'
import { FixPanel } from '@/components/FixPanel'
import { TreeNodeDialog } from '@/components/TreeNodeDialog'

const PRIMARY = '#c0392b'
const ACCENT_BG = 'rgba(192,57,43,0.08)'
const ACCENT_BORDER = 'rgba(192,57,43,0.35)'

export function TroubleshootingPage(): JSX.Element {
  const [servers, setServers] = useState<KnownServerInfo[]>([])
  const [selectedServer, setSelectedServer] = useState<KnownServerInfo | null>(null)
  const [report, setReport] = useState<DiagnosticReport | null>(null)
  const [status, setStatus] = useState<'idle' | 'scanning' | 'deep' | 'done' | 'error'>('idle')
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [activeFix, setActiveFix] = useState<string | null>(null)
  const [showSymptomDialog, setShowSymptomDialog] = useState(false)
  const [highlightCheck, setHighlightCheck] = useState<string | null>(null)

  // ── DeepSeek AI 配置与追问状态 ──
  const [aiConfigured, setAiConfigured] = useState(false)
  const [aiKeyInput, setAiKeyInput] = useState('')
  const [aiSaving, setAiSaving] = useState(false)
  const [aiMsg, setAiMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [aiAsking, setAiAsking] = useState(false)
  const [aiAnalysis, setAiAnalysis] = useState<DiagnosticAiAnalysis | null>(null)
  const [askInput, setAskInput] = useState('')

  useEffect(() => {
    const b = (window as any).__msmc_bridge__
    if (!b) return
    b.invoke('server:refresh').then((r: any) => {
      if (r?.known?.length) {
        setServers(r.known)
        setSelectedServer(r.known[0])
      }
    }).catch(() => {})
  }, [])

  // 挂载时查询 AI 配置状态（执行链：getAiStatus → C# IDeepSeekService）
  useEffect(() => {
    getAiStatus().then(s => setAiConfigured(s.configured)).catch(() => {})
  }, [])

  async function handleSaveApiKey(rawKey?: string) {
    const key = (rawKey ?? aiKeyInput).trim()
    setAiSaving(true)
    setAiMsg(null)
    try {
      const r = await setApiKey(key)
      if (r.success) {
        setAiConfigured(r.configured)
        setAiMsg({ ok: true, text: r.configured ? '✅ API Key 已保存（加密存储）' : '✅ 已清除 API Key' })
        setAiKeyInput('')
      } else {
        setAiMsg({ ok: false, text: `❌ 保存失败：${r.error || '未知错误'}` })
      }
    } catch (e: any) {
      setAiMsg({ ok: false, text: `❌ ${e?.message || String(e)}` })
    } finally {
      setAiSaving(false)
    }
  }

  async function handleAskAI() {
    if (!report) return
    setAiAsking(true)
    setAiMsg(null)
    try {
      const r = await askAI(report, askInput.trim())
      if (r.success && r.analysis) {
        setAiAnalysis(r.analysis)
        setAskInput('')
      } else {
        setAiMsg({ ok: false, text: `❌ ${r.error || 'AI 分析失败（检查网络或 API Key）'}` })
      }
    } catch (e: any) {
      setAiMsg({ ok: false, text: `❌ ${e?.message || String(e)}` })
    } finally {
      setAiAsking(false)
    }
  }

  const triggerQuickScan = useCallback(async (jarPath: string, _worldPath?: string) => {
    setStatus('scanning')
    setErrorMsg(null)
    setReport(null)
    setAiAnalysis(null)
    try {
      const r = await runDiagnostic(jarPath)
      if (r.success && r.report) {
        setReport(r.report)
        setAiAnalysis(r.report.aiAnalysis ?? null)
        setStatus('done')
      } else {
        setStatus('error')
        setErrorMsg(r.error || '快速扫描失败')
      }
    } catch (e: any) {
      setStatus('error')
      setErrorMsg(e?.message || String(e))
    }
  }, [])

  useEffect(() => {
    if (selectedServer) {
      triggerQuickScan(selectedServer.serverJarPath, selectedServer.workingDirectory)
    }
  }, [selectedServer, triggerQuickScan])

  async function handleDeepScan() {
    if (!selectedServer) return
    const jarPath = selectedServer.serverJarPath
    const worldPath = selectedServer.workingDirectory
    setStatus('deep')
    const check = await checkServerRunning(jarPath)
    if (check.running) {
      const shouldKill = confirm(
        '检测到服务器正在运行。运行时扫描可能读到脏数据。\n是否先杀掉服务器进程再扫描？\n[确定]杀掉并扫描  [取消]只扫系统/日志跳过存档'
      )
      if (shouldKill) {
        const r = await killServerAndScan(jarPath, worldPath)
        applyResult(r)
        return
      }
    }
    const r = await runDeepScan(jarPath, worldPath)
    applyResult(r)
  }

  function applyResult(r: { success: boolean; report?: DiagnosticReport; error?: string }) {
    if (r.success && r.report) {
      setReport(r.report)
      setStatus('done')
    } else {
      setStatus('error')
      setErrorMsg(r.error || '扫描失败')
    }
  }

  const s = report?.summary
  const criticalIssues = report?.issues.filter(i => i.severity >= 4) ?? []
  const errorIssues = report?.issues.filter(i => i.severity === 3) ?? []
  const warnIssues = report?.issues.filter(i => i.severity === 2) ?? []

  const allFixes: DiagnosticIssue[] = [...criticalIssues, ...errorIssues, ...warnIssues]

  const activeFixAction = activeFix
    ? allFixes.map(i => i.fix).filter((f): f is NonNullable<typeof f> => !!f).find(f => f.fixId === activeFix)
    : null

  return (
    <div style={{ padding: 20, height: '100%', overflowY: 'auto', background: 'var(--md-deep-background)' }}>
      {/* 顶部标题栏 */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 14, marginBottom: 20,
        paddingBottom: 14, borderBottom: '2px solid ' + PRIMARY,
      }}>
        <div style={{
          width: 44, height: 44, borderRadius: 10,
          background: ACCENT_BG, border: '1px solid ' + PRIMARY,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <FaWrench size={22} style={{ color: PRIMARY }} />
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 20, fontWeight: 800, color: PRIMARY }}>疑难解答</div>
          <div style={{ fontSize: 12, opacity: 0.7, marginTop: 2 }}>
            {status === 'scanning' && '🔍 快速扫描中...'}
            {status === 'deep' && '🔬 深度扫描中...'}
            {status === 'done' && s && `✅ ${s.totalChecks} 项检查，耗时 ${s.scanDurationMs}ms`}
            {status === 'error' && `❌ ${errorMsg}`}
            {status === 'idle' && '选择服务器后自动扫描'}
          </div>
        </div>
        {servers.length > 0 && (
          <select
            value={selectedServer?.knownServerId ?? ''}
            onChange={e => setSelectedServer(servers.find(s2 => s2.knownServerId === e.target.value) ?? null)}
            style={{
              padding: '6px 12px', borderRadius: 6,
              border: '1px solid ' + PRIMARY,
              background: 'var(--md-card-background)', color: 'var(--md-body)',
              fontSize: 13, minWidth: 200,
            }}
          >
            {servers.map(s2 => <option key={s2.knownServerId} value={s2.knownServerId}>{s2.name}</option>)}
          </select>
        )}
        {selectedServer && (
          <button
            onClick={() => setShowSymptomDialog(true)}
            style={{
              padding: '7px 14px', borderRadius: 6,
              border: '1px solid ' + PRIMARY, background: PRIMARY,
              color: '#fff', fontSize: 13, fontWeight: 600, cursor: 'pointer',
            }}
          >🩺 症状导航</button>
        )}
        {selectedServer && (
          <button
            onClick={handleDeepScan}
            disabled={status === 'scanning' || status === 'deep'}
            style={{
              padding: '7px 14px', borderRadius: 6,
              border: '1px solid ' + PRIMARY, background: ACCENT_BG,
              color: PRIMARY, fontSize: 13, fontWeight: 600,
              cursor: status === 'scanning' || status === 'deep' ? 'not-allowed' : 'pointer',
              opacity: status === 'scanning' || status === 'deep' ? 0.5 : 1,
            }}
          >🔬 深度扫描</button>
        )}
        {status === 'done' && selectedServer && (
          <button
            onClick={() => triggerQuickScan(selectedServer.serverJarPath, selectedServer.workingDirectory)}
            style={{
              padding: '7px 14px', borderRadius: 6, border: 'none',
              background: PRIMARY, color: '#fff', fontSize: 13, fontWeight: 600, cursor: 'pointer',
            }}
          >🔄 重扫</button>
        )}
      </div>

      {/* 进度条 */}
      {(status === 'scanning' || status === 'deep') && (
        <div style={{
          padding: 16, borderRadius: 10, background: ACCENT_BG,
          border: '1px solid ' + ACCENT_BORDER, marginBottom: 14,
        }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: PRIMARY, marginBottom: 8 }}>
            {status === 'scanning' ? '快速扫描' : '深度扫描'}
          </div>
          <div style={{ height: 6, borderRadius: 3, background: 'rgba(192,57,43,0.2)' }}>
            <div style={{ height: '100%', borderRadius: 3, background: PRIMARY, width: '60%',
              animation: 'msmc-progress-pulse 1.2s ease-in-out infinite' }} />
          </div>
        </div>
      )}

      {/* DeepSeek AI 配置入口（配置 = 使用 AI 的前置条件） */}
      <div style={{
        padding: 14, borderRadius: 10, marginBottom: 14,
        background: ACCENT_BG, border: '1px solid ' + ACCENT_BORDER,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          <span style={{ fontSize: 15 }}>🤖</span>
          <span style={{ fontSize: 13, fontWeight: 700, color: PRIMARY }}>DeepSeek AI 诊断助手</span>
          <span style={{
            fontSize: 11, padding: '1px 8px', borderRadius: 10,
            background: aiConfigured ? 'rgba(39,174,96,0.15)' : 'rgba(192,57,43,0.12)',
            color: aiConfigured ? '#27ae60' : PRIMARY,
            fontWeight: 600,
          }}>
            {aiConfigured ? '已配置 ✓' : '未配置'}
          </span>
        </div>
        <div style={{ fontSize: 11.5, opacity: 0.75, marginBottom: 10, lineHeight: 1.6 }}>
          {aiConfigured
            ? '已配置 API Key：跑完体检会自动附加 AI 诊断结论，也可以在下方向 AI 追问问题。'
            : '想让 AI 帮你总结诊断结论？在下方粘贴你的 DeepSeek API Key 并保存（密钥加密存储在本地，不会上报）。没有 Key？去 platform.deepseek.com 注册并创建即可。'}
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <input
            type="password"
            value={aiKeyInput}
            onChange={e => setAiKeyInput(e.target.value)}
            placeholder={aiConfigured ? '输入新 Key 可覆盖，或留空后点击「清除」' : '粘贴你的 DeepSeek API Key'}
            style={{
              flex: 1, minWidth: 220, padding: '7px 10px', borderRadius: 6,
              border: '1px solid ' + ACCENT_BORDER, background: 'var(--md-card-background)',
              color: 'var(--md-body)', fontSize: 12.5,
            }}
          />
          <button
            onClick={() => handleSaveApiKey()}
            disabled={aiSaving}
            style={{
              padding: '7px 14px', borderRadius: 6, border: 'none',
              background: PRIMARY, color: '#fff', fontSize: 12.5, fontWeight: 600,
              cursor: aiSaving ? 'not-allowed' : 'pointer', opacity: aiSaving ? 0.5 : 1,
            }}
          >{aiSaving ? '保存中...' : '保存 Key'}</button>
          {aiConfigured && (
            <button
              onClick={() => handleSaveApiKey('')}
              disabled={aiSaving}
              style={{
                padding: '7px 14px', borderRadius: 6,
                border: '1px solid ' + ACCENT_BORDER, background: 'transparent',
                color: PRIMARY, fontSize: 12.5, cursor: 'pointer',
              }}
            >清除</button>
          )}
        </div>
        {aiMsg && (
          <div style={{ marginTop: 8, fontSize: 11.5, color: aiMsg.ok ? '#27ae60' : PRIMARY }}>
            {aiMsg.text}
          </div>
        )}
      </div>

      {/* 结果展示 */}
      {report && (
        <>
          {s && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 8, marginBottom: 16 }}>
              <StatCard label="Critical" value={s.criticalCount} color={PRIMARY} />
              <StatCard label="Error" value={s.errorCount} color="#e67e22" />
              <StatCard label="Warning" value={s.warningCount} color="#f39c12" />
              <StatCard label="Ok/Info" value={s.okCount} color="#27ae60" />
              <StatCard label="AutoFix" value={s.autoFixableCount} color="#8e44ad" />
            </div>
          )}

          {criticalIssues.length > 0 && <IssueGroup title="💀 Critical" count={criticalIssues.length} issues={criticalIssues} onFix={setActiveFix} highlightCheck={highlightCheck} />}
          {errorIssues.length > 0 && <IssueGroup title="🔴 Error" count={errorIssues.length} issues={errorIssues} onFix={setActiveFix} highlightCheck={highlightCheck} />}
          {warnIssues.length > 0 && <IssueGroup title="🟧 Warning" count={warnIssues.length} issues={warnIssues} onFix={setActiveFix} highlightCheck={highlightCheck} />}

          {allFixes.length === 0 && (
            <div style={{
              padding: 20, borderRadius: 10, background: 'rgba(39,174,96,0.08)',
              border: '1px solid rgba(39,174,96,0.4)', textAlign: 'center',
            }}>
              <div style={{ fontSize: 16, color: '#27ae60', fontWeight: 600 }}>✅ 体检通过！</div>
              <div style={{ fontSize: 12, opacity: 0.7, marginTop: 6 }}>
                {s?.totalChecks ?? 0} 项检查全部 OK 或 Info 级别
              </div>
            </div>
          )}

          {activeFixAction && selectedServer && (
            <FixPanel
              serverJarPath={selectedServer.serverJarPath}
              worldPath={selectedServer.workingDirectory}
              fix={activeFixAction}
              onClose={() => setActiveFix(null)}
            />
          )}
        </>
      )}

      {status === 'error' && (
        <div style={{ padding: 14, borderRadius: 8, background: PRIMARY, color: '#fff' }}>
          ❌ {errorMsg}
        </div>
      )}

      {/* AI 诊断结论（报告自带 aiAnalysis 或追问结果） */}
      {aiAnalysis && (
        <div style={{ marginBottom: 18, padding: 14, borderRadius: 10, background: ACCENT_BG, border: '1px solid ' + ACCENT_BORDER }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
            <span style={{ fontSize: 15 }}>🤖</span>
            <span style={{ fontSize: 13, fontWeight: 700, color: PRIMARY }}>AI 诊断结论</span>
          </div>
          <div style={{ fontSize: 12.5, lineHeight: 1.7, marginBottom: 10 }}>{aiAnalysis.summary}</div>
          {aiAnalysis.keyFindings.length > 0 && (
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 4 }}>关键发现：</div>
              {aiAnalysis.keyFindings.map((f, i) => (
                <div key={i} style={{ fontSize: 12, lineHeight: 1.6, paddingLeft: 14, position: 'relative' }}>
                  <span style={{ position: 'absolute', left: 0, color: PRIMARY }}>•</span>{f}
                </div>
              ))}
            </div>
          )}
          {aiAnalysis.recommendedActions.length > 0 && (
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 4 }}>建议动作：</div>
              {aiAnalysis.recommendedActions.map((a, i) => (
                <div key={i} style={{ fontSize: 12, lineHeight: 1.6, paddingLeft: 14, position: 'relative' }}>
                  <span style={{ position: 'absolute', left: 0, color: a.dangerous ? PRIMARY : '#27ae60' }}>{a.dangerous ? '⚠' : '✓'}</span>
                  <span style={{ opacity: 0.92 }}>{a.label}</span>
                  {a.rationale ? <span style={{ opacity: 0.6 }}>（{a.rationale}）</span> : null}
                </div>
              ))}
            </div>
          )}
          {aiAnalysis.needMoreInfo && aiAnalysis.suggestedQuestions && aiAnalysis.suggestedQuestions.length > 0 && (
            <div style={{ marginBottom: 10, padding: 8, borderRadius: 6, background: 'rgba(255,193,7,0.12)', border: '1px solid rgba(255,193,7,0.4)' }}>
              <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 4 }}>🧐 还需更多信息，建议补充：</div>
              {aiAnalysis.suggestedQuestions.map((q, i) => (
                <div key={i} style={{ fontSize: 12, lineHeight: 1.6 }}>· {q}</div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* AI 追问输入框（仅报告就绪且有 AI 时显示） */}
      {report && (
        <div style={{ marginBottom: 18, padding: 14, borderRadius: 10, background: ACCENT_BG, border: '1px solid ' + ACCENT_BORDER }}>
          <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 6 }}>💬 向 AI 追问</div>
          <div style={{ display: 'flex', gap: 8 }}>
            <input
              value={askInput}
              onChange={e => setAskInput(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter' && askInput.trim() && !aiAsking) handleAskAI() }}
              placeholder={aiConfigured ? '例如：这个内存给的够吗？频繁崩溃是啥原因？' : '先在上方配置 DeepSeek API Key，才能向 AI 提问'}
              disabled={!aiConfigured}
              style={{
                flex: 1, padding: '7px 10px', borderRadius: 6,
                border: '1px solid ' + ACCENT_BORDER, background: 'var(--md-card-background)',
                color: 'var(--md-body)', fontSize: 12.5,
                opacity: aiConfigured ? 1 : 0.6,
              }}
            />
            <button
              onClick={handleAskAI}
              disabled={!aiConfigured || aiAsking || !askInput.trim()}
              style={{
                padding: '7px 14px', borderRadius: 6, border: 'none',
                background: PRIMARY, color: '#fff', fontSize: 12.5, fontWeight: 600,
                cursor: !aiConfigured || aiAsking || !askInput.trim() ? 'not-allowed' : 'pointer',
                opacity: !aiConfigured || aiAsking || !askInput.trim() ? 0.5 : 1,
              }}
            >{aiAsking ? 'AI 思考中...' : '提问'}</button>
          </div>
        </div>
      )}

      {/* 症状导航 Modal — 双轨布局第二轨 */}
      {showSymptomDialog && (
        <TreeNodeDialog
          onClose={() => setShowSymptomDialog(false)}
          onCheckReference={(checkId) => {
            setShowSymptomDialog(false)
            // 关闭对话框后高亮对应 issue
            setHighlightCheck(checkId)
            setTimeout(() => setHighlightCheck(null), 3500)
          }}
        />
      )}
    </div>
  )
}

function StatCard({ label, value, color }: { label: string; value: number; color: string }) {
  return (
    <div style={{
      padding: '12px 10px', borderRadius: 8,
      background: color + '14', border: '1px solid ' + color + '40',
      textAlign: 'center',
    }}>
      <div style={{ fontSize: 22, fontWeight: 800, color }}>{value}</div>
      <div style={{ fontSize: 10, opacity: 0.7, marginTop: 2 }}>{label}</div>
    </div>
  )
}

function IssueGroup({ title, count, issues, onFix, highlightCheck }: {
  title: string; count: number; issues: DiagnosticIssue[];
  onFix: (id: string) => void;
  highlightCheck?: string | null;
}) {
  return (
    <div style={{ marginBottom: 18 }}>
      <div style={{
        fontSize: 13, fontWeight: 700, color: PRIMARY, marginBottom: 8,
        paddingBottom: 5, borderBottom: '1px solid ' + ACCENT_BORDER,
      }}>
        {title} <span style={{ opacity: 0.6 }}>({count})</span>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {issues.map((issue, i) => (
          <DiagnosticReportCard
            key={i}
            issue={issue}
            onFix={onFix}
            highlight={!!highlightCheck && issue.issueId === highlightCheck}
          />
        ))}
      </div>
    </div>
  )
}
