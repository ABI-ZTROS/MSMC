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

// 直接向 C# 发送日志（绕过桥接，用于桥接初始化前的日志）
function rawLog(msg: string): void {
  console.log('[Bridge]', msg)
  if (window.chrome?.webview) {
    try {
      window.chrome.webview.postMessage({
        type: 'log',
        action: 'log',
        payload: `[JS] ${msg}`,
        timestamp: Date.now(),
      })
    } catch {
      // ignore
    }
  }
}

class Bridge implements MsmcBridge {
  private pendingRequests = new Map<string, PendingRequest>()
  private eventListeners = new Map<string, Array<(payload: unknown) => void>>()
  private requestIdCounter = 0
  private initialized = false
  private initPromise: Promise<void> | null = null

  constructor() {
    rawLog('Bridge 构造函数执行')
    this.init()
  }

  private generateId(): string {
    return `js_req_${++this.requestIdCounter}_${Date.now()}`
  }

  private init(): Promise<void> {
    rawLog('init() 开始')

    if (this.initPromise) {
      rawLog('initPromise 已存在，直接返回')
      return this.initPromise
    }

    this.initPromise = new Promise((resolve) => {
      const setup = () => {
        rawLog(`setup() 调用，initialized=${this.initialized}`)

        if (this.initialized) {
          rawLog('已初始化，跳过')
          return
        }

        if (window.chrome?.webview) {
          rawLog('检测到 chrome.webview，注册消息监听')
          window.chrome.webview.addEventListener('message', this.handleMessage.bind(this))
          this.initialized = true
          rawLog('✅ JS 端桥接初始化完成')
          resolve()
        } else {
          rawLog('⚠️ 未检测到 chrome.webview')
        }
      }

      if (document.readyState === 'complete') {
        rawLog('document.readyState = complete，立即执行 setup')
        setup()
      } else {
        rawLog(`document.readyState = ${document.readyState}，等待 load 事件`)
        window.addEventListener('load', setup, { once: true })
      }

      // 多次重试，确保 webview 对象已注入
      setTimeout(setup, 100)
      setTimeout(setup, 500)
      setTimeout(setup, 1000)
      setTimeout(setup, 2000)
      setTimeout(setup, 5000)
    })

    return this.initPromise
  }

  private handleMessage(event: { data: unknown }): void {
    const data = event.data as BridgeMessage
    rawLog(`收到消息: type=${data?.type}, action=${data?.action}, id=${data?.id ?? '(无)'}`)

    if (!data || !data.type) return

    switch (data.type) {
      case 'response': {
        if (data.id) {
          const pending = this.pendingRequests.get(data.id)
          if (pending) {
            clearTimeout(pending.timeout)
            this.pendingRequests.delete(data.id)
            if (data.success) {
              rawLog(`✅ 请求 ${data.action} 成功`)
              pending.resolve(data.payload)
            } else {
              rawLog(`❌ 请求 ${data.action} 失败: ${data.error}`)
              pending.reject(new Error(data.error || 'Unknown error'))
            }
          } else {
            rawLog(`⚠️ 未找到待处理请求: ${data.id}`)
          }
        }
        break
      }
      case 'event': {
        const listeners = this.eventListeners.get(data.action)
        if (listeners) {
          rawLog(`📢 触发事件: ${data.action} (${listeners.length} 个监听器)`)
          listeners.forEach((fn) => {
            try {
              fn(data.payload)
            } catch (e) {
              console.error('Event handler error:', e)
              rawLog(`❌ 事件处理错误: ${data.action} - ${e}`)
            }
          })
        } else {
          rawLog(`⚠️ 事件 ${data.action} 没有监听器`)
        }
        break
      }
      case 'request': {
        rawLog('⚠️ 收到 C# 发起的请求（暂不支持）')
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
      rawLog(`📤 发送消息: type=${message.type}, action=${message.action}`)
      window.chrome.webview.postMessage(message)
    } else {
      rawLog('⚠️ chrome.webview 不可用，无法发送消息')
    }
  }

  async invoke<T = unknown>(action: string, payload?: unknown): Promise<T> {
    rawLog(`invoke 开始: ${action}`)
    await this.init()
    rawLog(`init 完成，准备发送请求: ${action}`)

    return new Promise<T>((resolve, reject) => {
      const id = this.generateId()
      rawLog(`生成请求 ID: ${id}`)

      const timeout = window.setTimeout(() => {
        this.pendingRequests.delete(id)
        rawLog(`⏰ 请求超时: ${action}`)
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

      rawLog(`请求已发送: ${action} (${id})`)
    })
  }

  sendEvent(action: string, payload?: unknown): void {
    this.init().then(() => {
      rawLog(`发送事件: ${action}`)
      this.postMessage({
        type: 'event',
        action,
        payload,
        timestamp: Date.now(),
      })
    })
  }

  on(action: string, handler: (payload: unknown) => void): () => void {
    rawLog(`注册事件监听器: ${action}`)
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
