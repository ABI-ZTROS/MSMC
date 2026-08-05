import { useEffect, useRef, useState } from 'react'
import { useWizardStore } from '@/stores/wizardStore'
import { useToastStore } from '@/stores/toastStore'
import {
  downloadCore,
  onCoreDownloadProgress,
  onCoreDownloadCompleted,
} from '@/utils/bridge'
import type {
  CoreDownloadProgress,
  CoreDownloadCompleted,
} from '@/types/bridge'
import { CORE_CATALOG } from '@/types/wizard'

function formatBytes(bytes: number): string {
  if (bytes <= 0) return '0 MB'
  const mb = bytes / (1024 * 1024)
  if (mb >= 1024) {
    return `${(mb / 1024).toFixed(2)} GB`
  }
  return `${mb.toFixed(2)} MB`
}

export function Step5Complete() {
  const {
    selectedCore,
    selectedVersion,
    memoryMB,
    serverName,
    port,
    onlineMode,
  } = useWizardStore()
  const { showToast } = useToastStore()

  const [progress, setProgress] = useState<CoreDownloadProgress | null>(null)
  const [completed, setCompleted] = useState<CoreDownloadCompleted | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [isDownloading, setIsDownloading] = useState(false)
  const startedRef = useRef(false)

  const coreMeta = CORE_CATALOG.find((c) => c.key === selectedCore)
  const coreDisplayName = coreMeta?.name ?? (selectedCore ?? '未知')
  const coreLogo = coreMeta?.logo ?? '📦'

  useEffect(() => {
    if (startedRef.current) return
    if (!selectedCore || !selectedVersion) return

    startedRef.current = true
    setIsDownloading(true)
    setErrorMsg(null)

    let offProgress: (() => void) | null = null
    let offCompleted: (() => void) | null = null

    try {
      offProgress = onCoreDownloadProgress((p) => {
        setProgress(p)
      })
    } catch {
      // ignore
    }

    try {
      offCompleted = onCoreDownloadCompleted((c) => {
        setCompleted(c)
        setIsDownloading(false)
        if (c.error) {
          setErrorMsg(c.error)
        }
      })
    } catch {
      // ignore
    }

    downloadCore(selectedCore, selectedVersion, './servers', `${selectedCore}-${selectedVersion}.jar`)
      .then((res) => {
        if (!res.success) {
          setErrorMsg(res.error ?? '下载失败')
          setIsDownloading(false)
        } else {
          setCompleted({
            id: 'fallback',
            savedPath: res.savedPath ?? '',
            hashVerified: res.hashVerified ?? false,
            elapsedMs: 0,
          })
          setIsDownloading(false)
        }
      })
      .catch((err) => {
        setErrorMsg(err?.message ?? '下载出错')
        setIsDownloading(false)
      })

    return () => {
      offProgress?.()
      offCompleted?.()
    }
  }, [selectedCore, selectedVersion])

  const handleLaunch = () => {
    showToast('🎉 服务器准备启动！正在返回 Dashboard...', 'success', 3000)
    setTimeout(() => {
      window.location.hash = '#/'
    }, 1200)
  }

  const pct = progress?.pct ?? 0
  const displayPct = completed ? 100 : pct

  return (
    <div className="w-full">
      <div
        className="md-card p-8 text-center mb-6"
        style={{
          backgroundColor:
            'color-mix(in srgb, var(--md-success, #22c55e) 8%, var(--md-card-background))',
          border: '1px solid color-mix(in srgb, var(--md-success, #22c55e) 30%, var(--md-card-subtle-border))',
        }}
      >
        <div className="text-5xl mb-3">🎉</div>
        <h2
          className="text-2xl font-bold mb-2"
          style={{ color: 'var(--md-success-light, #86efac)' }}
        >
          服务器准备就绪！
        </h2>
        <p className="text-sm" style={{ color: 'var(--md-body-light)' }}>
          所有配置已保存，正在下载核心文件，完成后即可启动服务器。
        </p>
      </div>

      <div
        className="md-card p-6 mb-6"
        style={{
          border: '1px solid var(--md-card-subtle-border)',
        }}
      >
        <h3
          className="text-sm font-bold mb-4 flex items-center gap-2"
          style={{ color: 'var(--md-body)' }}
        >
          <span>📋</span> 配置摘要
        </h3>
        <div
          className="grid gap-3"
          style={{ gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' }}
        >
          {[
            {
              icon: coreLogo,
              label: '核心类型',
              value: (
                <span>
                  {coreDisplayName}
                  <span
                    className="ml-2 text-[11px] px-2 py-[2px] rounded"
                    style={{
                      backgroundColor: 'var(--md-card-hover)',
                      color: 'var(--md-body-lighter)',
                    }}
                  >
                    {selectedVersion}
                  </span>
                </span>
              ),
            },
            {
              icon: '💾',
              label: '分配内存',
              value: `${memoryMB} MB (-Xmx)`,
            },
            {
              icon: '🏷️',
              label: '服务器名',
              value: serverName || 'Minecraft Server',
            },
            {
              icon: '🔌',
              label: '端口',
              value: String(port),
            },
            {
              icon: onlineMode ? '✅' : '🔓',
              label: '正版验证',
              value: (
                <span
                  style={{
                    color: onlineMode
                      ? 'var(--md-success-light, #86efac)'
                      : 'var(--md-warning, #fbbf24)',
                    fontWeight: 600,
                  }}
                >
                  {onlineMode ? '开启' : '关闭'}
                </span>
              ),
            },
          ].map((item, idx) => (
            <div
              key={idx}
              className="flex items-center gap-3 p-3 rounded-lg"
              style={{
                backgroundColor: 'var(--md-card-hover)',
                border: '1px solid var(--md-card-subtle-border)',
              }}
            >
              <div
                className="flex-shrink-0 w-9 h-9 rounded-lg flex items-center justify-center text-lg"
                style={{
                  backgroundColor: 'var(--md-card-background)',
                  border: '1px solid var(--md-card-subtle-border)',
                }}
              >
                {item.icon}
              </div>
              <div className="flex-1 min-w-0">
                <div
                  className="text-[10px] font-semibold tracking-wider mb-0.5"
                  style={{ color: 'var(--md-body-lighter)' }}
                >
                  {item.label}
                </div>
                <div
                  className="text-sm font-semibold"
                  style={{ color: 'var(--md-body)' }}
                >
                  {item.value}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div
        className="md-card p-6 mb-6"
        style={{
          border: '1px solid var(--md-card-subtle-border)',
        }}
      >
        <h3
          className="text-sm font-bold mb-4 flex items-center gap-2"
          style={{ color: 'var(--md-body)' }}
        >
          <span>⬇️</span> 核心下载进度
        </h3>

        {errorMsg && !completed && (
          <div
            className="mb-4 p-3 rounded-lg text-xs flex items-start gap-2"
            style={{
              backgroundColor:
                'color-mix(in srgb, var(--md-danger, #ef4444) 12%, transparent)',
              color: 'var(--md-danger-light, #fca5a5)',
              border: '1px solid color-mix(in srgb, var(--md-danger, #ef4444) 30%, transparent)',
            }}
          >
            <span className="mt-[1px]">❌</span>
            <div>
              <div className="font-semibold mb-0.5">下载遇到问题</div>
              <div style={{ color: 'var(--md-body-lighter)' }}>{errorMsg}</div>
            </div>
          </div>
        )}

        <div
          className="h-3 w-full rounded-full overflow-hidden mb-3"
          style={{ backgroundColor: 'var(--md-card-hover)' }}
        >
          <div
            className="h-full transition-all duration-300 rounded-full"
            style={{
              width: `${displayPct}%`,
              backgroundColor: completed
                ? 'var(--md-success, #22c55e)'
                : isDownloading
                  ? 'var(--md-primary-hue-mid)'
                  : 'var(--md-warning, #f59e0b)',
            }}
          />
        </div>

        <div className="flex items-center justify-between mb-2">
          <div
            className="text-xs flex items-center gap-2"
            style={{ color: 'var(--md-body-light)' }}
          >
            {isDownloading && (
              <span className="inline-block w-2 h-2 rounded-full animate-pulse bg-[var(--md-primary-hue-mid)]" />
            )}
            {completed && !errorMsg && (
              <span className="text-[var(--md-success-light, #86efac)] font-semibold">
                ✅ 下载完成
              </span>
            )}
            {!isDownloading && !completed && !errorMsg && (
              <span className="text-[var(--md-warning, #fbbf24)]">⏳ 等待开始</span>
            )}
            {isDownloading && <span>正在下载 {coreDisplayName} {selectedVersion}...</span>}
          </div>
          <div
            className="text-sm font-bold"
            style={{
              color: completed
                ? 'var(--md-success-light, #86efac)'
                : 'var(--md-body)',
            }}
          >
            {displayPct.toFixed(1)}%
          </div>
        </div>

        {(progress || completed) && (
          <div
            className="text-[11px] flex items-center gap-3"
            style={{ color: 'var(--md-body-lighter)' }}
          >
            <span>
              已下载:{' '}
              <b style={{ color: 'var(--md-body-light)' }}>
                {formatBytes(
                  completed
                    ? progress?.total ?? 0
                    : progress?.downloaded ?? 0,
                )}
              </b>
            </span>
            {progress?.total && progress.total > 0 && (
              <span>
                / 总计:{' '}
                <b style={{ color: 'var(--md-body-light)' }}>
                  {formatBytes(progress.total)}
                </b>
              </span>
            )}
            {completed && (
              <span
                style={{
                  color: completed.hashVerified
                    ? 'var(--md-success-light, #86efac)'
                    : 'var(--md-warning, #fbbf24)',
                }}
              >
                哈希校验: {completed.hashVerified ? '✅ 已通过' : '⚠️ 未校验'}
              </span>
            )}
          </div>
        )}
      </div>

      <button
        type="button"
        onClick={handleLaunch}
        className="w-full md-btn md-btn-primary py-4 text-base font-bold flex items-center justify-center gap-3"
      >
        <span className="text-xl">🚀</span>
        现在就启动服务器
      </button>

      <div
        className="mt-4 text-center text-[11px]"
        style={{ color: 'var(--md-body-lighter)' }}
      >
        启动后会跳转到 Dashboard 面板，你可以在那里查看实时日志和玩家列表。
      </div>
    </div>
  )
}
