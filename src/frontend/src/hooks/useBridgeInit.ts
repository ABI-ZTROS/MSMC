import { useEffect } from 'react'
import { useAppStore } from '@/stores/appStore'
import { bridge, onStatusUpdate } from '@/utils/bridge'
import type { AppReadyEvent } from '@/types/bridge'

export function useBridgeInit(): void {
  const setReady = useAppStore((s) => s.setReady)
  const setVersion = useAppStore((s) => s.setVersion)
  const setAdmin = useAppStore((s) => s.setAdmin)
  const setTheme = useAppStore((s) => s.setTheme)
  const setStatusMessage = useAppStore((s) => s.setStatusMessage)

  useEffect(() => {
    let cancelled = false

    async function init(): Promise<void> {
      try {
        // JS 端主动拉取就绪状态，避免 C# 端事件推送时序问题
        const data = await bridge.invoke<AppReadyEvent>('app:getReadyState')
        if (cancelled) return

        setVersion(data.version)
        setAdmin(data.isAdmin)
        setTheme(data.theme)
        setStatusMessage(data.statusMessage ?? '')
        setReady(true)

        console.log('[MSMC] 应用初始化完成')
      } catch (e) {
        console.error('[MSMC] 获取就绪状态失败:', e)
        // 失败后重试，最多 10 次
        let retries = 0
        const retry = setInterval(async () => {
          retries++
          if (retries > 10 || cancelled) {
            clearInterval(retry)
            return
          }
          try {
            const data = await bridge.invoke<AppReadyEvent>('app:getReadyState')
            if (cancelled) return
            setVersion(data.version)
            setAdmin(data.isAdmin)
            setTheme(data.theme)
            setStatusMessage(data.statusMessage ?? '')
            setReady(true)
            clearInterval(retry)
            console.log('[MSMC] 应用初始化完成（重试成功）')
          } catch {
            // 继续重试
          }
        }, 500)
      }
    }

    init()

    const offStatus = onStatusUpdate((data) => {
      setStatusMessage(data.message)
    })

    return () => {
      cancelled = true
      offStatus()
    }
  }, [setReady, setVersion, setAdmin, setTheme, setStatusMessage])
}
