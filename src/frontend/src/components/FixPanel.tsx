// -----------------------------------------------------------------------------
// FixPanel.tsx — 修复执行面板
// 信任确认（全自动 / 每步确认 / 测试模式）+ 进度可视化 + 取消
// 血红色强制配色（#c0392b）
// -----------------------------------------------------------------------------
import { useState } from 'react'

import type { DiagnosticFixAction, DiagnosticFixResult, FixTrustMode } from '@/types/bridge'
import { executeFix, cancelFix } from '@/utils/bridge'

export interface FixPanelProps {
  serverJarPath: string
  worldPath?: string
  fix?: DiagnosticFixAction
  onDone?: (result: DiagnosticFixResult) => void
  onClose?: () => void
}

export function FixPanel({ serverJarPath, worldPath, fix, onDone, onClose }: FixPanelProps) {
  if (!fix) return null
  const [trustMode, setTrustMode] = useState<FixTrustMode>('Auto')
  const [running, setRunning] = useState(false)
  const [result, setResult] = useState<DiagnosticFixResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (!fix) return null

  async function handleExecute() {
    setRunning(true)
    setError(null)
    setResult(null)
    try {
      const r = await executeFix(fix!.fixId, trustMode, serverJarPath, worldPath, fix!.steps?.[0]?.params)
      setResult(r)
      onDone?.(r)
    } catch (e: any) {
      setError(e?.message || String(e))
    } finally {
      setRunning(false)
    }
  }

  async function handleCancel() {
    try { await cancelFix(fix!.fixId) } catch {}
    setRunning(false)
  }

  const primaryColor = '#c0392b'
  const accentBg = 'rgba(192,57,43,0.08)'

  return (
    <div
      style={{
        marginTop: 16,
        padding: 18,
        borderRadius: 12,
        border: `1px solid ${primaryColor}`,
        background: accentBg,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
        <div style={{ fontWeight: 700, color: primaryColor, fontSize: 15 }}>
          🛠️ 修复面板 — {fix.label}
          {fix.dangerous && (
            <span style={{ marginLeft: 8, fontSize: 11, padding: '2px 8px', borderRadius: 4, background: primaryColor, color: '#fff' }}>
              ⚠️ 有风险
            </span>
          )}
        </div>
        {onClose && (
          <button onClick={onClose} style={{
            border: 'none', background: 'transparent', cursor: 'pointer', color: '#888', fontSize: 18,
          }}>×</button>
        )}
      </div>

      {/* Diff 预览 / 步骤列表 */}
      <div style={{ background: 'rgba(0,0,0,0.25)', borderRadius: 8, padding: 12, marginBottom: 14, fontSize: 12 }}>
        <div style={{ fontWeight: 600, marginBottom: 8, opacity: 0.8 }}>执行计划 ({fix.steps.length} 步)</div>
        {fix.steps.map((step, i) => (
          <div key={i} style={{ display: 'flex', gap: 8, padding: '3px 0', opacity: 0.85 }}>
            <span style={{ color: primaryColor, fontWeight: 700, minWidth: 18 }}>{i + 1}.</span>
            <span>{step.label}</span>
            <span style={{ opacity: 0.5, marginLeft: 'auto' }}>(({step.actionType})</span>
            {step.dangerous && <span style={{ color: primaryColor }}>⚠️</span>}
          </div>
        ))}
      </div>

      {/* 信任模式选择 */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
        {(['Auto', 'StepByStep', 'DryRun'] as FixTrustMode[]).map(mode => (
          <button
            key={mode}
            onClick={() => setTrustMode(mode)}
            disabled={running}
            style={{
              flex: 1,
              padding: '8px 10px',
              borderRadius: 6,
              border: trustMode === mode ? `2px solid ${primaryColor}` : '1px solid rgba(255,255,255,0.15)',
              background: trustMode === mode ? accentBg : 'transparent',
              color: trustMode === mode ? primaryColor : 'var(--md-body)',
              fontSize: 12,
              fontWeight: 600,
              cursor: running ? 'not-allowed' : 'pointer',
              opacity: running ? 0.5 : 1,
            }}
          >
            {mode === 'Auto' && '⚡ 全自动执行'}
            {mode === 'StepByStep' && '👣 每步确认'}
            {mode === 'DryRun' && '🔍 测试模式'}
          </button>
        ))}
      </div>

      {/* 执行按钮 */}
      <div style={{ display: 'flex', gap: 8 }}>
        {!running ? (
          <button
            onClick={handleExecute}
            disabled={!fix}
            style={{
              flex: 1,
              padding: '10px 16px',
              borderRadius: 8,
              border: 'none',
              background: primaryColor,
              color: '#fff',
              fontSize: 13,
              fontWeight: 700,
              cursor: fix ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
            }}
          >
            ▶ 确认修复
          </button>
        ) : (
          <button
            onClick={handleCancel}
            style={{
              flex: 1,
              padding: '10px 16px',
              borderRadius: 8,
              border: `1px solid ${primaryColor}`,
              background: 'transparent',
              color: primaryColor,
              fontSize: 13,
              fontWeight: 700,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
            }}
          >
            ⏹ 取消
          </button>
        )}
      </div>

      {/* 结果显示 */}
      {result && (
        <div style={{
          marginTop: 14, padding: 12, borderRadius: 8,
          background: result.succeeded ? 'rgba(39,174,96,0.12)' : 'rgba(192,57,43,0.12)',
          border: `1px solid ${result.succeeded ? '#27ae60' : primaryColor}`,
        }}>
          <div style={{ fontWeight: 700, color: result.succeeded ? '#27ae60' : primaryColor, marginBottom: 6 }}>
            {result.succeeded ? '✅ 修复成功' : '❌ 修复失败'} ({result.stepsCompleted}/{result.stepsTotal} 步)
          </div>
          {result.backupPath && (
            <div style={{ fontSize: 11, opacity: 0.7, marginBottom: 4 }}>
              📦 备份路径: {result.backupPath}
            </div>
          )}
          {result.error && (
            <div style={{ fontSize: 12, color: primaryColor }}>{result.error}</div>
          )}
          {result.stepResults.map((sr, i) => (
            <div key={i} style={{ fontSize: 11, padding: '2px 0', opacity: sr.succeeded ? 0.8 : 1 }}>
              {sr.succeeded ? '✅' : '❌'} {sr.label}
              {sr.message && <span style={{ marginLeft: 6, opacity: 0.6 }}>— {sr.message}</span>}
            </div>
          ))}
        </div>
      )}

      {error && (
        <div style={{ marginTop: 14, padding: 10, borderRadius: 8, background: primaryColor, color: '#fff', fontSize: 12 }}>
          ❌ 执行异常: {error}
        </div>
      )}
    </div>
  )
}
