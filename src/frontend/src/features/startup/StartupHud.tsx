interface Props {
  version: string;
  phase: string;
  fps: number;
}

/**
 * 顶部 HUD：版本号 + session + phase + 真实 FPS
 * 砍掉了模拟的 CPU/MEM/uptime
 */
export function StartupHud({ version, phase, fps }: Props) {
  const sessionId = generateSessionId();

  return (
    <div style={{
      position: 'fixed',
      top: 16,
      left: 24,
      right: 24,
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      zIndex: 20,
      pointerEvents: 'none',
      fontFamily: '"SF Mono", "Consolas", monospace',
    }}>
      <div style={{
        display: 'flex',
        gap: 16,
        fontSize: 12,
        color: '#94a3b8',
      }}>
        <span>{version}</span>
        <span>SESSION {sessionId}</span>
        <span>● {phase.toUpperCase()}</span>
      </div>

      <div style={{
        fontSize: 12,
        color: fps >= 55 ? '#34d399' : fps >= 30 ? '#e8964a' : '#e74c3c',
      }}>
        FPS {fps}
      </div>
    </div>
  );
}

function generateSessionId(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let result = '';
  for (let i = 0; i < 6; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return result;
}
