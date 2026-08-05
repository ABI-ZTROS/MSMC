import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  FaPuzzlePiece,
  FaArrowsRotate,
  FaFolderOpen,
  FaTrashCan,
  FaGear,
  FaCloudArrowUp,
  FaCircleExclamation,
} from 'react-icons/fa6'
import {
  scanPlugins,
  togglePlugin,
  deletePlugin,
  openPluginFolder,
  getSelectedServer,
} from '@/utils/bridge'
import { Reveal } from '@/components/ui/Reveal'
import { useToastStore } from '@/stores/toastStore'
import type { PluginInfo } from '@/types/bridge'

export function PluginManagerPage(): JSX.Element {
  const navigate = useNavigate()
  const showToast = useToastStore((s) => s.showToast)
  const mountedRef = useRef(true)
  useEffect(() => {
    return () => {
      mountedRef.current = false
    }
  }, [])

  const [plugins, setPlugins] = useState<PluginInfo[]>([])
  const [serverDir, setServerDir] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [isDragOver, setIsDragOver] = useState(false)
  const [togglingFile, setTogglingFile] = useState<string | null>(null)
  const [deletingFile, setDeletingFile] = useState<string | null>(null)

  // 拉取当前选中服务器，再扫插件
  const refresh = useCallback(async (): Promise<void> => {
    setIsLoading(true)
    try {
      // 1) 先取当前选中的服务器，拿到 workingDirectory
      let dir = ''
      try {
        const sel = await getSelectedServer()
        if (sel?.workingDirectory) dir = sel.workingDirectory
      } catch (e) {
        console.warn('获取选中服务器失败:', e)
      }
      if (mountedRef.current) setServerDir(dir)

      if (!dir) {
        if (mountedRef.current) setPlugins([])
        showToast('请先在 Dashboard 选中服务器', 'warning')
        return
      }

      // 2) 扫插件
      const resp = await scanPlugins(dir)
      if (!mountedRef.current) return
      if (resp.success) {
        setPlugins(resp.items ?? [])
      } else {
        setPlugins([])
        showToast(`扫描插件失败: ${resp.error ?? '未知错误'}`, 'error')
      }
    } catch (e) {
      console.error('扫描插件失败:', e)
      if (mountedRef.current) {
        showToast(`扫描插件失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setIsLoading(false)
    }
  }, [showToast])

  // 进入页面自动扫描
  useEffect(() => {
    refresh()
  }, [refresh])

  // ── 拖放区：拖入 .jar 文件 ──
  const handleDrop = (e: React.DragEvent<HTMLDivElement>): void => {
    e.preventDefault()
    setIsDragOver(false)
    // 功能开发中：仅 toast 提示，不真处理文件
    const files = Array.from(e.dataTransfer.files ?? [])
    const jarCount = files.filter((f) => f.name.toLowerCase().endsWith('.jar')).length
    showToast(
      jarCount > 0
        ? `检测到 ${jarCount} 个 .jar 文件，拖放上传功能开发中`
        : '拖放上传功能开发中',
      'info',
    )
  }
  const handleDragOver = (e: React.DragEvent<HTMLDivElement>): void => {
    e.preventDefault()
    setIsDragOver(true)
  }
  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>): void => {
    e.preventDefault()
    setIsDragOver(false)
  }

  const handleToggle = async (p: PluginInfo): Promise<void> => {
    if (togglingFile) return
    setTogglingFile(p.filePath)
    try {
      const res = await togglePlugin(p.filePath, !p.enabled)
      if (!mountedRef.current) return
      if (res.success) {
        // 本地立即翻转状态
        setPlugins((prev) =>
          prev.map((it) =>
            it.filePath === p.filePath ? { ...it, enabled: !it.enabled } : it,
          ),
        )
        showToast(`${p.name} 已${!p.enabled ? '启用' : '禁用'}`, 'success')
      } else {
        showToast(`${p.name} 切换失败`, 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`切换失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setTogglingFile(null)
    }
  }

  const handleDelete = async (p: PluginInfo): Promise<void> => {
    if (deletingFile) return
    if (!window.confirm(`确定删除插件「${p.name}」吗？文件将被移除，操作不可撤销。`)) return
    setDeletingFile(p.filePath)
    try {
      const res = await deletePlugin(p.filePath)
      if (!mountedRef.current) return
      if (res.success) {
        setPlugins((prev) => prev.filter((it) => it.filePath !== p.filePath))
        showToast(`${p.name} 已删除`, 'success')
      } else {
        showToast(`${p.name} 删除失败`, 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`删除失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setDeletingFile(null)
    }
  }

  const handleOpenFolder = async (): Promise<void> => {
    if (!serverDir) {
      showToast('未检测到服务器目录', 'warning')
      return
    }
    // 插件目录通常为 serverDir/plugins
    const pluginsDir = `${serverDir}/plugins`
    try {
      const res = await openPluginFolder(pluginsDir)
      if (!mountedRef.current) return
      if (!res.success) showToast('打开文件夹失败', 'error')
    } catch (e) {
      if (mountedRef.current) {
        showToast(`打开失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    }
  }

  // 首字母大写用于圆形 logo
  const initialOf = (name: string): string => {
    const t = (name || '?').trim()
    return t.charAt(0).toUpperCase() || '?'
  }

  // 颜色映射：根据插件名首字母生成稳定色
  const avatarColor = (name: string): string => {
    const palette = [
      'var(--md-primary-hue-mid)',
      'var(--md-aquamarine-light)',
      'var(--md-accent-text)',
      'var(--md-gauge-green)',
      'var(--md-gauge-yellow)',
      'var(--md-aquamarine-soft)',
    ]
    let h = 0
    for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0
    return palette[h % palette.length]
  }

  return (
    <div className="md-page-enter h-full p-3 flex flex-col gap-3 overflow-y-auto">
      {/* 顶部：拖放区 */}
      <Reveal direction="up" delay={0}>
        <div
          className="md-card md-card-elevated p-6 flex flex-col items-center justify-center"
          style={{
            border: `2px dashed ${isDragOver ? 'var(--md-primary-hue-mid)' : 'var(--md-subtle-border)'}`,
            backgroundColor: isDragOver ? 'var(--md-primary-subtle-background)' : 'var(--md-card-background)',
            borderRadius: 'var(--md-radius)',
            transition: 'all 150ms var(--md-ease-standard)',
            minHeight: 120,
            cursor: 'pointer',
          }}
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
        >
          <FaCloudArrowUp
            size={36}
            className="md-breathe"
            style={{ color: 'var(--md-primary-hue-mid)', opacity: 0.7, marginBottom: 8 }}
          />
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            拖放 .jar 文件至此上传
          </div>
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4 }}>
            功能开发中（暂仅提示）
          </div>
        </div>
      </Reveal>

      {/* 操作栏 */}
      <Reveal direction="up" delay={60}>
        <div className="md-card md-card-elevated p-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FaPuzzlePiece size={18} style={{ color: 'var(--md-primary-hue-mid)' }} />
            <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--md-body)' }}>
              插件列表
            </span>
            <span className="md-badge" style={{ marginLeft: 8 }}>
              {plugins.length}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <button
              className="md-btn md-btn-outlined"
              onClick={refresh}
              disabled={isLoading}
              title="重新扫描插件"
            >
              <FaArrowsRotate size={14} className={isLoading ? 'md-spin' : ''} />
              扫描插件
            </button>
            <button
              className="md-btn md-btn-outlined"
              onClick={handleOpenFolder}
              title="在文件管理器中打开 plugins 目录"
            >
              <FaFolderOpen size={14} />
              打开文件夹
            </button>
          </div>
        </div>
      </Reveal>

      {/* 服务端目录提示 */}
      {serverDir && (
        <div
          className="truncate"
          style={{ fontSize: 11, color: 'var(--md-body-lighter)', padding: '0 4px' }}
          title={serverDir}
        >
          当前服务器: {serverDir}
        </div>
      )}

      {/* 网格卡片 */}
      {plugins.length === 0 ? (
        <Reveal direction="scale" delay={120} className="md-card md-card-elevated text-center" >
          <div style={{ padding: '40px 24px' }}>
            <FaPuzzlePiece
              size={56}
              className="md-breathe"
              style={{ color: 'var(--md-primary-hue-mid)', opacity: 0.3, margin: '0 auto' }}
            />
            <div style={{ marginTop: 16, fontSize: 15, fontWeight: 600, color: 'var(--md-body)' }}>
              {isLoading ? '正在扫描...' : '暂无插件'}
            </div>
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4 }}>
              将 .jar 文件放入服务器的 plugins 目录后点击「扫描插件」
            </div>
          </div>
        </Reveal>
      ) : (
        <div
          className="grid gap-3"
          style={{
            gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
          }}
        >
          {plugins.map((p, idx) => (
            <Reveal key={p.filePath} direction="up" delay={Math.min(idx * 30, 300)}>
              <div
                className="md-card md-card-elevated p-4 flex flex-col gap-2"
                style={{ height: '100%' }}
              >
                {/* 头部：圆形 logo + 名称 + 状态 */}
                <div className="flex items-start gap-3">
                  <div
                    className="flex items-center justify-center flex-shrink-0 rounded-full"
                    style={{
                      width: 40,
                      height: 40,
                      backgroundColor: 'var(--md-primary-subtle-background)',
                      color: avatarColor(p.name),
                      fontSize: 18,
                      fontWeight: 800,
                    }}
                  >
                    {initialOf(p.name)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div
                      className="truncate"
                      style={{ fontSize: 14, fontWeight: 700, color: 'var(--md-body)' }}
                      title={p.name}
                    >
                      {p.name || '(未知插件)'}
                    </div>
                    <div
                      className="flex items-center gap-2 mt-0.5"
                      style={{ fontSize: 11, color: 'var(--md-body-light)' }}
                    >
                      <span>v{p.version || '?'}</span>
                      {p.author && <span>· {p.author}</span>}
                    </div>
                  </div>
                </div>

                {/* 描述 */}
                <div
                  style={{
                    fontSize: 12,
                    color: 'var(--md-body-light)',
                    opacity: 0.85,
                    lineHeight: 1.5,
                    minHeight: 36,
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    overflow: 'hidden',
                  }}
                  title={p.description}
                >
                  {p.description || '(无描述)'}
                </div>

                {/* 无效标记 */}
                {!p.isValid && (
                  <div
                    className="flex items-center gap-1.5"
                    style={{
                      fontSize: 11,
                      color: 'var(--md-error-text)',
                      padding: '4px 8px',
                      borderRadius: 'var(--md-radius-small)',
                      backgroundColor: 'var(--md-error-subtle)',
                    }}
                  >
                    <FaCircleExclamation size={12} />
                    插件无效（缺少 plugin.yml 或解析失败）
                  </div>
                )}

                {/* 底部：操作按钮 */}
                <div className="flex items-center justify-between mt-auto pt-2"
                  style={{ borderTop: '1px solid var(--md-card-subtle-border)' }}
                >
                  {/* Toggle 开关 */}
                  <label
                    className="md-toggle"
                    title={p.enabled ? '点击禁用' : '点击启用'}
                    style={{ cursor: togglingFile === p.filePath ? 'wait' : 'pointer' }}
                  >
                    <input
                      type="checkbox"
                      checked={p.enabled}
                      disabled={togglingFile === p.filePath}
                      onChange={() => handleToggle(p)}
                    />
                    <span className="md-toggle-slider" />
                  </label>
                  <div className="flex items-center gap-1">
                    <button
                      className="md-btn md-btn-icon md-btn-outlined"
                      title="前往配置编辑"
                      onClick={() => navigate('/config')}
                      style={{ width: 32, height: 32 }}
                    >
                      <FaGear size={13} />
                    </button>
                    <button
                      className="md-btn md-btn-icon md-btn-danger"
                      title="删除插件"
                      onClick={() => handleDelete(p)}
                      disabled={deletingFile === p.filePath}
                      style={{ width: 32, height: 32 }}
                    >
                      <FaTrashCan size={13} />
                    </button>
                  </div>
                </div>
              </div>
            </Reveal>
          ))}
        </div>
      )}
    </div>
  )
}
