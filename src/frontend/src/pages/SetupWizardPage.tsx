import { WizardShell } from '@/components/wizard/WizardShell'
import { Step1CorePicker } from '@/components/wizard/Step1CorePicker'
import { useWizardStore } from '@/stores/wizardStore'

export function SetupWizardPage() {
  const { currentStep } = useWizardStore()

  const renderStep = () => {
    switch (currentStep) {
      case 0:
        return <Step1CorePicker />
      default:
        return (
          <div
            className="flex items-center justify-center h-64 rounded-lg"
            style={{
              backgroundColor: 'var(--md-card-background)',
              border: '1px solid var(--md-card-subtle-border)',
              color: 'var(--md-body-light)',
            }}
          >
            <div className="text-center">
              <div className="text-4xl mb-3">🛠️</div>
              <div className="font-semibold mb-1" style={{ color: 'var(--md-body)' }}>
                步骤 {currentStep + 1} 即将推出
              </div>
              <div className="text-xs">
                后续步骤将在 Task5 中实现：版本选择 / 内存分配 / 基础设置 / 完成页
              </div>
            </div>
          </div>
        )
    }
  }

  return <WizardShell onComplete={() => {}}>{renderStep()}</WizardShell>
}
