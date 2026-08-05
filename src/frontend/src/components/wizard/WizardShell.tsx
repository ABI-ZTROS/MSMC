import { type ReactNode } from 'react'
import { clsx } from 'clsx'
import {
  FaChevronLeft,
  FaChevronRight,
  FaCheck,
} from 'react-icons/fa6'
import { useWizardStore } from '@/stores/wizardStore'

const STEP_LABELS = ['选择核心', '选择版本', '分配内存', '基础设置', '完成']

interface WizardShellProps {
  children: ReactNode
  onComplete?: () => void
}

export function WizardShell({ children, onComplete }: WizardShellProps) {
  const {
    currentStep,
    totalSteps,
    nextStep,
    prevStep,
    selectedCore,
    selectedVersion,
    eulaAccepted,
  } = useWizardStore()
  const isFirst = currentStep === 0
  const isLast = currentStep === totalSteps - 1

  const canGoNext = (() => {
    switch (currentStep) {
      case 0:
        return !!selectedCore
      case 1:
        return !!selectedVersion
      case 2:
        return true
      case 3:
        return !!eulaAccepted
      default:
        return true
    }
  })()

  const handlePrimary = () => {
    if (isLast) {
      onComplete?.()
    } else {
      if (!canGoNext) return
      nextStep()
    }
  }

  return (
    <div
      className="w-full h-full flex flex-col"
      style={{ backgroundColor: 'var(--md-deep-background)' }}
    >
      {/* 顶部进度条区域 */}
      <div
        className="flex-shrink-0 px-8 pt-6 pb-4"
        style={{
          borderBottom: '1px solid var(--md-card-subtle-border)',
        }}
      >
        <div className="max-w-4xl mx-auto">
          {/* 步骤圆点 + 连接线 */}
          <div className="flex items-center justify-between mb-4">
            {STEP_LABELS.map((label, idx) => {
              const isDone = idx < currentStep
              const isActive = idx === currentStep
              return (
                <div key={label} className="flex items-center flex-1 last:flex-none">
                  <div className="flex flex-col items-center gap-2">
                    <div
                      className={clsx(
                        'w-9 h-9 rounded-full flex items-center justify-center text-sm font-semibold transition-all duration-200',
                        isDone && 'md-btn-primary',
                        isActive && !isDone && 'md-btn-outlined',
                        !isActive && !isDone &&
                          'border border-[var(--md-card-subtle-border)] bg-[var(--md-card-background)] text-[var(--md-body-light)]'
                      )}
                      style={{
                        color: isDone
                          ? 'white'
                          : isActive
                            ? 'var(--md-nav-item-selected)'
                            : undefined,
                      }}
                    >
                      {isDone ? <FaCheck size={14} /> : idx + 1}
                    </div>
                    <div
                      className={clsx(
                        'text-[11px] whitespace-nowrap transition-colors',
                        isActive
                          ? 'text-[var(--md-body)] font-semibold'
                          : isDone
                            ? 'text-[var(--md-body-light)]'
                            : 'text-[var(--md-body-lighter)]'
                      )}
                    >
                      {label}
                    </div>
                  </div>
                  {idx < STEP_LABELS.length - 1 && (
                    <div
                      className="flex-1 mx-2 h-[2px] rounded-full overflow-hidden"
                      style={{ backgroundColor: 'var(--md-card-hover)' }}
                    >
                      <div
                        className="h-full transition-all duration-300"
                        style={{
                          width: idx < currentStep ? '100%' : '0%',
                          backgroundColor: 'var(--md-primary-hue-mid)',
                        }}
                      />
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      </div>

      {/* 内容区 */}
      <div className="flex-1 overflow-y-auto px-8 py-6">
        <div className="max-w-4xl mx-auto">{children}</div>
      </div>

      {/* 底部按钮区 */}
      <div
        className="flex-shrink-0 px-8 py-4 flex items-center justify-between"
        style={{
          borderTop: '1px solid var(--md-card-subtle-border)',
          backgroundColor: 'var(--md-card-background)',
        }}
      >
        <div className="max-w-4xl mx-auto w-full flex items-center justify-between">
          <button
            className="md-btn md-btn-outlined flex items-center gap-2"
            disabled={isFirst}
            onClick={prevStep}
          >
            <FaChevronLeft size={14} />
            上一步
          </button>

          <div
            className="text-xs"
            style={{ color: 'var(--md-body-light)' }}
          >
            {currentStep + 1} / {totalSteps}
          </div>

          <button
            className={clsx(
              'md-btn flex items-center gap-2',
              isLast ? 'md-btn-primary' : 'md-btn-primary'
            )}
            onClick={handlePrimary}
          >
            {isLast ? (
              <>
                <FaCheck size={14} />
                完成
              </>
            ) : (
              <>
                下一步
                <FaChevronRight size={14} />
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  )
}
