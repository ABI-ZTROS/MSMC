import { useEffect, useState } from 'react'
import { bridge } from '@/utils/bridge'
import { clsx } from 'clsx'

interface RunningServer {
  processId: number
  serverType: string
  workingDirectory: string
  serverJarName: string
  serverPort: number
  isPortOpen: boolean
  portConflict: boolean
  displayName: string
  status: string
  maxHeapMemoryBytes: number
  initialHeapMemoryBytes: number
  usesAikarFlags: boolean
  gcType: string
  configFiles: string[]
}

interface KnownServer {
  id: string
  name: string
  serverType: string
  workingDirectory: string
  serverJarPath: string
  javaPath: string
  lastSeen: string
  status: string
}

interface ServerListData {
  running: RunningServer[]
  known: KnownServer[]
  isBusy: boolean
  isAutoDetectEnabled: boolean
}

function formatBytes(bytes: number): string {
  if (bytes >= 1 << 30) return `${(bytes / (1 << 30)).toFixed(0)} GB`
  if (bytes >= 1 << 20) return `${(bytes / (1 << 20)).toFixed(0)} MB`
  if (bytes >= 1 << 10) return `${(bytes / (1 << 10)).toFixed(0)} KB`
  return `${bytes} B`
}

function getStatusDotColor(status: string): string {
  switch (status) {
    case 'Running': return 'bg-emerald-500'
    case 'Starting': return 'bg-amber-500'
    case 'Stopping': return 'bg-orange-500'
    case 'Stopped': return 'bg-slate-500'
    case 'Error': return 'bg-red-500'
    default: return 'bg-slate-500'
  }
}

export function DashboardPage(): JSX.Element {
  const [data, setData] = useState<ServerListData | null>(null)
  const [selectedTab, setSelectedTab] = useState<'running' | 'known'>('running')
  const [selectedServer, setSelectedServer] = useState<RunningServer | KnownServer | null>(null)
  const [detailTab, setDetailTab] = useState<'console' | 'properties' | 'jvm'>('console')

  const fetchServers = async () => {
    try {
      const result = await bridge.invoke<ServerListData>('server:list')
      setData(result)
    } catch (e) {
      console.error('获取服务器列表失败:', e)
    }
  }

  const handleRefresh = async () => {
    try {
      await bridge.invoke('server:refresh')
      await fetchServers()
    } catch (e) {
      console.error('刷新服务器列表失败:', e)
    }
  }

  useEffect(() => {
    fetchServers()
    const interval = setInterval(fetchServers, 3000)
    return () => clearInterval(interval)
  }, [])

  const runningServers = data?.running ?? []
  const knownServers = data?.known ?? []
  const displayList = selectedTab === 'running' ? runningServers : knownServers

  return (
    <div className="h-full flex flex-col">
      {/* 顶部操作条 */}
      <div className="bg-slate-900 border-b border-slate-700/50 px-4 py-2.5 flex items-center gap-3">
        <button
          onClick={handleRefresh}
          className={clsx(
            'px-3.5 py-1.5 text-sm font-medium rounded-md transition-colors flex items-center gap-2',
            data?.isBusy
              ? 'bg-blue-600/50 text-blue-200 cursor-not-allowed'
              : 'bg-blue-600 hover:bg-blue-500 text-white'
          )}
          disabled={data?.isBusy}
        >
          <span className={clsx(data?.isBusy && 'animate-spin')}>🔄</span>
          刷新
        </button>

        <button className="px-3.5 py-1.5 text-sm font-medium rounded-md bg-slate-700 hover:bg-slate-600 text-slate-200 border border-slate-600 transition-colors flex items-center gap-2">
          📥 导入
        </button>

        <div className="flex-1" />

        {data?.isAutoDetectEnabled ? (
          <span className="flex items-center gap-1.5 text-emerald-400 text-xs">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            自动检测中
          </span>
        ) : (
          <span className="flex items-center gap-1.5 text-slate-500 text-xs">
            <span className="w-2 h-2 rounded-full bg-slate-500" />
            自动检测已停止
          </span>
        )}
      </div>

      {/* 主体区域：左侧列表 + 右侧详情 */}
      <div className="flex-1 flex min-h-0">
        {/* 左侧服务器列表 */}
        <div className="w-[280px] border-r border-slate-700/50 flex flex-col bg-slate-900/50">
          {/* Tab 切换：运行中 / 已知 */}
          <div className="flex border-b border-slate-700/50">
            <button
              onClick={() => setSelectedTab('running')}
              className={clsx(
                'flex-1 px-3 py-2 text-sm font-medium transition-colors border-b-2',
                selectedTab === 'running'
                  ? 'text-blue-400 border-blue-500 bg-blue-500/5'
                  : 'text-slate-400 border-transparent hover:text-slate-200'
              )}
            >
              运行中 ({runningServers.length})
            </button>
            <button
              onClick={() => setSelectedTab('known')}
              className={clsx(
                'flex-1 px-3 py-2 text-sm font-medium transition-colors border-b-2',
                selectedTab === 'known'
                  ? 'text-blue-400 border-blue-500 bg-blue-500/5'
                  : 'text-slate-400 border-transparent hover:text-slate-200'
              )}
            >
              已知 ({knownServers.length})
            </button>
          </div>

          {/* 服务器列表 */}
          <div className="flex-1 overflow-y-auto p-1.5">
            {displayList.length === 0 ? (
              <div className="text-center py-8 text-slate-500 text-sm">
                {selectedTab === 'running' ? '暂无运行中的服务器' : '暂无已知服务器'}
              </div>
            ) : (
              <div className="space-y-0.5">
                {displayList.map((server, idx) => (
                  <div
                    key={idx}
                    onClick={() => setSelectedServer(server)}
                    className={clsx(
                      'px-2.5 py-2 rounded-md cursor-pointer transition-colors',
                      selectedServer === server
                        ? 'bg-blue-600/15 text-blue-300'
                        : 'hover:bg-slate-800 text-slate-300'
                    )}
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className={clsx(
                          'w-2 h-2 rounded-full flex-shrink-0',
                          getStatusDotColor(server.status)
                        )}
                      />
                      <div className="flex-1 min-w-0">
                        <div className="text-sm font-medium truncate">
                          {server.serverType}
                        </div>
                        <div className="text-[11px] text-slate-500 truncate">
                          {server.workingDirectory.split('\\').pop() || server.workingDirectory}
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* 右侧详情区 */}
        <div className="flex-1 flex flex-col min-w-0">
          {selectedServer ? (
            <>
              {/* 详情 Tab 头 */}
              <div className="bg-slate-900 border-b border-slate-700/50 px-4">
                <div className="flex items-center gap-1">
                  {[
                    { key: 'console', label: '控制台', icon: '💻' },
                    { key: 'properties', label: '服务器属性', icon: '📋' },
                    { key: 'jvm', label: 'JVM 参数', icon: '⚙️' },
                  ].map((tab) => (
                    <button
                      key={tab.key}
                      onClick={() => setDetailTab(tab.key as typeof detailTab)}
                      className={clsx(
                        'px-4 py-2.5 text-sm font-medium transition-colors border-b-2 -mb-px',
                        detailTab === tab.key
                          ? 'text-blue-400 border-blue-500'
                          : 'text-slate-400 border-transparent hover:text-slate-200'
                      )}
                    >
                      {tab.icon} {tab.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* 详情内容 */}
              <div className="flex-1 overflow-y-auto p-4">
                {detailTab === 'console' && (
                  <div className="bg-slate-950 rounded-md border border-slate-700/50 p-3 h-full">
                    <div className="text-emerald-400 font-mono text-xs space-y-1">
                      <div className="text-slate-500">[系统] 控制台输出将在此显示...</div>
                      <div className="text-slate-500">[提示] 选中运行中的服务器即可查看实时控制台</div>
                    </div>
                  </div>
                )}

                {detailTab === 'properties' && (
                  <div className="space-y-3">
                    <div className="bg-slate-900 rounded-md border border-slate-700/50 p-4">
                      <div className="text-sm font-semibold text-slate-200 mb-3">📊 服务器详情</div>
                      <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
                        <div>
                          <span className="text-slate-500">服务器类型：</span>
                          <span className="text-slate-200">{selectedServer.serverType}</span>
                        </div>
                        <div>
                          <span className="text-slate-500">状态：</span>
                          <span className={clsx(
                            getStatusDotColor(selectedServer.status).replace('bg-', 'text-')
                          )}>
                            {selectedServer.status}
                          </span>
                        </div>
                        <div>
                          <span className="text-slate-500">端口：</span>
                          <span className="text-slate-200">{(selectedServer as RunningServer).serverPort ?? '-'}</span>
                        </div>
                        <div>
                          <span className="text-slate-500">最大内存：</span>
                          <span className="text-slate-200">
                            {formatBytes((selectedServer as RunningServer).maxHeapMemoryBytes || 0)}
                          </span>
                        </div>
                        <div className="col-span-2">
                          <span className="text-slate-500">工作目录：</span>
                          <span className="text-slate-200 font-mono text-xs">{selectedServer.workingDirectory}</span>
                        </div>
                        <div className="col-span-2">
                          <span className="text-slate-500">JAR 文件：</span>
                          <span className="text-slate-200 font-mono text-xs">
                            {(selectedServer as RunningServer).serverJarName || (selectedServer as KnownServer).serverJarPath}
                          </span>
                        </div>
                        {(selectedServer as RunningServer).usesAikarFlags && (
                          <div className="col-span-2">
                            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 text-xs">
                              ✨ 使用 Aikar 推荐参数
                            </span>
                          </div>
                        )}
                        {(selectedServer as RunningServer).gcType && (
                          <div>
                            <span className="text-slate-500">垃圾回收器：</span>
                            <span className="text-slate-200">{(selectedServer as RunningServer).gcType}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                )}

                {detailTab === 'jvm' && (
                  <div className="bg-slate-900 rounded-md border border-slate-700/50 p-4">
                    <div className="text-sm font-semibold text-slate-200 mb-3">⚙️ JVM 参数</div>
                    <div className="text-xs text-slate-400">
                      JVM 参数配置将在此显示...
                    </div>
                  </div>
                )}
              </div>
            </>
          ) : (
            <div className="flex-1 flex items-center justify-center text-slate-500 text-sm">
              <div className="text-center">
                <div className="text-4xl mb-3">🎮</div>
                <div>选择一个服务器查看详情</div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
