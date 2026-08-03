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
  FaShieldHalved,
  FaBolt,
  FaMoon,
  FaMemory,
  FaMicrochip,
  FaClock,
  FaPlug,
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
  getCpuPowerCapabilities,
  applyPowerProfile,
  restorePowerProfile,
  getCpuSetTopology,
  enableTimerResolution,
  disableTimerResolution,
  getTimerResolutionState,
  startPowerRequest,
  stopPowerRequest,
  getPowerRequestState,
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
  CpuPowerCapabilities,
  PowerProfile,
  ProcessQoSTier,
  CpuSetTopology,
  TimerResolutionResult,
  PowerRequestResult,
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

  // ─── 进程监管策略（崩溃重启/防睡眠/优先级/内存上限） ───
  // 后端 AppConfig.Supervisor 已扩展，前端走 localStorage 持久化 + 保存时 updateSettings 回传
  const DEFAULT_SUPERVISOR: SettingsData['supervisor'] = {
    enableCrashRestart: true,
    maxRestartAttemptsPerHour: 10,
    restartCooldownSeconds: 30,
    preventSystemSleepWhenRunning: true,
    processPriority: 'Normal',
    maxProcessMemoryBytes: 0,
    maxTotalRestartAttempts: -1,
  }

  const [supervisor, setSupervisor] = useState<SettingsData['supervisor']>(() => {
    try {
      const saved = localStorage.getItem('msmc_supervisor')
      if (saved) return { ...DEFAULT_SUPERVISOR, ...(JSON.parse(saved) as Partial<SettingsData['supervisor']>) }
    } catch { /* ignore */ }
    return DEFAULT_SUPERVISOR
  })

  const patchSupervisor = (patch: Partial<SettingsData['supervisor']>): void => {
    setSupervisor((prev) => {
      const next = { ...prev, ...patch }
      try { localStorage.setItem('msmc_supervisor', JSON.stringify(next)) } catch { /* ignore */ }
      return next
    })
  }

  const processPriorityOptions: Array<{ value: SettingsData['supervisor']['processPriority']; label: string; hint: string }> = [
    { value: 'Idle', label: '最低 (Idle)', hint: '只在系统完全空闲时运行，几乎不影响前台任务' },
    { value: 'BelowNormal', label: '低于标准 (BelowNormal)', hint: '轻度后台任务，推荐不影响游戏体验时使用' },
    { value: 'Normal', label: '标准 (Normal)', hint: '默认均衡调度，推荐绝大部分服主' },
    { value: 'AboveNormal', label: '高于标准 (AboveNormal)', hint: '大服/多人在线服，抢占更多 CPU 时间片' },
    { value: 'High', label: '高 (High)', hint: '竞技服或高 TPS 要求，可能轻微影响鼠标键盘响应' },
    { value: 'RealTime', label: '实时 (RealTime)', hint: '不推荐！抢占鼠标键盘/音频驱动，极端场景才用' },
  ]

  // ─── CPU 电源档位（T2 系统睿频 / T1 进程 QoS） ───
  const [cpuPowerCaps, setCpuPowerCaps] = useState<CpuPowerCapabilities | null>(null)
  const [applyingProfile, setApplyingProfile] = useState<PowerProfile | null>(null)
  const [powerError, setPowerError] = useState<string | null>(null)
  const [restoringProfile, setRestoringProfile] = useState(false)

  // 默认 MC 服务器主进程的 QoS 标签（持久化到 localStorage）
  const [serverQoSTier, setServerQoSTier] = useState<ProcessQoSTier>(() => {
    try {
      const saved = localStorage.getItem('msmc_server_qos')
      if (saved === 'High' || saved === 'Eco' || saved === 'Unset') return saved
    } catch { /* ignore */ }
    return 'High'
  })

  // ─── T3 用户层最大权限调度状态 ───
  // CPU Set 拓扑（P/E 核检测）
  const [cpuSetTopology, setCpuSetTopology] = useState<CpuSetTopology | null>(null)
  // 服务器启动时是否自动路由到 P-core
  const [autoPinPCores, setAutoPinPCores] = useState<boolean>(() => {
    try { return localStorage.getItem('msmc_auto_pin_pcores') === 'true' } catch { return false }
  })
  // 全局定时器精度档位（0=系统默认 15.6ms / 1=1ms / 2=0.5ms）
  const [timerTier, setTimerTier] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('msmc_timer_tier')
      return saved ? Number(saved) : 0
    } catch { return 0 }
  })
  const [timerState, setTimerState] = useState<TimerResolutionResult | null>(null)
  // 服务器进程 Priority Boost 策略（'auto'=系统默认 / 'disable'=禁用前台 boost）
  const [serverBoostMode, setServerBoostMode] = useState<'auto' | 'disable'>(() => {
    try {
      const saved = localStorage.getItem('msmc_server_boost')
      return saved === 'disable' ? 'disable' : 'auto'
    } catch { return 'auto' }
  })
  // Power Request 状态
  const [powerReqState, setPowerReqState] = useState<PowerRequestResult | null>(null)

  const powerProfileOptions: Array<{ value: PowerProfile; label: string; desc: string; color: string }> = [
    { value: 'UltimatePerformance', label: '极致性能', desc: 'Aggressive 睿频 + 100% 处理器状态 + 激进升频', color: 'var(--md-danger)' },
    { value: 'Balanced', label: '平衡', desc: '标准睿频 + 100% 处理器状态', color: 'var(--md-primary-hue-mid)' },
    { value: 'Efficient', label: '能效优先', desc: '能效优先的睿频 + 90% 处理器状态', color: 'var(--md-success)' },
    { value: 'PowerSaver', label: '极限省电', desc: '禁用睿频 + 80% 处理器状态', color: 'var(--md-body-light)' },
  ]

  const timerOptions: Array<{ tier: number; label: string; periodMs: number; desc: string }> = [
    { tier: 0, label: '系统默认 (15.6ms)', periodMs: 0, desc: '不修改系统 tick，最低功耗' },
    { tier: 1, label: '1ms (推荐 MC 服)', periodMs: 1, desc: '显著降低 20 TPS 主循环抖动，少量功耗' },
    { tier: 2, label: '0.5ms (极致)', periodMs: 1, desc: '更精细，但增加空闲功耗（实际仍受限于系统最小值）' },
  ]

  const refreshCpuPowerCaps = useCallback(async (): Promise<void> => {
    try {
      const caps = await getCpuPowerCapabilities()
      setCpuPowerCaps(caps)
    } catch (e) {
      console.error('获取 CPU 电源能力失败:', e)
    }
  }, [])

  const handleApplyPowerProfile = useCallback(async (profile: PowerProfile): Promise<void> => {
    setApplyingProfile(profile)
    setPowerError(null)
    try {
      const r = await applyPowerProfile(profile)
      if (!r.success) {
        setPowerError(r.error ?? '应用失败')
      }
      await refreshCpuPowerCaps()
    } catch (e) {
      setPowerError(e instanceof Error ? e.message : String(e))
    } finally {
      setApplyingProfile(null)
    }
  }, [refreshCpuPowerCaps])

  const handleRestorePowerProfile = useCallback(async (): Promise<void> => {
    setRestoringProfile(true)
    setPowerError(null)
    try {
      const r = await restorePowerProfile()
      if (!r.success) {
        setPowerError(r.error ?? '还原失败')
      }
      await refreshCpuPowerCaps()
    } catch (e) {
      setPowerError(e instanceof Error ? e.message : String(e))
    } finally {
      setRestoringProfile(false)
    }
  }, [refreshCpuPowerCaps])

  const handleSetServerQoS = useCallback((tier: ProcessQoSTier): void => {
    setServerQoSTier(tier)
    try { localStorage.setItem('msmc_server_qos', tier) } catch { /* ignore */ }
  }, [])

  // ─── T3 处理器 ───────────────────────────────────────────────────────────

  const refreshCpuSetTopology = useCallback(async (): Promise<void> => {
    try {
      const topo = await getCpuSetTopology()
      setCpuSetTopology(topo)
    } catch (e) {
      console.error('获取 CPU Set 拓扑失败:', e)
    }
  }, [])

  const refreshTimerState = useCallback(async (): Promise<void> => {
    try {
      const r = await getTimerResolutionState()
      setTimerState(r)
    } catch (e) {
      console.error('获取定时器精度状态失败:', e)
    }
  }, [])

  const refreshPowerRequestState = useCallback(async (): Promise<void> => {
    try {
      const r = await getPowerRequestState()
      setPowerReqState(r)
    } catch (e) {
      console.error('获取 Power Request 状态失败:', e)
    }
  }, [])

  const handleToggleAutoPinPCores = useCallback((enabled: boolean): void => {
    setAutoPinPCores(enabled)
    try { localStorage.setItem('msmc_auto_pin_pcores', enabled ? 'true' : 'false') } catch { /* ignore */ }
  }, [])

  const handleSetTimerTier = useCallback(async (tier: number): Promise<void> => {
    setTimerTier(tier)
    try { localStorage.setItem('msmc_timer_tier', String(tier)) } catch { /* ignore */ }
    const opt = timerOptions.find((o) => o.tier === tier)
    if (!opt) return
    try {
      if (opt.periodMs > 0) {
        await enableTimerResolution(opt.periodMs)
      } else {
        await disableTimerResolution()
      }
      await refreshTimerState()
    } catch (e) {
      console.error('设置定时器精度失败:', e)
    }
  }, [refreshTimerState])

  const handleSetServerBoostMode = useCallback((mode: 'auto' | 'disable'): void => {
    setServerBoostMode(mode)
    try { localStorage.setItem('msmc_server_boost', mode) } catch { /* ignore */ }
  }, [])

  const handleTogglePowerRequest = useCallback(async (): Promise<void> => {
    try {
      if (powerReqState?.active) {
        await stopPowerRequest()
      } else {
        await startPowerRequest('MSMC 服务器运行中（用户在设置页手动启用）')
      }
      await refreshPowerRequestState()
    } catch (e) {
      console.error('切换 Power Request 失败:', e)
    }
  }, [powerReqState?.active, refreshPowerRequestState])

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
      // 如果后端有带 supervisor 字段 → 覆盖 localStorage 默认值（确保后端 C# AppConfig.Supervisor 是真·源）
      if (resp.supervisor) patchSupervisor(resp.supervisor)
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
    refreshCpuPowerCaps()
    refreshCpuSetTopology()
    refreshTimerState()
    refreshPowerRequestState()
  }, [
    loadSettings,
    loadJavaList,
    loadSwatchesAndPresets,
    loadTeamInfo,
    refreshCpuPowerCaps,
    refreshCpuSetTopology,
    refreshTimerState,
    refreshPowerRequestState,
  ])

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
        supervisor,
      } as any)
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
        supervisor,
      } as any)
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
      {/* [THEME] 外观设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '0ms' }}>
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
      {/* [TOAST] 服务器设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '80ms' }}>
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
      {/* [SUPERVISOR] 进程监管策略卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '140ms' }}>
        <div className="flex items-center mb-2">
          <FaShieldHalved size={18} style={{ color: 'var(--md-accent-text)', marginRight: 8 }} />
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: 0, lineHeight: 1.2 }}
          >
            进程监管策略
          </h2>
        </div>
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-body-light)',
            margin: '4px 0 16px 26px',
            lineHeight: 1.55,
          }}
        >
          基于 Win32 Job Object 实现：崩溃自动重启、防止系统睡眠、设置进程优先级/内存硬上限。关闭 MSMC
          时所有服务器进程会被一并终止，不会像老版本出现“幽灵 Java。
        </div>

        {/* ✅ 1. 崩溃自动重启开关 */}
        <div className="md-field">
          <label className="md-switch md-switch-lg">
            <input
              type="checkbox"
              checked={supervisor.enableCrashRestart}
              onChange={(e) => patchSupervisor({ enableCrashRestart: e.target.checked })}
            />
            <span className="md-slider md-slider-lg" />
            <div className="md-switch-label">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <FaRotate size={13} style={{ color: 'var(--md-primary)' }} />
                <span style={{ fontSize: 13, color: 'var(--md-body)', fontWeight: 500 }}>
                  崩溃自动重启
                </span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 2 }}>
                服务器意外崩溃后按冷却时间自动拉起，直到达到次数上限后停止
              </div>
            </div>
          </label>
        </div>

        {/* ✅ 2. 每小时最大重启次数 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaBolt size={12} style={{ marginRight: 6, color: 'var(--md-warning)' }} />
          每小时最大重启次数
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={120}
            step={1}
            value={supervisor.maxRestartAttemptsPerHour}
            onChange={(e) => patchSupervisor({ maxRestartAttemptsPerHour: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 56, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxRestartAttemptsPerHour === 0 ? '不限' : supervisor.maxRestartAttemptsPerHour + ' 次/时'}
          </div>
          </div>
        </div>

        {/* ✅ 3. 重启冷却时间（秒） */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaMemory size={12} style={{ marginRight: 6, color: 'var(--md-info)' }} />
          重启冷却时间
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={600}
            step={1}
            value={supervisor.restartCooldownSeconds}
            onChange={(e) => patchSupervisor({ restartCooldownSeconds: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 72, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.restartCooldownSeconds} 秒
          </div>
          </div>
        </div>

        {/* ✅ 4. 防睡眠开关 */}
        <div className="md-field">
          <label className="md-switch md-switch-lg">
            <input
              type="checkbox"
              checked={supervisor.preventSystemSleepWhenRunning}
              onChange={(e) => patchSupervisor({ preventSystemSleepWhenRunning: e.target.checked })}
            />
            <span className="md-slider md-slider-lg" />
            <div className="md-switch-label">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <FaMoon size={13} style={{ color: 'var(--md-accent)' }} />
                <span style={{ fontSize: 13, color: 'var(--md-body)', fontWeight: 500 }}>
                  服务器运行时阻止系统睡眠
                </span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 2 }}>
                只要有一台监管的服务器在运行，Windows 就不会进入 Modern Standby / S3 睡眠
              </div>
            </div>
          </label>
        </div>

        {/* ✅ 5. 进程优先级下拉 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaBolt size={12} style={{ marginRight: 6, color: 'var(--md-primary)' }} />
          进程优先级 (Process Priority)
        </div>
        <select
          className="md-select"
          value={supervisor.processPriority}
          onChange={(e) => patchSupervisor({ processPriority: e.target.value as SettingsData['supervisor']['processPriority'] })}
          style={{ maxWidth: 480 }}
        >
          {processPriorityOptions.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 6, lineHeight: 1.6 }}>
          {processPriorityOptions.find((o) => o.value === supervisor.processPriority)?.hint}
        </div>
        </div>

        {/* ✅ 6. 进程内存硬上限（GB） */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaMemory size={12} style={{ marginRight: 6, color: 'var(--md-danger)' }} />
          进程内存硬上限 (Job Object 级别，超出内核直接 Kill)
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={128}
            step={1}
            value={Math.round(supervisor.maxProcessMemoryBytes / (1024 ** 3))}
            onChange={(e) => {
              const gb = Number(e.target.value)
              patchSupervisor({ maxProcessMemoryBytes: gb === 0 ? 0 : gb * (1024 ** 3) })
            }}
            className="md-range"
            style={{ flex: 1 }}
          />
          <div style={{ minWidth: 72, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxProcessMemoryBytes === 0
              ? '不限'
              : `${Math.round(supervisor.maxProcessMemoryBytes / (1024 ** 3))} GB`}
          </div>
          </div>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 4, lineHeight: 1.6 }}>
          在 JVM <code>-Xmx</code> 之外再套一层 OS 级别硬上限，防止内存泄漏直接打爆整机（推荐设置为略大于 -Xmx 2-4GB）
        </div>
        </div>

        {/* ✅ 7. 总重启次数上限 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaShield size={12} style={{ marginRight: 6, color: 'var(--md-success)' }} />
          总重启次数上限（防止一次性地图损坏导致无限重启）
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={-1}
            max={1000}
            step={1}
            value={supervisor.maxTotalRestartAttempts}
            onChange={(e) => patchSupervisor({ maxTotalRestartAttempts: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 96, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxTotalRestartAttempts === -1
              ? '不限次数'
              : supervisor.maxTotalRestartAttempts === 0
                ? '永不重启'
                : `${supervisor.maxTotalRestartAttempts} 次`}
          </div>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [CPU POWER] CPU 电源档位卡片（T2 睿频 + T1 进程 QoS） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '140ms' }}>
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}
        >
          <FaBolt size={14} style={{ marginRight: 6, color: 'var(--md-warning)' }} />
          CPU 电源档位与睿频管控
        </h2>
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-body-light)',
            marginBottom: 12,
          }}
        >
          仿安卓性能模式：系统级睿频档位（PERFBOOSTMODE）+ 进程级 QoS 能效标签。修改前自动快照，退出/崩溃可还原。
        </div>

        {/* 平台能力状态条 */}
        <div
          style={{
            display: 'flex',
            gap: 12,
            flexWrap: 'wrap',
            padding: '8px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
            marginBottom: 16,
            fontSize: 11,
          }}
        >
          <span>
            当前档位：
            <strong style={{ color: cpuPowerCaps?.currentBoostMode === 2 ? 'var(--md-danger)' : 'var(--md-body)' }}>
              {cpuPowerCaps?.currentProfileName ?? '加载中...'}
            </strong>
            {cpuPowerCaps?.currentBoostMode !== undefined && cpuPowerCaps.currentBoostMode >= 0 && (
              <span style={{ color: 'var(--md-body-lighter)' }}> (BoostMode={cpuPowerCaps.currentBoostMode})</span>
            )}
          </span>
          <span style={{ color: cpuPowerCaps?.isAdmin ? 'var(--md-success)' : 'var(--md-warning)' }}>
            {cpuPowerCaps?.isAdmin ? '✓ 管理员' : '⚠ 非管理员（仅可查询，无法修改电源策略）'}
          </span>
          {cpuPowerCaps?.hasPendingCrashSnapshot && (
            <span style={{ color: 'var(--md-danger)' }}>⚠ 检测到未还原的崩溃快照</span>
          )}
        </div>

        {/* T2: 电源档位预设按钮组 */}
        <div className="md-label" style={{ marginBottom: 8 }}>
          系统电源档位（睿频激进型，需管理员）
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 8, marginBottom: 12 }}>
          {powerProfileOptions.map((opt) => {
            const isCurrent = cpuPowerCaps?.currentProfileName === opt.value
            const isApplying = applyingProfile === opt.value
            return (
              <button
                key={opt.value}
                onClick={() => handleApplyPowerProfile(opt.value)}
                disabled={isApplying || (cpuPowerCaps !== null && !cpuPowerCaps.canModifyPowerProfile)}
                style={{
                  padding: '10px 12px',
                  borderRadius: 8,
                  border: isCurrent ? `2px solid ${opt.color}` : '1px solid var(--md-subtle-border)',
                  background: isCurrent ? `${opt.color}18` : 'var(--md-card-bg)',
                  cursor: isApplying || (cpuPowerCaps !== null && !cpuPowerCaps.canModifyPowerProfile) ? 'not-allowed' : 'pointer',
                  opacity: isApplying ? 0.6 : 1,
                  textAlign: 'left',
                  transition: 'all 0.15s ease',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: opt.color, display: 'inline-block' }} />
                  <strong style={{ fontSize: 13, color: 'var(--md-body)' }}>{opt.label}</strong>
                  {isCurrent && <span style={{ fontSize: 10, color: opt.color, fontWeight: 700 }}>● 当前</span>}
                </div>
                <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', lineHeight: 1.4 }}>
                  {isApplying ? '应用中...' : opt.desc}
                </div>
              </button>
            )
          })}
        </div>

        {/* 还原按钮 */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <button
            onClick={handleRestorePowerProfile}
            disabled={restoringProfile}
            className="md-btn md-btn-outlined"
            style={{ fontSize: 12 }}
          >
            {restoringProfile ? '还原中...' : '还原原始电源策略'}
          </button>
        </div>

        {powerError && (
          <div style={{ fontSize: 11, color: 'var(--md-danger)', marginBottom: 12, padding: '6px 10px', background: 'var(--md-danger-bg, rgba(255,0,0,0.06))', borderRadius: 6 }}>
            {powerError}
          </div>
        )}

        {/* T1: 服务器进程 QoS 标签 */}
        <div
          style={{
            borderTop: '1px solid var(--md-subtle-border)',
            paddingTop: 12,
            marginTop: 4,
          }}
        >
          <div className="md-label" style={{ marginBottom: 6 }}>
            MC 服务器进程 QoS 能效标签（启动时自动应用）
          </div>
          <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.5 }}>
            EcoQoS 等同安卓 schedtune：High=解除节流高性能 / Eco=降频调度到能效核 / Unset=系统默认
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            {([
              { value: 'High', label: 'High（高性能）', color: 'var(--md-danger)' },
              { value: 'Eco', label: 'Eco（能效优先）', color: 'var(--md-success)' },
              { value: 'Unset', label: 'Unset（系统默认）', color: 'var(--md-body-light)' },
            ] as Array<{ value: ProcessQoSTier; label: string; color: string }>).map((opt) => {
              const isSelected = serverQoSTier === opt.value
              return (
                <button
                  key={opt.value}
                  onClick={() => handleSetServerQoS(opt.value)}
                  style={{
                    padding: '6px 14px',
                    fontSize: 12,
                    fontWeight: 600,
                    color: isSelected ? '#fff' : opt.color,
                    background: isSelected ? opt.color : 'transparent',
                    border: `1px solid ${opt.color}`,
                    borderRadius: 6,
                    cursor: 'pointer',
                    transition: 'all 0.12s ease',
                  }}
                >
                  {opt.label}
                </button>
              )
            })}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginTop: 6 }}>
            当前选择：<strong>{serverQoSTier}</strong> — 将在服务器启动时自动应用到此进程
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [T3] 用户层最大权限调度卡片（CPU Set / Timer / Boost / PowerRequest） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '150ms' }}>
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}
        >
          <FaMicrochip size={14} style={{ marginRight: 6, color: 'var(--md-primary)' }} />
          用户层最大权限调度（零 SDK / 零驱动）
        </h2>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginBottom: 12, lineHeight: 1.5 }}>
          Win32 用户态 API 直通：CPU Set P/E 核路由 · winmm 定时器精度 · Priority Boost · Power Request 防睡眠
        </div>

        {/* ─── CPU Set P/E 核路由（异构 CPU 检测）─── */}
        <div style={{
          padding: '10px 12px',
          background: 'var(--md-card-bg)',
          borderRadius: 8,
          marginBottom: 12,
          border: cpuSetTopology?.isHybridCpu
            ? '1px solid var(--md-primary)'
            : '1px solid var(--md-subtle-border)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
            <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>CPU Set P/E 核路由</strong>
            {cpuSetTopology && (
              <span style={{
                fontSize: 10,
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: 10,
                background: cpuSetTopology.isHybridCpu
                  ? 'color-mix(in srgb, var(--md-primary) 18%, transparent)'
                  : 'color-mix(in srgb, var(--md-body-light) 15%, transparent)',
                color: cpuSetTopology.isHybridCpu ? 'var(--md-primary)' : 'var(--md-body-light)',
              }}>
                {cpuSetTopology.isHybridCpu ? '异构 CPU' : '同构 CPU'}
              </span>
            )}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
            Intel 12 代+ / AMD Ryzen 7000+ X3D 异构 CPU 可把 MC 主进程锁定到 P-core（性能核），
            避免 E-core 误调度导致 TPS 抖动。SchedulingClass&gt;0 的 CPU Set 视为 P-core。
          </div>
          {cpuSetTopology?.success && (
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', fontSize: 10, marginBottom: 8 }}>
              <span style={{ color: 'var(--md-body)' }}>
                检测到 <strong>{cpuSetTopology.totalCpuSets}</strong> 个 CPU Set
              </span>
              <span style={{ color: 'var(--md-danger)' }}>
                P-core: <strong>{cpuSetTopology.performanceCpuSetCount}</strong>
              </span>
              <span style={{ color: 'var(--md-success)' }}>
                E-core: <strong>{cpuSetTopology.efficiencyCpuSetCount}</strong>
              </span>
            </div>
          )}
          <label style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            cursor: cpuSetTopology?.isHybridCpu ? 'pointer' : 'not-allowed',
            opacity: cpuSetTopology?.isHybridCpu ? 1 : 0.5,
            padding: '6px 0',
          }}>
            <input
              type="checkbox"
              checked={autoPinPCores}
              disabled={!cpuSetTopology?.isHybridCpu}
              onChange={(e) => handleToggleAutoPinPCores(e.target.checked)}
              style={{ width: 14, height: 14, cursor: 'pointer' }}
            />
            <span style={{ fontSize: 11, color: 'var(--md-body)' }}>
              服务器启动时自动路由到 P-core
              {!cpuSetTopology?.isHybridCpu && (
                <span style={{ color: 'var(--md-warning)', marginLeft: 4 }}>
                  （当前 CPU 非异构，无需路由）
                </span>
              )}
            </span>
          </label>
        </div>

        {/* ─── winmm 定时器精度 ─── */}
        <div style={{
          padding: '10px 12px',
          background: 'var(--md-card-bg)',
          borderRadius: 8,
          marginBottom: 12,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
            <FaClock size={11} style={{ color: 'var(--md-warning)' }} />
            <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>winmm 定时器精度</strong>
            {timerState?.enabled && (
              <span style={{
                fontSize: 9,
                fontWeight: 700,
                padding: '1px 6px',
                borderRadius: 8,
                background: 'color-mix(in srgb, var(--md-success) 18%, transparent)',
                color: 'var(--md-success)',
              }}>
                ● 已启用 {timerState.periodMs}ms
              </span>
            )}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
            默认系统 tick 15.6ms → 提到 1ms 可显著降低 MC 20 TPS 主循环抖动。仅在服务器运行期间启用，
            应用退出时自动还原（Dispose 自动调用 timeEndPeriod）。
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}>
            {timerOptions.map((opt) => {
              const isSelected = timerTier === opt.tier
              return (
                <button
                  key={opt.tier}
                  onClick={() => handleSetTimerTier(opt.tier)}
                  style={{
                    padding: '8px 6px',
                    borderRadius: 6,
                    border: isSelected
                      ? '2px solid var(--md-primary)'
                      : '1px solid var(--md-subtle-border)',
                    background: isSelected
                      ? 'color-mix(in srgb, var(--md-primary) 10%, transparent)'
                      : 'transparent',
                    cursor: 'pointer',
                    textAlign: 'left',
                    transition: 'all 0.12s ease',
                  }}
                >
                  <div style={{
                    fontSize: 11,
                    fontWeight: 600,
                    color: isSelected ? 'var(--md-primary)' : 'var(--md-body)',
                    marginBottom: 2,
                  }}>
                    {opt.label}
                  </div>
                  <div style={{ fontSize: 9, color: 'var(--md-body-lighter)', lineHeight: 1.3 }}>
                    {opt.desc}
                  </div>
                </button>
              )
            })}
          </div>
        </div>

        {/* ─── Priority Boost + Power Request ─── */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          {/* Priority Boost */}
          <div style={{
            padding: '10px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
              <FaBolt size={11} style={{ color: 'var(--md-warning)' }} />
              <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>Priority Boost</strong>
            </div>
            <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
              控制服务器进程在窗口前台/输入事件时是否自动提升优先级。后台服建议禁用以稳定调度。
            </div>
            <div style={{ display: 'flex', gap: 6 }}>
              {([
                { value: 'auto', label: '系统默认' },
                { value: 'disable', label: '禁用前台 boost' },
              ] as Array<{ value: 'auto' | 'disable'; label: string }>).map((opt) => {
                const isSelected = serverBoostMode === opt.value
                return (
                  <button
                    key={opt.value}
                    onClick={() => handleSetServerBoostMode(opt.value)}
                    style={{
                      flex: 1,
                      padding: '6px 8px',
                      fontSize: 11,
                      fontWeight: 600,
                      color: isSelected ? '#fff' : 'var(--md-body)',
                      background: isSelected ? 'var(--md-primary)' : 'transparent',
                      border: `1px solid ${isSelected ? 'var(--md-primary)' : 'var(--md-subtle-border)'}`,
                      borderRadius: 6,
                      cursor: 'pointer',
                      transition: 'all 0.12s ease',
                    }}
                  >
                    {opt.label}
                  </button>
                )
              })}
            </div>
          </div>

          {/* Power Request */}
          <div style={{
            padding: '10px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
              <FaPlug size={11} style={{ color: 'var(--md-success)' }} />
              <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>Power Request 防睡眠</strong>
              {powerReqState?.active && (
                <span style={{
                  fontSize: 9,
                  fontWeight: 700,
                  padding: '1px 6px',
                  borderRadius: 8,
                  background: 'color-mix(in srgb, var(--md-success) 18%, transparent)',
                  color: 'var(--md-success)',
                }}>
                  ● 活跃
                </span>
              )}
            </div>
            <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
              命名化防睡眠请求（比 SetThreadExecutionState 更可靠），崩溃时句柄自动释放。
            </div>
            <button
              onClick={handleTogglePowerRequest}
              style={{
                width: '100%',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: powerReqState?.active ? 'var(--md-danger)' : '#fff',
                background: powerReqState?.active
                  ? 'transparent'
                  : 'var(--md-success)',
                border: `1px solid ${powerReqState?.active ? 'var(--md-danger)' : 'var(--md-success)'}`,
                borderRadius: 6,
                cursor: 'pointer',
                transition: 'all 0.12s ease',
              }}
            >
              {powerReqState?.active ? '停止 Power Request' : '启动 Power Request'}
            </button>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [JAVA] Java 管理卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '160ms' }}>
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
      {/* [INFO] 关于卡片 */}
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
      {/* [LOG] 底部操作栏 */}
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
