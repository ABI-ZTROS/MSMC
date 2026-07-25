import { useEffect, useState } from 'react'
import { FiCheck, FiX, FiAlertTriangle, FiInfo } from 'react-icons/fi'
import { useToastStore, type ToastItem as ToastItemType } from '@/stores/toastStore'

const typeStyles: Record<ToastItemType['type'], { bg: string; border: string; icon: string; iconColor: string }> = {
  success: {
    bg: 'var(--md-success-subtle-background)',
    border: 'rgba(76, 175, 80, 0.3)',
    icon: 'var(--md-gauge-green)',
    iconColor: 'var(--md-gauge-green)',
  },
  error: {
    bg: 'var(--md-danger-subtle-background)',
    border: 'rgba(244, 54, 76, 0.3)',
    icon: 'var(--md-gauge-red)',
    iconColor: 'var(--md-error-text)',
  },
  warning: {
    bg: 'var(--md-warning-subtle-background)',
    border: 'rgba(255, 193, 7, 0.3)',
    icon: 'var(--md-gauge-yellow)',
    iconColor: 'var(--md-gauge-yellow)',
  },
  info: {
    bg: 'var(--md-primary-subtle-background)',
    border: 'var(--md-primary-subtle-border)',
    icon: 'var(--md-primary-hue-mid)',
    iconColor: 'var(--md-primary-hue-light)',
  },
}

const ToastIcon = ({ type }: { type: ToastItemType['type'] }) => {
  const color = typeStyles[type].iconColor
  const size = 18

  switch (type) {
    case 'success':
      return <FiCheck size={size} color={color} />
    case 'error':
      return <FiX size={size} color={color} />
    case 'warning':
      return <FiAlertTriangle size={size} color={color} />
    case 'info':
      return <FiInfo size={size} color={color} />
  }
}

interface ToastItemProps {
  toast: ToastItemType
  onClose: () => void
}

const ToastItem = ({ toast, onClose }: ToastItemProps) => {
  const [isVisible, setIsVisible] = useState(false)
  const [isLeaving, setIsLeaving] = useState(false)

  useEffect(() => {
    const timer = setTimeout(() => setIsVisible(true), 10)
    return () => clearTimeout(timer)
  }, [])

  useEffect(() => {
    if (toast.duration && toast.duration > 0) {
      const timer = setTimeout(() => {
        handleClose()
      }, toast.duration)
      return () => clearTimeout(timer)
    }
  }, [toast.duration])

  const handleClose = () => {
    setIsLeaving(true)
    setTimeout(() => {
      onClose()
    }, 250)
  }

  const style = typeStyles[toast.type]

  return (
    <div
      onClick={handleClose}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--md-spacing-3)',
        padding: 'var(--md-spacing-3) var(--md-spacing-4)',
        backgroundColor: style.bg,
        border: `1px solid ${style.border}`,
        borderRadius: 'var(--md-radius)',
        boxShadow: 'var(--md-shadow-3)',
        minWidth: '280px',
        maxWidth: '400px',
        cursor: 'pointer',
        opacity: isLeaving ? 0 : isVisible ? 1 : 0,
        transform: isLeaving
          ? 'translateX(100%)'
          : isVisible
          ? 'translateX(0)'
          : 'translateX(100%)',
        transition: 'opacity 250ms var(--md-ease-standard), transform 250ms var(--md-ease-standard)',
        backdropFilter: 'blur(8px)',
        userSelect: 'none',
      }}
    >
      <div style={{ flexShrink: 0, display: 'flex', alignItems: 'center' }}>
        <ToastIcon type={toast.type} />
      </div>
      <span
        style={{
          flex: 1,
          color: 'var(--md-body)',
          fontSize: 'var(--md-font-size-base)',
          lineHeight: 1.4,
          wordBreak: 'break-word',
        }}
      >
        {toast.message}
      </span>
      <button
        onClick={(e) => {
          e.stopPropagation()
          handleClose()
        }}
        style={{
          flexShrink: 0,
          background: 'none',
          border: 'none',
          padding: '4px',
          margin: '-4px',
          cursor: 'pointer',
          color: 'var(--md-body-lighter)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          borderRadius: 'var(--md-radius-small)',
          transition: 'background-color 150ms, color 150ms',
        }}
        onMouseEnter={(e) => {
          e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.1)'
          e.currentTarget.style.color = 'var(--md-body)'
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.backgroundColor = 'transparent'
          e.currentTarget.style.color = 'var(--md-body-lighter)'
        }}
      >
        <FiX size={16} />
      </button>
    </div>
  )
}

export const ToastContainer = () => {
  const toasts = useToastStore((s) => s.toasts)
  const removeToast = useToastStore((s) => s.removeToast)

  return (
    <div
      style={{
        position: 'fixed',
        top: 'var(--md-spacing-4)',
        right: 'var(--md-spacing-4)',
        zIndex: 9999,
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--md-spacing-2)',
        pointerEvents: 'none',
      }}
    >
      {toasts.map((toast) => (
        <div key={toast.id} style={{ pointerEvents: 'auto' }}>
          <ToastItem toast={toast} onClose={() => removeToast(toast.id)} />
        </div>
      ))}
    </div>
  )
}
