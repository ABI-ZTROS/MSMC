import { clsx } from 'clsx'
import { useWizardStore } from '@/stores/wizardStore'
import { CORE_CATALOG, type CoreType, type CoreTag } from '@/types/wizard'

const TAG_STYLES: Record<CoreTag, { bg: string; color: string }> = {
  推荐: {
    bg: 'color-mix(in srgb, var(--md-primary-hue-mid) 18%, transparent)',
    color: 'var(--md-primary-hue-light)',
  },
  性能: {
    bg: 'color-mix(in srgb, var(--md-aquamarine-mid, #2dd4bf) 18%, transparent)',
    color: 'var(--md-aquamarine-light, #5eead4)',
  },
  模组: {
    bg: 'color-mix(in srgb, #a855f7 18%, transparent)',
    color: '#c4b5fd',
  },
  代理: {
    bg: 'color-mix(in srgb, #f59e0b 18%, transparent)',
    color: '#fcd34d',
  },
  原版: {
    bg: 'color-mix(in srgb, var(--md-body-light) 18%, transparent)',
    color: 'var(--md-body-light)',
  },
}

export function Step1CorePicker() {
  const { selectedCore, setSelectedCore } = useWizardStore()

  return (
    <div className="w-full">
      {/* 标题区 */}
      <div className="mb-6">
        <h2
          className="text-xl font-bold mb-2"
          style={{ color: 'var(--md-body)' }}
        >
          选择服务器核心
        </h2>
        <p
          className="text-sm"
          style={{ color: 'var(--md-body-light)' }}
        >
          核心决定了你的服务器能装什么（插件 / Mod / 代理），推荐新手从 Paper 开始。
        </p>
      </div>

      {/* 卡片网格：每行3个 */}
      <div
        className="grid gap-4"
        style={{
          gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
        }}
      >
        {CORE_CATALOG.map((core) => {
          const isSelected = selectedCore === core.key
          const tagStyle = TAG_STYLES[core.tag]
          return (
            <button
              key={core.key}
              type="button"
              onClick={() => setSelectedCore(core.key as CoreType)}
              className={clsx(
                'md-card md-card-hover p-4 text-left transition-all duration-200 cursor-pointer',
                'hover:scale-[1.02] active:scale-[0.99]'
              )}
              style={{
                borderWidth: isSelected ? '2px' : '1px',
                borderColor: isSelected
                  ? 'var(--md-primary-hue-mid)'
                  : 'var(--md-card-subtle-border)',
                boxShadow: isSelected
                  ? '0 0 0 3px color-mix(in srgb, var(--md-primary-hue-mid) 20%, transparent)'
                  : undefined,
                backgroundColor: isSelected
                  ? 'color-mix(in srgb, var(--md-primary-subtle-background) 40%, var(--md-card-background))'
                  : undefined,
              }}
            >
              <div className="flex items-start gap-3">
                {/* Logo */}
                <div
                  className="flex-shrink-0 w-12 h-12 rounded-lg flex items-center justify-center text-2xl"
                  style={{
                    backgroundColor: 'var(--md-card-hover)',
                    border: '1px solid var(--md-card-subtle-border)',
                  }}
                >
                  {core.logo}
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span
                      className="font-semibold text-sm"
                      style={{ color: 'var(--md-body)' }}
                    >
                      {core.name}
                    </span>
                    {/* Tag 小徽标 */}
                    <span
                      className="flex-shrink-0 px-2 py-[2px] rounded-md text-[10px] font-semibold"
                      style={{
                        backgroundColor: tagStyle.bg,
                        color: tagStyle.color,
                      }}
                    >
                      {core.tag}
                    </span>
                  </div>
                  <p
                    className="text-xs leading-relaxed"
                    style={{ color: 'var(--md-body-light)' }}
                  >
                    {core.desc}
                  </p>
                </div>
              </div>

              {/* 选中指示器 */}
              {isSelected && (
                <div
                  className="mt-3 pt-3 flex items-center gap-2 text-xs font-semibold"
                  style={{
                    borderTop: '1px dashed var(--md-card-subtle-border)',
                    color: 'var(--md-primary-hue-light)',
                  }}
                >
                  <span
                    className="w-2 h-2 rounded-full"
                    style={{ backgroundColor: 'var(--md-primary-hue-mid)' }}
                  />
                  已选择此核心
                </div>
              )}
            </button>
          )
        })}
      </div>

      {/* 底部提示 */}
      {!selectedCore && (
        <div
          className="mt-6 p-3 rounded-lg text-xs flex items-center gap-2"
          style={{
            backgroundColor: 'var(--md-primary-subtle-background)',
            color: 'var(--md-body-light)',
            border: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <span>💡</span>
          <span>
            不确定选什么？推荐 <b style={{ color: 'var(--md-primary-hue-light)' }}>Paper</b> —— 90% 的插件服都用它，生态最成熟。
          </span>
        </div>
      )}
    </div>
  )
}
