import { useEffect, useState, useMemo, useCallback } from 'react'
import { clsx } from 'clsx'
import { Reveal } from '@/components/ui/Reveal'
import {
  getBridge,
  getServerList,
  getSelectedServer,
  selectServer,
  getJvmDefinitions,
  getJvmState,
  addJvmArgument,
  removeJvmArgument,
  updateJvmArgument,
  setJvmMemory,
  applyJvmPreset,
  addCustomJvmArgument,
} from '@/utils/bridge'

const bridge = getBridge()
import type {
  ServerInfo,
  KnownServerInfo,
  ServerListResponse,
  JvmArgumentDefinition,
  JvmArgumentCategory,
  JvmStateResponse,
} from '@/types/bridge'

// ─── 辅助函数 ───

// 状态点颜色：依据端口冲突 / 端口开放状态决定（与 WPF DataTrigger 一致）
function getRunningStatusDot(server: ServerInfo): string {
  const conflictStr = String(server.portConflict ?? '').toLowerCase()
  if (conflictStr === 'true' || conflictStr === '1') return 'md-status-dot-yellow'
  if (!server.isPortOpen) return 'md-status-dot-red'
  return 'md-status-dot-green'
}

// ─── JVM 参数辅助函数 ───

// 分类中文名称映射
const categoryLabels: Record<JvmArgumentCategory, string> = {
  Memory: '内存',
  GarbageCollection: '垃圾回收',
  Performance: '性能调优',
  Encoding: '编码',
  Security: '安全',
  Debug: '调试',
  ServerBehavior: '服务器行为',
  Other: '其他',
}

// 从完整参数字符串中提取基础名（去掉值部分）
function getArgBaseName(arg: string): string {
  if (!arg) return ''
  // -XX:+UseG1GC / -XX:-UseG1GC -> -XX:UseG1GC (BooleanFlag)
  if (arg.startsWith('-XX:+') || arg.startsWith('-XX:-')) {
    return '-XX:' + arg.slice(5)
  }
  // -XX:MaxGCPauseMillis=200 -> -XX:MaxGCPauseMillis=
  if (arg.startsWith('-XX:') && arg.includes('=')) {
    return arg.substring(0, arg.indexOf('=') + 1)
  }
  // -Xmx4G -> -Xmx
  if (arg.startsWith('-Xmx') || arg.startsWith('-Xms') || arg.startsWith('-Xss')) {
    return arg.substring(0, 4)
  }
  // -Dfile.encoding=UTF-8 -> -Dfile.encoding=
  if (arg.startsWith('-D') && arg.includes('=')) {
    return arg.substring(0, arg.indexOf('=') + 1)
  }
  return arg
}

// 从完整参数字符串中提取值
function getArgValue(arg: string): string {
  if (!arg) return ''
  if (arg.startsWith('-XX:+')) return 'true'
  if (arg.startsWith('-XX:-')) return 'false'
  if (arg.startsWith('-XX:') && arg.includes('=')) {
    return arg.substring(arg.indexOf('=') + 1)
  }
  if (arg.startsWith('-Xmx') || arg.startsWith('-Xms') || arg.startsWith('-Xss')) {
    return arg.substring(4)
  }
  if (arg.startsWith('-D') && arg.includes('=')) {
    return arg.substring(arg.indexOf('=') + 1)
  }
  return ''
}

// 根据参数定义和值构建完整参数字符串
function buildFullArg(def: JvmArgumentDefinition, value: string): string {
  if (def.valueType === 'BooleanFlag') {
    const enabled = value === 'true' || value === '+'
    return def.flag.replace(/[-+]$/, enabled ? '+' : '-')
  }
  if (def.valueType === 'None') {
    return def.flag
  }
  // 带值的参数
  const base = def.flag.endsWith('=') ? def.flag : def.flag + '='
  return base + value
}

// ─── 子组件：运行中服务器列表项 ───

interface RunningItemProps {
  server: ServerInfo
  isSelected: boolean
  onSelect: () => void
  onStop: () => void
}

function RunningServerItem({ server, isSelected, onSelect, onStop }: RunningItemProps): JSX.Element {
  return (
    <div
      onClick={onSelect}
      style={{
        padding: '6px 8px',
        marginBottom: 2,
        cursor: 'pointer',
        borderRadius: 'var(--md-radius-small)',
        background: isSelected ? 'var(--md-primary-subtle-background)' : 'transparent',
        transition: 'background-color var(--md-duration-fast) var(--md-ease-standard)',
      }}
    >
      <div className="flex items-center" style={{ gap: 6 }}>
        <span className={clsx('md-status-dot', getRunningStatusDot(server))} />
        <div className="flex-1 min-w-0">
          <div
            style={{
              fontSize: 11,
              fontWeight: 500,
              color: 'var(--md-body)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {server.displayName}
          </div>
          <div style={{ fontSize: 9, opacity: 0.6, color: 'var(--md-body-light)' }}>
            {`内存 ${server.formattedMaxMemory} | ${server.networkStatusText}`}
          </div>
        </div>
        <button
          onClick={(e) => {
            e.stopPropagation()
            onStop()
          }}
          className="md-btn md-btn-flat md-btn-icon"
          title="停止"
        >
          ⏹
        </button>
      </div>
    </div>
  )
}

// ─── 子组件：已知服务器列表项 ───

interface KnownItemProps {
  server: KnownServerInfo
  isSelected: boolean
  onSelect: () => void
  onStart: () => void
  onDelete: () => void
}

function KnownServerItem({ server, isSelected, onSelect, onStart, onDelete }: KnownItemProps): JSX.Element {
  return (
    <div
      onClick={onSelect}
      style={{
        padding: '6px 8px',
        marginBottom: 2,
        cursor: 'pointer',
        borderRadius: 'var(--md-radius-small)',
        background: isSelected ? 'var(--md-primary-subtle-background)' : 'transparent',
        transition: 'background-color var(--md-duration-fast) var(--md-ease-standard)',
      }}
    >
      <div className="flex items-center" style={{ gap: 6 }}>
        <span style={{ fontSize: 10, color: 'var(--md-accent-text)' }}>★</span>
        <div className="flex-1 min-w-0">
          <div
            style={{
              fontSize: 11,
              fontWeight: 500,
              color: 'var(--md-body)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {server.name}
          </div>
          <div style={{ fontSize: 9, opacity: 0.6, color: 'var(--md-body-light)' }}>
            {`端口 ${server.port}`}
          </div>
        </div>
        <div className="flex items-center">
          <button
            onClick={(e) => {
              e.stopPropagation()
              onStart()
            }}
            className="md-btn md-btn-flat md-btn-icon"
            title="启动"
            style={{ color: 'var(--md-primary-hue-mid)' }}
          >
            ▶
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation()
              onDelete()
            }}
            className="md-btn md-btn-flat md-btn-icon"
            title="删除"
          >
            🗑
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── 子组件：服务器分组（Expander 风格，对应 WPF 的 Expander） ───

interface ServerGroupProps {
  title: string
  icon: string
  count: number
  defaultExpanded?: boolean
  children: React.ReactNode
}

function ServerGroup({ title, icon, count, defaultExpanded = true, children }: ServerGroupProps): JSX.Element {
  const [expanded, setExpanded] = useState(defaultExpanded)
  return (
    <div className="md-expander" style={{ marginTop: 4 }}>
      <div className="md-expander-header" onClick={() => setExpanded(!expanded)}>
        <span className={clsx('md-expander-icon', expanded && 'md-expander-icon-expanded')}>▶</span>
        <span style={{ fontSize: 12 }}>{icon}</span>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--md-body)' }}>{title}</span>
        <span className="md-badge" style={{ marginLeft: 'auto' }}>
          {count}
        </span>
      </div>
      {expanded && <div style={{ marginTop: 4 }}>{children}</div>}
    </div>
  )
}

// ─── 主页面 ───

export function DashboardPage(): JSX.Element {
  const [serverList, setServerList] = useState<ServerListResponse | null>(null)
  const [selectedServer, setSelectedServer] = useState<ServerInfo | null>(null)
  const [searchKeyword, setSearchKeyword] = useState('')
  const [detailTab, setDetailTab] = useState<'console' | 'jvm' | 'command'>('console')
  const [isBusy, setIsBusy] = useState(false)
  const [busyReason, setBusyReason] = useState('')
  const [operationMessage, setOperationMessage] = useState('')
  const [autoDetectEnabled, setAutoDetectEnabled] = useState(false)

  // JVM 参数相关 state
  const [jvmDefinitions, setJvmDefinitions] = useState<JvmArgumentDefinition[]>([])
  const [jvmState, setJvmState] = useState<JvmStateResponse | null>(null)
  const [jvmCategory, setJvmCategory] = useState<JvmArgumentCategory>('GarbageCollection')
  const [editingArg, setEditingArg] = useState<{ def: JvmArgumentDefinition; value: string; mode: 'add' | 'edit'; oldArg?: string } | null>(null)
  const [customArgInput, setCustomArgInput] = useState('')
  const [jvmMemoryInitial, setJvmMemoryInitial] = useState('')
  const [jvmMemoryMax, setJvmMemoryMax] = useState('')

  // 拉取服务器列表
  const fetchServerList = async () => {
    try {
      const data = await getServerList()
      setServerList(data)
      // 同步后端的自动检测状态，避免前端状态与后端不同步
      if (typeof data.isAutoDetectEnabled === 'boolean') {
        setAutoDetectEnabled(data.isAutoDetectEnabled)
      }
    } catch (e) {
      console.error('获取服务器列表失败:', e)
    }
  }

  // 拉取当前选中服务器详情
  const fetchSelectedServer = async () => {
    try {
      const data = await getSelectedServer()
      setSelectedServer(data)
    } catch (e) {
      console.error('获取选中服务器失败:', e)
    }
  }

  const handleRefresh = async () => {
    setIsBusy(true)
    setBusyReason('正在刷新服务器列表...')
    try {
      await bridge.invoke('server:refresh')
      await fetchServerList()
      await fetchSelectedServer()
    } catch (e) {
      console.error('刷新失败:', e)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleSelectServer = async (displayName: string) => {
    setIsBusy(true)
    setBusyReason('正在切换服务器...')
    try {
      await selectServer(displayName)
      await fetchSelectedServer()
    } catch (e) {
      console.error('选择服务器失败:', e)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleStart = async () => {
    setIsBusy(true)
    setBusyReason('正在启动服务器...')
    setOperationMessage('')
    try {
      const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:start')
      if (result?.success) {
        setOperationMessage(result.message || '启动成功')
      } else {
        setOperationMessage(`启动失败: ${result?.error || result?.message || '未知错误'}`)
      }
      await fetchServerList()
      await fetchSelectedServer()
    } catch (e) {
      setOperationMessage(`启动失败: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleStop = async () => {
    setIsBusy(true)
    setBusyReason('正在停止服务器...')
    setOperationMessage('')
    try {
      const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:stop')
      if (result?.success) {
        setOperationMessage(result.message || '停止成功')
      } else {
        setOperationMessage(`停止失败: ${result?.error || result?.message || '未知错误'}`)
      }
      await fetchServerList()
      await fetchSelectedServer()
    } catch (e) {
      setOperationMessage(`停止失败: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleImport = async () => {
    setIsBusy(true)
    setBusyReason('正在导入服务器...')
    try {
      const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:import')
      if (result.success) {
        await fetchServerList()
      } else {
        setOperationMessage(`导入失败: ${result.error || result.message || '未知错误'}`)
      }
    } catch (e) {
      console.error('导入失败:', e)
      setOperationMessage(`导入失败: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleToggleAutoDetect = async () => {
    try {
      const result = await bridge.invoke<{ success: boolean; isEnabled?: boolean }>('server:toggleAutoDetect')
      // 使用后端返回的实际状态，而非翻转本地状态
      if (result && typeof result.isEnabled === 'boolean') {
        setAutoDetectEnabled(result.isEnabled)
      } else {
        // 后端未返回状态时，回退到翻转本地状态
        setAutoDetectEnabled(!autoDetectEnabled)
      }
    } catch (e) {
      console.error('切换自动检测失败:', e)
    }
  }

  const handleCopyCommand = () => {
    if (selectedServer?.fullCommandLine) {
      navigator.clipboard?.writeText(selectedServer.fullCommandLine).catch(() => {})
    }
  }

  // ─── JVM 参数方法 ───

  const fetchJvmDefinitions = useCallback(async () => {
    try {
      const resp = await getJvmDefinitions()
      setJvmDefinitions(resp.definitions)
    } catch (e) {
      console.error('获取 JVM 参数定义失败:', e)
    }
  }, [])

  const fetchJvmState = useCallback(async () => {
    try {
      const resp = await getJvmState()
      setJvmState(resp)
      if (resp.hasServer) {
        setJvmMemoryInitial(resp.initialMemory)
        setJvmMemoryMax(resp.maxMemory)
      }
    } catch (e) {
      console.error('获取 JVM 状态失败:', e)
    }
  }, [])

  const selectedArgBaseNames = useMemo(() => {
    if (!jvmState?.selectedArguments) return new Set<string>()
    return new Set(jvmState.selectedArguments.map((a) => getArgBaseName(a).toLowerCase()))
  }, [jvmState])

  const filteredDefinitions = useMemo(() => {
    return jvmDefinitions.filter((d) => d.category === jvmCategory)
  }, [jvmDefinitions, jvmCategory])

  const categories = useMemo(() => {
    const set = new Set(jvmDefinitions.map((d) => d.category))
    return Array.from(set) as JvmArgumentCategory[]
  }, [jvmDefinitions])

  const handleAddArgument = async (def: JvmArgumentDefinition) => {
    if (def.valueType === 'None' || def.valueType === 'BooleanFlag') {
      try {
        await addJvmArgument(def.flag)
        await fetchJvmState()
        await fetchSelectedServer()
      } catch (e) {
        console.error('添加参数失败:', e)
      }
    } else {
      setEditingArg({ def, value: def.defaultValue ?? '', mode: 'add' })
    }
  }

  const handleRemoveArgument = async (arg: string) => {
    try {
      await removeJvmArgument(arg)
      await fetchJvmState()
      await fetchSelectedServer()
    } catch (e) {
      console.error('移除参数失败:', e)
    }
  }

  const handleEditArgument = (arg: string) => {
    const base = getArgBaseName(arg)
    const value = getArgValue(arg)
    const def = jvmDefinitions.find(
      (d) => getArgBaseName(d.flag).toLowerCase() === base.toLowerCase(),
    )
    if (def) {
      setEditingArg({ def, value, mode: 'edit', oldArg: arg })
    }
  }

  const handleSaveEditingArg = async () => {
    if (!editingArg) return
    const { def, value, mode, oldArg } = editingArg

    if (mode === 'add') {
      const full = buildFullArg(def, value)
      try {
        await addJvmArgument(full)
      } catch (e) {
        console.error('添加参数失败:', e)
      }
    } else if (mode === 'edit' && oldArg) {
      try {
        await updateJvmArgument(oldArg, value)
      } catch (e) {
        console.error('更新参数失败:', e)
      }
    }

    setEditingArg(null)
    await fetchJvmState()
    await fetchSelectedServer()
  }

  const handleApplyPreset = async (preset: 'aikar' | 'g1gc' | 'zgc') => {
    try {
      await applyJvmPreset(preset)
      await fetchJvmState()
      await fetchSelectedServer()
    } catch (e) {
      console.error('应用预设失败:', e)
    }
  }

  const handleAddCustomArg = async () => {
    if (!customArgInput.trim()) return
    try {
      await addCustomJvmArgument(customArgInput.trim())
      setCustomArgInput('')
      await fetchJvmState()
      await fetchSelectedServer()
    } catch (e) {
      console.error('添加自定义参数失败:', e)
    }
  }

  const handleMemoryBlur = async () => {
    if (!jvmState?.hasServer) return
    try {
      await setJvmMemory(jvmMemoryInitial, jvmMemoryMax)
      await fetchJvmState()
      await fetchSelectedServer()
    } catch (e) {
      console.error('设置内存失败:', e)
    }
  }

  const isArgSelected = (def: JvmArgumentDefinition): boolean => {
    const base = getArgBaseName(def.flag).toLowerCase()
    return selectedArgBaseNames.has(base)
  }

  useEffect(() => {
    fetchServerList()
    fetchSelectedServer()
    fetchJvmDefinitions()
    fetchJvmState()
    // 后台轮询，不触发忙碌遮罩
    const interval = setInterval(() => {
      fetchServerList()
      fetchSelectedServer()
      fetchJvmState()
    }, 3000)
    return () => clearInterval(interval)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // 搜索过滤
  const keyword = searchKeyword.toLowerCase()
  const runningServers = (serverList?.running ?? []).filter(
    (s) => !keyword || s.displayName.toLowerCase().includes(keyword),
  )
  const knownServers = (serverList?.known ?? []).filter(
    (s) => !keyword || s.name.toLowerCase().includes(keyword),
  )

  return (
    <div className="md-page-enter h-full flex flex-col relative">
      {/* ═══ 顶部操作条 ═══ */}
      <div
        className="flex items-center"
        style={{
          background: 'var(--md-card-background)',
          borderBottom: '1px solid var(--md-card-subtle-border)',
          padding: '10px 16px',
          gap: 8,
        }}
      >
        {/* 左侧：操作按钮 */}
        <button
          onClick={handleRefresh}
          disabled={isBusy}
          className="md-btn md-btn-primary"
          title="立即刷新服务器列表"
        >
          <span className={clsx(isBusy && 'md-spin')}>🔄</span>
          <span style={{ fontWeight: 600 }}>刷新</span>
        </button>

        <button
          onClick={handleToggleAutoDetect}
          className="md-btn md-btn-outlined"
          title={autoDetectEnabled ? '点击停止自动检测' : '点击开始自动检测'}
        >
          <span>{autoDetectEnabled ? '⏸' : '▶'}</span>
          <span>{autoDetectEnabled ? '自动检测中' : '开启自动检测'}</span>
        </button>

        <button
          onClick={handleImport}
          disabled={isBusy}
          className="md-btn md-btn-outlined"
          title="选择 JAR 文件导入到已知服务器列表"
        >
          <span>➕</span>
          <span>导入服务器</span>
        </button>

        {/* 中间：选中服务器状态 */}
        <div className="flex-1 flex items-center justify-center">
          {selectedServer && (
            <div className="flex items-center" style={{ gap: 6 }}>
              <span className={clsx('md-status-dot', getRunningStatusDot(selectedServer))} />
              <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--md-body)' }}>
                {selectedServer.status}
              </span>
              <span style={{ fontSize: 12, opacity: 0.5 }}>·</span>
              <span
                style={{
                  fontSize: 12,
                  opacity: 0.7,
                  color: 'var(--md-body)',
                  maxWidth: 280,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                {selectedServer.displayName}
              </span>
            </div>
          )}
        </div>

        {/* 右侧：忙碌提示 */}
        {isBusy && (
          <div
            className="flex items-center"
            style={{
              background: 'var(--md-card-hover)',
              borderRadius: 'var(--md-radius-small)',
              padding: '6px 10px',
              gap: 8,
            }}
          >
            <div
              className="md-spin"
              style={{
                width: 14,
                height: 14,
                border: '2px solid var(--md-primary-hue-mid)',
                borderTopColor: 'transparent',
                borderRadius: '50%',
              }}
            />
            <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--md-body)' }}>
              {busyReason}
            </span>
          </div>
        )}
      </div>

      {/* ═══ 中间区域：左列表 + 右 Tab ═══ */}
      <div className="flex-1 flex min-h-0">
        {/* 左侧：服务器列表（280px） */}
        <div
          className="flex flex-col"
          style={{
            width: 280,
            background: 'var(--md-card-background)',
            borderRight: '1px solid var(--md-card-subtle-border)',
          }}
        >
          {/* 搜索框 */}
          <div style={{ padding: '8px 8px 4px' }}>
            <div className="flex items-center" style={{ gap: 6 }}>
              <span style={{ fontSize: 12, opacity: 0.6 }}>🔍</span>
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="搜索服务器..."
                className="flex-1 bg-transparent outline-none"
                style={{ fontSize: 12, padding: '4px 0', color: 'var(--md-body)' }}
              />
              {searchKeyword && (
                <button
                  onClick={() => setSearchKeyword('')}
                  className="md-btn md-btn-flat md-btn-icon"
                  title="清空搜索"
                >
                  ✕
                </button>
              )}
            </div>
          </div>

          {/* 列表区 */}
          <div className="flex-1 overflow-y-auto" style={{ padding: '0 8px 8px' }}>
            {/* 运行中分组 */}
            <ServerGroup title="运行中" icon="🖥" count={runningServers.length}>
              {runningServers.length === 0 ? (
                <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                  <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                    {searchKeyword ? '没有匹配的服务器' : '暂无运行中的服务器'}
                  </div>
                </div>
              ) : (
                runningServers.map((server, idx) => (
                  <div
                    key={`running-${idx}`}
                    className="md-stagger-item"
                    style={{ animationDelay: `${idx * 40}ms` }}
                  >
                    <RunningServerItem
                      server={server}
                      isSelected={selectedServer?.displayName === server.displayName}
                      onSelect={() => handleSelectServer(server.displayName)}
                      onStop={handleStop}
                    />
                  </div>
                ))
              )}
            </ServerGroup>

            {/* 已知服务器分组 */}
            <ServerGroup title="已知服务器" icon="📚" count={knownServers.length}>
              {knownServers.length === 0 ? (
                <div className="md-empty-state" style={{ padding: '20px 8px' }}>
                  <div className="md-empty-state-icon" style={{ fontSize: 32, opacity: 0.3 }}>
                    📂
                  </div>
                  <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                    还没有已知服务器
                  </div>
                  <div style={{ fontSize: 10, opacity: 0.5 }}>点击「导入服务器」开始</div>
                </div>
              ) : (
                knownServers.map((server, idx) => (
                  <div
                    key={`known-${idx}`}
                    className="md-stagger-item"
                    style={{ animationDelay: `${idx * 40}ms` }}
                  >
                    <KnownServerItem
                      server={server}
                      isSelected={selectedServer?.isKnown === true && selectedServer.displayName === server.name}
                      onSelect={() => handleSelectServer(server.name)}
                      onStart={async () => {
                        try {
                          // Pattern5 修复：优先传 knownServerId，其次 name，避免同名冲突
                          const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:startKnown', {
                            knownServerId: server.knownServerId,
                            id: server.id,
                            name: server.name,
                          })
                          if (!result?.success) {
                            setOperationMessage(`启动失败: ${result?.error || '未知错误'}`)
                          }
                          await fetchServerList()
                          await fetchSelectedServer()
                        } catch (e) {
                          setOperationMessage(`启动失败: ${e instanceof Error ? e.message : String(e)}`)
                        }
                      }}
                      onDelete={async () => {
                        try {
                          // Pattern5 修复：优先传 knownServerId
                          const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:removeKnown', {
                            knownServerId: server.knownServerId,
                            id: server.id,
                            name: server.name,
                          })
                          if (result.success) {
                            await fetchServerList()
                          } else {
                            setOperationMessage(`删除失败: ${result.error || result.message || '未知错误'}`)
                          }
                        } catch (e) {
                          console.error('删除失败:', e)
                          setOperationMessage(`删除失败: ${e instanceof Error ? e.message : String(e)}`)
                        }
                      }}
                    />
                  </div>
                ))
              )}
            </ServerGroup>
          </div>
        </div>

        {/* 右侧：Tab 详情区 */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="md-tabs">
            <div
              className={clsx('md-tab', detailTab === 'console' && 'md-tab-active')}
              onClick={() => setDetailTab('console')}
            >
              🎛 控制台
            </div>
            <div
              className={clsx('md-tab', detailTab === 'jvm' && 'md-tab-active')}
              onClick={() => setDetailTab('jvm')}
            >
              ⚙ JVM 参数
            </div>
            <div
              className={clsx('md-tab', detailTab === 'command' && 'md-tab-active')}
              onClick={() => setDetailTab('command')}
            >
              📋 命令预览
            </div>
          </div>

          <div className="flex-1 overflow-y-auto" style={{ padding: 16 }}>
            {!selectedServer ? (
              <div className="md-empty-state h-full">
                <div className="md-empty-state-icon">🎮</div>
                <div className="md-empty-state-text">选择一个服务器查看详情</div>
              </div>
            ) : (
              <>
                {/* ─── 控制台 Tab ─── */}
                {detailTab === 'console' && (
                  <div>
                    {/* 服务器控制卡片 */}
                    <Reveal direction="up" delay={0} className="md-card md-card-elevated" style={{ padding: 16, marginBottom: 12 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        🚀 服务器控制
                      </div>
                      <div className="flex items-center" style={{ gap: 8 }}>
                        <button
                          onClick={handleStart}
                          disabled={isBusy}
                          className="md-btn md-btn-primary"
                          style={{ minHeight: 36, padding: '8px 16px' }}
                        >
                          <span>▶</span>
                          <span style={{ fontWeight: 600 }}>启动服务器</span>
                        </button>
                        <button
                          onClick={handleStop}
                          disabled={isBusy}
                          className="md-btn md-btn-danger"
                          style={{ minHeight: 36, padding: '8px 16px' }}
                        >
                          <span>⏹</span>
                          <span style={{ fontWeight: 600 }}>停止服务器</span>
                        </button>
                        <button
                          onClick={async () => {
                            try {
                              const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:saveAsKnown')
                              if (result.success) {
                                await fetchServerList()
                              } else {
                                setOperationMessage(`保存失败: ${result.error || result.message || '未知错误'}`)
                              }
                            } catch (e) {
                              console.error('保存到已知失败:', e)
                              setOperationMessage(`保存失败: ${e instanceof Error ? e.message : String(e)}`)
                            }
                          }}
                          // Q1 修复：如果已经是已知服务器（isKnown=true），则不需要再显示「保存到已知」按钮
                          style={{ minHeight: 36, padding: '8px 16px', display: selectedServer && selectedServer.isKnown ? 'none' : undefined }}
                          className="md-btn md-btn-outlined"
                          disabled={isBusy}
                        >
                          <span>💾</span>
                          <span>保存到已知</span>
                        </button>
                      </div>
                      {operationMessage && (
                        <div
                          style={{
                            fontSize: 12,
                            marginTop: 12,
                            opacity: 0.8,
                            color: 'var(--md-body)',
                          }}
                        >
                          {operationMessage}
                        </div>
                      )}
                    </Reveal>

                    {/* 服务器详情卡片 */}
                    <Reveal direction="up" delay={80} className="md-card md-card-elevated" style={{ padding: 16, marginBottom: 12 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        📊 服务器详情
                      </div>
                      <div style={{ display: 'grid', gridTemplateColumns: '100px 1fr', rowGap: 8 }}>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          工作路径
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.workingDirectory}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          JAR 路径
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.serverJarPath}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          JAR 名称
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.serverJarName}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          Java
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.javaPath}
                        </div>
                      </div>
                    </Reveal>

                    {/* 检测日志卡片 */}
                    <Reveal direction="up" delay={160} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        📝 检测日志
                      </div>
                      <div
                        className="md-terminal"
                        style={{ minHeight: 120, maxHeight: 200, overflowY: 'auto' }}
                      >
                        <div style={{ opacity: 0.5 }}>[系统] 暂无检测日志</div>
                      </div>
                    </Reveal>
                  </div>
                )}

                {/* ─── JVM 参数 Tab ─── */}
                {detailTab === 'jvm' && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    {/* 内存设置卡片 */}
                    <Reveal direction="up" delay={0} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        💾 内存设置
                      </div>
                      <div className="grid grid-cols-2" style={{ gap: 12 }}>
                        <div>
                          <div
                            style={{
                              fontSize: 11,
                              opacity: 0.7,
                              marginBottom: 4,
                              color: 'var(--md-body-light)',
                            }}
                          >
                            初始堆内存 (-Xms)
                          </div>
                          <input
                            value={jvmMemoryInitial}
                            onChange={(e) => setJvmMemoryInitial(e.target.value)}
                            onBlur={handleMemoryBlur}
                            className="md-input"
                            placeholder="如 2G、512M"
                            disabled={!jvmState?.hasServer || jvmState.isRunning}
                          />
                        </div>
                        <div>
                          <div
                            style={{
                              fontSize: 11,
                              opacity: 0.7,
                              marginBottom: 4,
                              color: 'var(--md-body-light)',
                            }}
                          >
                            最大堆内存 (-Xmx)
                          </div>
                          <input
                            value={jvmMemoryMax}
                            onChange={(e) => setJvmMemoryMax(e.target.value)}
                            onBlur={handleMemoryBlur}
                            className="md-input"
                            placeholder="如 4G、2048M"
                            disabled={!jvmState?.hasServer || jvmState.isRunning}
                          />
                        </div>
                      </div>
                      {jvmState?.isRunning && (
                        <div style={{ fontSize: 11, color: 'var(--md-primary-hue-mid)', marginTop: 8 }}>
                          ⚠️ 服务器运行中无法修改内存设置
                        </div>
                      )}
                    </Reveal>

                    {/* 快速预设卡片 */}
                    <Reveal direction="up" delay={70} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        🚀 快速预设
                      </div>
                      <div className="flex items-center" style={{ gap: 8, flexWrap: 'wrap' }}>
                        <button
                          onClick={() => handleApplyPreset('aikar')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                        >
                          🌟 Aikar 优化
                        </button>
                        <button
                          onClick={() => handleApplyPreset('g1gc')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                        >
                          📊 G1GC 回收器
                        </button>
                        <button
                          onClick={() => handleApplyPreset('zgc')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                        >
                          ⚡ ZGC 回收器
                        </button>
                      </div>
                    </Reveal>

                    {/* 已选参数卡片 */}
                    <Reveal direction="up" delay={140} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        ✅ 已选参数 ({jvmState?.selectedArguments?.length ?? 0})
                      </div>
                      {!jvmState?.selectedArguments || jvmState.selectedArguments.length === 0 ? (
                        <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                          <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                            暂无已选参数，从下方分类中添加
                          </div>
                        </div>
                      ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                          {jvmState.selectedArguments.map((arg, idx) => {
                            const base = getArgBaseName(arg)
                            const def = jvmDefinitions.find(
                              (d) => getArgBaseName(d.flag).toLowerCase() === base.toLowerCase(),
                            )
                            return (
                              <div
                                key={idx}
                                className="flex items-center"
                                style={{
                                  background: 'var(--md-card-hover)',
                                  borderRadius: 'var(--md-radius-small)',
                                  padding: '8px 10px',
                                  gap: 8,
                                }}
                              >
                                <div className="flex-1" style={{ minWidth: 0 }}>
                                  <div
                                    style={{
                                      fontSize: 12,
                                      fontWeight: 600,
                                      color: 'var(--md-body)',
                                      marginBottom: 2,
                                    }}
                                  >
                                    {def?.name || base}
                                  </div>
                                  <div
                                    style={{
                                      fontFamily: 'var(--md-font-mono)',
                                      fontSize: 11,
                                      color: 'var(--md-body-light)',
                                      whiteSpace: 'nowrap',
                                      overflow: 'hidden',
                                      textOverflow: 'ellipsis',
                                    }}
                                  >
                                    {arg}
                                  </div>
                                </div>
                                {def && def.valueType !== 'None' && (
                                  <button
                                    onClick={() => handleEditArgument(arg)}
                                    className="md-btn md-btn-flat md-btn-icon"
                                    title="编辑值"
                                    style={{ fontSize: 12 }}
                                    disabled={jvmState.isRunning}
                                  >
                                    ✏️
                                  </button>
                                )}
                                <button
                                  onClick={() => handleRemoveArgument(arg)}
                                  className="md-btn md-btn-flat md-btn-icon"
                                  title="移除"
                                  style={{ fontSize: 12, color: 'var(--md-error)' }}
                                  disabled={jvmState.isRunning}
                                >
                                  ✕
                                </button>
                              </div>
                            )
                          })}
                        </div>
                      )}
                    </Reveal>

                    {/* 可选参数分类卡片 */}
                    <Reveal direction="up" delay={210} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        ➕ 添加参数
                      </div>

                      {/* 分类标签 */}
                      <div
                        style={{
                          display: 'flex',
                          gap: 6,
                          flexWrap: 'wrap',
                          marginBottom: 12,
                        }}
                      >
                        {categories.map((cat) => (
                          <button
                            key={cat}
                            onClick={() => setJvmCategory(cat)}
                            className={clsx(
                              'md-chip',
                              jvmCategory === cat && 'md-chip-primary',
                            )}
                            style={{
                              cursor: 'pointer',
                              fontSize: 11,
                            }}
                          >
                            {categoryLabels[cat]}
                          </button>
                        ))}
                      </div>

                      {/* 可选参数列表 */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {filteredDefinitions.map((def) => {
                          const selected = isArgSelected(def)
                          return (
                            <div
                              key={def.flag}
                              className="flex items-center"
                              style={{
                                background: selected
                                  ? 'var(--md-primary-tint-soft)'
                                  : 'var(--md-card-hover)',
                                borderRadius: 'var(--md-radius-small)',
                                padding: '8px 10px',
                                gap: 8,
                                opacity: selected ? 0.6 : 1,
                              }}
                            >
                              <div className="flex-1" style={{ minWidth: 0 }}>
                                <div
                                  style={{
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: 'var(--md-body)',
                                    marginBottom: 2,
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 6,
                                  }}
                                >
                                  {def.name}
                                  {def.recommended && (
                                    <span
                                      style={{
                                        fontSize: 9,
                                        color: 'var(--md-success)',
                                        fontWeight: 700,
                                        border: '1px solid var(--md-success)',
                                        borderRadius: 3,
                                        padding: '0 4px',
                                      }}
                                    >
                                      推荐
                                    </span>
                                  )}
                                  {def.warning && (
                                    <span
                                      style={{
                                        fontSize: 9,
                                        color: 'var(--md-error)',
                                        fontWeight: 700,
                                        border: '1px solid var(--md-error)',
                                        borderRadius: 3,
                                        padding: '0 4px',
                                      }}
                                    >
                                      警告
                                    </span>
                                  )}
                                </div>
                                <div
                                  style={{
                                    fontSize: 10.5,
                                    color: 'var(--md-body-light)',
                                    lineHeight: 1.4,
                                  }}
                                >
                                  {def.description}
                                </div>
                                <div
                                  style={{
                                    fontFamily: 'var(--md-font-mono)',
                                    fontSize: 10,
                                    color: 'var(--md-muted)',
                                    marginTop: 4,
                                  }}
                                >
                                  {def.flag}
                                </div>
                              </div>
                              {selected ? (
                                <span
                                  style={{
                                    fontSize: 11,
                                    color: 'var(--md-success)',
                                    fontWeight: 600,
                                  }}
                                >
                                  已添加
                                </span>
                              ) : (
                                <button
                                  onClick={() => handleAddArgument(def)}
                                  className="md-btn md-btn-primary"
                                  style={{ fontSize: 11, padding: '4px 10px' }}
                                  disabled={!jvmState?.hasServer || jvmState.isRunning}
                                >
                                  + 添加
                                </button>
                              )}
                            </div>
                          )
                        })}
                        {filteredDefinitions.length === 0 && (
                          <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                            <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                              该分类下暂无参数
                            </div>
                          </div>
                        )}
                      </div>
                    </Reveal>

                    {/* 自定义参数卡片 */}
                    <Reveal direction="up" delay={280} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        🛠️ 自定义参数
                      </div>
                      <div className="flex items-center" style={{ gap: 8 }}>
                        <input
                          value={customArgInput}
                          onChange={(e) => setCustomArgInput(e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleAddCustomArg()
                          }}
                          className="md-input flex-1"
                          placeholder="输入自定义参数，如 -XX:+UnlockExperimentalVMOptions"
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                        />
                        <button
                          onClick={handleAddCustomArg}
                          className="md-btn md-btn-primary"
                          style={{ fontSize: 11 }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning || !customArgInput.trim()}
                        >
                          添加
                        </button>
                      </div>
                    </Reveal>

                    {/* 参数编辑弹窗 */}
                    {editingArg && (
                      <div
                        style={{
                          position: 'fixed',
                          inset: 0,
                          background: 'rgba(0,0,0,0.5)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          zIndex: 9999,
                        }}
                        onClick={() => setEditingArg(null)}
                      >
                        <div
                          className="md-card"
                          style={{
                            padding: 20,
                            width: 360,
                            maxWidth: '90vw',
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <div
                            style={{
                              fontSize: 14,
                              fontWeight: 700,
                              marginBottom: 4,
                              color: 'var(--md-body)',
                            }}
                          >
                            {editingArg.mode === 'add' ? '添加参数' : '编辑参数'}
                          </div>
                          <div
                            style={{
                              fontSize: 11,
                              color: 'var(--md-body-light)',
                              marginBottom: 16,
                            }}
                          >
                            {editingArg.def.name}
                          </div>

                          {editingArg.def.description && (
                            <div
                              style={{
                                fontSize: 11,
                                color: 'var(--md-body-light)',
                                marginBottom: 12,
                                padding: 8,
                                background: 'var(--md-card-hover)',
                                borderRadius: 'var(--md-radius-small)',
                              }}
                            >
                              {editingArg.def.description}
                            </div>
                          )}

                          {/* 根据值类型显示不同控件 */}
                          {editingArg.def.valueType === 'BooleanFlag' ? (
                            <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
                              <button
                                onClick={() =>
                                  setEditingArg({ ...editingArg, value: 'true' })
                                }
                                className={clsx(
                                  'md-btn',
                                  editingArg.value === 'true'
                                    ? 'md-btn-primary'
                                    : 'md-btn-outlined',
                                )}
                                style={{ flex: 1 }}
                              >
                                ✓ 启用 (+)
                              </button>
                              <button
                                onClick={() =>
                                  setEditingArg({ ...editingArg, value: 'false' })
                                }
                                className={clsx(
                                  'md-btn',
                                  editingArg.value === 'false'
                                    ? 'md-btn-primary'
                                    : 'md-btn-outlined',
                                )}
                                style={{ flex: 1 }}
                              >
                                ✕ 禁用 (-)
                              </button>
                            </div>
                          ) : editingArg.def.valueType === 'Enum' &&
                            editingArg.def.allowedValues ? (
                            <div
                              style={{
                                display: 'flex',
                                flexDirection: 'column',
                                gap: 4,
                                marginBottom: 16,
                              }}
                            >
                              <div
                                style={{
                                  fontSize: 11,
                                  color: 'var(--md-body-light)',
                                  marginBottom: 4,
                                }}
                              >
                                选择值：
                              </div>
                              {editingArg.def.allowedValues.map((val) => (
                                <button
                                  key={val}
                                  onClick={() => setEditingArg({ ...editingArg, value: val })}
                                  className={clsx(
                                    'md-btn',
                                    editingArg.value === val
                                      ? 'md-btn-primary'
                                      : 'md-btn-outlined',
                                  )}
                                  style={{ textAlign: 'left', fontSize: 11 }}
                                >
                                  {val}
                                </button>
                              ))}
                            </div>
                          ) : (
                            <div style={{ marginBottom: 16 }}>
                              <div
                                style={{
                                  fontSize: 11,
                                  color: 'var(--md-body-light)',
                                  marginBottom: 4,
                                }}
                              >
                                值
                                {editingArg.def.defaultValue && (
                                  <span style={{ opacity: 0.7 }}>
                                    {' '}
                                    （默认：{editingArg.def.defaultValue}）
                                  </span>
                                )}
                              </div>
                              <input
                                value={editingArg.value}
                                onChange={(e) =>
                                  setEditingArg({ ...editingArg, value: e.target.value })
                                }
                                className="md-input"
                                placeholder={editingArg.def.defaultValue ?? ''}
                              />
                              {(editingArg.def.minimumValue || editingArg.def.maximumValue) && (
                                <div
                                  style={{
                                    fontSize: 10,
                                    color: 'var(--md-muted)',
                                    marginTop: 4,
                                  }}
                                >
                                  范围：
                                  {editingArg.def.minimumValue ?? '无下限'} ~{' '}
                                  {editingArg.def.maximumValue ?? '无上限'}
                                </div>
                              )}
                            </div>
                          )}

                          {editingArg.def.warning && (
                            <div
                              style={{
                                fontSize: 11,
                                color: 'var(--md-error)',
                                marginBottom: 12,
                                padding: 8,
                                background: 'rgba(239,68,68,0.1)',
                                borderRadius: 'var(--md-radius-small)',
                              }}
                            >
                              ⚠️ {editingArg.def.warning}
                            </div>
                          )}

                          <div className="flex items-center" style={{ gap: 8 }}>
                            <button
                              onClick={() => setEditingArg(null)}
                              className="md-btn md-btn-outlined"
                              style={{ flex: 1 }}
                            >
                              取消
                            </button>
                            <button
                              onClick={handleSaveEditingArg}
                              className="md-btn md-btn-primary"
                              style={{ flex: 1 }}
                            >
                              确定
                            </button>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* ─── 命令预览 Tab ─── */}
                {detailTab === 'command' && (
                  <div style={{ display: 'flex', flexDirection: 'column', minHeight: 400 }}>
                    <div className="flex items-center" style={{ marginBottom: 12, gap: 12 }}>
                      <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--md-body)' }}>
                        完整启动命令
                      </div>
                      <button
                        onClick={handleCopyCommand}
                        className="md-btn md-btn-outlined"
                      >
                        📋 复制
                      </button>
                    </div>
                    <div
                      className="md-terminal"
                      style={{
                        flex: 1,
                        padding: 16,
                        overflow: 'auto',
                        borderRadius: 'var(--md-radius)',
                      }}
                    >
                      <pre
                        style={{
                          fontFamily: 'var(--md-font-mono)',
                          fontSize: 13,
                          color: 'var(--md-success-foreground)',
                          whiteSpace: 'pre-wrap',
                          wordBreak: 'break-all',
                          margin: 0,
                        }}
                      >
                        {selectedServer.fullCommandLine}
                      </pre>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>

      {/* ═══ 底部：启动命令预览条 ═══ */}
      <div
        className="flex items-center"
        style={{
          background: 'var(--md-terminal-background)',
          borderTop: '1px solid var(--md-card-subtle-border)',
          padding: '8px 12px',
          gap: 6,
        }}
      >
        <span style={{ color: 'var(--md-success-foreground)', fontSize: 14 }}>▶</span>
        <span style={{ color: 'var(--md-success-foreground)', fontSize: 11, fontWeight: 600 }}>
          启动命令
        </span>
        <div
          className="flex-1"
          style={{
            fontFamily: 'var(--md-font-mono)',
            fontSize: 11,
            color: 'var(--md-success-foreground)',
            margin: '0 10px',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
          }}
          title={selectedServer?.fullCommandLine || ''}
        >
          {selectedServer?.fullCommandLine || ''}
        </div>
        <button
          onClick={handleCopyCommand}
          className="md-btn md-btn-flat md-btn-icon"
          title="复制启动命令到剪贴板"
          disabled={!selectedServer}
        >
          📋
        </button>
      </div>

      {/* ═══ 检测中遮罩 ═══ */}
      {isBusy && (
        <div
          className="absolute inset-0 flex flex-col items-center justify-center"
          style={{
            background: 'var(--md-loading-overlay)',
            borderRadius: 'var(--md-radius)',
          }}
        >
          <div
            className="md-spin"
            style={{
              width: 48,
              height: 48,
              border: '4px solid var(--md-white)',
              borderTopColor: 'transparent',
              borderRadius: '50%',
              marginBottom: 12,
            }}
          />
          <span style={{ color: 'var(--md-white)', fontSize: 14, opacity: 0.8 }}>
            {busyReason || '处理中...'}
          </span>
        </div>
      )}
    </div>
  )
}
