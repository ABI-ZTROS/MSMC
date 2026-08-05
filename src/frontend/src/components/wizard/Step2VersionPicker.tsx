import { useEffect, useState } from 'react'
import { clsx } from 'clsx'
import { useWizardStore } from '@/stores/wizardStore'
import { listCoreVersions } from '@/utils/bridge'

const FALLBACK_VERSIONS = ['1.21.1', '1.21', '1.20.6', '1.20.4', '1.20.1', '1.19.4']

export function Step2VersionPicker() {
  const { selectedCore, selectedVersion, setSelectedVersion } = useWizardStore()
  const [versions, setVersions] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setErrorMsg(null)

    if (!selectedCore) {
      setVersions(FALLBACK_VERSIONS)
      setLoading(false)
      return
    }

    listCoreVersions(selectedCore)
      .then((res) => {
        if (cancelled) return
        if (res.success && res.versions && res.versions.length > 0) {
          setVersions(res.versions)
        } else {
          setVersions(FALLBACK_VERSIONS)
          if (res.error) setErrorMsg(res.error)
        }
      })
      .catch((err) => {
        if (cancelled) return
        setVersions(FALLBACK_VERSIONS)
        setErrorMsg(err?.message ?? '加载失败，使用本地版本列表')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [selectedCore])

  const displayVersions = versions.length > 0 ? versions : FALLBACK_VERSIONS

  return (
    <div className="w-full">
      <div className="mb-6">
        <h2 className="text-xl font-bold mb-2" style={{ color: 'var(--md-body)' }}>
          选择版本
        </h2>
        <p className="text-sm" style={{ color: 'var(--md-body-light)' }}>
          选择你要运行的 Minecraft 版本。推荐使用最新稳定版以获得最佳体验和安全更新。
        </p>
      </div>

      {loading && (
        <div
          className="mb-4 p-3 rounded-lg text-xs flex items-center gap-2"
          style={{
            backgroundColor: 'var(--md-card-hover)',
            color: 'var(--md-body-light)',
            border: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <span className="animate-pulse">⏳</span>
          <span>正在获取版本列表...</span>
        </div>
      )}

      {!loading && errorMsg && (
        <div
          className="mb-4 p-3 rounded-lg text-xs flex items-center gap-2"
          style={{
            backgroundColor: 'color-mix(in srgb, var(--md-warning, #f59e0b) 12%, transparent)',
            color: 'var(--md-warning, #fbbf24)',
            border: '1px solid color-mix(in srgb, var(--md-warning, #f59e0b) 30%, transparent)',
          }}
        >
          <span>⚠️</span>
          <span>{errorMsg}，已切换到本地推荐版本</span>
        </div>
      )}

      <div
        className="grid gap-3"
        style={{
          gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
        }}
      >
        {displayVersions.map((ver, idx) => {
          const isSelected = selectedVersion === ver
          const isRecommended = idx === 0
          return (
            <button
              key={ver}
              type="button"
              onClick={() => setSelectedVersion(ver)}
              className={clsx(
                'md-card md-card-hover p-4 text-center transition-all duration-200 cursor-pointer',
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
              <div
                className="font-bold text-lg mb-1"
                style={{ color: isSelected ? 'var(--md-primary-hue-light)' : 'var(--md-body)' }}
              >
                {ver}
              </div>
              {isRecommended && (
                <div
                  className="text-[10px] font-semibold px-2 py-[2px] rounded-md inline-block"
                  style={{
                    backgroundColor:
                      'color-mix(in srgb, var(--md-success, #22c55e) 15%, transparent)',
                    color: 'var(--md-success-light, #86efac)',
                  }}
                >
                  ✅ 推荐
                </div>
              )}
              {isSelected && !isRecommended && (
                <div
                  className="text-[10px] font-semibold px-2 py-[2px] rounded-md inline-block"
                  style={{
                    backgroundColor:
                      'color-mix(in srgb, var(--md-primary-hue-mid) 15%, transparent)',
                    color: 'var(--md-primary-hue-light)',
                  }}
                >
                  已选择
                </div>
              )}
            </button>
          )
        })}
      </div>

      {!selectedVersion && (
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
            选最新版（标 <b style={{ color: 'var(--md-success-light, #86efac)' }}>✅ 推荐</b>）即可，
            除非你有特定的 Mod / 插件只支持旧版本。
          </span>
        </div>
      )}
    </div>
  )
}
