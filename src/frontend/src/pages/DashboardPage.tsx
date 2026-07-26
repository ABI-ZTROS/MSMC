import { useEffect, useState } from 'react'
import { clsx } from 'clsx'
import { bridge, getServerList, getSelectedServer, selectServer } from '@/utils/bridge'
import type { ServerInfo, KnownServerInfo, ServerListResponse } from '@/types/bridge'

// ─── 辅助函数 ───

// 状态点颜色：依据端口冲突 / 端口开放状态决定（与 WPF DataTrigger 一致）
function getRunningStatusDot(server: ServerInfo): string {
  const conflictStr = String(server.portConflict ?? '').toLowerCase()
  if (conflictStr === 'true' || conflictStr === '1') return 'md-status-dot-yellow'
  if (!server.isPortOpen) return 'md-status-dot-red'
  return 'md-status-dot-green'
}

// 从完整启动命令中解析出 JVM 参数（- 开头的 token）
function parseJvmArgs(cmd: string): string[] {
  if (!cmd) return []
  const tokens = cmd.match(/"[^"]+"|\S+/g) || []
  return tokens.filter((t) => t.startsWith('-'))
}

// 格式化字节数
function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return '-'
  if (bytes >= 1 << 30) return `${(bytes / (1 << 30)).toFixed(0)}G`
  if (bytes >= 1 << 20) return `${(bytes / (1 << 20)).toFixed(0)}M`
  if (bytes >= 1 << 10) return `${(bytes / (1 << 10)).toFixed(0)}K`
  return `${bytes}B`
}

// ─── 子组件：运行中服务器列表项 ───

interface RunningItemProps {
  server: ServerInfo
  isSelected: boolean
  onSelect: () => void
  onStart: () => void
}

function RunningServerItem({ server, isSelected, onSelect, onStart }: RunningItemProps): JSX.Element {
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
            onStart()
          }}
          className="md-btn md-btn-flat md-btn-icon"
          title="启动"
        >
          ▶
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

  // 拉取服务器列表
  const fetchServerList = async () => {
    try {
      const data = await getServerList()
      setServerList(data)
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
      await bridge.invoke('server:start')
      setOperationMessage('启动命令已发送')
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
      await bridge.invoke('server:stop')
      setOperationMessage('停止命令已发送')
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
      await bridge.invoke('server:import')
      await fetchServerList()
    } catch (e) {
      console.error('导入失败:', e)
    } finally {
      setIsBusy(false)
      setBusyReason('')
    }
  }

  const handleToggleAutoDetect = async () => {
    try {
      await bridge.invoke('server:toggleAutoDetect')
      setAutoDetectEnabled(!autoDetectEnabled)
    } catch (e) {
      console.error('切换自动检测失败:', e)
    }
  }

  const handleCopyCommand = () => {
    if (selectedServer?.fullCommandLine) {
      navigator.clipboard?.writeText(selectedServer.fullCommandLine).catch(() => {})
    }
  }

  useEffect(() => {
    fetchServerList()
    fetchSelectedServer()
    // 后台轮询，不触发忙碌遮罩
    const interval = setInterval(() => {
      fetchServerList()
      fetchSelectedServer()
    }, 3000)
    return () => clearInterval(interval)
  }, [])

  // 搜索过滤
  const keyword = searchKeyword.toLowerCase()
  const runningServers = (serverList?.running ?? []).filter(
    (s) => !keyword || s.displayName.toLowerCase().includes(keyword),
  )
  const knownServers = (serverList?.known ?? []).filter(
    (s) => !keyword || s.name.toLowerCase().includes(keyword),
  )

  const jvmArgs = selectedServer ? parseJvmArgs(selectedServer.fullCommandLine) : []

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
                  <RunningServerItem
                    key={`running-${idx}`}
                    server={server}
                    isSelected={selectedServer?.displayName === server.displayName}
                    onSelect={() => handleSelectServer(server.displayName)}
                    onStart={handleStart}
                  />
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
                  <KnownServerItem
                    key={`known-${idx}`}
                    server={server}
                    isSelected={selectedServer?.isKnown === true && selectedServer.displayName === server.name}
                    onSelect={() => handleSelectServer(server.name)}
                    onStart={() =>
                      bridge
                        .invoke('server:startKnown', server.name)
                        .then(fetchServerList)
                        .catch(console.error)
                    }
                    onDelete={() =>
                      bridge
                        .invoke('server:removeKnown', server.name)
                        .then(fetchServerList)
                        .catch(console.error)
                    }
                  />
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
                    <div className="md-card" style={{ padding: 16, marginBottom: 12 }}>
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
                          onClick={() =>
                            bridge
                              .invoke('server:saveAsKnown')
                              .then(fetchServerList)
                              .catch(console.error)
                          }
                          className="md-btn md-btn-outlined"
                          style={{ minHeight: 36, padding: '8px 16px' }}
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
                    </div>

                    {/* 服务器详情卡片 */}
                    <div className="md-card" style={{ padding: 16, marginBottom: 12 }}>
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
                    </div>

                    {/* 检测日志卡片 */}
                    <div className="md-card" style={{ padding: 16 }}>
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
                    </div>
                  </div>
                )}

                {/* ─── JVM 参数 Tab ─── */}
                {detailTab === 'jvm' && (
                  <div className="md-card" style={{ padding: 16 }}>
                    <div
                      style={{
                        fontSize: 15,
                        fontWeight: 700,
                        marginBottom: 12,
                        color: 'var(--md-body)',
                      }}
                    >
                      ⚙️ JVM 参数设置
                    </div>

                    {/* 内存设置 */}
                    <div className="md-subsection-title">内存设置</div>
                    <div className="grid grid-cols-2" style={{ gap: 12, marginBottom: 12 }}>
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
                          readOnly
                          value={formatBytes(selectedServer.initialHeapMemoryBytes)}
                          className="md-input"
                          placeholder="2G"
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
                          readOnly
                          value={formatBytes(selectedServer.maxHeapMemoryBytes)}
                          className="md-input"
                          placeholder="4G"
                        />
                      </div>
                    </div>

                    {/* 快速预设 */}
                    <div className="md-subsection-title">快速预设</div>
                    <div className="flex items-center" style={{ gap: 8, marginBottom: 12 }}>
                      <button
                        className="md-btn md-btn-outlined"
                        style={{ fontSize: 'var(--md-font-size-sm)' }}
                      >
                        🚀 Aikar
                      </button>
                      <button
                        className="md-btn md-btn-outlined"
                        style={{ fontSize: 'var(--md-font-size-sm)' }}
                      >
                        📊 G1GC
                      </button>
                      <button
                        className="md-btn md-btn-outlined"
                        style={{ fontSize: 'var(--md-font-size-sm)' }}
                      >
                        ⚡ ZGC
                      </button>
                    </div>

                    {/* GC 信息 */}
                    {selectedServer.gcType && (
                      <>
                        <div className="md-subsection-title">垃圾回收器</div>
                        <div style={{ marginBottom: 12 }}>
                          <span className="md-chip md-chip-primary">{selectedServer.gcType}</span>
                          {selectedServer.usesAikarFlags && (
                            <span className="md-chip md-chip-success" style={{ marginLeft: 6 }}>
                              ✨ Aikar 标志
                            </span>
                          )}
                        </div>
                      </>
                    )}

                    {/* 已选参数列表（从启动命令解析） */}
                    <div className="md-subsection-title">已选参数（来自启动命令）</div>
                    {jvmArgs.length === 0 ? (
                      <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                        <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                          暂无 JVM 参数
                        </div>
                      </div>
                    ) : (
                      <div>
                        {jvmArgs.map((arg, idx) => (
                          <div
                            key={idx}
                            className="flex items-center"
                            style={{
                              background: 'var(--md-card-hover)',
                              borderRadius: 'var(--md-radius-small)',
                              padding: '6px 8px',
                              marginBottom: 4,
                              gap: 8,
                            }}
                          >
                            <div
                              className="flex-1"
                              style={{
                                fontFamily: 'var(--md-font-mono)',
                                fontSize: 11,
                                color: 'var(--md-body)',
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                              }}
                            >
                              {arg}
                            </div>
                          </div>
                        ))}
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
