import { useCallback, useEffect, useRef, useState } from 'react';
import type { BootPhase, InitPayload, ProgressPayload, LogEntry, LogPayload } from './bootState';

export interface StartupBridgeState {
  phase: BootPhase;
  progress: number;
  statusText: string;
  logs: LogEntry[];
  version: string;
  primaryColor: string;
  backgroundColor: string;
  isCompleted: boolean;
  isFailed: boolean;
}

export interface StartupBridgeActions {
  restart: () => void;
  openTroubleshooting: () => void;
  copyLog: () => void;
  close: () => void;
  sendDragMove: () => void;
}

export function useStartupBridge(): StartupBridgeState & StartupBridgeActions {
  const [phase, setPhase] = useState<BootPhase>('boot');
  const [progress, setProgress] = useState(0);
  const [statusText, setStatusText] = useState('正在初始化...');
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [version, setVersion] = useState('v0.0.0');
  const [primaryColor, setPrimaryColor] = useState('#5DC8E8');
  const [isCompleted, setIsCompleted] = useState(false);
  const [isFailed, setIsFailed] = useState(false);

  const logIdCounter = useRef(0);
  // 用 ref 持有最新 phase，避免 handleMessage 的 useCallback 依赖 phase 导致每次
  // phase 切换都重建 handler → 重新 addEventListener/removeEventListener 的抖动
  const phaseRef = useRef<BootPhase>('boot');
  phaseRef.current = phase;

  const [backgroundColor, setBackgroundColor] = useState('#081420');

  const sendEvent = useCallback((action: string, payload: unknown = {}) => {
    const bridge = (window as any).__msmc_bridge__;
    if (bridge?.sendEvent) {
      bridge.sendEvent(action, payload);
    }
  }, []);

  const addLog = useCallback((message: string, type: LogEntry['type'] = 'info') => {
    setLogs((prev) => {
      const entry: LogEntry = {
        id: logIdCounter.current++,
        message,
        type,
        timestamp: Date.now(),
      };
      const next = [...prev, entry];
      if (next.length > 200) next.shift();
      return next;
    });
  }, []);

  const handleMessage = useCallback((rawEvent: any) => {
    try {
      const data = rawEvent?.data;
      if (!data || !data.action) return;

      switch (data.action) {
        case 'startup:init': {
          const payload = data.payload as InitPayload;
          setVersion(payload.version ? `v${payload.version}` : 'v0.0.0');
          setPrimaryColor(payload.primaryColor || '#5DC8E8');
          if (payload.backgroundColor) setBackgroundColor(payload.backgroundColor);
          break;
        }

        case 'startup:progress': {
          const payload = data.payload as ProgressPayload;
          const pct = Math.max(0, Math.min(100, payload.percent));
          setProgress(pct);
          setStatusText(payload.status || '');
          if (pct > 0 && phaseRef.current === 'boot') {
            setPhase('running');
          }
          break;
        }

        case 'startup:log': {
          const payload = data.payload as LogPayload;
          let type: LogEntry['type'] = 'info';
          if (payload.isError) type = 'error';
          else if (payload.isSuccess) type = 'ok';
          else if (payload.message?.toLowerCase().includes('warn')) type = 'warn';
          else if (payload.message?.toLowerCase().includes('error')) type = 'error';
          else if (payload.message?.toLowerCase().includes('ok') || payload.message?.toLowerCase().includes('success')) type = 'ok';

          addLog(payload.message || '', type);

          if (type === 'error' && phaseRef.current !== 'error') {
            setPhase('warn');
          }
          break;
        }

        case 'startup:completed': {
          setPhase('success');
          setIsCompleted(true);
          setStatusText(data.payload?.message || '初始化完成');
          setProgress(100);
          break;
        }

        case 'startup:failed': {
          setPhase('error');
          setIsFailed(true);
          setStatusText(data.payload?.message || '启动失败');
          addLog(data.payload?.message || '启动失败', 'error');
          break;
        }

        case 'startup:themeChanged': {
          if (data.payload?.primaryColor) {
            setPrimaryColor(data.payload.primaryColor);
          }
          if (data.payload?.backgroundColor) {
            setBackgroundColor(data.payload.backgroundColor);
          }
          break;
        }
      }
    } catch (err) {
      console.error('[StartupBridge] 解析事件失败:', err, rawEvent);
    }
  }, [addLog]);

  useEffect(() => {
    const handler = (event: any) => handleMessage(event);

    const webview = (window as any).chrome?.webview;
    if (webview?.addEventListener) {
      webview.addEventListener('message', handler);
    }

    const readyTimer = setTimeout(() => {
      sendEvent('startup:ready', { ts: Date.now() });
    }, 100);

    return () => {
      clearTimeout(readyTimer);
      if (webview?.removeEventListener) {
        webview.removeEventListener('message', handler);
      }
    };
  }, [handleMessage, sendEvent]);

  const restart = useCallback(() => {
    setPhase('boot');
    setProgress(0);
    setStatusText('正在重启...');
    setIsCompleted(false);
    setIsFailed(false);
    setLogs([]);
    logIdCounter.current = 0;
    sendEvent('startup:restart', {});
  }, [sendEvent]);

  const openTroubleshooting = useCallback(() => {
    sendEvent('startup:openTroubleshooting', {});
  }, [sendEvent]);

  const copyLog = useCallback(async () => {
    const text = logs
      .map((l) => `[${l.type.toUpperCase()}] ${new Date(l.timestamp).toISOString()} ${l.message}`)
      .join('\n');
    try {
      await navigator.clipboard.writeText(text);
    } catch (err) {
      console.warn('[StartupBridge] 复制日志失败:', err);
    }
  }, [logs]);

  const close = useCallback(() => {
    sendEvent('startup:close', {});
  }, [sendEvent]);

  const sendDragMove = useCallback(() => {
    sendEvent('startup:dragMove', {});
  }, [sendEvent]);

  return {
    phase,
    progress,
    statusText,
    logs,
    version,
    primaryColor,
    backgroundColor,
    isCompleted,
    isFailed,
    restart,
    openTroubleshooting,
    copyLog,
    close,
    sendDragMove,
  };
}
