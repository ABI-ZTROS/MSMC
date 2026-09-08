// Phase 状态机：驱动配色切换
export type BootPhase = 'boot' | 'running' | 'warn' | 'error' | 'success';

export interface BootPhaseConfig {
  cssClass: string;
  bgGlow: string;
}

export const BOOT_PHASE_CONFIG: Record<BootPhase, BootPhaseConfig> = {
  boot:    { cssClass: 'phase-boot',    bgGlow: '#5DC8E8' },
  running: { cssClass: 'phase-running', bgGlow: '#5DC8E8' },
  warn:    { cssClass: 'phase-warn',    bgGlow: '#e8964a' },
  error:   { cssClass: 'phase-error',   bgGlow: '#c0392b' },
  success: { cssClass: 'phase-success', bgGlow: '#34d399' },
};

export interface LogEntry {
  id: number;
  message: string;
  type: 'info' | 'ok' | 'warn' | 'error' | 'debug';
  timestamp: number;
}

export interface InitPayload {
  version: string;
  primaryColor: string;
}

export interface ProgressPayload {
  percent: number;
  status: string;
}

export interface LogPayload {
  message: string;
  isError?: boolean;
  isSuccess?: boolean;
}
