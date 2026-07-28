// ─────────────────────────────────────────────────────────────────────
// 桥接消息基础类型
// ─────────────────────────────────────────────────────────────────────

export type BridgeMessageType = 'request' | 'response' | 'event' | 'log'

export interface BridgeMessage {
  type: BridgeMessageType
  id?: string
  action: string
  payload?: unknown
  error?: string
  success?: boolean
  timestamp?: number
}

// ─────────────────────────────────────────────────────────────────────
// 应用通用类型
// ─────────────────────────────────────────────────────────────────────

export interface AppInfo {
  version: string
  name: string
  fullName: string
}

export interface ThemeInfo {
  mode: 'light' | 'dark'
  primaryColor: string
}

export interface AppReadyEvent {
  version: string
  isAdmin: boolean
  theme: ThemeInfo
  statusMessage?: string
}

export interface StatusUpdateEvent {
  message: string
}

// ─────────────────────────────────────────────────────────────────────
// 系统监控类型
// ─────────────────────────────────────────────────────────────────────

export interface SystemMetrics {
  timestamp: number
  cpuUsagePercent: number
  memoryUsagePercent: number
  diskUsagePercent: number
  totalMemoryBytes: number
  usedMemoryBytes: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskName: string
  totalThreadCount: number
  javaCpuUsagePercent: number
  javaWorkingSetBytes: number
  javaThreadCount: number
  perCoreCpuUsages: number[]
  isMonitoring: boolean
  memoryInfoText: string
  diskInfoText: string
}

export interface CpuInfo {
  modelName: string
  manufacturer: string
  physicalCores: number
  logicalCores: number
  socketCount: number
  numaNodeCount: number
  isHyperThreadingEnabled: boolean
  logicalToPhysicalCoreMap: number[]
  isRecognized: boolean
}

export interface HistoryPoint {
  timestamp: number
  cpuUsagePercent: number
  memoryUsagePercent: number
}

export interface HistoryRangeResult {
  points: HistoryPoint[]
  days: number
}

// ─────────────────────────────────────────────────────────────────────
// 服务器类型
// ─────────────────────────────────────────────────────────────────────

export interface ServerInfo {
  processId: number
  serverType: string
  workingDirectory: string
  serverJarPath: string
  serverJarName: string
  javaPath: string
  fullCommandLine: string
  serverPort: number
  isPortOpen: boolean
  portConflict: string
  displayName: string
  status: string
  maxHeapMemoryBytes: number
  initialHeapMemoryBytes: number
  usesAikarFlags: boolean
  gcType: string
  configFiles: string[]
  networkStatusText: string
  formattedMaxMemory: string
  lastSeenAt?: string
  isKnown?: boolean
  // Q3: 选中服务器的关联「已知服务器」ID。可空。
  // 仅当服务器在后端被成功关联到 KnownServers 列表时才填充，未关联时不传。
  knownServerId?: string
}

export interface KnownServerInfo {
  // 原始字段 id 做兼容；统一推荐使用 knownServerId，与 ServerInfo 命名一致
  id?: string
  knownServerId: string
  name: string
  serverJarPath: string
  workingDirectory: string
  javaPath?: string
  port: number
  initialHeapMemoryBytes?: number
  maxHeapMemoryBytes?: number
  group?: string
  isFavorite?: boolean
  addedAt?: string
  lastSeenAt: string
  status?: string
}

export interface ServerListResponse {
  running: ServerInfo[]
  known: KnownServerInfo[]
  isBusy: boolean
  isAutoDetectEnabled: boolean
}

// ─────────────────────────────────────────────────────────────────────
// 网络监控类型
// ─────────────────────────────────────────────────────────────────────

export interface NetworkStatus {
  totalPorts: number
  usedPorts: number
  usedPercentage: number
  systemPorts: number
  registeredPorts: number
  dynamicPorts: number
  uploadSpeedMB: number
  downloadSpeedMB: number
  speedMaximumMB: number
  uploadSpeedText: string
  downloadSpeedText: string
  todayUploadText: string
  todayDownloadText: string
  dailyAnalysisText: string
  isRefreshing: boolean
  currentHour: number
}

export interface PortInfo {
  port: number
  protocol: string
  processId: number | null
  processName: string
  isOpen: boolean
  portRange: 'System' | 'Registered' | 'Dynamic'
}

export interface PortsResponse {
  ports: PortInfo[]
  count: number
}

export interface BridgeRule {
  listenAddress: string
  listenPort: number
  connectAddress: string
  connectPort: number
  protocol: string
  engine: string
}

export interface BridgeRulesResponse {
  rules: BridgeRule[]
  count: number
}

export interface CommonPortInfo {
  port: number
  name: string
  description: string
  category: string
}

export interface AddBridgeRequest {
  listenAddress: string
  listenPort: number
  connectAddress: string
  connectPort: number
  addFirewall: boolean
  protocol?: string
}

export interface KillProcessRequest {
  port: number
  protocol: string
}

export interface HourlyHistoryResponse {
  upload: number[]
  download: number[]
}

// ─────────────────────────────────────────────────────────────────────
// 配置编辑类型
// ─────────────────────────────────────────────────────────────────────

export interface ConfigFileItem {
  fileName: string
  fullPath: string
  relativePath: string
  isDirectory: boolean
  children: ConfigFileItem[]
}

export interface ConfigFileTreeResponse {
  tree: ConfigFileItem[]
  count: number
  configFileCountText: string
  hasServerDirectory: boolean
  serverWorkingDirectory: string
  selectedServerName: string | null
}

export interface AvailableServer {
  displayName: string
  workingDirectory: string
  serverJarName: string
  serverJarPath: string
  serverPort: number
}

export interface AvailableServersResponse {
  servers: AvailableServer[]
}

export interface ConfigEntry {
  key: string
  value: string
  originalValue: string
  displayName: string
  friendlyDisplayName: string
  description: string
  isModified: boolean
  isValid: boolean
  errorMessage: string | null
  requiresRestart: boolean
  isBoolType: boolean
  isEnumType: boolean
  isNumericType: boolean
  isStringType: boolean
  allowedValues: string[] | null
  minValue: number | null
  maxValue: number | null
  valueType: string
}

export interface ConfigEntryGroup {
  key: string
  items: ConfigEntry[]
}

export interface ConfigEntriesResponse {
  groups: ConfigEntryGroup[]
  totalCount: number
  hasUnsavedChanges: boolean
  isLoading: boolean
  loadProgress: number
  selectedConfigFile: string | null
  selectedConfigFileName: string | null
  saveStatusMessage: string | null
  isSaveError: boolean
  isCurrentServerRunning?: boolean
  modifiedCount?: number
}

export interface UpdateConfigValueRequest {
  key: string
  value: string
}

export interface ConfigSaveResult {
  success: boolean
  message: string | null
  requiresRestart?: boolean
  errorType?: string
  errorDetail?: string
}

// ─────────────────────────────────────────────────────────────────────
// 设置类型
// ─────────────────────────────────────────────────────────────────────

export interface SettingsData {
  primaryColorHex: string
  accentColorHex: string
  backgroundColorHex: string
  cardColorHex: string
  textColorHex: string
  borderColorHex: string
  cornerRadius: number
  animationDuration: number
  enableAnimations: boolean
  enableWindowsNotifications: boolean
  preferJavaw: boolean
  statusMessage: string
  isDarkMode: boolean
}

export interface JavaInstallationInfo {
  javaPath: string
  javaHome: string
  versionString: string
  versionDisplay: string
  isDefault: boolean
  isCustom: boolean
}

export interface JavaListResponse {
  javas: JavaInstallationInfo[]
  isScanning: boolean
  selectedJava: string | null
}

export type ThemePreset = 'SkyBlue' | 'OceanBlue' | 'BlueOrange' | 'TealPink' | 'RedYellow'

export interface ThemeApplyResult {
  success: boolean
  primaryColorHex: string
  accentColorHex?: string
  isDarkMode?: boolean
  enableAnimations?: boolean
}

export interface SwatchInfo {
  color: string
  label: string
}

export interface PresetInfo {
  key: ThemePreset
  label: string
  primary: string
  accent: string
}

export interface SwatchesResponse {
  swatches: SwatchInfo[]
}

export interface PresetsResponse {
  presets: PresetInfo[]
}

// ─────────────────────────────────────────────────────────────────────
// 关于页面 - 团队信息类型
// ─────────────────────────────────────────────────────────────────────

export interface TeamMember {
  name: string
  role: string
  github?: string
  avatar?: string
  note?: string
  isClickable?: boolean
  hasHeartIcon?: boolean
  hasCrossIcon?: boolean
  isMemorial?: boolean
  description?: string
}

export interface TeamInfoResponse {
  primaryDevelopers: TeamMember[]
  specialThanks: TeamMember[]
  memorial: TeamMember[]
  contributors: TeamMember[]
}

// ─────────────────────────────────────────────────────────────────────
// JVM 参数类型
// ─────────────────────────────────────────────────────────────────────

export type JvmArgumentValueType =
  | 'None'
  | 'Number'
  | 'MemorySize'
  | 'BooleanFlag'
  | 'String'
  | 'Enum'

export type JvmArgumentCategory =
  | 'Memory'
  | 'GarbageCollection'
  | 'Performance'
  | 'Encoding'
  | 'Security'
  | 'Debug'
  | 'ServerBehavior'
  | 'Other'

export interface JvmArgumentDefinition {
  flag: string
  name: string
  description: string
  valueType: JvmArgumentValueType
  category: JvmArgumentCategory
  defaultValue: string | null
  minimumValue: string | null
  maximumValue: string | null
  allowedValues: string[] | null
  recommended: boolean
  warning: string | null
  requiresExperimentalUnlock: boolean
}

export interface JvmDefinitionsResponse {
  definitions: JvmArgumentDefinition[]
}

export interface JvmStateResponse {
  hasServer: boolean
  isKnownServer: boolean
  isRunning: boolean
  initialMemory: string
  maxMemory: string
  selectedArguments: string[]
}

export interface JvmUpdateArgumentRequest {
  oldArg: string
  newValue: string
}

export interface JvmSetMemoryRequest {
  initial?: string
  max?: string
}

export type JvmPresetType = 'aikar' | 'g1gc' | 'zgc'

// ─────────────────────────────────────────────────────────────────────
// 进程管理类型
// ─────────────────────────────────────────────────────────────────────

export interface ProcessAffinityInfo {
  processId: number
  processName: string
  isMinecraftServer: boolean
  isJavaProcess: boolean
  isSystemProcess: boolean
  displayName: string
  affinityMask: number
  allowedCoreIndices: number[]
  cpuUsagePercent: number
  workingSetBytes: number
  threadCount: number
  priorityClass: string
  commandLine: string
}

export interface KillProcessByIdRequest {
  pid: number
  graceful?: boolean
}

export interface SetAffinityRequest {
  pid: number
  affinityMask: number
}
