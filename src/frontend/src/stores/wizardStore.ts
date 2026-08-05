import { create } from 'zustand'
import type { CoreType } from '@/types/wizard'

interface WizardState {
  currentStep: number
  totalSteps: number

  selectedCore: CoreType | null
  selectedVersion: string | null
  memoryMB: number
  serverName: string
  port: number
  eulaAccepted: boolean
  onlineMode: boolean

  setStep: (step: number) => void
  nextStep: () => void
  prevStep: () => void
  resetWizard: () => void

  setSelectedCore: (core: CoreType | null) => void
  setSelectedVersion: (version: string | null) => void
  setMemoryMB: (mb: number) => void
  setServerName: (name: string) => void
  setPort: (port: number) => void
  setEulaAccepted: (accepted: boolean) => void
  setOnlineMode: (enabled: boolean) => void
}

export const useWizardStore = create<WizardState>((set) => ({
  currentStep: 0,
  totalSteps: 5,

  selectedCore: null,
  selectedVersion: null,
  memoryMB: 4096,
  serverName: '我的服务器',
  port: 25565,
  eulaAccepted: false,
  onlineMode: true,

  setStep: (step) => set({ currentStep: step }),
  nextStep: () => set((state) => ({
    currentStep: Math.min(state.currentStep + 1, state.totalSteps - 1),
  })),
  prevStep: () => set((state) => ({
    currentStep: Math.max(state.currentStep - 1, 0),
  })),
  resetWizard: () => set({
    currentStep: 0,
    selectedCore: null,
    selectedVersion: null,
    memoryMB: 4096,
    serverName: '我的服务器',
    port: 25565,
    eulaAccepted: false,
    onlineMode: true,
  }),

  setSelectedCore: (core) => set({ selectedCore: core }),
  setSelectedVersion: (version) => set({ selectedVersion: version }),
  setMemoryMB: (mb) => set({ memoryMB: mb }),
  setServerName: (name) => set({ serverName: name }),
  setPort: (port) => set({ port }),
  setEulaAccepted: (accepted) => set({ eulaAccepted: accepted }),
  setOnlineMode: (enabled) => set({ onlineMode: enabled }),
}))
