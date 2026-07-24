import { useEffect } from 'react'
import { useAppStore } from '@/stores/appStore'
import { onAppReady, onStatusUpdate } from '@/utils/bridge'
import type { AppReadyEvent } from '@/types/bridge'

export function useBridgeInit(): void {
  const setReady = useAppStore((s) => s.setReady)
  const setVersion = useAppStore((s) => s.setVersion)
  const setAdmin = useAppStore((s) => s.setAdmin)
  const setTheme = useAppStore((s) => s.setTheme)
  const setStatusMessage = useAppStore((s) => s.setStatusMessage)

  useEffect(() => {
    const offReady = onAppReady((data: AppReadyEvent) => {
      setVersion(data.version)
      setAdmin(data.isAdmin)
      setTheme(data.theme)
      setReady(true)
    })

    const offStatus = onStatusUpdate((data) => {
      setStatusMessage(data.message)
    })

    return () => {
      offReady()
      offStatus()
    }
  }, [setReady, setVersion, setAdmin, setTheme, setStatusMessage])
}
