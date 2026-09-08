import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  phase: BootPhase;
  keyframesCss: string;
}

/**
 * 极简背景：极深水蓝黑 + 中心柔化呼吸光晕
 * 光晕颜色跟随 phase 主色动态切换
 * 仅 2 层 DOM，零 JS 计算，性能友好
 */
export function StartupBackground({ phase, keyframesCss }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <>
      <style>{keyframesCss}</style>

      <div
        style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: '#081420',
          zIndex: 0,
        }}
      />

      <div
        style={{
          position: 'fixed',
          inset: 0,
          background: `radial-gradient(
            ellipse at center,
            ${config.bgGlow}14 0%,
            transparent 70%
          )`,
          animation: 'startupBreathe 6s cubic-bezier(0.4, 0, 0.6, 1) infinite',
          zIndex: 1,
          pointerEvents: 'none',
        }}
      />
    </>
  );
}
