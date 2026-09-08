import { useEffect, useRef, useState } from 'react';
import type { LogEntry } from './bootState';

interface Props {
  logs: LogEntry[];
  autoScroll?: boolean;
}

/**
 * 日志面板
 * - 默认自动滚动到底部
 * - 用户手动上滚时停止自动滚动，滚到底部后恢复
 * - tag 染色：info=#94a3b8, ok=#34d399, warn=#e8964a, error=#e74c3c, debug=#64748b
 */
const TYPE_COLORS: Record<LogEntry['type'], string> = {
  info: '#94a3b8',
  ok: '#34d399',
  warn: '#e8964a',
  error: '#e74c3c',
  debug: '#64748b',
};

export function StartupLogPanel({ logs, autoScroll = true }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [userScrolledUp, setUserScrolledUp] = useState(false);

  useEffect(() => {
    if (!containerRef.current) return;
    if (autoScroll && !userScrolledUp) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight;
    }
  }, [logs, autoScroll, userScrolledUp]);

  const handleScroll = () => {
    if (!containerRef.current) return;
    const { scrollTop, scrollHeight, clientHeight } = containerRef.current;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;

    if (distanceFromBottom < 20) {
      setUserScrolledUp(false);
    } else if (scrollTop > 0) {
      setUserScrolledUp(true);
    }
  };

  return (
    <div style={{
      width: '100%',
      maxWidth: 560,
      height: 160,
      backgroundColor: 'rgba(0,0,0,0.35)',
      borderRadius: 8,
      border: '1px solid rgba(148, 163, 184, 0.15)',
      overflow: 'hidden',
      display: 'flex',
      flexDirection: 'column',
    }}>
      <div style={{
        padding: '8px 12px',
        borderBottom: '1px solid rgba(148, 163, 184, 0.1)',
        fontSize: 11,
        color: '#64748b',
        fontFamily: '"SF Mono", "Consolas", monospace',
        display: 'flex',
        justifyContent: 'space-between',
      }}>
        <span>CONSOLE · {logs.length} entries</span>
        {userScrolledUp && (
          <span
            onClick={() => {
              setUserScrolledUp(false);
              if (containerRef.current) {
                containerRef.current.scrollTop = containerRef.current.scrollHeight;
              }
            }}
            style={{
              cursor: 'pointer',
              color: '#5DC8E8',
            }}
          >
            ↓ scroll to bottom
          </span>
        )}
      </div>

      <div
        ref={containerRef}
        onScroll={handleScroll}
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: '6px 12px',
          fontFamily: '"SF Mono", "Consolas", monospace',
          fontSize: 12,
          lineHeight: 1.6,
        }}
      >
        {logs.length === 0 ? (
          <span style={{ color: '#64748b' }}>等待日志输出...</span>
        ) : (
          logs.map((entry) => (
            <div key={entry.id} style={{ display: 'flex', gap: 8 }}>
              <span style={{
                color: TYPE_COLORS[entry.type],
                fontWeight: entry.type === 'error' ? 700 : 500,
                flexShrink: 0,
                minWidth: 36,
              }}>
                [{entry.type.toUpperCase()}]
              </span>
              <span style={{ color: '#cbd5e1', wordBreak: 'break-all' }}>
                {entry.message}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
