import { useEffect, useState, useCallback } from 'react'
import { FaWrench } from 'react-icons/fa6'
import type { DiagnosticReport, DiagnosticIssue, KnownServerInfo } from '@/types/bridge'
import { runDiagnostic, runDeepScan, checkServerRunning, killServerAndScan } from '@/utils/bridge'
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

  const triggerQuickScan = useCallback(async (jarPath: string, _worldPath?: string) => {
    setStatus('scanning')
    setErrorMsg(null)
    setReport(null)
    try {
      const r = await runDiagnostic(jarPath)
      if (r.success && r.report) {
        setReport(r.report)
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
