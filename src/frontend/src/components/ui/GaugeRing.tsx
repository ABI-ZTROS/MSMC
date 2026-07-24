import { useEffect, useRef, useState } from 'react'
import { clsx } from 'clsx'

interface GaugeRingProps {
  value: number
  max?: number
  label?: string
  sublabel?: string
  size?: number
  strokeWidth?: number
  color?: 'primary' | 'success' | 'warning' | 'danger'
  showValue?: boolean
  unit?: string
  animated?: boolean
  className?: string
}

const colorMap = {
  primary: {
    stroke: '#3b82f6',
    glow: 'rgba(59, 130, 246, 0.4)',
  },
  success: {
    stroke: '#22c55e',
    glow: 'rgba(34, 197, 94, 0.4)',
  },
  warning: {
    stroke: '#f59e0b',
    glow: 'rgba(245, 158, 11, 0.4)',
  },
  danger: {
    stroke: '#ef4444',
    glow: 'rgba(239, 68, 68, 0.4)',
  },
}

export function GaugeRing({
  value,
  max = 100,
  label,
  sublabel,
  size = 160,
  strokeWidth = 10,
  color = 'primary',
  showValue = true,
  unit = '%',
  animated = true,
  className,
}: GaugeRingProps): JSX.Element {
  const [displayValue, setDisplayValue] = useState(0)
  const rafRef = useRef<number>()

  const radius = (size - strokeWidth) / 2
  const circumference = radius * 2 * Math.PI
  const clampedValue = Math.min(Math.max(value, 0), max)
  const progress = clampedValue / max
  const offset = circumference - progress * circumference

  useEffect(() => {
    if (!animated) {
      setDisplayValue(clampedValue)
      return
    }

    const startValue = displayValue
    const endValue = clampedValue
    const duration = 800
    const startTime = performance.now()

    const animate = (currentTime: number) => {
      const elapsed = currentTime - startTime
      const progress = Math.min(elapsed / duration, 1)
      const eased = 1 - Math.pow(1 - progress, 3)
      const current = startValue + (endValue - startValue) * eased

      setDisplayValue(current)

      if (progress < 1) {
        rafRef.current = requestAnimationFrame(animate)
      }
    }

    rafRef.current = requestAnimationFrame(animate)

    return () => {
      if (rafRef.current) {
        cancelAnimationFrame(rafRef.current)
      }
    }
  }, [clampedValue, animated])

  const colors = colorMap[color]

  return (
    <div className={clsx('relative inline-flex items-center justify-center', className)}>
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        className="transform -rotate-90"
      >
        <defs>
          <filter id={`glow-${color}`} x="-50%" y="-50%" width="200%" height="200%">
            <feGaussianBlur stdDeviation="3" result="coloredBlur" />
            <feMerge>
              <feMergeNode in="coloredBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <linearGradient id={`gradient-${color}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={colors.stroke} stopOpacity="1" />
            <stop offset="100%" stopColor={colors.stroke} stopOpacity="0.7" />
          </linearGradient>
        </defs>

        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-slate-100 dark:text-slate-700/50"
        />

        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={`url(#gradient-${color})`}
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          filter={`url(#glow-${color})`}
          style={{
            transition: animated ? 'stroke-dashoffset 0.8s cubic-bezier(0.4, 0, 0.2, 1)' : 'none',
          }}
        />
      </svg>

      <div className="absolute inset-0 flex flex-col items-center justify-center">
        {showValue && (
          <>
            <span
              className="text-3xl font-bold number-animate text-slate-900 dark:text-slate-100"
              style={{ fontSize: size * 0.22 }}
            >
              {Math.round(displayValue)}
              <span className="text-lg font-medium text-slate-400 dark:text-slate-500 ml-0.5">
                {unit}
              </span>
            </span>
          </>
        )}
        {label && (
          <span
            className="text-slate-500 dark:text-slate-400 font-medium mt-1"
            style={{ fontSize: size * 0.085 }}
          >
            {label}
          </span>
        )}
        {sublabel && (
          <span
            className="text-slate-400 dark:text-slate-500 mt-0.5"
            style={{ fontSize: size * 0.07 }}
          >
            {sublabel}
          </span>
        )}
      </div>
    </div>
  )
}
