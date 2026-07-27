import type {
  BridgeMessage,
  AppInfo,
  AppReadyEvent,
  SystemMetrics,
  HistoryPoint,
  CpuInfo,
  ServerListResponse,
  ServerInfo,
  NetworkStatus,
  PortsResponse,
  BridgeRulesResponse,
  CommonPortInfo,
  AddBridgeRequest,
  KillProcessRequest,
  HourlyHistoryResponse,
  AvailableServersResponse,
  ConfigFileTreeResponse,
  ConfigEntriesResponse,
  UpdateConfigValueRequest,
  ConfigSaveResult,
  SettingsData,
  JavaListResponse,
  ThemePreset,
  ThemeApplyResult,
  SwatchesResponse,
  PresetsResponse,
  TeamInfoResponse,
  JvmDefinitionsResponse,
  JvmStateResponse,
  JvmUpdateArgumentRequest,
  JvmSetMemoryRequest,
  JvmPresetType,
} from '@/types/bridge'

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
  private cleanupTimer: number | null = null

  constructor() {
    rawLog('Bridge 构造函数执行')
    this.init()
    // 启动周期性清理，防止超时后残留请求对象导致内存泄漏
    this.cleanupTimer = window.setInterval(() => this.cleanupExpiredRequests(), 30000)
  }

  /// <summary>
  /// 清理已过期但尚未被移除的 pending 请求（兜底防护）
  /// </summary>
  private cleanupExpiredRequests(): void {
    const now = Date.now()
    let cleaned = 0
    for (const [id, req] of this.pendingRequests) {
      // 解析请求 ID 中的时间戳（格式：js_req_{counter}_{timestamp}）
      const parts = id.split('_')
      const timestamp = parts.length >= 3 ? parseInt(parts[parts.length - 1], 10) : 0
      if (timestamp > 0 && now - timestamp > 30000) {
        clearTimeout(req.timeout)
        req.reject(new Error('Request expired by cleanup'))
        this.pendingRequests.delete(id)
        cleaned++
      }
    }
    if (cleaned > 0) {
      rawLog(`🧹 清理了 ${cleaned} 个过期 pending 请求`)
    }
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

    // 统一转小写，兼容 C# 端枚举序列化的大小写
    const type = String(data.type).toLowerCase()

    switch (type) {
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
      default: {
        rawLog(`⚠️ 未知消息类型: ${data.type}`)
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
      }, 10000)

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

// ═════════════════════════════════════════════════════════════════════
// 基础 API
// ═════════════════════════════════════════════════════════════════════

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

// ═════════════════════════════════════════════════════════════════════
// 系统监控 API
// ═════════════════════════════════════════════════════════════════════

export function getSystemMetrics(): Promise<SystemMetrics> {
  return bridge.invoke<SystemMetrics>('systemMonitor:getMetrics')
}

export function getSystemHistory(): Promise<HistoryPoint[]> {
  return bridge.invoke<HistoryPoint[]>('systemMonitor:getHistory')
}

export function getCpuInfo(): Promise<CpuInfo> {
  return bridge.invoke<CpuInfo>('systemMonitor:getCpuInfo')
}

// ═════════════════════════════════════════════════════════════════════
// 服务器管理 API
// ═════════════════════════════════════════════════════════════════════

export function getServerList(): Promise<ServerListResponse> {
  return bridge.invoke<ServerListResponse>('server:list')
}

export function getSelectedServer(): Promise<ServerInfo | null> {
  return bridge.invoke<ServerInfo | null>('server:getSelected')
}

export function selectServer(displayName: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('server:select', displayName)
}

// ═════════════════════════════════════════════════════════════════════
// 网络监控 API
// ═════════════════════════════════════════════════════════════════════

export function getNetworkStatus(): Promise<NetworkStatus> {
  return bridge.invoke<NetworkStatus>('network:getStatus')
}

export function getPorts(): Promise<PortsResponse> {
  return bridge.invoke<PortsResponse>('network:getPorts')
}

export function getBridgeRules(): Promise<BridgeRulesResponse> {
  return bridge.invoke<BridgeRulesResponse>('network:getBridgeRules')
}

export function addBridge(req: AddBridgeRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:addBridge', req)
}

export function removeBridge(listenAddress: string, listenPort: number, protocol: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:removeBridge', { listenAddress, listenPort, protocol })
}

export function killProcess(req: KillProcessRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:killProcess', req)
}

export function getCommonPorts(): Promise<{ ports: CommonPortInfo[] }> {
  return bridge.invoke<{ ports: CommonPortInfo[] }>('network:getCommonPorts')
}

export function refreshNetwork(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('network:refresh')
}

export function getHourlyHistory(): Promise<HourlyHistoryResponse> {
  return bridge.invoke<HourlyHistoryResponse>('network:getHourlyHistory')
}

// ═════════════════════════════════════════════════════════════════════
// 配置编辑 API
// ═════════════════════════════════════════════════════════════════════

export function getAvailableServers(): Promise<AvailableServersResponse> {
  return bridge.invoke<AvailableServersResponse>('config:getAvailableServers')
}

export function getConfigFileTree(): Promise<ConfigFileTreeResponse> {
  return bridge.invoke<ConfigFileTreeResponse>('config:getFileTree')
}

export function selectConfigFile(relativePath: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:selectFile', relativePath)
}

export function getConfigEntries(): Promise<ConfigEntriesResponse> {
  return bridge.invoke<ConfigEntriesResponse>('config:getEntries')
}

export function updateConfigValue(req: UpdateConfigValueRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:updateValue', req)
}

export function saveConfig(): Promise<ConfigSaveResult> {
  return bridge.invoke<ConfigSaveResult>('config:save')
}

export function resetConfig(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:reset')
}

export function undoConfig(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:undo')
}

export function selectConfigServer(name: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:selectServer', name)
}

export function rescanConfigFiles(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:rescan')
}

// ═════════════════════════════════════════════════════════════════════
// 设置 API
// ═════════════════════════════════════════════════════════════════════

export function getSettings(): Promise<SettingsData> {
  return bridge.invoke<SettingsData>('settings:get')
}

export function setPrimaryColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setPrimaryColor', hex)
}

export function setAccentColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setAccentColor', hex)
}

export function applyTheme(): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:applyTheme')
}

export function saveSettings(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:save')
}

export function setPreset(preset: ThemePreset): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:setPreset', preset)
}

export function resetSettings(): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:reset')
}

export function toggleAnimations(): Promise<{ success: boolean; enableAnimations: boolean }> {
  return bridge.invoke<{ success: boolean; enableAnimations: boolean }>('settings:toggleAnimations')
}

export function testNotification(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:testNotification')
}

export function getJavaList(): Promise<JavaListResponse> {
  return bridge.invoke<JavaListResponse>('settings:getJavaList')
}

export function rescanJava(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:rescanJava')
}

export function getPresets(): Promise<PresetsResponse> {
  return bridge.invoke<PresetsResponse>('settings:getPresets')
}

export function getPrimarySwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getPrimarySwatches')
}

export function getAccentSwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getAccentSwatches')
}

// ═════════════════════════════════════════════════════════════════════
// 关于页面 API
// ═════════════════════════════════════════════════════════════════════

export function getTeamInfo(): Promise<TeamInfoResponse> {
  return bridge.invoke<TeamInfoResponse>('about:getTeamInfo')
}

// ═════════════════════════════════════════════════════════════════════
// JVM 参数相关 API
// ═════════════════════════════════════════════════════════════════════

export function getJvmDefinitions(): Promise<JvmDefinitionsResponse> {
  return bridge.invoke<JvmDefinitionsResponse>('jvm:getDefinitions')
}

export function getJvmState(): Promise<JvmStateResponse> {
  return bridge.invoke<JvmStateResponse>('jvm:getState')
}

export function addJvmArgument(flag: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:addArgument', flag)
}

export function removeJvmArgument(flag: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:removeArgument', flag)
}

export function updateJvmArgument(
  oldArg: string,
  newValue: string,
): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:updateArgument', {
    oldArg,
    newValue,
  } as JvmUpdateArgumentRequest)
}

export function setJvmMemory(initial?: string, max?: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:setMemory', {
    initial,
    max,
  } as JvmSetMemoryRequest)
}

export function applyJvmPreset(preset: JvmPresetType): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:applyPreset', preset)
}

export function addCustomJvmArgument(arg: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:addCustom', arg)
}
