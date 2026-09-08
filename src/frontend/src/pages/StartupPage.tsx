import { useEffect, useState } from 'react';
import { StartupBackground } from '../features/startup/StartupBackground';
import { StartupProgressBar } from '../features/startup/StartupProgressBar';
import { StartupHud } from '../features/startup/StartupHud';
import { StartupLogo } from '../features/startup/StartupLogo';
import { StartupLogPanel } from '../features/startup/StartupLogPanel';
import { StartupErrorKit } from '../features/startup/StartupErrorKit';
import { useStartupBridge } from '../features/startup/useStartupBridge';
import { useFpsMonitor } from '../features/startup/useFpsMonitor';
import { STARTUP_KEYFRAMES } from '../features/startup/keyframes';
import { BOOT_PHASE_CONFIG } from '../features/startup/bootState';

/**
 * 启动页主壳 — 只组合子组件，零业务逻辑
 * 所有状态由 useStartupBridge hook 管理
 */
export default function StartupPage() {
  const bridge = useStartupBridge();
  const fps = useFpsMonitor(true);

  const [fadingOut, setFadingOut] = useState(false);

  useEffect(() => {
    if (bridge.isCompleted && !fadingOut) {
      const timer = setTimeout(() => {
        setFadingOut(true);
        setTimeout(() => {
          bridge.close();
        }, 500);
      }, 2000);
      return () => clearTimeout(timer);
    }
  }, [bridge.isCompleted, fadingOut, bridge]);

  const phaseConfig = BOOT_PHASE_CONFIG[bridge.phase];

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: bridge.backgroundColor,
        opacity: fadingOut ? 0 : 1,
        transition: 'opacity 500ms cubic-bezier(0.4, 0, 1, 1)',
        overflow: 'hidden',
        userSelect: 'none',
      }}
      onMouseDown={() => bridge.sendDragMove()}
    >
      <StartupBackground phase={bridge.phase} keyframesCss={STARTUP_KEYFRAMES} />

      <StartupHud version={bridge.version} phase={bridge.phase} fps={fps} />

      <div
        style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 32,
          padding: '100px 24px',
          zIndex: 10,
        }}
      >
        <StartupLogo />

        <StartupProgressBar
          progress={bridge.progress}
          phase={bridge.phase}
          showSuccessCheck={bridge.isCompleted}
        />

        <div style={{
          fontSize: 14,
          color: bridge.phase === 'error' ? '#e74c3c' : phaseConfig.bgGlow,
          fontWeight: 500,
          textAlign: 'center',
          maxWidth: 480,
        }}>
          {bridge.statusText}
        </div>

        {bridge.phase === 'error' && (
          <StartupErrorKit
            logs={bridge.logs}
            onRestart={bridge.restart}
            onOpenTroubleshooting={bridge.openTroubleshooting}
            onCopyLog={bridge.copyLog}
          />
        )}

        <StartupLogPanel logs={bridge.logs} />
      </div>
    </div>
  );
}
