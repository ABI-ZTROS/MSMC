import { create } from 'zustand'
import type { ThemeInfo } from '@/types/bridge'
import { argbToRgb } from '@/utils/theme'

interface AppState {
  isReady: boolean
  version: string
  isAdmin: boolean
  theme: ThemeInfo
  statusMessage: string
  sidebarCollapsed: boolean

  setReady: (ready: boolean) => void
  setVersion: (version: string) => void
  setAdmin: (isAdmin: boolean) => void
  setTheme: (theme: ThemeInfo) => void
  setStatusMessage: (message: string) => void
  toggleSidebar: () => void
  setSidebarCollapsed: (collapsed: boolean) => void
}

export const useAppStore = create<AppState>((set) => ({
  isReady: false,
  version: '0.0.0',
  isAdmin: false,
  theme: {
    mode: 'dark',
    primaryColor: '#3b82f6',
  },
  statusMessage: '就绪',
  sidebarCollapsed: false,

  setReady: (ready) => set({ isReady: ready }),
  setVersion: (version) => set({ version }),
  setAdmin: (isAdmin) => set({ isAdmin }),
  setTheme: (theme) => {
    if (theme.mode === 'dark') {
      document.documentElement.classList.add('dark')
    } else {
      document.documentElement.classList.remove('dark')
    }
    if (theme.primaryColor) {
      document.documentElement.style.setProperty('--md-primary-hue-mid', argbToRgb(theme.primaryColor))
    }
    set({ theme })
  },
  setStatusMessage: (message) => set({ statusMessage: message }),
  toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
  setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
}))
