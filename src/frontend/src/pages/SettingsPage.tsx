import { useCallback, useEffect, useState } from 'react'
import {
  FaGear,
  FaBell,
  FaRotate,
  FaCheck,
  FaShield,
  FaMugHot,
} from 'react-icons/fa6'
import {
  getSettings,
  setPrimaryColor,
  setAccentColor,
  applyTheme,
  saveSettings,
  setPreset,
  resetSettings,
  toggleAnimations,
  testNotification,
  getJavaList,
  rescanJava,
  getAppInfo,
} from '@/utils/bridge'
import type {
  SettingsData,
  JavaInstallationInfo,
  JavaListResponse,
  AppInfo,
  ThemePreset,
} from '@/types/bridge'

// ─────────────────────────────────────────────────────────────────────
// 预设色板（与 WPF SettingsPage.xaml 保持一致）
// ─────────────────────────────────────────────────────────────────────
interface Swatch {
  color: string
  label: string
}

const primarySwatches: Swatch[] = [
  { color: '#7B1FA2', label: '深紫' },
  { color: '#1565C0', label: '蓝' },
  { color: '#00897B', label: '青绿' },
  { color: '#C62828', label: '红' },
  { color: '#F57C00', label: '橙' },
  { color: '#2E7D32', label: '绿' },
  { color: '#0D47A1', label: '深蓝' },
  { color: '#4A148C', label: '深紫红' },
]

const accentSwatches: Swatch[] = [
  { color: '#CDDC39', label: '青柠' },
  { color: '#FF9800', label: '橙' },
  { color: '#E91E63', label: '粉红' },
  { color: '#FFD600', label: '黄' },
  { color: '#00BCD4', label: '青' },
  { color: '#8BC34A', label: '浅绿' },
  { color: '#FF5722', label: '深橙' },
  { color: '#6366F1', label: '靛蓝' },
]

interface PresetOption {
  key: ThemePreset
  label: string
  primary: string
  accent: string
}

const presetOptions: PresetOption[] = [
  { key: 'SkyBlue', label: '苍穹蓝', primary: '#3B82F6', accent: '#FB7185' },
  { key: 'BlueOrange', label: '科技蓝', primary: '#1565C0', accent: '#FF9800' },
  { key: 'TealPink', label: '清新绿', primary: '#00897B', accent: '#E91E63' },
  { key: 'RedYellow', label: '火焰红', primary: '#C62828', accent: '#FFD600' },
  { key: 'OceanBlue', label: '海洋蓝', primary: '#0097A7', accent: '#FFD740' },
]

// 颜色归一化：统一为 #RRGGBB 用于比较
function normalizeHex(hex: string): string {
  if (!hex) return ''
  let h = hex.trim().toUpperCase()
  if (h.length === 8 && h.startsWith('#')) h = '#' + h.slice(2) // #AARRGGBB -> #RRGGBB
  return h
}

// ─────────────────────────────────────────────────────────────────────
// 设置页主组件
// ─────────────────────────────────────────────────────────────────────
export function SettingsPage(): JSX.Element {
  const [settings, setSettings] = useState<SettingsData | null>(null)
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null)
  const [javaList, setJavaList] = useState<JavaInstallationInfo[]>([])
  const [isScanningJava, setIsScanningJava] = useState(false)
  const [statusMessage, setStatusMessage] = useState('')

  // HEX 输入框本地值（失焦时才提交到后端）
  const [primaryHexInput, setPrimaryHexInput] = useState('')
  const [accentHexInput, setAccentHexInput] = useState('')

  // 以下设置项桥接 API 暂未提供独立 setter，使用本地状态承载（初始值来自 getSettings）
  const [cornerRadius, setCornerRadius] = useState(0)
  const [animationDuration, setAnimationDuration] = useState(200)
  const [enableWindowsNotifications, setEnableWindowsNotifications] = useState(false)
  const [preferJavaw, setPreferJavaw] = useState(false)

  const loadSettings = useCallback(async (): Promise<void> => {
    try {
      const resp = await getSettings()
      setSettings(resp)
      setPrimaryHexInput(resp.primaryColorHex)
      setAccentHexInput(resp.accentColorHex)
      setCornerRadius(resp.cornerRadius)
      setAnimationDuration(resp.animationDuration)
      setEnableWindowsNotifications(resp.enableWindowsNotifications)
      setPreferJavaw(resp.preferJavaw)
      setStatusMessage(resp.statusMessage)
    } catch (e) {
      console.error('获取设置失败:', e)
    }
  }, [])

  const loadJavaList = useCallback(async (): Promise<void> => {
    try {
      const resp: JavaListResponse = await getJavaList()
      setJavaList(resp.javas)
      setIsScanningJava(resp.isScanning)
    } catch (e) {
      console.error('获取 Java 列表失败:', e)
    }
  }, [])

  useEffect(() => {
    loadSettings()
    loadJavaList()
    getAppInfo()
      .then((info) => setAppInfo(info))
      .catch((e) => console.error('获取应用信息失败:', e))
  }, [loadSettings, loadJavaList])

  // ─── 颜色设置 ───
  const handleSetPrimary = async (hex: string): Promise<void> => {
    try {
      await setPrimaryColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置主色失败:', e)
    }
  }

  const handleSetAccent = async (hex: string): Promise<void> => {
    try {
      await setAccentColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置强调色失败:', e)
    }
  }

  const handlePrimaryHexBlur = async (): Promise<void> => {
    const val = primaryHexInput.trim()
    if (val && val !== (settings?.primaryColorHex ?? '')) {
      await handleSetPrimary(val)
    }
  }

  const handleAccentHexBlur = async (): Promise<void> => {
    const val = accentHexInput.trim()
    if (val && val !== (settings?.accentColorHex ?? '')) {
      await handleSetAccent(val)
    }
  }

  const handleSetPreset = async (preset: ThemePreset): Promise<void> => {
    try {
      await setPreset(preset)
      await loadSettings()
    } catch (e) {
      console.error('应用预设失败:', e)
    }
  }

  // ─── 动画设置 ───
  const handleToggleAnimations = async (): Promise<void> => {
    try {
      const result = await toggleAnimations()
      if (result.success) {
        await loadSettings()
      }
    } catch (e) {
      console.error('切换动画失败:', e)
    }
  }

  // ─── Java 管理 ───
  const handleRescanJava = async (): Promise<void> => {
    try {
      setIsScanningJava(true)
      await rescanJava()
      await loadJavaList()
    } catch (e) {
      console.error('重新扫描 Java 失败:', e)
    } finally {
      setIsScanningJava(false)
    }
  }

  // ─── 通知测试 ───
  const handleTestNotification = async (): Promise<void> => {
    try {
      await testNotification()
    } catch (e) {
      console.error('发送测试通知失败:', e)
    }
  }

  // ─── 底部操作栏 ───
  const handleApplyTheme = async (): Promise<void> => {
    try {
      const result = await applyTheme()
      setStatusMessage(result.success ? '主题已应用' : '主题应用失败')
      await loadSettings()
    } catch (e) {
      console.error('应用主题失败:', e)
      setStatusMessage('应用主题失败')
    }
  }

  const handleSave = async (): Promise<void> => {
    try {
      const result = await saveSettings()
      setStatusMessage(result.success ? '设置已保存' : '保存设置失败')
      await loadSettings()
    } catch (e) {
      console.error('保存设置失败:', e)
      setStatusMessage('保存设置失败')
    }
  }

  const handleReset = async (): Promise<void> => {
    try {
      const result = await resetSettings()
      setStatusMessage(result.success ? '已重置为默认设置' : '重置失败')
      await loadSettings()
    } catch (e) {
      console.error('重置设置失败:', e)
      setStatusMessage('重置设置失败')
    }
  }

  const enableAnimations = settings?.enableAnimations ?? true
  const primaryColorHex = settings?.primaryColorHex ?? '#3B82F6'
  const accentColorHex = settings?.accentColorHex ?? '#FB7185'

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      {/* ═══ 标题 ═══ */}
      <div className="flex items-center mb-4">
        <FaGear
          size={32}
          style={{ color: 'var(--md-accent-text)', marginRight: 12 }}
        />
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, color: 'var(--md-body)' }}>
            外观设置
          </h1>
          <p
            style={{
              fontSize: 13,
              color: 'var(--md-body-light)',
            }}
          >
            自定义颜色、圆角和动画效果
          </p>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* 🎨 外观设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card p-5 mb-4">
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          颜色方案
        </h2>

        <div className="grid grid-cols-2 gap-4">
          {/* 主色调 */}
          <div>
            <div
              style={{
                fontSize: 13,
                color: 'var(--md-body)',
                margin: '8px 0 4px 0',
              }}
            >
              主色调
            </div>
            <div className="flex items-center" style={{ marginBottom: 8 }}>
              <div
                style={{
                  width: 54,
                  height: 54,
                  backgroundColor: primaryColorHex,
                  border: '2px solid var(--md-swatch-hover-border)',
                  borderRadius: 8,
                  marginRight: 12,
                  flexShrink: 0,
                }}
                title="当前主色"
              />
              <div>
                <input
                  className="md-input"
                  style={{ width: 120, height: 32, fontSize: 12 }}
                  value={primaryHexInput}
                  onChange={(e) => setPrimaryHexInput(e.target.value)}
                  onBlur={handlePrimaryHexBlur}
                  placeholder={primaryColorHex}
                />
                <div
                  style={{
                    fontSize: 11,
                    color: 'var(--md-body-light)',
                    marginTop: 4,
                  }}
                >
                  输入 HEX 值后失焦生效
                </div>
              </div>
            </div>
            <div
              style={{
                fontSize: 12,
                color: 'var(--md-body-light)',
                marginBottom: 8,
              }}
            >
              用于标题栏、按钮等主要元素
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                margin: '8px 0 4px 0',
              }}
            >
              预设主色
            </div>
            <div className="flex flex-wrap" style={{ gap: 10 }}>
              {primarySwatches.map((s) => {
                const selected = normalizeHex(s.color) === normalizeHex(primaryColorHex)
                return (
                  <button
                    key={s.color}
                    className={`md-swatch ${selected ? 'md-swatch-selected' : ''}`}
                    style={{ backgroundColor: s.color }}
                    title={s.label}
                    onClick={() => handleSetPrimary(s.color)}
                  />
                )
              })}
            </div>
          </div>

          {/* 强调色 */}
          <div>
            <div
              style={{
                fontSize: 13,
                color: 'var(--md-body)',
                margin: '8px 0 4px 0',
              }}
            >
              强调色
            </div>
            <div className="flex items-center" style={{ marginBottom: 8 }}>
              <div
                style={{
                  width: 54,
                  height: 54,
                  backgroundColor: accentColorHex,
                  border: '2px solid var(--md-swatch-hover-border)',
                  borderRadius: 8,
                  marginRight: 12,
                  flexShrink: 0,
                }}
                title="当前强调色"
              />
              <div>
                <input
                  className="md-input"
                  style={{ width: 120, height: 32, fontSize: 12 }}
                  value={accentHexInput}
                  onChange={(e) => setAccentHexInput(e.target.value)}
                  onBlur={handleAccentHexBlur}
                  placeholder={accentColorHex}
                />
                <div
                  style={{
                    fontSize: 11,
                    color: 'var(--md-body-light)',
                    marginTop: 4,
                  }}
                >
                  输入 HEX 值后失焦生效
                </div>
              </div>
            </div>
            <div
              style={{
                fontSize: 12,
                color: 'var(--md-body-light)',
                marginBottom: 8,
              }}
            >
              用于高亮文字、图标等强调元素
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                margin: '8px 0 4px 0',
              }}
            >
              预设强调色
            </div>
            <div className="flex flex-wrap" style={{ gap: 10 }}>
              {accentSwatches.map((s) => {
                const selected = normalizeHex(s.color) === normalizeHex(accentColorHex)
                return (
                  <button
                    key={s.color}
                    className={`md-swatch ${selected ? 'md-swatch-selected' : ''}`}
                    style={{ backgroundColor: s.color }}
                    title={s.label}
                    onClick={() => handleSetAccent(s.color)}
                  />
                )
              })}
            </div>
          </div>
        </div>

        {/* 快速预设方案 */}
        <div style={{ marginTop: 16 }}>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            快速方案
          </div>
          <div className="flex flex-wrap" style={{ gap: 8 }}>
            {presetOptions.map((p) => (
              <button
                key={p.key}
                className="md-btn md-btn-outlined"
                style={{ padding: '12px 20px', minHeight: 44 }}
                onClick={() => handleSetPreset(p.key)}
              >
                <span
                  style={{
                    width: 20,
                    height: 20,
                    borderRadius: 4,
                    backgroundColor: p.primary,
                  }}
                />
                <span
                  style={{
                    width: 20,
                    height: 20,
                    borderRadius: 4,
                    backgroundColor: p.accent,
                    marginLeft: 4,
                  }}
                />
                <span style={{ marginLeft: 8 }}>{p.label}</span>
              </button>
            ))}
          </div>
        </div>

        {/* 圆角设置 */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
          >
            圆角设置
          </h2>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            控件圆角半径
          </div>
          <input
            type="range"
            min={0}
            max={24}
            step={2}
            value={cornerRadius}
            onChange={(e) => setCornerRadius(Number(e.target.value))}
            style={{ width: 400, margin: '8px 0' }}
          />
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              marginBottom: 8,
            }}
          >
            当前: {cornerRadius}px
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            控制按钮、卡片、输入框等元素的圆角大小
          </div>

          {/* 圆角预览 */}
          <div style={{ marginTop: 12 }}>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                marginBottom: 6,
              }}
            >
              预览
            </div>
            <div className="flex">
              <div
                style={{
                  width: 60,
                  height: 36,
                  backgroundColor: 'var(--md-card-background)',
                  border: '1px solid var(--md-accent-text)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                }}
              >
                按钮
              </div>
              <div
                style={{
                  width: 80,
                  height: 36,
                  backgroundColor: 'var(--md-card-background)',
                  border: '1px solid var(--md-subtle-border)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                  marginLeft: 12,
                }}
              >
                卡片
              </div>
              <div
                style={{
                  width: 100,
                  height: 36,
                  backgroundColor: 'var(--md-card-hover)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                  marginLeft: 12,
                }}
              >
                输入框
              </div>
            </div>
          </div>
        </div>

        {/* 动画设置 */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
          >
            动画设置
          </h2>
          <div className="flex items-center" style={{ marginTop: 8 }}>
            <label className="md-toggle">
              <input
                type="checkbox"
                checked={enableAnimations}
                onChange={handleToggleAnimations}
              />
              <span className="md-toggle-slider" />
            </label>
            <div style={{ marginLeft: 12 }}>
              <div
                style={{
                  color: 'var(--md-body)',
                  fontSize: 13,
                }}
              >
                启用动画效果
              </div>
              <div
                style={{
                  fontSize: 11,
                  color: 'var(--md-body-light)',
                }}
              >
                页面切换、按钮悬停等动效
              </div>
            </div>
          </div>

          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '12px 0 4px 0',
            }}
          >
            动画速度
          </div>
          <input
            type="range"
            min={50}
            max={1000}
            step={50}
            value={animationDuration}
            onChange={(e) => setAnimationDuration(Number(e.target.value))}
            disabled={!enableAnimations}
            style={{ width: 400, margin: '8px 0' }}
          />
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              marginBottom: 8,
            }}
          >
            当前: {animationDuration}ms
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            控制页面切换、按钮悬停等动画的持续时间
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* 🔔 服务器设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card p-5 mb-4">
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          服务器设置
        </h2>

        {/* Windows 通知 */}
        <div className="flex items-center" style={{ marginTop: 8 }}>
          <label className="md-toggle">
            <input
              type="checkbox"
              checked={enableWindowsNotifications}
              onChange={(e) => setEnableWindowsNotifications(e.target.checked)}
            />
            <span className="md-toggle-slider" />
          </label>
          <div style={{ marginLeft: 12 }}>
            <div
              style={{
                color: 'var(--md-body)',
                fontSize: 13,
              }}
            >
              Windows 通知中心
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
              }}
            >
              重要信息通过系统通知弹出
            </div>
          </div>
        </div>

        {/* 优先使用 javaw */}
        <div className="flex items-center" style={{ marginTop: 16 }}>
          <label className="md-toggle">
            <input
              type="checkbox"
              checked={preferJavaw}
              onChange={(e) => setPreferJavaw(e.target.checked)}
            />
            <span className="md-toggle-slider" />
          </label>
          <div style={{ marginLeft: 12 }}>
            <div
              style={{
                color: 'var(--md-body)',
                fontSize: 13,
              }}
            >
              优先使用 javaw.exe
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
              }}
            >
              无控制台窗口启动，后台静默运行
            </div>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* ☕ Java 管理卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card p-5 mb-4">
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}
        >
          Java 运行环境
        </h2>
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-body-light)',
            marginBottom: 12,
          }}
        >
          管理系统中的 Java 安装，设置默认版本和启动方式
        </div>

        {/* 标题 + 重新扫描按钮 */}
        <div className="flex items-center justify-between" style={{ margin: '8px 0' }}>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            已检测到的 Java 版本
          </div>
          <button
            className="md-btn md-btn-outlined"
            style={{ padding: '8px 12px', minHeight: 32 }}
            disabled={isScanningJava}
            onClick={handleRescanJava}
          >
            <FaRotate
              size={14}
              className={isScanningJava ? 'md-spin' : ''}
            />
            <span style={{ marginLeft: 6, fontSize: 12 }}>重新扫描</span>
          </button>
        </div>

        {/* Java 列表 */}
        <div
          style={{
            backgroundColor: 'var(--md-card-hover)',
            borderRadius: 'var(--md-radius)',
            padding: 8,
            maxHeight: 300,
            overflowY: 'auto',
          }}
        >
          {javaList.length === 0 ? (
            <div
              style={{
                textAlign: 'center',
                padding: 24,
                color: 'var(--md-body-lighter)',
                fontSize: 13,
              }}
            >
              {isScanningJava ? '正在扫描...' : '未检测到 Java 安装'}
            </div>
          ) : (
            <div className="space-y-1.5">
              {javaList.map((java) => (
                <div
                  key={java.javaPath}
                  className="flex items-center"
                  style={{
                    padding: 10,
                    borderRadius: 'var(--md-radius-small)',
                    backgroundColor: 'var(--md-card-background)',
                    border: '1px solid var(--md-card-subtle-border)',
                  }}
                >
                  {/* Java 图标 */}
                  <div
                    style={{
                      width: 36,
                      height: 36,
                      backgroundColor: 'var(--md-primary-subtle-background)',
                      borderRadius: 'var(--md-radius-small)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      flexShrink: 0,
                    }}
                  >
                    <FaMugHot
                      size={20}
                      style={{ color: 'var(--md-accent-text)' }}
                    />
                  </div>
                  {/* Java 信息 */}
                  <div
                    style={{
                      marginLeft: 10,
                      flex: 1,
                      minWidth: 0,
                    }}
                  >
                    <div
                      style={{
                        fontWeight: 600,
                        color: 'var(--md-body)',
                        fontSize: 13,
                      }}
                    >
                      {java.versionDisplay || java.versionString || '未知版本'}
                    </div>
                    <div
                      className="truncate"
                      style={{
                        fontSize: 11,
                        color: 'var(--md-body-light)',
                        marginTop: 2,
                      }}
                      title={java.javaPath}
                    >
                      {java.javaPath}
                    </div>
                    <div className="flex" style={{ marginTop: 2, gap: 4 }}>
                      {java.isDefault && (
                        <span
                          style={{
                            backgroundColor: 'var(--md-accent-text)',
                            borderRadius: 4,
                            padding: '2px 6px',
                            fontSize: 10,
                            fontWeight: 700,
                            color: 'var(--md-card-background)',
                          }}
                        >
                          默认
                        </span>
                      )}
                      {java.isCustom && (
                        <span
                          style={{
                            backgroundColor: 'var(--md-primary-hue-mid)',
                            borderRadius: 4,
                            padding: '2px 6px',
                            fontSize: 10,
                            fontWeight: 700,
                            color: 'var(--md-white)',
                          }}
                        >
                          自定义
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* ℹ️ 关于卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card p-5 mb-4">
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          关于 MSMC
        </h2>

        {/* 应用信息 */}
        <div
          className="flex flex-col items-center"
          style={{ margin: '4px 0 16px 0' }}
        >
          <div
            style={{
              width: 64,
              height: 64,
              borderRadius: 'var(--md-radius-large)',
              backgroundColor: 'var(--md-primary-subtle-background)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <FaShield
              size={40}
              style={{ color: 'var(--md-accent-text)' }}
            />
          </div>
          <div
            style={{
              fontSize: 20,
              fontWeight: 700,
              color: 'var(--md-body)',
              marginTop: 10,
            }}
          >
            {appInfo?.name ?? 'MSMC'}
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            {appInfo?.fullName ?? 'Minecraft Server Management Console'}
          </div>
          <div
            style={{
              fontSize: 11,
              color: 'var(--md-body-light)',
              opacity: 0.7,
              marginTop: 4,
            }}
          >
            v{appInfo?.version ?? '0.1.0'}
          </div>
        </div>

        {/* 测试通知按钮 */}
        <button
          className="md-btn md-btn-outlined"
          style={{ padding: '12px 20px', minHeight: 44 }}
          onClick={handleTestNotification}
        >
          <FaBell size={16} />
          <span style={{ marginLeft: 8 }}>发送测试通知</span>
        </button>
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-body-light)',
            marginTop: 8,
          }}
        >
          点击测试按钮可以验证通知功能是否正常工作
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* 📋 底部操作栏 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="flex" style={{ gap: 8, marginTop: 16 }}>
        <button
          className="md-btn md-btn-outlined"
          style={{ padding: '12px 20px', minHeight: 44 }}
          onClick={handleReset}
        >
          <FaRotate size={16} />
          <span style={{ marginLeft: 8 }}>重置为默认</span>
        </button>
        <button
          className="md-btn md-btn-primary"
          style={{ padding: '12px 20px', minHeight: 44 }}
          onClick={handleApplyTheme}
        >
          <FaCheck size={16} />
          <span style={{ marginLeft: 8 }}>应用主题</span>
        </button>
        <button
          className="md-btn md-btn-primary"
          style={{ padding: '12px 20px', minHeight: 44, fontWeight: 600 }}
          onClick={handleSave}
        >
          <FaCheck size={16} />
          <span style={{ marginLeft: 8 }}>保存设置</span>
        </button>
      </div>

      {/* 状态信息 */}
      {statusMessage && (
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-accent-text)',
            marginTop: 16,
          }}
        >
          {statusMessage}
        </div>
      )}
    </div>
  )
}
