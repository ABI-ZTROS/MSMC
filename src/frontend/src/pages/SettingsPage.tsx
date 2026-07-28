import { useCallback, useEffect, useState } from 'react'
import {
  FaGear,
  FaBell,
  FaRotate,
  FaCheck,
  FaShield,
  FaMugHot,
  FaHeart,
  FaGithub,
  FaXmark,
  FaUser,
  FaFolderOpen,
  FaPlus,
  FaTrashCan,
  FaStar,
} from 'react-icons/fa6'
import {
  getSettings,
  setPrimaryColor,
  setAccentColor,
  applyTheme,
  saveSettings,
  updateSettings,
  setPreset,
  resetSettings,
  toggleAnimations,
  testNotification,
  getJavaList,
  rescanJava,
  addJavaPath,
  removeJavaPath,
  setDefaultJava,
  browseJavaPath,
  getAppInfo,
  getPresets,
  getPrimarySwatches,
  getAccentSwatches,
  getTeamInfo,
} from '@/utils/bridge'
import type {
  SettingsData,
  JavaInstallationInfo,
  JavaListResponse,
  AppInfo,
  ThemePreset,
  SwatchInfo,
  PresetInfo,
  TeamInfoResponse,
} from '@/types/bridge'
import {
  applySettingsToCss,
  applyPrimaryColor,
  applyAccentColor,
  applyCornerRadius,
  applyAnimationSettings,
} from '@/utils/theme'
import { ColorPicker } from '@/components/ui/ColorPicker'
import abiAvatar from '@/assets/avatars/ABI-ZTROS.png'
import yanlanxiangAvatar from '@/assets/avatars/yanlanxiang.jpg'
import mochaAvatar from '@/assets/avatars/MochaCello92377.png'
import catstackAvatar from '@/assets/avatars/CatStack-pixe.png'

const avatarMap: Record<string, string> = {
  'ABI-ZTROS': abiAvatar,
  '烟蓝湘': yanlanxiangAvatar,
  'MochaCello92377': mochaAvatar,
  'CatStack-pixe': catstackAvatar,
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

  // 自选 Java 路径相关状态
  const [newJavaPath, setNewJavaPath] = useState('')
  const [javaOpInProgress, setJavaOpInProgress] = useState(false)

  // 色板和预设数据
  const [primarySwatches, setPrimarySwatches] = useState<SwatchInfo[]>([])
  const [accentSwatches, setAccentSwatches] = useState<SwatchInfo[]>([])
  const [presetOptions, setPresetOptions] = useState<PresetInfo[]>([])
  const [swatchesLoading, setSwatchesLoading] = useState(true)

  // 团队信息
  const [teamInfo, setTeamInfo] = useState<TeamInfoResponse | null>(null)
  const [teamLoading, setTeamLoading] = useState(true)

  // 以下设置项桥接 API 暂未提供独立 setter，使用本地状态承载（初始值来自 getSettings）
  // 同时使用 localStorage 做持久化，避免页面刷新后丢失
  const [cornerRadius, setCornerRadius] = useState(() => {
    const saved = localStorage.getItem('msmc_cornerRadius')
    return saved ? Number(saved) : 0
  })
  const [animationDuration, setAnimationDuration] = useState(() => {
    const saved = localStorage.getItem('msmc_animationDuration')
    return saved ? Number(saved) : 200
  })
  const [enableWindowsNotifications, setEnableWindowsNotifications] = useState(() => {
    const saved = localStorage.getItem('msmc_enableWindowsNotifications')
    return saved ? saved === 'true' : false
  })
  const [preferJavaw, setPreferJavaw] = useState(() => {
    const saved = localStorage.getItem('msmc_preferJavaw')
    return saved ? saved === 'true' : false
  })

  const loadSettings = useCallback(async (): Promise<void> => {
    try {
      const resp = await getSettings()
      setSettings(resp)
      setCornerRadius(resp.cornerRadius)
      setAnimationDuration(resp.animationDuration)
      setEnableWindowsNotifications(resp.enableWindowsNotifications)
      setPreferJavaw(resp.preferJavaw)
      setStatusMessage(resp.statusMessage)
      applySettingsToCss(resp)
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

  const loadSwatchesAndPresets = useCallback(async (): Promise<void> => {
    try {
      setSwatchesLoading(true)
      const [presetsResp, primaryResp, accentResp] = await Promise.all([
        getPresets(),
        getPrimarySwatches(),
        getAccentSwatches(),
      ])
      setPresetOptions(presetsResp.presets)
      setPrimarySwatches(primaryResp.swatches)
      setAccentSwatches(accentResp.swatches)
    } catch (e) {
      console.error('获取色板和预设失败:', e)
    } finally {
      setSwatchesLoading(false)
    }
  }, [])

  const loadTeamInfo = useCallback(async (): Promise<void> => {
    try {
      setTeamLoading(true)
      const resp = await getTeamInfo()
      setTeamInfo(resp)
    } catch (e) {
      console.error('获取团队信息失败:', e)
    } finally {
      setTeamLoading(false)
    }
  }, [])

  useEffect(() => {
    loadSettings()
    loadJavaList()
    loadSwatchesAndPresets()
    loadTeamInfo()
    getAppInfo()
      .then((info) => setAppInfo(info))
      .catch((e) => console.error('获取应用信息失败:', e))
  }, [loadSettings, loadJavaList, loadSwatchesAndPresets, loadTeamInfo])

  // ─── 颜色设置 ───
  const handlePrimaryPreview = (hex: string): void => {
    applyPrimaryColor(hex)
  }

  const handleAccentPreview = (hex: string): void => {
    applyAccentColor(hex)
  }

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

  const handleSetPreset = async (preset: ThemePreset): Promise<void> => {
    try {
      const result = await setPreset(preset)
      if (result.success) {
        await loadSettings()
      } else {
        setStatusMessage('应用预设失败')
      }
    } catch (e) {
      console.error('应用预设失败:', e)
      setStatusMessage('应用预设失败')
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
      const result = await rescanJava()
      if (result.success) {
        await loadJavaList()
      } else {
        setStatusMessage('重新扫描 Java 失败')
      }
    } catch (e) {
      console.error('重新扫描 Java 失败:', e)
      setStatusMessage('重新扫描 Java 失败')
    } finally {
      setIsScanningJava(false)
    }
  }

  // 浏览选择 Java 安装目录
  const handleBrowseJavaPath = async (): Promise<void> => {
    try {
      setJavaOpInProgress(true)
      const result = await browseJavaPath()
      if (result.success && result.path) {
        setNewJavaPath(result.path)
      }
    } catch (e) {
      console.error('浏览 Java 路径失败:', e)
      setStatusMessage('浏览 Java 路径失败')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  // 添加自定义 Java 路径
  const handleAddJavaPath = async (): Promise<void> => {
    const path = newJavaPath.trim()
    if (!path) {
      setStatusMessage('请输入或选择 Java 路径')
      return
    }
    try {
      setJavaOpInProgress(true)
      const result = await addJavaPath(path)
      if (result.success) {
        setNewJavaPath('')
        setStatusMessage(result.statusMessage || '已添加 Java 路径')
        await loadJavaList()
      } else {
        setStatusMessage(result.error || result.statusMessage || '添加 Java 路径失败')
      }
    } catch (e) {
      console.error('添加 Java 路径失败:', e)
      setStatusMessage('添加 Java 路径失败')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  // 设为默认 Java
  const handleSetDefaultJava = async (java: JavaInstallationInfo): Promise<void> => {
    try {
      setJavaOpInProgress(true)
      const result = await setDefaultJava(java.javaPath)
      if (result.success) {
        setStatusMessage(result.statusMessage || '已设为默认 Java')
        await loadJavaList()
      } else {
        setStatusMessage(result.error || '设为默认 Java 失败')
      }
    } catch (e) {
      console.error('设为默认 Java 失败:', e)
      setStatusMessage('设为默认 Java 失败')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  // 移除自定义 Java 路径
  const handleRemoveJavaPath = async (java: JavaInstallationInfo): Promise<void> => {
    if (!java.isCustom) {
      setStatusMessage('只能移除自定义添加的 Java 路径')
      return
    }
    try {
      setJavaOpInProgress(true)
      const result = await removeJavaPath(java.javaPath)
      if (result.success) {
        setStatusMessage(result.statusMessage || '已移除 Java 路径')
        await loadJavaList()
      } else {
        setStatusMessage(result.error || '移除 Java 路径失败')
      }
    } catch (e) {
      console.error('移除 Java 路径失败:', e)
      setStatusMessage('移除 Java 路径失败')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  // ─── 通知测试 ───
  const handleTestNotification = async (): Promise<void> => {
    try {
      const result = await testNotification()
      if (!result.success) {
        setStatusMessage('发送测试通知失败')
      }
    } catch (e) {
      console.error('发送测试通知失败:', e)
      setStatusMessage('发送测试通知失败')
    }
  }

  // ─── 底部操作栏 ───
  const handleApplyTheme = async (): Promise<void> => {
    try {
      const updateResult = await updateSettings({
        cornerRadius,
        animationDuration,
        enableAnimations: settings?.enableAnimations ?? true,
        enableWindowsNotifications,
        preferJavaw,
      })
      if (!updateResult?.success) {
        setStatusMessage(`应用设置失败: ${updateResult?.error || '未知错误'}`)
        return
      }

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
      const updateResult = await updateSettings({
        cornerRadius,
        animationDuration,
        enableAnimations: settings?.enableAnimations ?? true,
        enableWindowsNotifications,
        preferJavaw,
      })
      if (!updateResult?.success) {
        setStatusMessage(`应用设置失败: ${updateResult?.error || '未知错误'}`)
        return
      }

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
          <ColorPicker
            label="主色调"
            value={primaryColorHex}
            onChange={handlePrimaryPreview}
            onChangeEnd={handleSetPrimary}
            presets={primarySwatches.map((s) => s.color)}
          />
          <ColorPicker
            label="强调色"
            value={accentColorHex}
            onChange={handleAccentPreview}
            onChangeEnd={handleSetAccent}
            presets={accentSwatches.map((s) => s.color)}
          />
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
            {swatchesLoading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <div
                  key={i}
                  className="md-btn md-btn-outlined md-skeleton"
                  style={{
                    backgroundColor: 'var(--md-card-hover)',
                    borderColor: 'transparent',
                    opacity: 0.6,
                  }}
                >
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: 'var(--md-subtle-border)',
                    }}
                  />
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: 'var(--md-subtle-border)',
                      marginLeft: 4,
                    }}
                  />
                  <span
                    style={{
                      marginLeft: 8,
                      width: 60,
                      height: 14,
                      backgroundColor: 'var(--md-subtle-border)',
                      borderRadius: 2,
                    }}
                  />
                </div>
              ))
            ) : (
              presetOptions.map((p) => (
                <button
                  key={p.key}
                  className="md-btn md-btn-outlined"
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
              ))
            )}
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
            onChange={(e) => {
              const val = Number(e.target.value)
              setCornerRadius(val)
              applyCornerRadius(val)
              localStorage.setItem('msmc_cornerRadius', String(val))
            }}
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
            onChange={(e) => {
              const val = Number(e.target.value)
              setAnimationDuration(val)
              applyAnimationSettings(val, enableAnimations)
              localStorage.setItem('msmc_animationDuration', String(val))
            }}
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
              onChange={(e) => {
                const val = e.target.checked
                setEnableWindowsNotifications(val)
                localStorage.setItem('msmc_enableWindowsNotifications', String(val))
              }}
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
              onChange={(e) => {
                const val = e.target.checked
                setPreferJavaw(val)
                localStorage.setItem('msmc_preferJavaw', String(val))
              }}
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
              无控制台窗口启动（不推荐，服务器日志将不可见）
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
            disabled={isScanningJava}
            onClick={handleRescanJava}
          >
            <FaRotate
              size={14}
              className={isScanningJava ? 'md-spin' : ''}
            />
            <span style={{ marginLeft: 6 }}>重新扫描</span>
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
                  {/* 操作按钮 */}
                  <div
                    className="flex items-center"
                    style={{ gap: 4, flexShrink: 0, marginLeft: 8 }}
                  >
                    {!java.isDefault && (
                      <button
                        className="md-btn md-btn-outlined"
                        disabled={javaOpInProgress}
                        onClick={() => handleSetDefaultJava(java)}
                        title="设为默认 Java"
                        style={{ padding: '4px 8px', fontSize: 11 }}
                      >
                        <FaStar size={11} />
                        <span style={{ marginLeft: 4 }}>设为默认</span>
                      </button>
                    )}
                    {java.isCustom && (
                      <button
                        className="md-btn md-btn-outlined"
                        disabled={javaOpInProgress}
                        onClick={() => handleRemoveJavaPath(java)}
                        title="移除自定义 Java 路径"
                        style={{
                          padding: '4px 8px',
                          fontSize: 11,
                          color: 'var(--md-accent-text)',
                        }}
                      >
                        <FaTrashCan size={11} />
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 添加自定义 Java 路径 */}
        <div style={{ marginTop: 12 }}>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            自选 Java 路径
          </div>
          <div
            style={{
              fontSize: 11,
              color: 'var(--md-body-light)',
              marginBottom: 8,
            }}
          >
            手动指定本机上未自动检测到的 Java 安装目录（支持 Java 和 JDK）
          </div>
          <div className="flex items-center" style={{ gap: 8 }}>
            <input
              type="text"
              value={newJavaPath}
              onChange={(e) => setNewJavaPath(e.target.value)}
              placeholder="例如：C:\Program Files\Java\jdk-21"
              disabled={javaOpInProgress}
              style={{
                flex: 1,
                minWidth: 0,
                padding: '8px 10px',
                borderRadius: 'var(--md-radius-small)',
                border: '1px solid var(--md-card-subtle-border)',
                backgroundColor: 'var(--md-card-background)',
                color: 'var(--md-body)',
                fontSize: 12,
              }}
            />
            <button
              className="md-btn md-btn-outlined"
              onClick={handleBrowseJavaPath}
              disabled={javaOpInProgress}
              title="浏览选择 Java 安装目录"
            >
              <FaFolderOpen size={14} />
              <span style={{ marginLeft: 6 }}>浏览</span>
            </button>
            <button
              className="md-btn md-btn-filled"
              onClick={handleAddJavaPath}
              disabled={javaOpInProgress || !newJavaPath.trim()}
              title="添加自定义 Java 路径"
            >
              <FaPlus size={14} />
              <span style={{ marginLeft: 6 }}>添加</span>
            </button>
          </div>
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

        {/* 开发团队标题 */}
        <div
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            paddingTop: 16,
            marginBottom: 12,
          }}
        >
          <h3
            style={{
              fontSize: 15,
              fontWeight: 600,
              color: 'var(--md-body)',
              margin: 0,
              textAlign: 'center',
            }}
          >
            开发团队
          </h3>
        </div>

        {teamLoading ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              {Array.from({ length: 2 }).map((_, i) => (
                <div
                  key={i}
                  style={{
                    padding: 12,
                    borderRadius: 'var(--md-radius)',
                  }}
                  className="md-skeleton"
                >
                  <div className="flex items-center">
                    <div
                      style={{
                        width: 48,
                        height: 48,
                        borderRadius: '50%',
                        backgroundColor: 'var(--md-subtle-border)',
                        flexShrink: 0,
                      }}
                    />
                    <div style={{ marginLeft: 10, flex: 1 }}>
                      <div
                        style={{
                          width: '60%',
                          height: 14,
                          backgroundColor: 'var(--md-subtle-border)',
                          borderRadius: 2,
                          marginBottom: 6,
                        }}
                      />
                      <div
                        style={{
                          width: '80%',
                          height: 12,
                          backgroundColor: 'var(--md-subtle-border)',
                          borderRadius: 2,
                        }}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
            <div
              style={{
                padding: 16,
                borderRadius: 'var(--md-radius)',
              }}
              className="md-skeleton"
            >
              <div className="flex items-center justify-center">
                <div
                  style={{
                    width: 56,
                    height: 56,
                    borderRadius: '50%',
                    backgroundColor: 'var(--md-subtle-border)',
                  }}
                />
              </div>
              <div
                style={{
                  width: '40%',
                  height: 14,
                  backgroundColor: 'var(--md-subtle-border)',
                  borderRadius: 2,
                  margin: '10px auto 6px auto',
                }}
              />
              <div
                style={{
                  width: '60%',
                  height: 12,
                  backgroundColor: 'var(--md-subtle-border)',
                  borderRadius: 2,
                  margin: '0 auto',
                }}
              />
            </div>
          </div>
        ) : (
          <>
            {/* 主开发者 + 特别感谢 两列布局 */}
            <div className="grid grid-cols-2 gap-4" style={{ marginBottom: 16 }}>
              {/* 主开发者 */}
              <div>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  主开发者
                </div>
                <div className="space-y-2">
                  {teamInfo?.primaryDevelopers.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 12,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 48,
                          height: 48,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={24}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 10, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 13,
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                          }}
                        >
                          {member.name}
                          {member.hasHeartIcon && (
                            <FaHeart
                              size={12}
                              style={{ color: 'var(--md-accent-text)' }}
                            />
                          )}
                          {member.hasCrossIcon && (
                            <FaXmark
                              size={14}
                              style={{ color: 'var(--md-body-light)' }}
                            />
                          )}
                        </div>
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--md-body-light)',
                            marginTop: 2,
                          }}
                        >
                          {member.role}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 11,
                              color: 'var(--md-accent-text)',
                              marginTop: 2,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={12} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* 中间爱心 + 特别感谢 */}
              <div style={{ position: 'relative' }}>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  特别感谢
                </div>
                <div className="space-y-2">
                  {teamInfo?.specialThanks.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 12,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 48,
                          height: 48,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={24}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 10, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 13,
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                          }}
                        >
                          {member.name}
                          {member.hasHeartIcon && (
                            <FaHeart
                              size={12}
                              style={{ color: 'var(--md-accent-text)' }}
                            />
                          )}
                          {member.hasCrossIcon && (
                            <FaXmark
                              size={14}
                              style={{ color: 'var(--md-body-light)' }}
                            />
                          )}
                        </div>
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--md-body-light)',
                            marginTop: 2,
                          }}
                        >
                          {member.role}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 11,
                              color: 'var(--md-accent-text)',
                              marginTop: 2,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={12} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* 中间爱心装饰 */}
            <div
              style={{
                display: 'flex',
                justifyContent: 'center',
                margin: '-28px 0 12px 0',
                position: 'relative',
                zIndex: 1,
              }}
            >
              <div
                style={{
                  width: 36,
                  height: 36,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-card-background)',
                  border: '2px solid var(--md-card-subtle-border)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <FaHeart
                  size={18}
                  style={{ color: 'var(--md-accent-text)' }}
                />
              </div>
            </div>

            {/* 纪念卡片 */}
            {teamInfo?.memorial && teamInfo.memorial.length > 0 && (
              <div
                style={{
                  marginBottom: 16,
                  padding: 20,
                  background: 'linear-gradient(135deg, var(--md-memorial-gold-bg-start) 0%, var(--md-memorial-gold-bg-end) 100%)',
                  border: '2px solid var(--md-memorial-gold)',
                  borderRadius: 'var(--md-radius)',
                  position: 'relative',
                  overflow: 'hidden',
                }}
              >
                <div
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    background: 'radial-gradient(circle at 50% 0%, var(--md-memorial-gold-glow) 0%, transparent 60%)',
                    pointerEvents: 'none',
                  }}
                />
                {teamInfo.memorial.map((member, idx) => (
                  <div
                    key={idx}
                    className="flex flex-col items-center"
                    style={{ position: 'relative', zIndex: 1 }}
                  >
                    <div
                      style={{
                        width: 64,
                        height: 64,
                        borderRadius: '50%',
                        backgroundColor: 'var(--md-memorial-gold-bg-soft)',
                        border: '2px solid var(--md-memorial-gold)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      <FaUser
                        size={32}
                        style={{ color: 'var(--md-memorial-gold)' }}
                      />
                    </div>
                    <div
                      style={{
                        fontSize: 16,
                        fontWeight: 700,
                        color: 'var(--md-memorial-gold)',
                        marginTop: 10,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 8,
                      }}
                    >
                      {member.name}
                      <FaHeart
                        size={14}
                        style={{ color: 'var(--md-accent-text)' }}
                      />
                    </div>
                    <div
                      style={{
                        fontSize: 12,
                        color: 'var(--md-memorial-gold-soft)',
                        marginTop: 4,
                      }}
                    >
                      {member.role}
                    </div>
                    {member.description && (
                      <div
                        style={{
                          fontSize: 11,
                          color: 'var(--md-memorial-gold-muted)',
                          marginTop: 8,
                          textAlign: 'center',
                          fontStyle: 'italic',
                        }}
                      >
                        {member.description}
                      </div>
                    )}
                    {member.github && (
                      <a
                        href={`https://github.com/${member.github}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                          fontSize: 11,
                          color: 'var(--md-memorial-gold)',
                          marginTop: 6,
                          display: 'flex',
                          alignItems: 'center',
                          gap: 4,
                          textDecoration: 'none',
                        }}
                      >
                        <FaGithub size={12} />
                        @{member.github}
                      </a>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* 贡献者 */}
            {teamInfo?.contributors && teamInfo.contributors.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  贡献者
                </div>
                <div className="grid grid-cols-2 gap-2">
                  {teamInfo.contributors.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 10,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 40,
                          height: 40,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={20}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 8, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 12,
                          }}
                        >
                          {member.name}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 10,
                              color: 'var(--md-accent-text)',
                              marginTop: 1,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 3,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={10} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}

        {/* 测试通知按钮 */}
        <div
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            paddingTop: 16,
          }}
        >
          <button
            className="md-btn md-btn-outlined"
            style={{ width: '100%' }}
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
              textAlign: 'center',
            }}
          >
            点击测试按钮可以验证通知功能是否正常工作
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* 📋 底部操作栏 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="flex" style={{ gap: 8, marginTop: 16 }}>
        <button
          className="md-btn md-btn-outlined"
          onClick={handleReset}
        >
          <FaRotate size={16} />
          <span style={{ marginLeft: 8 }}>重置为默认</span>
        </button>
        <button
          className="md-btn md-btn-primary"
          onClick={handleApplyTheme}
        >
          <FaCheck size={16} />
          <span style={{ marginLeft: 8 }}>应用主题</span>
        </button>
        <button
          className="md-btn md-btn-primary"
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
