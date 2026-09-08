import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  progress: number;
  phase: BootPhase;
  showSuccessCheck: boolean;
}

/**
 * 横条进度条：
 * - 百分比放在条外部右侧（避免白字在亮色填充上只有 1.93:1 对比度的陷阱）
 * - 颜色跟随 phase 动态切换
 * - 贝塞尔缓动 400ms
 */
export function StartupProgressBar({ progress, phase, showSuccessCheck }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: '16px',
      zIndex: 10,
    }}>
      <div
        style={{
          width: 280,
          height: 6,
          borderRadius: 3,
          backgroundColor: `${config.bgGlow}26`,
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            width: `${Math.max(0, Math.min(100, progress))}%`,
            height: '100%',
            borderRadius: 3,
            background: `linear-gradient(90deg, ${config.bgGlow}, ${config.bgGlow}cc)`,
            transition: 'width 400ms cubic-bezier(0.4, 0, 0.2, 1)',
          }}
        />
      </div>

      <span
        style={{
          fontSize: 13,
          fontWeight: 600,
          color: '#e2e8f0',
          fontVariantNumeric: 'tabular-nums',
          minWidth: 32,
          textAlign: 'left',
        }}
      >
        {progress}%
      </span>

      {showSuccessCheck && (
        <span
          style={{
            fontSize: 20,
            color: '#34d399',
            animation: 'startupSuccessPop 400ms cubic-bezier(0.34, 1.56, 0.64, 1) forwards',
          }}
        >
          ✓
        </span>
      )}
    </div>
  );
}
