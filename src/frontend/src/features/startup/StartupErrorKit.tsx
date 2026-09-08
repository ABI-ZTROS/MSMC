import type { LogEntry } from './bootState';

interface Props {
  logs: LogEntry[];
  onRestart: () => void;
  onOpenTroubleshooting: () => void;
  onCopyLog: () => void;
}

/**
 * 失败态急救箱
 * - 关键错误高亮卡片（过滤最近 10 条 ERROR）
 * - 三个急救按钮：重开 / 疑难解答 / 复制日志
 */
export function StartupErrorKit({ logs, onRestart, onOpenTroubleshooting, onCopyLog }: Props) {
  const errorLogs = logs
    .filter((l) => l.type === 'error')
    .slice(-10);

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      gap: 16,
      zIndex: 15,
    }}>
      {errorLogs.length > 0 && (
        <div style={{
          width: '100%',
          maxWidth: 560,
          backgroundColor: 'rgba(231, 76, 60, 0.08)',
          border: '1px solid rgba(231, 76, 60, 0.4)',
          borderRadius: 8,
          padding: '12px 16px',
          fontFamily: '"SF Mono", "Consolas", monospace',
        }}>
          <div style={{
            fontSize: 11,
            color: '#e74c3c',
            fontWeight: 600,
            marginBottom: 8,
            textTransform: 'uppercase',
            letterSpacing: 1,
          }}>
            ⚠ Critical Errors ({errorLogs.length})
          </div>
          {errorLogs.map((log, i) => (
            <div key={i} style={{
              fontSize: 12,
              color: '#fca5a5',
              marginBottom: 4,
              wordBreak: 'break-all',
            }}>
              {log.message}
            </div>
          ))}
        </div>
      )}

      <div style={{
        display: 'flex',
        gap: 12,
        flexWrap: 'wrap',
      }}>
        <ErrorButton onClick={onRestart} label="🔄 重新启动" color="#5DC8E8" />
        <ErrorButton onClick={onOpenTroubleshooting} label="🩺 疑难解答" color="#e8964a" />
        <ErrorButton onClick={onCopyLog} label="📋 复制日志" color="#94a3b8" />
      </div>
    </div>
  );
}

function ErrorButton({ onClick, label, color }: { onClick: () => void; label: string; color: string }) {
  return (
    <button
      onClick={onClick}
      style={{
        padding: '8px 16px',
        backgroundColor: 'transparent',
        border: `1px solid ${color}`,
        color,
        borderRadius: 6,
        fontSize: 13,
        fontWeight: 500,
        cursor: 'pointer',
        transition: 'background-color 150ms ease',
        fontFamily: 'inherit',
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.backgroundColor = `${color}1f`;
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.backgroundColor = 'transparent';
      }}
    >
      {label}
    </button>
  );
}
