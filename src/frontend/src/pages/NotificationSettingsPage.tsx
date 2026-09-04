import React, { useState, useCallback } from 'react'
import {
  FaBell,
  FaDiscord,
  FaEnvelope,
  FaWindows,
  FaServer,
  FaLock,
} from 'react-icons/fa6'
import { testNotificationChannel, dispatchNotification, getNotificationConfig, saveNotificationConfig } from '@/utils/bridge'
import type {
  NotificationEvent,
  NotificationEventType,
  NotificationDispatchResult,
  NotificationChannelConfig,
} from '@/types/bridge'

interface ChannelDef {
  key: 'windows' | 'discord' | 'email' | 'webhook'
  name: string
  desc: string
  icon: typeof FaWindows
  iconColor: string
  /** 是否支持前端直接开关：Windows Toast 默认启用且零配置 */
  hasToggle: boolean
  /** 其他通道是否需后端配置（Webhook URL / SMTP / Discord URL） */
  needsCredentials: boolean
}

const CHANNEL_DEFS: ChannelDef[] = [
  {
    key: 'windows',
    name: 'Windows 通知',
    desc: '系统原生 Toast（Win10/Win11）',
    icon: FaWindows,
    iconColor: '#0078D4',
    hasToggle: true,
    needsCredentials: false,
  },
  {
    key: 'discord',
    name: 'Discord Webhook',
    desc: '支持消息嵌入和格式化',
    icon: FaDiscord,
    iconColor: '#5865F2',
    hasToggle: false,
    needsCredentials: true,
  },
  {
    key: 'email',
    name: '邮件通知',
    desc: 'SMTP 邮件推送',
    icon: FaEnvelope,
    iconColor: '#EA4335',
    hasToggle: false,
    needsCredentials: true,
  },
  {
    key: 'webhook',
    name: '通用 Webhook',
    desc: '自定义 HTTP 端点',
    icon: FaServer,
    iconColor: 'var(--md-accent-text)',
    hasToggle: false,
    needsCredentials: true,
  },
]

export function NotificationSettingsPage(): JSX.Element {
  const [testing, setTesting] = useState(false)
  const [lastResult, setLastResult] = useState<NotificationDispatchResult | null>(null)
  const [statusMsg, setStatusMsg] = useState('')
  const [config, setConfig] = useState<NotificationChannelConfig | null>(null)
  const [loading, setLoading] = useState(true)

  // 页面加载时读通知配置（一次性）
  React.useEffect(() => {
    (async () => {
      try {
        const resp = await getNotificationConfig()
        if (resp.success && resp.config) {
          const parsed = JSON.parse(resp.config) as NotificationChannelConfig
          setConfig(parsed)
        } else {
          // 后端无配置时用默认值（WindowsToast.Enabled = true）
          setConfig({ windowsToast: { enabled: true }, retryMaxAttempts: 3, retryBaseDelayMs: 1000 })
        }
      } catch {
        setConfig({ windowsToast: { enabled: true }, retryMaxAttempts: 3, retryBaseDelayMs: 1000 })
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const handleToggleWindows = async (enabled: boolean) => {
    if (!config) return
    const next: NotificationChannelConfig = {
      ...config,
      windowsToast: { enabled },
    }
    setConfig(next)
    try {
      const resp = await saveNotificationConfig(next)
      if (!resp.success) {
        setStatusMsg(`❌ 保存失败：${resp.error ?? '未知错误'}`)
      } else {
        setStatusMsg(enabled ? '✅ Windows 通知已开启' : '✅ Windows 通知已关闭')
      }
    } catch (e) {
      setStatusMsg(`❌ 保存失败：${(e as Error).message}`)
    }
  }

  const eventTypes: { value: NotificationEventType; label: string; color: string }[] = [
    { value: 'ServerCrashed', label: '服务器崩溃', color: '#e74c3c' },
    { value: 'ServerStarted', label: '服务器启动', color: '#2ecc71' },
    { value: 'ServerStopped', label: '服务器停止', color: '#f39c12' },
    { value: 'BackupCompleted', label: '备份完成', color: '#3498db' },
    { value: 'BackupFailed', label: '备份失败', color: '#e67e22' },
    { value: 'ManualTest', label: '手动测试', color: '#9b59b6' },
  ]

  const handleTest = useCallback(async () => {
    setTesting(true)
    setStatusMsg('')
    try {
      const result = await testNotificationChannel('这是一条来自 MSMC 的测试通知')
      setLastResult(result)
      setStatusMsg(
        result.isSuccess
          ? `✅ 发送成功：${result.successfulChannels}/${result.totalChannels} 个通道`
          : `❌ 发送失败`
      )
    } catch (e) {
      setStatusMsg(`❌ 测试通知失败：${(e as Error).message}`)
    } finally {
      setTesting(false)
    }
  }, [])

  const handleDispatch = useCallback(async (evt: NotificationEvent) => {
    setTesting(true)
    try {
      const result = await dispatchNotification(evt)
      setLastResult(result)
      setStatusMsg(
        result.isSuccess
          ? `✅ ${evt.title}：${result.successfulChannels}/${result.totalChannels} 通道成功`
          : `❌ ${evt.title} 发送失败`
      )
    } catch (e) {
      setStatusMsg(`❌ 发送失败：${(e as Error).message}`)
    } finally {
      setTesting(false)
    }
  }, [])

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      <div className="flex items-center mb-4">
        <FaBell size={32} style={{ color: 'var(--md-accent-text)', marginRight: 12 }} />
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, color: 'var(--md-body)' }}>通知中心</h1>
          <p style={{ fontSize: 13, color: 'var(--md-body-light)' }}>
            配置通知通道并测试通知推送
          </p>
        </div>
      </div>

      {/* 通道状态展示 —— 诚实化 UI：配置入口尚未开放，仅展示通道列表与状态 */}
      <div className="md-card md-card-elevated p-5 mb-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: 0 }}>
            通知通道
          </h2>
          <span
            style={{
              fontSize: 11,
              padding: '3px 10px',
              borderRadius: 10,
              background: 'var(--md-card-hover)',
              color: 'var(--md-body-light)',
            }}
          >
            Windows 通知已开放开关；其他通道需后端配置凭证
          </span>
        </div>
        <p style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 14, lineHeight: 1.5 }}>
          MSMC 支持多通道通知推送。各通道的详细配置功能将在后续版本中开放，届时可在此页面设置 Webhook 地址、邮件 SMTP 等参数。
          当前可通过下方「通知测试」验证已配置的通道是否正常工作。
        </p>
        <div className="grid grid-cols-2 gap-3">
          {CHANNEL_DEFS.map((ch) => {
            const Icon = ch.icon
            const windowsEnabled = config?.windowsToast?.enabled ?? true
            const discordEnabled = config?.discord?.enabled ?? false
            const emailEnabled = config?.email?.enabled ?? false
            const webhookEnabled = config?.genericWebhook?.enabled ?? false
            const channelEnabled =
              ch.key === 'windows' ? windowsEnabled :
              ch.key === 'discord' ? discordEnabled :
              ch.key === 'email' ? emailEnabled :
              ch.key === 'webhook' ? webhookEnabled : false

            return (
              <div
                key={ch.key}
                className="md-card"
                style={{
                  padding: 12,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 12,
                  opacity: ch.needsCredentials && !channelEnabled ? 0.75 : 1,
                }}
              >
                <div
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: 8,
                    background: 'var(--md-card-hover)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0,
                  }}
                >
                  <Icon size={20} style={{ color: ch.iconColor }} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                    {ch.name}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--md-body-light)' }}>
                    {ch.desc}
                  </div>
                </div>
                {ch.hasToggle ? (
                  // ✅ Windows Toast：前端直接开关（即时持久化 + 即时生效）
                  <label
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      cursor: 'pointer',
                      fontSize: 11,
                      color: channelEnabled ? '#22C55E' : 'var(--md-body-lighter)',
                    }}
                    title={loading ? '加载中...' : (channelEnabled ? '点击关闭' : '点击开启')}
                  >
                    <input
                      type="checkbox"
                      checked={channelEnabled}
                      disabled={loading}
                      onChange={(e) => handleToggleWindows(e.target.checked)}
                      style={{ width: 16, height: 16, cursor: 'pointer' }}
                    />
                    <span>{channelEnabled ? '启用' : '关闭'}</span>
                  </label>
                ) : (
                  // 🔒 其他通道：需要凭证，UI 诚实展示"需后端配置"
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      fontSize: 10,
                      color: 'var(--md-body-lighter)',
                      padding: '3px 8px',
                      borderRadius: 10,
                      background: 'var(--md-subtle-background)',
                      flexShrink: 0,
                    }}
                    title="需后端配置 Webhook URL / SMTP 等凭证"
                  >
                    <FaLock size={10} />
                    <span>{channelEnabled ? '已启用(有凭证)' : '需配置'}</span>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      </div>

      {/* 测试区域 */}
      <div className="md-card md-card-elevated p-5 mb-4">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
          通知测试
        </h2>

        <p style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 16 }}>
          选择事件类型发送模拟通知，验证所有通道是否正常工作
        </p>

        <div className="flex flex-wrap" style={{ gap: 8, marginBottom: 16 }}>
          {eventTypes.map((et) => (
            <button
              key={et.value}
              className="md-btn md-btn-outlined"
              onClick={() =>
                handleDispatch({
                  eventType: et.value,
                  title: et.label,
                  message: `这是一条 ${et.label} 的测试消息，来自 MSMC`,
                  sourceModule: 'NotificationTest',
                })
              }
              disabled={testing}
              style={{ fontSize: 12 }}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: '50%',
                  backgroundColor: et.color,
                  marginRight: 6,
                  display: 'inline-block',
                }}
              />
              {et.label}
            </button>
          ))}
        </div>

        <button
          className="md-btn md-btn-primary"
          onClick={handleTest}
          disabled={testing}
          style={{ width: '100%' }}
        >
          {testing ? '发送中...' : '🔔 发送综合测试通知'}
        </button>

        {statusMsg && (
          <div
            style={{
              marginTop: 12,
              padding: '10px 14px',
              background: statusMsg.startsWith('✅')
                ? 'var(--md-success-subtle-background)'
                : 'var(--md-danger-subtle-background)',
              borderRadius: 'var(--md-radius)',
              fontSize: 13,
              color: 'var(--md-body)',
            }}
          >
            {statusMsg}
          </div>
        )}

        {lastResult && (
          <div style={{ marginTop: 12 }}>
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 8 }}>
              通道详细结果：
            </div>
            <div className="flex flex-wrap" style={{ gap: 6 }}>
              {Object.entries(lastResult.channelResults ?? {}).map(([channel, success]) => (
                <span
                  key={channel}
                  style={{
                    padding: '4px 10px',
                    borderRadius: 12,
                    fontSize: 11,
                    backgroundColor: success
                      ? 'var(--md-success-subtle-background)'
                      : 'var(--md-danger-subtle-background)',
                    color: success ? 'var(--md-success-text)' : 'var(--md-danger-text)',
                  }}
                >
                  {channel}: {success ? '✅' : '❌'}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
