import type { DiagnosticIssue, DiagnosticCheckResult, Severity } from '@/types/bridge'

export interface ReportCardProps {
  issue?: DiagnosticIssue
  check?: DiagnosticCheckResult
  onFix?: (fixId: string) => void
  highlight?: boolean
}

// Severity → 图标 + 颜色 + 背景（强制，不走主题系统）
const SEVERITY_MAP: Record<Severity, { icon: string; color: string; bg: string; border: string; label: string }> = {
  0: { icon: '✅', color: '#27ae60', bg: 'rgba(39,174,96,0.08)', border: 'rgba(39,174,96,0.4)', label: 'OK' },
  1: { icon: 'ℹ️', color: '#3498db', bg: 'rgba(52,152,219,0.08)', border: 'rgba(52,152,219,0.4)', label: 'INFO' },
  2: { icon: '🟧', color: '#f39c12', bg: 'rgba(243,156,18,0.08)', border: 'rgba(243,156,18,0.4)', label: 'WARN' },
  3: { icon: '❌', color: '#e67e22', bg: 'rgba(230,126,34,0.10)', border: 'rgba(230,126,34,0.5)', label: 'ERROR' },
  4: { icon: '💀', color: '#c0392b', bg: 'rgba(192,57,43,0.12)', border: 'rgba(192,57,43,0.6)', label: 'CRITICAL' },
}

export function DiagnosticReportCard({ issue, check, onFix, highlight }: ReportCardProps) {
  const data = issue ?? check
  if (!data) return null

  const sev = data.severity as Severity
  const cfg = SEVERITY_MAP[sev]

  // 区分 issue / check 的字段
  const fixAction = issue ? issue.fix : check?.suggestedFix
  const hasFix = !!fixAction

  return (
    <div
      style={{
        padding: '12px 14px', borderRadius: 10,
        background: highlight ? 'rgba(192,57,43,0.18)' : cfg.bg,
        border: highlight ? '2px solid #c0392b' : `1px solid ${cfg.border}`,
        boxShadow: highlight ? '0 0 16px rgba(192,57,43,0.45)' : 'none',
        display: 'flex', gap: 10, fontSize: 13,
        transition: 'all 0.25s ease',
      }}
    >
      <div style={{ fontSize: 18, marginTop: 2, flexShrink: 0 }}>{cfg.icon}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 600, color: cfg.color, marginBottom: 3 }}>
          <span style={{ fontSize: 10, marginRight: 6, opacity: 0.7 }}>[{cfg.label}]</span>
          {data.title}
        </div>
        <div style={{ color: 'var(--md-body-light, #888)', lineHeight: 1.5 }}>{data.detail}</div>
        {issue && (
          <div style={{ marginTop: 6, fontSize: 11, opacity: 0.6 }}>
            {issue.hint && <span style={{ marginRight: 6 }}>💡 {issue.hint}</span>}
            {issue.suggestion && <span>👉 {issue.suggestion}</span>}
          </div>
        )}
        {hasFix && onFix && fixAction && (
          <div style={{ marginTop: 8, display: 'flex', gap: 8 }}>
            <button
              onClick={() => onFix(fixAction!.fixId)}
              style={{
                padding: '4px 10px', borderRadius: 6,
                border: `1px solid ${cfg.color}`,
                background: cfg.bg, color: cfg.color,
                fontSize: 12, fontWeight: 600, cursor: 'pointer',
              }}
            >🔧 自动修复</button>
          </div>
        )}
      </div>
    </div>
  )
}

export { SEVERITY_MAP }
