import { useCallback, useEffect, useRef, useState } from 'react'
import {
  FaFolderOpen,
  FaFolder,
  FaFileLines,
  FaArrowsRotate,
  FaPen,
  FaRotateLeft,
  FaRotate,
  FaFloppyDisk,
  FaLightbulb,
  FaChevronRight,
  FaCheck,
  FaCircleExclamation,
  FaPowerOff,
  FaTriangleExclamation,
} from 'react-icons/fa6'
import {
  getAvailableServers,
  getConfigFileTree,
  selectConfigFile,
  getConfigEntries,
  updateConfigValue,
  saveConfig,
  resetConfig,
  undoConfig,
  selectConfigServer,
  rescanConfigFiles,
} from '@/utils/bridge'
import { Reveal } from '@/components/ui/Reveal'
import { useToastStore } from '@/stores/toastStore'
import type {
  AvailableServer,
  ConfigFileItem,
  ConfigFileTreeResponse,
  ConfigEntry,
  ConfigEntryGroup,
  ConfigEntriesResponse,
} from '@/types/bridge'

// ─────────────────────────────────────────────────────────────────────
// 配置文件树节点（递归渲染，支持目录展开/折叠）
// ─────────────────────────────────────────────────────────────────────
interface ConfigTreeItemProps {
  node: ConfigFileItem
  depth: number
  selectedFile: string | null
  expandedDirs: Set<string>
  onSelectFile: (path: string) => void
  onToggleDir: (path: string) => void
}

function ConfigTreeItem({
  node,
  depth,
  selectedFile,
  expandedDirs,
  onSelectFile,
  onToggleDir,
}: ConfigTreeItemProps): JSX.Element {
  const isExpanded = expandedDirs.has(node.relativePath)
  const isSelected = selectedFile === node.relativePath
  const indent = 8 + depth * 12

  if (node.isDirectory) {
    return (
      <div className="md-tree-item">
        <div
          className="md-tree-item-header"
          style={{ paddingLeft: indent }}
          onClick={() => onToggleDir(node.relativePath)}
        >
          <FaChevronRight
            size={10}
            style={{
              color: 'var(--md-body-light)',
              transition: 'transform var(--md-duration-normal) var(--md-ease-standard)',
              transform: isExpanded ? 'rotate(90deg)' : 'none',
            }}
          />
          <FaFolder size={14} style={{ color: 'var(--md-primary-hue-mid)' }} />
          <span
            className="truncate"
            style={{
              fontSize: 'var(--md-font-size-base)',
              color: 'var(--md-body)',
            }}
          >
            {node.fileName}
          </span>
        </div>
        {isExpanded && node.children.length > 0 && (
          <div className="md-tree-item-children">
            {node.children.map((child) => (
              <ConfigTreeItem
                key={child.relativePath}
                node={child}
                depth={depth + 1}
                selectedFile={selectedFile}
                expandedDirs={expandedDirs}
                onSelectFile={onSelectFile}
                onToggleDir={onToggleDir}
              />
            ))}
          </div>
        )}
      </div>
    )
  }

  return (
    <div
      className={`md-tree-item-header ${isSelected ? 'md-tree-item-selected' : ''}`}
      style={{ paddingLeft: indent + 22 }}
      onClick={() => onSelectFile(node.relativePath)}
      title={node.relativePath}
    >
      <FaFileLines
        size={14}
        style={{ color: isSelected ? 'var(--md-primary-hue-mid)' : 'var(--md-body-light)' }}
      />
      <span
        className="truncate"
        style={{
          fontSize: 'var(--md-font-size-base)',
          color: 'var(--md-body)',
        }}
      >
        {node.fileName}
      </span>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
// 配置项编辑控件（根据类型动态选择）
// ─────────────────────────────────────────────────────────────────────
interface ConfigEntryEditorProps {
  entry: ConfigEntry
  displayValue: string
  onChange: (value: string) => void
}

function ConfigEntryEditor({
  entry,
  displayValue,
  onChange,
}: ConfigEntryEditorProps): JSX.Element {
  const controlStyle: React.CSSProperties = {
    width: 200,
    maxWidth: 400,
    height: 36,
  }

  if (entry.isBoolType) {
    return (
      <label className="md-toggle">
        <input
          type="checkbox"
          checked={displayValue === 'true'}
          onChange={(e) => onChange(e.target.checked ? 'true' : 'false')}
        />
        <span className="md-toggle-slider" />
      </label>
    )
  }

  if (entry.isEnumType) {
    return (
      <select
        className="md-select"
        style={controlStyle}
        value={displayValue}
        onChange={(e) => onChange(e.target.value)}
      >
        {(entry.allowedValues ?? []).map((v) => (
          <option key={v} value={v}>
            {v}
          </option>
        ))}
      </select>
    )
  }

  if (entry.isNumericType) {
    const rangeTip =
      entry.minValue != null && entry.maxValue != null
        ? `默认值: ${entry.originalValue}\n范围: ${entry.minValue} - ${entry.maxValue}`
        : `默认值: ${entry.originalValue}`
    return (
      <input
        type="number"
        className="md-input"
        style={controlStyle}
        value={displayValue}
        min={entry.minValue ?? undefined}
        max={entry.maxValue ?? undefined}
        onChange={(e) => onChange(e.target.value)}
        title={rangeTip}
        placeholder="输入数值"
      />
    )
  }

  return (
    <input
      type="text"
      className="md-input"
      style={controlStyle}
      value={displayValue}
      onChange={(e) => onChange(e.target.value)}
      placeholder="输入文本"
    />
  )
}

// ─────────────────────────────────────────────────────────────────────
// 配置编辑页主组件
// ─────────────────────────────────────────────────────────────────────
export function ConfigEditorPage(): JSX.Element {
  const showToast = useToastStore((s) => s.showToast)

  const [availableServers, setAvailableServers] = useState<AvailableServer[]>([])
  const [selectedServerName, setSelectedServerName] = useState<string | null>(null)
  const [serverWorkingDirectory, setServerWorkingDirectory] = useState('')
  const [configFileTree, setConfigFileTree] = useState<ConfigFileItem[]>([])
  const [configFileCountText, setConfigFileCountText] = useState('')
  const [hasServerDirectory, setHasServerDirectory] = useState(false)

  const [selectedConfigFile, setSelectedConfigFile] = useState<string | null>(null)
  const [selectedConfigFileName, setSelectedConfigFileName] = useState<string | null>(null)

  const [configGroups, setConfigGroups] = useState<ConfigEntryGroup[]>([])
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false)
  const [saveStatusMessage, setSaveStatusMessage] = useState<string | null>(null)
  const [isSaveError, setIsSaveError] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(0)
  const [isFetchingEntries, setIsFetchingEntries] = useState(false)
  const [isServerRunning, setIsServerRunning] = useState(false)
  const [modifiedCount, setModifiedCount] = useState(0)

  const [expandedDirs, setExpandedDirs] = useState<Set<string>>(new Set())
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set())
  const [pendingValues, setPendingValues] = useState<Record<string, string>>({})

  const [showSaveErrorModal, setShowSaveErrorModal] = useState(false)
  const [saveErrorInfo, setSaveErrorInfo] = useState<{ type: string; detail: string } | null>(null)
  const [showRestartConfirm, setShowRestartConfirm] = useState(false)

  const loadFileTree = useCallback(async (): Promise<void> => {
    try {
      const resp: ConfigFileTreeResponse = await getConfigFileTree()
      setConfigFileTree(resp.tree)
      setConfigFileCountText(resp.configFileCountText)
      setServerWorkingDirectory(resp.serverWorkingDirectory)
      setHasServerDirectory(resp.hasServerDirectory)
      setSelectedServerName(resp.selectedServerName)
    } catch (e) {
      console.error('获取配置文件树失败:', e)
    }
  }, [])

  const loadEntries = useCallback(async (): Promise<ConfigEntriesResponse | null> => {
    setIsFetchingEntries(true)
    try {
      const resp = await getConfigEntries()
      setConfigGroups(resp.groups)
      setHasUnsavedChanges(resp.hasUnsavedChanges)
      setSaveStatusMessage(resp.saveStatusMessage)
      setIsSaveError(resp.isSaveError)
      setIsLoading(resp.isLoading)
      setLoadProgress(resp.loadProgress)
      setSelectedConfigFile(resp.selectedConfigFile)
      setSelectedConfigFileName(resp.selectedConfigFileName)
      setIsServerRunning(resp.isCurrentServerRunning ?? false)
      setModifiedCount(resp.modifiedCount ?? 0)
      return resp
    } catch (e) {
      console.error('获取配置条目失败:', e)
      return null
    } finally {
      setIsFetchingEntries(false)
    }
  }, [])

  // 防抖定时器引用，用于配置项值变更
  const debounceTimerRef = useRef<Record<string, number>>({})

  // 初始化：拉取服务器列表 + 文件树
  useEffect(() => {
    const init = async (): Promise<void> => {
      try {
        const resp = await getAvailableServers()
        setAvailableServers(resp.servers)
        await loadFileTree()
      } catch (e) {
        console.error('初始化配置编辑器失败:', e)
      }
    }
    init()

    return () => {
      // 组件卸载时清理所有防抖定时器
      Object.values(debounceTimerRef.current).forEach((timer) => window.clearTimeout(timer))
      debounceTimerRef.current = {}
    }
  }, [loadFileTree])

  const handleSelectServer = async (name: string): Promise<void> => {
    if (name === selectedServerName) return
    try {
      await selectConfigServer(name)
      setSelectedServerName(name)
      setPendingValues({})
      setSelectedConfigFile(null)
      setSelectedConfigFileName(null)
      setConfigGroups([])
      setExpandedDirs(new Set())
      setExpandedGroups(new Set())
      setSaveStatusMessage(null)
      await loadFileTree()
    } catch (e) {
      console.error('选择服务器失败:', e)
    }
  }

  const handleRescan = async (): Promise<void> => {
    try {
      const result = await rescanConfigFiles()
      if (result.success) {
        await loadFileTree()
      } else {
        showToast('重新扫描失败', 'error')
      }
    } catch (e) {
      console.error('重新扫描失败:', e)
      showToast('重新扫描失败', 'error')
    }
  }

  const handleSelectFile = async (path: string): Promise<void> => {
    if (path === selectedConfigFile) return
    try {
      await selectConfigFile(path)
      setPendingValues({})
      setSelectedConfigFile(path)
      const resp = await loadEntries()
      if (resp) {
        // 默认展开所有分组（对应 WPF IsExpanded="True"）
        setExpandedGroups(new Set(resp.groups.map((g) => g.key)))
      }
    } catch (e) {
      console.error('选择配置文件失败:', e)
    }
  }

  const handleToggleDir = (path: string): void => {
    setExpandedDirs((prev) => {
      const next = new Set(prev)
      if (next.has(path)) next.delete(path)
      else next.add(path)
      return next
    })
  }

  const handleToggleGroup = (key: string): void => {
    setExpandedGroups((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  const handleValueChange = (entry: ConfigEntry, value: string): void => {
    // 本地立即更新（避免输入丢失焦点）
    setPendingValues((prev) => ({ ...prev, [entry.key]: value }))

    // 清除该配置项之前的防抖定时器
    const existingTimer = debounceTimerRef.current[entry.key]
    if (existingTimer) {
      window.clearTimeout(existingTimer)
    }

    // 数值和文本类型添加 300ms 防抖，布尔和枚举立即提交
    const delay = entry.isBoolType || entry.isEnumType ? 0 : 300

    debounceTimerRef.current[entry.key] = window.setTimeout(() => {
      updateConfigValue({ key: entry.key, value }).catch((e) =>
        console.error('更新配置值失败:', e)
      )
      delete debounceTimerRef.current[entry.key]
    }, delay)
  }

  const handleSave = async (): Promise<void> => {
    try {
      const result = await saveConfig()
      setSaveStatusMessage(result.message)
      setIsSaveError(!result.success)
      setPendingValues({})
      await loadEntries()

      if (result.success) {
        if (result.requiresRestart) {
          setShowRestartConfirm(true)
        } else {
          showToast('配置保存成功', 'success')
        }
      } else {
        if (result.errorType === 'FileLocked') {
          setSaveErrorInfo({
            type: result.errorType,
            detail: result.errorDetail ?? result.message ?? '',
          })
          setShowSaveErrorModal(true)
        } else {
          showToast(result.message ?? '保存失败', 'error')
        }
      }
    } catch (e) {
      console.error('保存配置失败:', e)
      setSaveStatusMessage('保存失败')
      setIsSaveError(true)
      showToast('保存失败', 'error')
    }
  }

  const handleReset = async (): Promise<void> => {
    try {
      const result = await resetConfig()
      if (result.success) {
        setPendingValues({})
        await loadEntries()
      } else {
        showToast('重置修改失败', 'error')
      }
    } catch (e) {
      console.error('重置修改失败:', e)
      showToast('重置修改失败', 'error')
    }
  }

  const handleUndo = async (): Promise<void> => {
    try {
      const result = await undoConfig()
      if (result.success) {
        setPendingValues({})
        await loadEntries()
      } else {
        showToast('撤销失败', 'error')
      }
    } catch (e) {
      console.error('撤销失败:', e)
      showToast('撤销失败', 'error')
    }
  }

  const getDisplayValue = (entry: ConfigEntry): string =>
    entry.key in pendingValues ? pendingValues[entry.key] : entry.value

  const isModifiedLocal = (entry: ConfigEntry): boolean =>
    getDisplayValue(entry) !== entry.originalValue

  const showLoading = isFetchingEntries || isLoading

  return (
    <div className="md-page-enter h-full p-3 flex gap-3">
      {/* ═══════════════════════════════════════════════════════════ */}
      {/* 📁 左侧：配置文件卡片（服务器选择 + 文件树 + 统计） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <Reveal
        direction="left"
        delay={0}
        className="md-card md-card-elevated flex flex-col flex-shrink-0 overflow-hidden"
        style={{ width: 280 }}
      >
        {/* 标题栏（主色背景） */}
        <div
          className="flex items-center px-4 py-3"
          style={{
            backgroundColor: 'var(--md-primary-hue-mid)',
            color: 'var(--md-white)',
          }}
        >
          <FaFolderOpen size={20} style={{ marginRight: 8 }} />
          <span style={{ fontSize: 15, fontWeight: 700 }}>配置文件</span>
        </div>

        {/* 服务器选择区 */}
        <div
          className="px-3 py-2.5"
          style={{
            borderBottom: '1px solid var(--md-card-subtle-border)',
            backgroundColor: 'var(--md-card-background)',
          }}
        >
          <div
            style={{
              fontSize: 'var(--md-font-size-sm)',
              fontWeight: 600,
              opacity: 0.6,
              marginBottom: 6,
            }}
          >
            选择服务器
          </div>
          <div className="flex gap-1">
            <select
              className="md-select"
              style={{ height: 32, fontSize: 12, flex: 1 }}
              value={selectedServerName ?? ''}
              onChange={(e) => handleSelectServer(e.target.value)}
            >
              {availableServers.length === 0 && <option value="">选择服务器...</option>}
              {availableServers.map((s) => (
                <option key={s.displayName} value={s.displayName}>
                  {s.displayName}
                </option>
              ))}
            </select>
            <button
              className="md-btn md-btn-outlined md-btn-icon"
              style={{ height: 32, width: 32 }}
              title="重新扫描配置文件"
              onClick={handleRescan}
            >
              <FaArrowsRotate size={14} />
            </button>
          </div>
          {serverWorkingDirectory && (
            <div
              className="truncate mt-1.5"
              style={{ fontSize: 10, opacity: 0.5 }}
              title={serverWorkingDirectory}
            >
              {serverWorkingDirectory}
            </div>
          )}
        </div>

        {/* 文件树 */}
        <div className="flex-1 overflow-y-auto p-2">
          {configFileTree.length === 0 ? (
            <div
              className="text-center py-8"
              style={{
                color: 'var(--md-body-lighter)',
                fontSize: 'var(--md-font-size-sm)',
              }}
            >
              {hasServerDirectory ? '暂无配置文件' : '请先选择服务器'}
            </div>
          ) : (
            configFileTree.map((node) => (
              <ConfigTreeItem
                key={node.relativePath}
                node={node}
                depth={0}
                selectedFile={selectedConfigFile}
                expandedDirs={expandedDirs}
                onSelectFile={handleSelectFile}
                onToggleDir={handleToggleDir}
              />
            ))
          )}
        </div>

        {/* 文件统计 */}
        <div
          className="px-3 py-2"
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            fontSize: 'var(--md-font-size-sm)',
            color: 'var(--md-body-light)',
            opacity: 0.7,
          }}
        >
          {configFileCountText}
        </div>
      </Reveal>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* ✏️ 右侧：编辑区（操作栏 + 配置项分组列表） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="flex-1 flex flex-col gap-3 min-w-0">
        {/* 顶部操作栏 */}
        <Reveal direction="up" delay={80} className="md-card md-card-elevated p-4 flex flex-col gap-2">
          <div className="flex items-center justify-between gap-3">
            {/* 左侧：当前文件名 + 副标题 */}
            <div className="min-w-0">
              <div className="flex items-center gap-3">
                <div
                  className="truncate"
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                  }}
                  title={selectedConfigFile ?? ''}
                >
                  {selectedConfigFileName ?? '未选择文件'}
                </div>
                {modifiedCount > 0 && (
                  <div
                    style={{
                      fontSize: 'var(--md-font-size-sm)',
                      color: 'var(--md-body-light)',
                      flexShrink: 0,
                    }}
                  >
                    已修改 {modifiedCount} 项
                  </div>
                )}
              </div>
              <div
                className="flex items-center mt-1"
                style={{ opacity: 0.6 }}
              >
                <FaPen size={14} style={{ marginRight: 4 }} />
                <span style={{ fontSize: 'var(--md-font-size-sm)' }}>配置编辑器</span>
              </div>
            </div>
            {/* 右侧：操作按钮 */}
            <div className="flex items-center gap-2 flex-shrink-0">
              <button
                className="md-btn md-btn-outlined"
                disabled={!hasUnsavedChanges || isServerRunning}
                title="撤销最近一次编辑"
                onClick={handleUndo}
              >
                <FaRotateLeft size={16} />
                撤销
              </button>
              <button
                className="md-btn md-btn-outlined"
                disabled={!hasUnsavedChanges || isServerRunning}
                onClick={handleReset}
              >
                <FaRotate size={16} />
                重置修改
              </button>
              <button
                className="md-btn md-btn-primary"
                disabled={!hasUnsavedChanges || isServerRunning}
                title="Ctrl+S 也可以保存哦"
                onClick={handleSave}
              >
                <FaFloppyDisk size={16} />
                保存配置
              </button>
            </div>
          </div>
          {/* 服务器运行警告横幅 */}
          {isServerRunning && (
            <div
              className="flex items-center"
              style={{
                height: 40,
                paddingLeft: 16,
                paddingRight: 16,
                backgroundColor: 'var(--md-warning-subtle-background)',
                borderRadius: 'var(--md-radius-small)',
                marginBottom: 12,
              }}
            >
              <FaTriangleExclamation
                size={18}
                style={{
                  color: 'var(--md-gauge-yellow)',
                  marginRight: 10,
                  flexShrink: 0,
                }}
              />
              <span
                style={{
                  fontSize: 'var(--md-font-size-base)',
                  color: 'var(--md-body)',
                }}
              >
                服务器正在运行，修改配置不会立即生效，请停止服务器后保存
              </span>
            </div>
          )}
          {/* 保存状态消息 */}
          {saveStatusMessage && (
            <div
              style={{
                fontSize: 'var(--md-font-size-base)',
                color: isSaveError
                  ? 'var(--md-error-text)'
                  : 'var(--md-gauge-green)',
              }}
            >
              {saveStatusMessage}
            </div>
          )}
        </Reveal>

        {/* 配置项列表区 */}
        <div className="flex-1 min-h-0 relative">
          {/* 加载遮罩 */}
          {showLoading && (
            <div
              className="absolute inset-0 flex flex-col items-center justify-center z-10"
              style={{
                backgroundColor: 'var(--md-loading-overlay)',
                borderRadius: 'var(--md-radius)',
              }}
            >
              <FaArrowsRotate
                size={48}
                className="md-spin"
                style={{ color: 'var(--md-primary-hue-mid)' }}
              />
              <div
                className="mt-4 mb-2"
                style={{ fontSize: 14, color: 'var(--md-body)' }}
              >
                正在加载配置...
              </div>
              <div className="md-progress" style={{ width: 200 }}>
                <div
                  className="md-progress-bar"
                  style={{ width: `${loadProgress}%` }}
                />
              </div>
              <div
                className="mt-1"
                style={{
                  fontSize: 'var(--md-font-size-sm)',
                  color: 'var(--md-body-light)',
                  opacity: 0.7,
                }}
              >
                {loadProgress}%
              </div>
            </div>
          )}

          {/* 空状态：尚未选择文件 */}
          {!selectedConfigFile && !showLoading && (
            <div className="h-full flex items-center justify-center">
              <Reveal
                direction="scale"
                delay={120}
                className="md-card md-card-elevated text-center"
                style={{ padding: '40px 48px' }}
              >
                <FaFileLines
                  size={72}
                  className="md-breathe"
                  style={{
                    color: 'var(--md-primary-hue-mid)',
                    opacity: 0.3,
                    margin: '0 auto',
                  }}
                />
                <div
                  className="mt-5 mb-1"
                  style={{
                    fontSize: 18,
                    fontWeight: 600,
                    color: 'var(--md-body)',
                  }}
                >
                  选择左侧的配置文件
                </div>
                <div
                  style={{
                    fontSize: 13,
                    opacity: 0.5,
                    color: 'var(--md-body)',
                  }}
                >
                  开始编辑服务器配置
                </div>
                <div
                  className="inline-flex items-center mt-5 px-3 py-2"
                  style={{
                    backgroundColor: 'var(--md-accent-subtle-border)',
                    borderRadius: 'var(--md-radius-small)',
                    color: 'var(--md-accent-text)',
                    fontSize: 12,
                  }}
                >
                  <FaLightbulb size={16} style={{ marginRight: 8 }} />
                  支持 server.properties / YAML / JSON 格式
                </div>
              </Reveal>
            </div>
          )}

          {/* 配置项分组列表（按分类分组的 Expander） */}
          {selectedConfigFile && !showLoading && (
            <div className="h-full overflow-y-auto pr-1">
              {configGroups.length === 0 ? (
                <div
                  className="text-center py-8"
                  style={{ color: 'var(--md-body-lighter)' }}
                >
                  该文件无可编辑的配置项
                </div>
              ) : (
                <div className="space-y-1">
                  {configGroups.map((group) => {
                    const isGroupExpanded = expandedGroups.has(group.key)
                    return (
                      <div key={group.key} className="md-expander">
                        {/* 分组标题 */}
                        <div
                          className="md-expander-header"
                          onClick={() => handleToggleGroup(group.key)}
                        >
                          <FaChevronRight
                            size={12}
                            className="md-expander-icon"
                            style={{
                              transform: isGroupExpanded
                                ? 'rotate(90deg)'
                                : 'none',
                            }}
                          />
                          <FaFolder
                            size={18}
                            style={{ color: 'var(--md-primary-hue-mid)' }}
                          />
                          <span
                            style={{
                              fontSize: 'var(--md-font-size-md)',
                              fontWeight: 700,
                              color: 'var(--md-primary-hue-mid)',
                            }}
                          >
                            {group.key}
                          </span>
                          <span className="md-badge" style={{ marginLeft: 10 }}>
                            {group.items.length}
                          </span>
                        </div>
                        {/* 分组内容：配置项卡片列表 */}
                        {isGroupExpanded && (
                          <div className="px-2 py-2 space-y-1.5">
                            {group.items.map((entry) => {
                              const modified = isModifiedLocal(entry)
                              const displayValue = getDisplayValue(entry)
                              return (
                                <div
                                  key={entry.key}
                                  className="relative rounded-lg p-3 border border-transparent transition-colors bg-[var(--md-card-background)] hover:bg-[var(--md-card-hover)] hover:border-[var(--md-accent-subtle-border)]"
                                  style={{
                                    borderRadius: 'var(--md-radius-small)',
                                  }}
                                >
                                  <div className="flex items-start justify-between gap-3">
                                    {/* 左侧：名称 + 描述 + 键名 */}
                                    <div className="min-w-0 flex-1">
                                      <div className="flex items-center">
                                        <span
                                          className="truncate"
                                          style={{
                                            fontSize: 13,
                                            fontWeight: 600,
                                            color: 'var(--md-body)',
                                          }}
                                          title={entry.friendlyDisplayName}
                                        >
                                          {entry.friendlyDisplayName}
                                        </span>
                                        {entry.requiresRestart && (
                                          <FaRotateLeft
                                            size={14}
                                            style={{
                                              marginLeft: 6,
                                              color: 'var(--md-gauge-yellow)',
                                            }}
                                            title="修改此项需要重启服务器"
                                          />
                                        )}
                                      </div>
                                      {entry.description && (
                                        <div
                                          className="mt-0.5 truncate"
                                          style={{
                                            fontSize: 'var(--md-font-size-sm)',
                                            color: 'var(--md-body-light)',
                                            opacity: 0.7,
                                          }}
                                          title={entry.description}
                                        >
                                          {entry.description}
                                        </div>
                                      )}
                                      <div
                                        className="mt-1 truncate"
                                        style={{
                                          fontSize: 10,
                                          color: 'var(--md-body-light)',
                                          opacity: 0.5,
                                          fontFamily: 'var(--md-font-mono)',
                                        }}
                                        title={entry.key}
                                      >
                                        {entry.key}
                                      </div>
                                    </div>
                                    {/* 右侧：编辑控件 + 错误提示 */}
                                    <div className="flex flex-col items-end flex-shrink-0">
                                      <ConfigEntryEditor
                                        entry={entry}
                                        displayValue={displayValue}
                                        onChange={(v) =>
                                          handleValueChange(entry, v)
                                        }
                                      />
                                      {!entry.isValid && entry.errorMessage && (
                                        <div
                                          className="mt-1.5"
                                          style={{
                                            color: 'var(--md-error-text)',
                                            fontSize: 'var(--md-font-size-sm)',
                                            fontWeight: 500,
                                          }}
                                        >
                                          {entry.errorMessage}
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                  {/* 修改状态指示器（右上角小圆点） */}
                                  {modified && (
                                    <span
                                      className="absolute rounded-full"
                                      style={{
                                        top: 8,
                                        right: 8,
                                        width: 8,
                                        height: 8,
                                        backgroundColor: 'var(--md-gauge-yellow)',
                                      }}
                                      title="已修改，尚未保存"
                                    />
                                  )}
                                </div>
                              )
                            })}
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {showSaveErrorModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'var(--md-modal-backdrop)',
            zIndex: 10000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            animation: 'mdFadeIn 0.2s ease-out',
          }}
          onClick={() => setShowSaveErrorModal(false)}
        >
          <div
            className="md-card"
            style={{
              width: 420,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-4">
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-error-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <FaCircleExclamation
                  size={24}
                  style={{ color: 'var(--md-error-text)' }}
                />
              </div>
              <div className="flex-1 min-w-0">
                <div
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                    marginBottom: 8,
                  }}
                >
                  保存失败 - 文件被占用
                </div>
                <div
                  style={{
                    fontSize: 'var(--md-font-size-base)',
                    color: 'var(--md-body-light)',
                    lineHeight: 1.6,
                  }}
                >
                  {saveErrorInfo?.detail}
                  <br />
                  请关闭正在使用该文件的程序（如服务器进程或文本编辑器）后重试
                </div>
              </div>
            </div>
            <div className="flex justify-end mt-6">
              <button
                className="md-btn md-btn-primary"
                onClick={() => setShowSaveErrorModal(false)}
              >
                我知道了
              </button>
            </div>
          </div>
        </div>
      )}

      {showRestartConfirm && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'var(--md-modal-backdrop)',
            zIndex: 10000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            animation: 'mdFadeIn 0.2s ease-out',
          }}
          onClick={() => setShowRestartConfirm(false)}
        >
          <div
            className="md-card"
            style={{
              width: 420,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-4">
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-primary-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <FaCheck size={24} style={{ color: 'var(--md-primary-hue-mid)' }} />
              </div>
              <div className="flex-1 min-w-0">
                <div
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                    marginBottom: 8,
                  }}
                >
                  保存成功，是否重启服务器？
                </div>
                <div
                  style={{
                    fontSize: 'var(--md-font-size-base)',
                    color: 'var(--md-body-light)',
                    lineHeight: 1.6,
                  }}
                >
                  部分配置需要重启服务器才能生效，是否现在重启？
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                className="md-btn md-btn-outlined"
                onClick={() => setShowRestartConfirm(false)}
              >
                稍后重启
              </button>
              <button
                className="md-btn md-btn-primary"
                onClick={() => {
                  setShowRestartConfirm(false)
                  showToast('重启功能开发中', 'info')
                }}
              >
                <FaPowerOff size={14} />
                立即重启
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
