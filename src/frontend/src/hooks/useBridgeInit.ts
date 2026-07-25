import { useEffect } from 'react'
import { useAppStore } from '@/stores/appStore'
import { bridge, onStatusUpdate, getSettings } from '@/utils/bridge'
import type { AppReadyEvent } from '@/types/bridge'
import { applySettingsToCss } from '@/utils/theme'

// 简单的日志函数，同时输出到 console 和 C# 日志
function log(msg: string): void {
  console.log('[useBridgeInit]', msg)
  if (typeof window !== 'undefined' && (window as any).chrome?.webview) {
    try {
      ;(window as any).chrome.webview.postMessage({
        type: 'log',
        action: 'log',
        payload: `[JS-useBridgeInit] ${msg}`,
        timestamp: Date.now(),
      })
    } catch {
      // ignore
    }
  }
}

export function useBridgeInit(): void {
  const setReady = useAppStore((s) => s.setReady)
  const setVersion = useAppStore((s) => s.setVersion)
  const setAdmin = useAppStore((s) => s.setAdmin)
  const setTheme = useAppStore((s) => s.setTheme)
  const setStatusMessage = useAppStore((s) => s.setStatusMessage)

  useEffect(() => {
    let cancelled = false
    let retryTimer: number | null = null

    log('useEffect 执行，开始初始化桥接')

    async function init(): Promise<void> {
      log('init() 开始')

      try {
        log('调用 bridge.invoke(app:getReadyState)...')
        const data = await bridge.invoke<AppReadyEvent>('app:getReadyState')
        log(`收到响应: version=${data.version}, isAdmin=${data.isAdmin}, theme=${data.theme.mode}`)

        if (cancelled) {
          log('已取消，丢弃响应')
          return
        }

        setVersion(data.version)
        setAdmin(data.isAdmin)
        setTheme(data.theme)
        setStatusMessage(data.statusMessage ?? '')
        setReady(true)

        try {
          const settings = await getSettings()
          applySettingsToCss(settings)
          log('✅ 设置已应用到 CSS')
        } catch (e) {
          log(`⚠️ 获取设置失败: ${e}`)
        }

        log('✅ 应用初始化完成，isReady = true')
      } catch (e) {
        log(`❌ 获取就绪状态失败: ${e}`)
        // 失败后重试，最多 10 次
        let retries = 0
        const retry = () => {
          retries++
          if (retries > 10 || cancelled) {
            log(`❌ 重试 ${retries - 1} 次后放弃`)
            return
          }
          log(`🔄 第 ${retries} 次重试...`)
          bridge
            .invoke<AppReadyEvent>('app:getReadyState')
            .then(async (data) => {
              if (cancelled) return
              setVersion(data.version)
              setAdmin(data.isAdmin)
              setTheme(data.theme)
              setStatusMessage(data.statusMessage ?? '')
              setReady(true)

              try {
                const settings = await getSettings()
                applySettingsToCss(settings)
              } catch {
                // ignore
              }

              log(`✅ 第 ${retries} 次重试成功`)
            })
            .catch(() => {
              retryTimer = window.setTimeout(retry, 500)
            })
        }
        retryTimer = window.setTimeout(retry, 500)
      }
    }

    init()

    const offStatus = onStatusUpdate((data) => {
      log(`收到状态更新: ${data.message}`)
      setStatusMessage(data.message)
    })

    return () => {
      log('useEffect 清理')
      cancelled = true
      if (retryTimer) clearTimeout(retryTimer)
      offStatus()
    }
  }, [setReady, setVersion, setAdmin, setTheme, setStatusMessage])
}
