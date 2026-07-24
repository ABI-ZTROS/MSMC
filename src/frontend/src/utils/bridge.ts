import type { BridgeMessage, AppInfo, AppReadyEvent } from '@/types/bridge'

declare global {
  interface Window {
    __msmc_bridge__?: MsmcBridge
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void
        addEventListener: (event: string, handler: (event: { data: unknown }) => void) => void
      }
    }
  }
}

export interface MsmcBridge {
  invoke: <T = unknown>(action: string, payload?: unknown) => Promise<T>
  sendEvent: (action: string, payload?: unknown) => void
  on: (action: string, handler: (payload: unknown) => void) => () => void
  log: (message: unknown) => void
}

type PendingRequest = {
  resolve: (value: unknown) => void
  reject: (reason: unknown) => void
  timeout: number
}

class Bridge implements MsmcBridge {
  private pendingRequests = new Map<string, PendingRequest>()
  private eventListeners = new Map<string, Array<(payload: unknown) => void>>()
  private requestIdCounter = 0
  private initialized = false
  private initPromise: Promise<void> | null = null

  constructor() {
    this.init()
  }

  private generateId(): string {
    return `js_req_${++this.requestIdCounter}_${Date.now()}`
  }

  private init(): Promise<void> {
    if (this.initPromise) return this.initPromise

    this.initPromise = new Promise((resolve) => {
      const setup = () => {
        if (this.initialized) return

        if (window.chrome?.webview) {
          window.chrome.webview.addEventListener('message', this.handleMessage.bind(this))
          this.initialized = true
          resolve()
          console.log('[MSMC Bridge] JS端桥接初始化完成')
        }
      }

      if (document.readyState === 'complete') {
        setup()
      } else {
        window.addEventListener('load', setup, { once: true })
      }

      setTimeout(setup, 100)
      setTimeout(setup, 500)
      setTimeout(setup, 1000)
    })

    return this.initPromise
  }

  private handleMessage(event: { data: unknown }): void {
    const data = event.data as BridgeMessage
    if (!data || !data.type) return

    switch (data.type) {
      case 'response': {
        if (data.id) {
          const pending = this.pendingRequests.get(data.id)
          if (pending) {
            clearTimeout(pending.timeout)
            this.pendingRequests.delete(data.id)
            if (data.success) {
              pending.resolve(data.payload)
            } else {
              pending.reject(new Error(data.error || 'Unknown error'))
            }
          }
        }
        break
      }
      case 'event': {
        const listeners = this.eventListeners.get(data.action)
        if (listeners) {
          listeners.forEach((fn) => {
            try {
              fn(data.payload)
            } catch (e) {
              console.error('Event handler error:', e)
            }
          })
        }
        break
      }
      case 'request': {
        console.warn('Unsupported request from C# to JS')
        break
      }
      case 'log': {
        console.log('[C#]', data.payload)
        break
      }
    }
  }

  private postMessage(message: BridgeMessage): void {
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage(message)
    }
  }

  async invoke<T = unknown>(action: string, payload?: unknown): Promise<T> {
    await this.init()

    return new Promise<T>((resolve, reject) => {
      const id = this.generateId()
      const timeout = window.setTimeout(() => {
        this.pendingRequests.delete(id)
        reject(new Error(`Request timeout: ${action}`))
      }, 30000)

      this.pendingRequests.set(id, {
        resolve: resolve as (value: unknown) => void,
        reject,
        timeout,
      })

      this.postMessage({
        type: 'request',
        id,
        action,
        payload,
        timestamp: Date.now(),
      })
    })
  }

  sendEvent(action: string, payload?: unknown): void {
    this.init().then(() => {
      this.postMessage({
        type: 'event',
        action,
        payload,
        timestamp: Date.now(),
      })
    })
  }

  on(action: string, handler: (payload: unknown) => void): () => void {
    if (!this.eventListeners.has(action)) {
      this.eventListeners.set(action, [])
    }
    this.eventListeners.get(action)!.push(handler)

    return () => {
      const listeners = this.eventListeners.get(action)
      if (listeners) {
        const idx = listeners.indexOf(handler)
        if (idx > -1) listeners.splice(idx, 1)
      }
    }
  }

  log(message: unknown): void {
    this.init().then(() => {
      this.postMessage({
        type: 'log',
        action: 'log',
        payload: message,
        timestamp: Date.now(),
      })
    })
  }

  isAvailable(): boolean {
    return this.initialized
  }
}

export const bridge = new Bridge()

export function ping(): Promise<{ pong: boolean; timestamp: number; message: string }> {
  return bridge.invoke<{ pong: boolean; timestamp: number; message: string }>('ping')
}

export function getAppTime(): Promise<string> {
  return bridge.invoke<string>('app:getTime')
}

export function getAppInfo(): Promise<AppInfo> {
  return bridge.invoke<AppInfo>('app:getInfo')
}

export function onAppReady(handler: (data: AppReadyEvent) => void): () => void {
  return bridge.on('app:ready', (payload) => handler(payload as AppReadyEvent))
}

export function onStatusUpdate(handler: (data: { message: string }) => void): () => void {
  return bridge.on('status:update', (payload) => handler(payload as { message: string }))
}
