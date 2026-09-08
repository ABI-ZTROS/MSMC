// Phase 状态机：驱动配色切换
// 三态配色（用户约定）：水蓝(boot/running/success) → 日落黄(warn) → 血红(error)
// success 是启动完成态，与 running 同属"正常"范畴，沿用水蓝
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
  success: { cssClass: 'phase-success', bgGlow: '#5DC8E8' },   // 完成态同 running 水蓝
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
  backgroundColor?: string;
}

export interface ThemeChangedPayload {
  primaryColor: string;
  isDarkMode: boolean;
  backgroundColor?: string;
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
