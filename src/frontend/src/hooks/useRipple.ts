import { useCallback } from 'react'

export function useRipple() {
  return useCallback((e: React.MouseEvent<HTMLElement>) => {
    const target = e.currentTarget
    const rect = target.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * 100
    const y = ((e.clientY - rect.top) / rect.height) * 100
    target.style.setProperty('--ripple-x', `${x}%`)
    target.style.setProperty('--ripple-y', `${y}%`)
  }, [])
}
