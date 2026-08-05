import { useCallback, useEffect, useRef, useState } from 'react'
import {
  FaBoxArchive,
  FaFloppyDisk,
  FaRotate,
  FaTrashCan,
  FaArrowsRotate,
  FaCircle,
} from 'react-icons/fa6'
import {
  createBackup,
  listBackups,
  restoreBackup,
  deleteBackup,
  getSelectedServer,
} from '@/utils/bridge'
import { Reveal } from '@/components/ui/Reveal'
import { useToastStore } from '@/stores/toastStore'
import type { BackupSnapshot } from '@/types/bridge'

// 字节数 → MB 文本
function bytesToMB(bytes: number): string {
  if (!bytes || bytes <= 0) return '0 MB'
  const mb = bytes / (1024 * 1024)
  if (mb < 1) return `${(bytes / 1024).toFixed(1)} KB`
  return `${mb.toFixed(2)} MB`
}

// 时间戳格式化
function formatTimestamp(s: string): string {
  if (!s) return '-'
  // 尝试解析 ISO，失败则原样返回
  const d = new Date(s)
  if (isNaN(d.getTime())) return s
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

export function BackupManagerPage(): JSX.Element {
  const showToast = useToastStore((s) => s.showToast)
  const mountedRef = useRef(true)
  useEffect(() => {
    return () => {
      mountedRef.current = false
    }
  }, [])

  const [snapshots, setSnapshots] = useState<BackupSnapshot[]>([])
  const [serverDir, setServerDir] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [creating, setCreating] = useState(false)
  const [restoringPath, setRestoringPath] = useState<string | null>(null)
  const [deletingPath, setDeletingPath] = useState<string | null>(null)

  const ensureServerDir = useCallback(async (): Promise<string> => {
    try {
      const sel = await getSelectedServer()
      if (sel?.workingDirectory) {
        if (mountedRef.current) setServerDir(sel.workingDirectory)
        return sel.workingDirectory
      }
    } catch (e) {
      console.warn('获取选中服务器失败:', e)
    }
    return ''
  }, [])

  const loadList = useCallback(async (): Promise<void> => {
    setIsLoading(true)
    const dir = await ensureServerDir()
    if (!dir) {
      if (mountedRef.current) {
        setSnapshots([])
        showToast('请先在 Dashboard 选中服务器', 'warning')
      }
      setIsLoading(false)
      return
    }
    try {
      const resp = await listBackups(dir)
      if (!mountedRef.current) return
      if (resp.success) {
        setSnapshots(resp.snapshots ?? [])
      } else {
        setSnapshots([])
        showToast(`获取备份列表失败: ${resp.error ?? '未知错误'}`, 'error')
      }
    } catch (e) {
      console.error('获取备份列表失败:', e)
      if (mountedRef.current) {
        setSnapshots([])
        showToast(`获取备份列表失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setIsLoading(false)
    }
  }, [ensureServerDir, showToast])

  useEffect(() => {
    loadList()
  }, [loadList])

  // ── 立即备份：prompt 输入标签 → createBackup ──
  const handleCreate = async (): Promise<void> => {
    if (creating) return
    if (!serverDir) {
      showToast('未检测到服务器目录', 'warning')
      return
    }
    const label = window.prompt('请输入备份标签（可留空）', '') ?? null
    // 用户取消（点 Cancel）时 prompt 返回 null，直接 return
    if (label === null) return
    setCreating(true)
    try {
      const resp = await createBackup(serverDir, label.trim() || undefined)
      if (!mountedRef.current) return
      if (resp.success) {
        showToast('备份已创建', 'success')
        await loadList()
      } else {
        showToast('备份创建失败', 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`备份创建失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setCreating(false)
    }
  }

  // ── 还原 ──
  const handleRestore = async (snap: BackupSnapshot): Promise<void> => {
    if (restoringPath) return
    if (!window.confirm(`确定还原备份「${snap.label || formatTimestamp(snap.timestamp)}」吗？当前世界数据将被覆盖。`)) return
    setRestoringPath(snap.backupFilePath)
    try {
      const resp = await restoreBackup(serverDir, snap.backupFilePath)
      if (!mountedRef.current) return
      if (resp.success) {
        showToast('备份已还原', 'success')
      } else {
        showToast(`还原失败: ${resp.error ?? '未知错误'}`, 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`还原失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setRestoringPath(null)
    }
  }

  // ── 删除 ──
  const handleDelete = async (snap: BackupSnapshot): Promise<void> => {
    if (deletingPath) return
    if (!window.confirm(`确定删除备份「${snap.label || formatTimestamp(snap.timestamp)}」吗？操作不可撤销。`)) return
    setDeletingPath(snap.backupFilePath)
    try {
      const resp = await deleteBackup(snap.backupFilePath)
      if (!mountedRef.current) return
      if (resp.success) {
        setSnapshots((prev) => prev.filter((s) => s.backupFilePath !== snap.backupFilePath))
        showToast('备份已删除', 'success')
      } else {
        showToast('删除失败', 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`删除失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setDeletingPath(null)
    }
  }

  return (
    <div className="md-page-enter h-full p-3 flex flex-col gap-3 overflow-y-auto">
      {/* 顶部操作栏 */}
      <Reveal direction="up" delay={0}>
        <div className="md-card md-card-elevated p-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FaBoxArchive size={18} style={{ color: 'var(--md-primary-hue-mid)' }} />
            <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--md-body)' }}>
              备份恢复
            </span>
            <span className="md-badge" style={{ marginLeft: 8 }}>
              {snapshots.length}
            </span>
          </div>
          <div className="flex items-center gap-2">
            {serverDir && (
              <span
                className="truncate"
                style={{ fontSize: 11, color: 'var(--md-body-lighter)', maxWidth: 280 }}
                title={serverDir}
              >
                {serverDir}
              </span>
            )}
            <button
              className="md-btn md-btn-outlined"
              onClick={loadList}
              disabled={isLoading}
              title="刷新备份列表"
              style={{ height: 34 }}
            >
              <FaArrowsRotate size={13} className={isLoading ? 'md-spin' : ''} />
              刷新
            </button>
            <button
              className="md-btn md-btn-primary"
              onClick={handleCreate}
              disabled={creating || !serverDir}
              title="立即创建备份"
              style={{ height: 34 }}
            >
              <FaFloppyDisk size={14} />
              立即备份
            </button>
          </div>
        </div>
      </Reveal>

      {/* 时间线 */}
      {snapshots.length === 0 ? (
        <Reveal direction="scale" delay={80} className="md-card md-card-elevated text-center">
          <div style={{ padding: '48px 24px' }}>
            <FaBoxArchive
              size={56}
              className="md-breathe"
              style={{ color: 'var(--md-primary-hue-mid)', opacity: 0.3, margin: '0 auto' }}
            />
            <div style={{ marginTop: 16, fontSize: 15, fontWeight: 600, color: 'var(--md-body)' }}>
              {isLoading ? '正在加载...' : '暂无备份'}
            </div>
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4 }}>
              点击右上「立即备份」创建第一个备份
            </div>
          </div>
        </Reveal>
      ) : (
        <div className="relative pl-6">
          {/* 时间线主轴 */}
          <div
            aria-hidden
            style={{
              position: 'absolute',
              left: 12,
              top: 8,
              bottom: 8,
              width: 2,
              backgroundColor: 'var(--md-subtle-border)',
            }}
          />
          <div className="space-y-3">
            {snapshots.map((snap, idx) => (
              <Reveal key={snap.backupFilePath + idx} direction="up" delay={Math.min(idx * 40, 320)}>
                <div className="relative">
                  {/* 节点圆点 */}
                  <div
                    style={{
                      position: 'absolute',
                      left: -22,
                      top: 18,
                      width: 12,
                      height: 12,
                      borderRadius: '50%',
                      backgroundColor: 'var(--md-primary-hue-mid)',
                      boxShadow: '0 0 0 3px var(--md-card-background), 0 0 8px var(--md-primary-hue-light)',
                    }}
                  />
                  <div className="md-card md-card-elevated p-4">
                    <div className="flex items-start justify-between gap-3 flex-wrap">
                      {/* 左侧：标签 + 时间 + 大小 + 世界名 */}
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span
                            style={{
                              fontSize: 14,
                              fontWeight: 700,
                              color: 'var(--md-body)',
                            }}
                          >
                            {snap.label || '(未命名备份)'}
                          </span>
                          <span
                            className="inline-flex items-center"
                            style={{
                              fontSize: 11,
                              color: 'var(--md-body-light)',
                              padding: '2px 8px',
                              borderRadius: 'var(--md-radius-small)',
                              backgroundColor: 'var(--md-primary-subtle-background)',
                            }}
                          >
                            {bytesToMB(snap.sizeBytes)}
                          </span>
                        </div>
                        <div
                          className="mt-1 flex items-center gap-2"
                          style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.85 }}
                        >
                          <FaCircle size={6} style={{ color: 'var(--md-gauge-green)' }} />
                          <span>{formatTimestamp(snap.timestamp)}</span>
                          {snap.sha1 && (
                            <span
                              style={{ fontFamily: 'var(--md-font-mono)', opacity: 0.6 }}
                              title={snap.sha1}
                            >
                              · sha1: {snap.sha1.substring(0, 8)}...
                            </span>
                          )}
                        </div>
                        {snap.worldNames && snap.worldNames.length > 0 && (
                          <div
                            className="mt-2 flex flex-wrap gap-1"
                          >
                            {snap.worldNames.map((w) => (
                              <span
                                key={w}
                                style={{
                                  fontSize: 10,
                                  padding: '1px 6px',
                                  borderRadius: 'var(--md-radius-small)',
                                  backgroundColor: 'var(--md-card-hover)',
                                  color: 'var(--md-body-light)',
                                }}
                              >
                                {w}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                      {/* 右侧：还原 / 删除 */}
                      <div className="flex items-center gap-2 flex-shrink-0">
                        <button
                          className="md-btn md-btn-outlined"
                          onClick={() => handleRestore(snap)}
                          disabled={restoringPath === snap.backupFilePath}
                          title="还原此备份"
                          style={{ height: 32 }}
                        >
                          <FaRotate size={13} className={restoringPath === snap.backupFilePath ? 'md-spin' : ''} />
                          还原
                        </button>
                        <button
                          className="md-btn md-btn-icon md-btn-danger"
                          onClick={() => handleDelete(snap)}
                          disabled={deletingPath === snap.backupFilePath}
                          title="删除此备份"
                          style={{ width: 32, height: 32 }}
                        >
                          <FaTrashCan size={13} />
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
