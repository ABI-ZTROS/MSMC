export type BridgeMessageType = 'request' | 'response' | 'event' | 'log'

export interface BridgeMessage {
  type: BridgeMessageType
  id?: string
  action: string
  payload?: unknown
  error?: string
  success?: boolean
  timestamp?: number
}

export interface AppInfo {
  version: string
  name: string
  fullName: string
}

export interface ThemeInfo {
  mode: 'light' | 'dark'
  primaryColor: string
}

export interface AppReadyEvent {
  version: string
  isAdmin: boolean
  theme: ThemeInfo
  statusMessage?: string
}

export interface StatusUpdateEvent {
  message: string
}
