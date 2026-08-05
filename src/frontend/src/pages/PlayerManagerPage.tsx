import { useCallback, useEffect, useRef, useState } from 'react'
import {
  FaUsers,
  FaGavel,
  FaCrown,
  FaCircle,
  FaPlus,
  FaXmark,
  FaArrowsRotate,
} from 'react-icons/fa6'
import {
  getOnlinePlayers,
  listPlayerFiles,
  upsertPlayerEntry,
  removePlayerEntry,
  getSelectedServer,
} from '@/utils/bridge'
import { Reveal } from '@/components/ui/Reveal'
import { useToastStore } from '@/stores/toastStore'
import type { OnlinePlayer } from '@/types/bridge'

// Tab 类型：在线 / 白名单 / 封禁 / OP
type TabKey = 'online' | 'wl' | 'ban' | 'ops'

interface TabDef {
  key: TabKey
  label: string
  icon: React.ReactNode
}

const TABS: TabDef[] = [
  { key: 'online', label: '在线', icon: <FaCircle size={12} style={{ color: 'var(--md-gauge-green)' }} /> },
  { key: 'wl', label: '白名单', icon: <FaUsers size={14} /> },
  { key: 'ban', label: '封禁', icon: <FaGavel size={14} /> },
  { key: 'ops', label: 'OP', icon: <FaCrown size={14} /> },
]

// 列表条目统一形态：name + uuid（白名单/封禁/OP 共用）
interface PlayerEntry {
  name?: string
  uuid?: string
  [k: string]: unknown
}

export function PlayerManagerPage(): JSX.Element {
  const showToast = useToastStore((s) => s.showToast)
  const mountedRef = useRef(true)
  useEffect(() => {
    return () => {
      mountedRef.current = false
    }
  }, [])

  const [tab, setTab] = useState<TabKey>('online')
  const [serverDir, setServerDir] = useState('')

  // 在线玩家
  const [onlinePlayers, setOnlinePlayers] = useState<OnlinePlayer[]>([])
  // 白名单 / 封禁 / OP 三个 list 共用一个 state（按当前 tab 切换内容）
  const [entries, setEntries] = useState<PlayerEntry[]>([])
  const [isLoading, setIsLoading] = useState(false)

  // 添加 modal 状态
  const [showAddModal, setShowAddModal] = useState(false)
  const [addName, setAddName] = useState('')
  const [addUuid, setAddUuid] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  // 取选中服务器
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

  // 在线玩家
  const loadOnline = useCallback(async (): Promise<void> => {
    setIsLoading(true)
    try {
      const resp = await getOnlinePlayers()
      if (!mountedRef.current) return
      if (resp.success) {
        setOnlinePlayers(resp.players ?? [])
      } else {
        setOnlinePlayers([])
      }
    } catch (e) {
      console.error('获取在线玩家失败:', e)
      if (mountedRef.current) setOnlinePlayers([])
    } finally {
      if (mountedRef.current) setIsLoading(false)
    }
  }, [])

  // 列表型（白名单/封禁/OP）
  const loadEntries = useCallback(
    async (type: TabKey): Promise<void> => {
      setIsLoading(true)
      const dir = await ensureServerDir()
      if (!dir) {
        if (mountedRef.current) {
          setEntries([])
          showToast('请先在 Dashboard 选中服务器', 'warning')
        }
        setIsLoading(false)
        return
      }
      try {
        const resp = await listPlayerFiles(type, dir)
        if (!mountedRef.current) return
        if (resp.success) {
          setEntries((resp.entries ?? []) as PlayerEntry[])
        } else {
          setEntries([])
          showToast(`获取列表失败: ${resp.error ?? '未知错误'}`, 'error')
        }
      } catch (e) {
        console.error('获取玩家列表失败:', e)
        if (mountedRef.current) {
          setEntries([])
          showToast(`获取列表失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
        }
      } finally {
        if (mountedRef.current) setIsLoading(false)
      }
    },
    [ensureServerDir, showToast],
  )

  // 切换 tab 时加载数据
  useEffect(() => {
    if (tab === 'online') loadOnline()
    else loadEntries(tab)
  }, [tab, loadOnline, loadEntries])

  // ── 添加条目 ──
  const handleAdd = async (): Promise<void> => {
    const name = addName.trim()
    if (!name) {
      showToast('请输入名称', 'warning')
      return
    }
    setIsSubmitting(true)
    try {
      const dir = await ensureServerDir()
      if (!dir) {
        showToast('未检测到服务器目录', 'warning')
        return
      }
      const entry: PlayerEntry = { name, uuid: addUuid.trim() || undefined }
      const res = await upsertPlayerEntry(tab, dir, entry)
      if (!mountedRef.current) return
      if (res.success) {
        showToast(`已添加「${name}」`, 'success')
        setShowAddModal(false)
        setAddName('')
        setAddUuid('')
        await loadEntries(tab)
      } else {
        showToast('添加失败', 'error')
      }
    } catch (e) {
      if (mountedRef.current) {
        showToast(`添加失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
      }
    } finally {
      if (mountedRef.current) setIsSubmitting(false)
    }
  }

  // ── 移除条目 ──
  const handleRemove = async (e: PlayerEntry): Promise<void> => {
    const id = e.uuid || e.name || ''
    if (!id) {
      showToast('缺少标识符', 'warning')
      return
    }
    if (!window.confirm(`确定移除「${e.name || e.uuid}」？`)) return
    try {
      const dir = await ensureServerDir()
      if (!dir) {
        showToast('未检测到服务器目录', 'warning')
        return
      }
      const res = await removePlayerEntry(tab, dir, id)
      if (!mountedRef.current) return
      if (res.success) {
        showToast(`已移除「${e.name || e.uuid}」`, 'success')
        await loadEntries(tab)
      } else {
        showToast('移除失败', 'error')
      }
    } catch (err) {
      if (mountedRef.current) {
        showToast(`移除失败: ${err instanceof Error ? err.message : String(err)}`, 'error')
      }
    }
  }

  // 刷新当前 tab
  const handleRefresh = (): void => {
    if (tab === 'online') loadOnline()
    else loadEntries(tab)
  }

  return (
    <div className="md-page-enter h-full p-3 flex flex-col gap-3">
      {/* 顶部：Tab 切换 + 刷新 */}
      <Reveal direction="up" delay={0}>
        <div className="md-card md-card-elevated p-2 flex items-center justify-between">
          <div className="flex items-center gap-1">
            {TABS.map((t) => {
              const active = tab === t.key
              return (
                <button
                  key={t.key}
                  onClick={() => setTab(t.key)}
                  className="md-btn md-btn-outlined"
                  style={{
                    height: 34,
                    padding: '0 14px',
                    fontSize: 13,
                    fontWeight: active ? 700 : 500,
                    borderColor: active ? 'var(--md-primary-hue-mid)' : 'var(--md-subtle-border)',
                    backgroundColor: active ? 'var(--md-primary-subtle-background)' : 'transparent',
                    color: active ? 'var(--md-primary-hue-mid)' : 'var(--md-body-light)',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                  }}
                >
                  {t.icon}
                  {t.label}
                </button>
              )
            })}
          </div>
          <div className="flex items-center gap-2">
            {serverDir && (
              <span
                className="truncate"
                style={{ fontSize: 11, color: 'var(--md-body-lighter)', maxWidth: 300 }}
                title={serverDir}
              >
                {serverDir}
              </span>
            )}
            <button
              className="md-btn md-btn-icon md-btn-outlined"
              title="刷新"
              onClick={handleRefresh}
              disabled={isLoading}
              style={{ width: 34, height: 34 }}
            >
              <FaArrowsRotate size={14} className={isLoading ? 'md-spin' : ''} />
            </button>
            {tab !== 'online' && (
              <button
                className="md-btn md-btn-primary"
                onClick={() => setShowAddModal(true)}
                style={{ height: 34 }}
              >
                <FaPlus size={12} />
                添加
              </button>
            )}
          </div>
        </div>
      </Reveal>

      {/* 内容区 */}
      <div className="flex-1 min-h-0 overflow-y-auto">
        {tab === 'online' ? (
          <OnlineListView players={onlinePlayers} isLoading={isLoading} onRefresh={handleRefresh} />
        ) : (
          <EntryListView
            entries={entries}
            isLoading={isLoading}
            tab={tab}
            onRemove={handleRemove}
            onRefresh={handleRefresh}
          />
        )}
      </div>

      {/* 添加 modal */}
      {showAddModal && (
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
          onClick={() => setShowAddModal(false)}
        >
          <div
            className="md-card"
            style={{
              width: 380,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)', marginBottom: 16 }}>
              添加{tab === 'wl' ? '白名单' : tab === 'ban' ? '封禁' : 'OP'}条目
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div>
                <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                  玩家名 *
                </label>
                <input
                  type="text"
                  className="md-input"
                  style={{ width: '100%', height: 36 }}
                  placeholder="如 Steve"
                  value={addName}
                  onChange={(e) => setAddName(e.target.value)}
                  autoFocus
                />
              </div>
              <div>
                <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                  UUID（可选）
                </label>
                <input
                  type="text"
                  className="md-input"
                  style={{ width: '100%', height: 36, fontFamily: 'var(--md-font-mono)', fontSize: 11 }}
                  placeholder="00000000-0000-0000-0000-000000000000"
                  value={addUuid}
                  onChange={(e) => setAddUuid(e.target.value)}
                />
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                className="md-btn md-btn-outlined"
                onClick={() => setShowAddModal(false)}
                disabled={isSubmitting}
              >
                取消
              </button>
              <button className="md-btn md-btn-primary" onClick={handleAdd} disabled={isSubmitting}>
                <FaPlus size={12} />
                确认添加
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ── 在线玩家视图 ──
function OnlineListView({
  players,
  isLoading,
  onRefresh,
}: {
  players: OnlinePlayer[]
  isLoading: boolean
  onRefresh: () => void
}): JSX.Element {
  if (isLoading && players.length === 0) {
    return (
      <div className="md-card md-card-elevated text-center" style={{ padding: 40 }}>
        <FaArrowsRotate size={36} className="md-spin" style={{ color: 'var(--md-primary-hue-mid)' }} />
        <div style={{ marginTop: 12, fontSize: 13, color: 'var(--md-body-light)' }}>正在获取在线玩家...</div>
      </div>
    )
  }
  if (players.length === 0) {
    return (
      <Reveal direction="scale" delay={80} className="md-card md-card-elevated text-center">
        <div style={{ padding: '40px 24px' }}>
          <FaCircle size={48} className="md-breathe" style={{ color: 'var(--md-gauge-green)', opacity: 0.3, margin: '0 auto' }} />
          <div style={{ marginTop: 16, fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            当前无在线玩家
          </div>
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4 }}>
            服务器未运行或日志中未检测到登录记录
          </div>
          <button className="md-btn md-btn-outlined mt-4" onClick={onRefresh}>
            <FaArrowsRotate size={12} />
            重新获取
          </button>
        </div>
      </Reveal>
    )
  }
  return (
    <div className="grid gap-2" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))' }}>
      {players.map((p, idx) => (
        <Reveal key={p.name + idx} direction="up" delay={Math.min(idx * 20, 200)}>
          <div className="md-card md-card-elevated p-3 flex items-center gap-3">
            <div
              className="rounded-full flex-shrink-0"
              style={{
                width: 10,
                height: 10,
                backgroundColor: p.online ? 'var(--md-gauge-green)' : 'var(--md-body-lighter)',
                boxShadow: p.online ? '0 0 8px var(--md-gauge-green)' : 'none',
              }}
            />
            <div className="flex-1 min-w-0">
              <div className="truncate" style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                {p.name}
              </div>
              <div style={{ fontSize: 11, color: 'var(--md-body-light)' }}>
                {p.online ? '在线' : `上次: ${p.lastSeen || '-'}`}
              </div>
            </div>
          </div>
        </Reveal>
      ))}
    </div>
  )
}

// ── 列表条目视图（白名单 / 封禁 / OP） ──
function EntryListView({
  entries,
  isLoading,
  tab,
  onRemove,
  onRefresh,
}: {
  entries: PlayerEntry[]
  isLoading: boolean
  tab: TabKey
  onRemove: (e: PlayerEntry) => void
  onRefresh: () => void
}): JSX.Element {
  const emptyText =
    tab === 'wl' ? '暂无白名单条目' : tab === 'ban' ? '暂无封禁条目' : '暂无 OP 条目'
  if (isLoading && entries.length === 0) {
    return (
      <div className="md-card md-card-elevated text-center" style={{ padding: 40 }}>
        <FaArrowsRotate size={36} className="md-spin" style={{ color: 'var(--md-primary-hue-mid)' }} />
        <div style={{ marginTop: 12, fontSize: 13, color: 'var(--md-body-light)' }}>加载中...</div>
      </div>
    )
  }
  if (entries.length === 0) {
    return (
      <Reveal direction="scale" delay={80} className="md-card md-card-elevated text-center">
        <div style={{ padding: '40px 24px' }}>
          <FaUsers size={48} className="md-breathe" style={{ color: 'var(--md-primary-hue-mid)', opacity: 0.3, margin: '0 auto' }} />
          <div style={{ marginTop: 16, fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {emptyText}
          </div>
          <button className="md-btn md-btn-outlined mt-4" onClick={onRefresh}>
            <FaArrowsRotate size={12} />
            重新加载
          </button>
        </div>
      </Reveal>
    )
  }
  return (
    <div className="space-y-1.5">
      {entries.map((e, idx) => (
        <Reveal key={(e.uuid || e.name || '') + idx} direction="up" delay={Math.min(idx * 15, 200)}>
          <div className="md-card md-card-elevated p-3 flex items-center gap-3">
            <div
              className="flex items-center justify-center flex-shrink-0 rounded-full"
              style={{
                width: 32,
                height: 32,
                backgroundColor: 'var(--md-primary-subtle-background)',
                color: 'var(--md-primary-hue-mid)',
                fontSize: 13,
                fontWeight: 800,
              }}
            >
              {(e.name || '?').charAt(0).toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <div className="truncate" style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                {e.name || '(未命名)'}
              </div>
              {e.uuid && (
                <div
                  className="truncate"
                  style={{
                    fontSize: 10,
                    color: 'var(--md-body-light)',
                    opacity: 0.6,
                    fontFamily: 'var(--md-font-mono)',
                  }}
                  title={e.uuid}
                >
                  {e.uuid}
                </div>
              )}
            </div>
            <button
              className="md-btn md-btn-icon md-btn-danger"
              title="移除"
              onClick={() => onRemove(e)}
              style={{ width: 30, height: 30 }}
            >
              <FaXmark size={13} />
            </button>
          </div>
        </Reveal>
      ))}
    </div>
  )
}
