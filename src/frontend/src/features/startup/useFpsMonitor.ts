import { useEffect, useRef, useState } from 'react';

/**
 * 真实 FPS 监测（RAF 驱动，零 setInterval 开销）
 * 计算最近 60 帧的平均 FPS
 */
export function useFpsMonitor(enabled = true): number {
  const [fps, setFps] = useState(0);
  const frameTimes = useRef<number[]>([]);
  const rafId = useRef<number | null>(null);

  useEffect(() => {
    if (!enabled) return;

    let lastFrame = performance.now();

    const tick = (now: number) => {
      const delta = now - lastFrame;
      lastFrame = now;

      frameTimes.current.push(delta);
      if (frameTimes.current.length > 60) {
        frameTimes.current.shift();
      }

      const avgDelta = frameTimes.current.reduce((a, b) => a + b, 0) / frameTimes.current.length;
      const currentFps = Math.round(1000 / avgDelta);

      setFps((prev) => (Math.abs(prev - currentFps) >= 1 ? currentFps : prev));

      rafId.current = requestAnimationFrame(tick);
    };

    rafId.current = requestAnimationFrame(tick);
    return () => {
      if (rafId.current !== null) {
        cancelAnimationFrame(rafId.current);
      }
    };
  }, [enabled]);

  return fps;
}
