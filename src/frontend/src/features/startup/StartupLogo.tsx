import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  phase: BootPhase;
  lines?: string[];
}

/**
 * ASCII LOGO 组件
 * 后续用户会单独提供新内容，这里只是骨架
 */
const DEFAULT_LOGO: string[] = [
  '  __  __  ____   _____  ____',
  ' |  \\/  |/ ___| / ____|/ ___|',
  ' | \\  / | |     | |    | |',
  ' | |\\/| | |     | |    | |___',
  ' | |  | | |___  | |____ \\___ \\',
  ' |_|  |_|\\____| \\_____|____/',
];

export function StartupLogo({ phase, lines = DEFAULT_LOGO }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <div style={{
      fontFamily: '"SF Mono", "Consolas", monospace',
      fontSize: 14,
      lineHeight: 1.2,
      textAlign: 'center',
      whiteSpace: 'pre',
      userSelect: 'none',
      color: config.bgGlow,
      opacity: 0.85,
      letterSpacing: 1,
    }}>
      {lines.join('\n')}
    </div>
  );
}
