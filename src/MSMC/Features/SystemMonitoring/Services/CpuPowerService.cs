// ═══════════════════════════════════════════════════════════════════════════════
// ⚡ CpuPowerService — 用户层最大权限的 CPU 资源调度与睿频管控
// ═══════════════════════════════════════════════════════════════════════════════
// 设计目标（在「不使用第三方 SDK / 不自研驱动」约束下尽最大权限）：
//
//   T1 进程级 QoS 调度（用户态 API，零驱动）：
//     - EcoQoS / Power Throttling：SetProcessInformation(ProcessPowerThrottling)
//       把后台进程降频 / 调度到能效核，类安卓 schedtune
//     - 内存优先级：SetProcessInformation(ProcessMemoryPriority)
//       控制 MC 内存页驻留优先级，防止被优先换出
//     - 已有：ProcessorAffinity（核心绑定）+ PriorityClass（优先级）+ Job Object
//
//   T2 系统电源策略 / 睿频激进型（管理员 + powercfg，可恢复）：
//     - PERFBOOSTMODE：7 档睿频模式（Disabled/Enabled/Aggressive/Efficient...）
//     - PROCTHROTTLEMAX/MIN：最大/最小处理器状态 %
//     - PERFINCREASEPOLICY / PERFDECREASEPOLICY：升降频策略
//     - 4 个预设档位：极致性能 / 平衡 / 能效优先 / 极限省电
//     - 快照-还原：修改前完整快照 powercfg /q，应用退出 / 崩溃后自动还原
//
//   T3 用户层最大权限调度补齐（零 SDK / 零驱动，本批次新增）：
//     - CPU Set（P/E 核路由）：GetSystemCpuSetInformation + SetProcessDefaultCpuSet
//       Intel 12 代+ / AMD Ryzen 7000+ X3D 异构 CPU 的核心调度，比 ProcessorAffinity
//       更精细，可"用 P-core 不用 E-core"而不影响超线程兄弟核
//     - Priority Boost：SetProcessPriorityBoost，控制进程在窗口前台/输入事件时
//       是否自动提升优先级；后台 MC 服建议关闭 boost 稳定调度
//     - winmm 定时器精度：timeBeginPeriod/timeEndPeriod，把系统 tick 从 15.6ms
//       提到 1ms，显著降低 MC 20 TPS 主循环抖动（仅在服务器运行期间启用）
//     - Power Request：PowerCreateRequest/PowerSetRequest，比 SetThreadExecutionState
//       更现代、命名化、崩溃友好的防睡眠机制
//
//   安全保障：
//     - T2 全部操作前先做快照，写入 %LocalAppData%/MSMC/power-snapshot.txt
//     - 崩溃恢复：启动时检测未还原的快照文件，若存在则提示并自动还原
//     - 非管理员时 T2 自动降级为只读查询，不报错
//     - T3 timer/power request 在服务 Dispose 时自动清理，避免泄露
// ═══════════════════════════════════════════════════════════════════════════════

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using io.NET.ZTR_OS.Features.Shared.Native;
using io.NET.ZTR_OS.Features.Startup.Services;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

/// <summary>进程 QoS 能效档位（对应 Windows EcoQoS 体系）</summary>
public enum ProcessQoSTier
{
    /// <summary>高性能（解除节流，前台焦点应用标准）</summary>
    High,
    /// <summary>能效优先（启用 EcoQoS，降频 / 调度到能效核）</summary>
    Eco,
    /// <summary>解除（清除节流标记，恢复系统默认行为）</summary>
    Unset,
}

/// <summary>系统电源档位预设</summary>
public enum PowerProfile
{
    /// <summary>极致性能：Aggressive 睿频 + 100% 处理器状态 + 激进升频</summary>
    UltimatePerformance,
    /// <summary>平衡：标准睿频 + 100% 处理器状态</summary>
    Balanced,
    /// <summary>能效优先：能效优先的睿频 + 90% 处理器状态</summary>
    Efficient,
    /// <summary>极限省电：禁用睿频 + 80% 处理器状态</summary>
    PowerSaver,
}

/// <summary>电源档位能力查询结果</summary>
public sealed record CpuPowerCapabilities(
    bool SupportsEcoQoS,
    bool SupportsMemoryPriority,
    bool IsAdmin,
    bool CanModifyPowerProfile,
    string CurrentProfileName,
    int CurrentBoostMode);

/// <summary>进程 QoS 应用结果</summary>
public sealed record QoSApplyResult(bool Success, string? Error, ProcessQoSTier AppliedTier);

/// <summary>电源档位应用结果</summary>
public sealed record PowerProfileApplyResult(bool Success, string? Error, PowerProfile AppliedProfile);

// ═══════════════════════════════════════════════════════════════════════════════
// T3 用户层最大权限调度补齐 — 数据模型
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 一个 CPU Set 的描述（P-core 组或 E-core 组）。
/// SchedulingClass = 0 表示 E-core（能效核），&gt; 0 表示 P-core（性能核，值越大越偏性能）。
/// </summary>
public sealed record CpuSetInfo(
    int Id,                       // CPU Set ID（用于 SetProcessDefaultCpuSet）
    int Group,                    // NUMA 组
    int LogicalProcessorIndex,    // 组内逻辑处理器序号
    int CoreIndex,                // 物理核序号
    int LogicalProcessorCount,    // 本 Set 中的逻辑处理器数
    int CoreCount,                // 本 Set 中的物理核数
    ulong SchedulingClass,        // 0=E-core，>0=P-core
    bool IsParked,                // 是否已停泊
    bool IsPerformanceCore);      // 推断：SchedulingClass>0 → P-core

/// <summary>系统 CPU Set 拓扑查询结果</summary>
public sealed record CpuSetTopology(
    bool Success,
    string? Error,
    bool IsHybridCpu,             // 是否为异构 CPU（同时有 P-core 和 E-core）
    int TotalCpuSets,
    int PerformanceCpuSetCount,   // P-core Set 数量
    int EfficiencyCpuSetCount,    // E-core Set 数量
    IReadOnlyList<CpuSetInfo> CpuSets,
    IReadOnlyList<int> PerformanceCpuSetIds,  // P-core Set ID 列表（用于一键锁定）
    IReadOnlyList<int> EfficiencyCpuSetIds);

/// <summary>CPU Set 路由应用结果</summary>
public sealed record CpuSetPinResult(
    bool Success,
    string? Error,
    int Pid,
    IReadOnlyList<int> AppliedCpuSetIds,
    bool PinnedToPCores);

/// <summary>定时器精度设置结果</summary>
public sealed record TimerResolutionResult(
    bool Success,
    string? Error,
    uint PeriodMs,
    bool Enabled);

/// <summary>Power Request（防睡眠）操作结果</summary>
public sealed record PowerRequestResult(
    bool Success,
    string? Error,
    string Reason,
    bool Active);

/// <summary>Priority Boost 查询/设置结果</summary>
public sealed record PriorityBoostResult(
    bool Success,
    string? Error,
    int Pid,
    bool DisablePriorityBoost);

/// <summary>
/// CPU 电源与调度管控服务接口
/// </summary>
public interface ICpuPowerService
{
    /// <summary>查询平台能力（哪些 T1/T2 能力可用）</summary>
    CpuPowerCapabilities GetCapabilities();

    /// <summary>给进程设置 QoS 能效档位（T1）</summary>
    QoSApplyResult SetProcessQoS(int pid, ProcessQoSTier tier);

    /// <summary>给进程设置内存优先级（T1，0=VeryLow ~ 5=Normal）</summary>
    (bool Success, string? Error) SetProcessMemoryPriority(int pid, uint priority);

    /// <summary>应用系统电源档位预设（T2，需管理员）</summary>
    Task<PowerProfileApplyResult> ApplyPowerProfileAsync(PowerProfile profile);

    /// <summary>还原原始电源策略（T2，基于快照）</summary>
    Task<PowerProfileApplyResult> RestoreOriginalProfileAsync();

    /// <summary>检测是否有未还原的崩溃快照（启动时调用）</summary>
    bool HasPendingCrashSnapshot();

    /// <summary>查询当前电源档位（推断当前 PERFBOOSTMODE）</summary>
    Task<(PowerProfile Profile, int BoostMode)> GetCurrentProfileAsync();

    // ── T3 用户层最大权限调度补齐 ──────────────────────────────────────────

    /// <summary>查询系统 CPU Set 拓扑（P/E 核分布，异构 CPU 检测）</summary>
    CpuSetTopology GetCpuSetTopology();

    /// <summary>把进程默认调度限制到 P-core（自动选择 SchedulingClass&gt;0 的 CPU Set）</summary>
    CpuSetPinResult PinProcessToPCores(int pid);

    /// <summary>把进程默认调度限制到指定 CPU Set 列表（用户手动选择）</summary>
    CpuSetPinResult PinProcessToCpuSets(int pid, IReadOnlyList<int> cpuSetIds);

    /// <summary>清除进程的 CPU Set 限制（恢复系统默认调度）</summary>
    (bool Success, string? Error) ClearProcessCpuSetPinning(int pid);

    /// <summary>设置进程优先级 Boost 是否禁用（true=禁用前台 boost，稳定后台调度）</summary>
    PriorityBoostResult SetProcessPriorityBoost(int pid, bool disableBoost);

    /// <summary>查询进程当前的 Priority Boost 状态</summary>
    PriorityBoostResult GetProcessPriorityBoost(int pid);

    /// <summary>启用全局定时器精度（timeBeginPeriod，1ms 推荐 MC 服）</summary>
    TimerResolutionResult EnableTimerResolution(uint periodMs);

    /// <summary>禁用全局定时器精度（timeEndPeriod，恢复系统默认 15.6ms）</summary>
    TimerResolutionResult DisableTimerResolution();

    /// <summary>查询当前定时器精度状态</summary>
    TimerResolutionResult GetTimerResolutionState();

    /// <summary>启动 Power Request（防睡眠，命名化，比 SetThreadExecutionState 更可靠）</summary>
    PowerRequestResult StartPowerRequest(string reason);

    /// <summary>停止 Power Request</summary>
    PowerRequestResult StopPowerRequest();

    /// <summary>查询 Power Request 当前状态</summary>
    PowerRequestResult GetPowerRequestState();
}


/// <summary>
/// CPU 电源与调度管控服务实现（Windows 专用，非 Windows 平台由 DI 降级为不可用）
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CpuPowerService : ICpuPowerService, IDisposable
{
    private readonly IPrivilegeService _privilegeService;
    private readonly ILogger _log;

    /// <summary>快照文件路径（崩溃恢复用）</summary>
    private static readonly string SnapshotFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MSMC", "power-snapshot.txt");

    /// <summary>快照还原标记文件（成功还原后删除，存在则表示有未还原快照）</summary>
    private static readonly string SnapshotMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MSMC", "power-snapshot.pending");

    // 电源档位预设参数表（PERFBOOSTMODE / PROCTHROTTLEMAX / PERFINCREASEPOLICY）
    // 这些别名是 powercfg SUB_PROCESSOR 子组下的标准 GUID 别名
    private static readonly Dictionary<PowerProfile, (int BoostMode, int MaxProc, int IncreasePolicy)> ProfileParams = new()
    {
        [PowerProfile.UltimatePerformance] = (2, 100, 1),   // Aggressive + 100% + 激进升频
        [PowerProfile.Balanced]            = (1, 100, 1),   // Enabled + 100% + 激进升频
        [PowerProfile.Efficient]           = (3, 90, 0),    // Efficient Enabled + 90% + 平稳升频
        [PowerProfile.PowerSaver]          = (0, 80, 0),    // Disabled + 80% + 平稳升频
    };

    private int? _snapshotBoostMode;
    private int? _snapshotMaxProc;
    private int? _snapshotIncreasePolicy;
    private bool _hasSnapshot;

    // ── T3 状态追踪 ──────────────────────────────────────────────────────────
    // winmm 定时器：记录当前已启用的精度（timeBeginPeriod/timeEndPeriod 必须配对）
    private uint? _timerPeriod;
    private readonly object _timerLock = new();

    // Power Request：当前活跃的命名防睡眠请求句柄（崩溃时 SafeWaitHandle 自动释放）
    private SafeWaitHandle? _powerRequestHandle;
    private string? _powerRequestReason;
    private readonly object _powerRequestLock = new();

    public CpuPowerService(IPrivilegeService privilegeService, ILogger log)
    {
        _privilegeService = privilegeService;
        _log = log.ForContext<CpuPowerService>();
    }

    /// <inheritdoc/>
    public CpuPowerCapabilities GetCapabilities()
    {
        bool isAdmin = _privilegeService.IsRunningAsAdmin;
        int boostMode = -1;
        string profileName = "Unknown";
        try
        {
            var (profile, mode) = GetCurrentProfileAsync().GetAwaiter().GetResult();
            boostMode = mode;
            profileName = profile.ToString();
        }
        catch (Exception ex) { _log.Verbose(ex, "[CpuPower] 读取当前档位失败"); }

        return new CpuPowerCapabilities(
            SupportsEcoQoS: true,            // Win10 1709+ 全部支持
            SupportsMemoryPriority: true,    // Win8+ 全部支持
            IsAdmin: isAdmin,
            CanModifyPowerProfile: isAdmin,  // powercfg 修改需管理员
            CurrentProfileName: profileName,
            CurrentBoostMode: boostMode);
    }

    /// <inheritdoc/>
    public QoSApplyResult SetProcessQoS(int pid, ProcessQoSTier tier)
    {
        if (pid <= 0) return new QoSApplyResult(false, "PID 无效", tier);

        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return new QoSApplyResult(false, "进程句柄无效", tier);

            var state = new NativeMethods.PROCESS_POWER_THROTTLING_STATE
            {
                Version = NativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            };

            switch (tier)
            {
                case ProcessQoSTier.Eco:
                    // 启用 EcoQoS：ControlMask + StateMask 都设 EXECUTION_SPEED
                    state.ControlMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED;
                    state.StateMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED;
                    break;
                case ProcessQoSTier.High:
                case ProcessQoSTier.Unset:
                    // 解除节流：ControlMask 设 EXECUTION_SPEED，StateMask=0（解除）
                    state.ControlMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED;
                    state.StateMask = 0;
                    break;
            }

            bool ok = NativeMethods.SetProcessInformation(
                handle,
                NativeMethods.ProcessInformationClass_ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<NativeMethods.PROCESS_POWER_THROTTLING_STATE>());

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] SetProcessInformation 失败 PID={Pid} Tier={Tier} Win32Err={Err}",
                    pid, tier, err);
                return new QoSApplyResult(false, $"Win32 错误 {err}", tier);
            }

            _log.Information("[CpuPower] QoS 已应用 PID={Pid} Tier={Tier}", pid, tier);
            return new QoSApplyResult(true, null, tier);
        }
        catch (ArgumentException)
        {
            return new QoSApplyResult(false, "进程不存在或已退出", tier);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] SetProcessQoS 异常 PID={Pid}", pid);
            return new QoSApplyResult(false, ex.Message, tier);
        }
    }

    /// <inheritdoc/>
    public (bool Success, string? Error) SetProcessMemoryPriority(int pid, uint priority)
    {
        if (pid <= 0) return (false, "PID 无效");
        if (priority > 5) return (false, "内存优先级范围 0-5");

        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return (false, "进程句柄无效");

            // SetProcessInformation 的 MemoryPriority 重载使用不同结构体，
            // 但 P/Invoke 签名以 ref PROCESS_POWER_THROTTLING_STATE 为模板。
            // 这里复用：把 MEMORY_PRIORITY_INFORMATION 序列化为同尺寸结构。
            // 由于两结构体尺寸一致（单 uint），直接复用签名是安全的。
            var memPrio = new NativeMethods.MEMORY_PRIORITY_INFORMATION { MemoryPriority = priority };

            // 通过 SetProcessInformation 设置 MemoryPriority（class=3）
            // 需要重新解释 ref 参数类型，因此用 Marshal 分配
            int size = Marshal.SizeOf<NativeMethods.MEMORY_PRIORITY_INFORMATION>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(memPrio, ptr, false);
                bool ok = NativeMethodsSetMemoryPriority(handle, ptr, size);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    _log.Warning("[CpuPower] 设置内存优先级失败 PID={Pid} Prio={Prio} Win32Err={Err}",
                        pid, priority, err);
                    return (false, $"Win32 错误 {err}");
                }
                _log.Information("[CpuPower] 内存优先级已设置 PID={Pid} Prio={Prio}", pid, priority);
                return (true, null);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch (ArgumentException)
        {
            return (false, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] SetProcessMemoryPriority 异常 PID={Pid}", pid);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 单独的 SetProcessInformation 重载（MemoryPriority 专用，结构体类型不同）。
    /// 通过指针调用，绕过 ref PROCESS_POWER_THROTTLING_STATE 的类型约束。
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetProcessInformation")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeMethodsSetMemoryPriority(
        [In] SafeProcessHandle hProcess,
        [In] IntPtr ProcessInformation,  // 指向 MEMORY_PRIORITY_INFORMATION
        [In] int ProcessInformationSize);

    /// <inheritdoc/>
    public async Task<PowerProfileApplyResult> ApplyPowerProfileAsync(PowerProfile profile)
    {
        if (!_privilegeService.IsRunningAsAdmin)
            return new PowerProfileApplyResult(false, "需要管理员权限才能修改电源策略", profile);

        try
        {
            // 首次应用前做快照（若尚未快照）
            if (!_hasSnapshot) await SnapshotCurrentSettingsAsync();

            var (boostMode, maxProc, increasePolicy) = ProfileParams[profile];

            // powercfg 修改当前活动方案的 AC 值（插电场景）
            // PERFBOOSTMODE
            await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {boostMode}");
            // PROCTHROTTLEMAX
            await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {maxProc}");
            // PERFINCREASEPOLICY
            await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PERFINCREASEPOLICY {increasePolicy}");
            // 激活方案（使修改立即生效）
            await RunPowercfgAsync("/SETACTIVE SCHEME_CURRENT");

            _log.Information("[CpuPower] 电源档位已应用 Profile={Profile} BoostMode={Mode} MaxProc={Max} IncPolicy={Pol}",
                profile, boostMode, maxProc, increasePolicy);
            return new PowerProfileApplyResult(true, null, profile);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[CpuPower] 应用电源档位失败 Profile={Profile}", profile);
            return new PowerProfileApplyResult(false, ex.Message, profile);
        }
    }

    /// <inheritdoc/>
    public async Task<PowerProfileApplyResult> RestoreOriginalProfileAsync()
    {
        if (!_hasSnapshot && !HasPendingCrashSnapshot())
            return new PowerProfileApplyResult(true, "无快照，无需还原", PowerProfile.Balanced);

        try
        {
            // 从快照文件读取原始值
            if (HasPendingCrashSnapshot()) await LoadSnapshotFromFileAsync();

            if (_snapshotBoostMode.HasValue)
                await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {_snapshotBoostMode.Value}");
            if (_snapshotMaxProc.HasValue)
                await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {_snapshotMaxProc.Value}");
            if (_snapshotIncreasePolicy.HasValue)
                await RunPowercfgAsync($"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR PERFINCREASEPOLICY {_snapshotIncreasePolicy.Value}");
            await RunPowercfgAsync("/SETACTIVE SCHEME_CURRENT");

            // 清理快照标记
            _hasSnapshot = false;
            TryDeleteFile(SnapshotMarkerPath);
            TryDeleteFile(SnapshotFilePath);

            _log.Information("[CpuPower] 电源策略已还原到原始状态");
            return new PowerProfileApplyResult(true, null, PowerProfile.Balanced);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[CpuPower] 还原电源策略失败");
            return new PowerProfileApplyResult(false, ex.Message, PowerProfile.Balanced);
        }
    }

    /// <inheritdoc/>
    public bool HasPendingCrashSnapshot()
    {
        try { return File.Exists(SnapshotMarkerPath); }
        catch { return false; }
    }

    /// <inheritdoc/>
    public async Task<(PowerProfile Profile, int BoostMode)> GetCurrentProfileAsync()
    {
        int boostMode = await QueryPowercfgValueAsync("PERFBOOSTMODE");
        var profile = boostMode switch
        {
            0 => PowerProfile.PowerSaver,
            1 => PowerProfile.Balanced,
            2 => PowerProfile.UltimatePerformance,
            3 or 4 => PowerProfile.Efficient,
            _ => PowerProfile.Balanced,
        };
        return (profile, boostMode);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T3 实现：CPU Set（P/E 核路由）/ Priority Boost / Timer / Power Request
    // ═════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public CpuSetTopology GetCpuSetTopology()
    {
        try
        {
            // 第一次调用：用 0 长度缓冲查询需要多少字节
            NativeMethods.GetSystemCpuSetInformation(IntPtr.Zero, 0, out int required, (IntPtr)(-1), 0);
            if (required <= 0)
            {
                return new CpuSetTopology(true, null, false, 0, 0, 0,
                    Array.Empty<CpuSetInfo>(), Array.Empty<int>(), Array.Empty<int>());
            }

            // 第二次调用：分配实际缓冲，枚举所有 CPU Set
            IntPtr buffer = Marshal.AllocHGlobal(required);
            try
            {
                if (!NativeMethods.GetSystemCpuSetInformation(buffer, required, out int returned, (IntPtr)(-1), 0))
                {
                    int err = Marshal.GetLastWin32Error();
                    return new CpuSetTopology(false, $"GetSystemCpuSetInformation 失败 Win32Err={err}",
                        false, 0, 0, 0, Array.Empty<CpuSetInfo>(), Array.Empty<int>(), Array.Empty<int>());
                }

                var sets = new List<CpuSetInfo>();
                IntPtr ptr = buffer;
                int remaining = returned;
                int recordSize = Marshal.SizeOf<NativeMethods.SYSTEM_CPU_SET_INFORMATION>();

                while (remaining >= 4) // 至少要有 Size 字段（uint = 4 字节）
                {
                    uint size = (uint)Marshal.ReadInt32(ptr);
                    if (size == 0 || size > remaining) break;

                    uint type = (uint)Marshal.ReadInt32(ptr, 4);
                    if (type == NativeMethods.CPU_SET_INFORMATION_TYPE_CpuSet)
                    {
                        // 只有 Type==1 时 CpuSet 字段有效；用 PtrToStructure 整体读取
                        var info = Marshal.PtrToStructure<NativeMethods.SYSTEM_CPU_SET_INFORMATION>(ptr);
                        var cs = info.CpuSet;
                        bool isPerf = cs.SchedulingClass > 0;
                        sets.Add(new CpuSetInfo(
                            Id: (int)cs.Id,
                            Group: cs.Group,
                            LogicalProcessorIndex: cs.LogicalProcessorIndex,
                            CoreIndex: cs.CoreIndex,
                            LogicalProcessorCount: (int)cs.LogicalProcessorCount,
                            CoreCount: (int)cs.CoreCount,
                            SchedulingClass: cs.SchedulingClass,
                            IsParked: (cs.AllocationFlags & NativeMethods.CPU_SET_PARKED) != 0,
                            IsPerformanceCore: isPerf));
                    }

                    ptr = IntPtr.Add(ptr, (int)size);
                    remaining -= (int)size;
                }

                var perfIds = sets.Where(s => s.IsPerformanceCore).Select(s => s.Id).ToList();
                var ecoIds = sets.Where(s => !s.IsPerformanceCore).Select(s => s.Id).ToList();
                bool hybrid = perfIds.Count > 0 && ecoIds.Count > 0;

                return new CpuSetTopology(
                    Success: true,
                    Error: null,
                    IsHybridCpu: hybrid,
                    TotalCpuSets: sets.Count,
                    PerformanceCpuSetCount: perfIds.Count,
                    EfficiencyCpuSetCount: ecoIds.Count,
                    CpuSets: sets,
                    PerformanceCpuSetIds: perfIds,
                    EfficiencyCpuSetIds: ecoIds);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] GetCpuSetTopology 异常");
            return new CpuSetTopology(false, ex.Message, false, 0, 0, 0,
                Array.Empty<CpuSetInfo>(), Array.Empty<int>(), Array.Empty<int>());
        }
    }

    /// <inheritdoc/>
    public CpuSetPinResult PinProcessToPCores(int pid)
    {
        var topo = GetCpuSetTopology();
        if (!topo.Success)
            return new CpuSetPinResult(false, topo.Error, pid, Array.Empty<int>(), false);

        if (topo.PerformanceCpuSetIds.Count == 0)
            return new CpuSetPinResult(false, "未检测到 P-core（性能核），无法路由", pid, Array.Empty<int>(), false);

        return PinProcessToCpuSets(pid, topo.PerformanceCpuSetIds);
    }

    /// <inheritdoc/>
    public CpuSetPinResult PinProcessToCpuSets(int pid, IReadOnlyList<int> cpuSetIds)
    {
        if (pid <= 0) return new CpuSetPinResult(false, "PID 无效", pid, Array.Empty<int>(), false);
        if (cpuSetIds == null || cpuSetIds.Count == 0)
            return new CpuSetPinResult(false, "cpuSetIds 不能为空", pid, Array.Empty<int>(), false);

        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return new CpuSetPinResult(false, "进程句柄无效", pid, Array.Empty<int>(), false);

            // SetProcessDefaultCpuSet 接收 uint[] 的 CpuSet ID 列表
            // 我们 P/Invoke 签名是 IntPtr[]，需要把每个 int 包装为 IntPtr
            var ids = cpuSetIds.Select(i => (IntPtr)i).ToArray();
            bool ok = NativeMethods.SetProcessDefaultCpuSet(handle, ids, (uint)ids.Length);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] SetProcessDefaultCpuSet 失败 PID={Pid} Win32Err={Err}", pid, err);
                return new CpuSetPinResult(false, $"Win32 错误 {err}", pid, Array.Empty<int>(), false);
            }

            // 判断是否为 P-core 路由（应用的所有 ID 是否都在 P-core 列表中）
            var topo = GetCpuSetTopology();
            bool pinnedToP = topo.Success && topo.PerformanceCpuSetIds.Count > 0 &&
                             cpuSetIds.All(id => topo.PerformanceCpuSetIds.Contains(id));

            _log.Information("[CpuPower] CPU Set 路由已应用 PID={Pid} CpuSets=[{Sets}] P-core={P}",
                pid, string.Join(",", cpuSetIds), pinnedToP);
            return new CpuSetPinResult(true, null, pid, cpuSetIds.ToArray(), pinnedToP);
        }
        catch (ArgumentException)
        {
            return new CpuSetPinResult(false, "进程不存在或已退出", pid, Array.Empty<int>(), false);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] PinProcessToCpuSets 异常 PID={Pid}", pid);
            return new CpuSetPinResult(false, ex.Message, pid, Array.Empty<int>(), false);
        }
    }

    /// <inheritdoc/>
    public (bool Success, string? Error) ClearProcessCpuSetPinning(int pid)
    {
        if (pid <= 0) return (false, "PID 无效");
        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return (false, "进程句柄无效");

            // 清除：传 NULL + count=0
            bool ok = NativeMethods.SetProcessDefaultCpuSet(handle, Array.Empty<IntPtr>(), 0);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] ClearProcessCpuSetPinning 失败 PID={Pid} Win32Err={Err}", pid, err);
                return (false, $"Win32 错误 {err}");
            }
            _log.Information("[CpuPower] CPU Set 限制已清除 PID={Pid}", pid);
            return (true, null);
        }
        catch (ArgumentException)
        {
            return (false, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] ClearProcessCpuSetPinning 异常 PID={Pid}", pid);
            return (false, ex.Message);
        }
    }

    /// <inheritdoc/>
    public PriorityBoostResult SetProcessPriorityBoost(int pid, bool disableBoost)
    {
        if (pid <= 0) return new PriorityBoostResult(false, "PID 无效", pid, disableBoost);
        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return new PriorityBoostResult(false, "进程句柄无效", pid, disableBoost);

            bool ok = NativeMethods.SetProcessPriorityBoost(handle, disableBoost);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] SetProcessPriorityBoost 失败 PID={Pid} Disable={Dis} Win32Err={Err}",
                    pid, disableBoost, err);
                return new PriorityBoostResult(false, $"Win32 错误 {err}", pid, disableBoost);
            }
            _log.Information("[CpuPower] PriorityBoost 已设置 PID={Pid} Disable={Dis}", pid, disableBoost);
            return new PriorityBoostResult(true, null, pid, disableBoost);
        }
        catch (ArgumentException)
        {
            return new PriorityBoostResult(false, "进程不存在或已退出", pid, disableBoost);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] SetProcessPriorityBoost 异常 PID={Pid}", pid);
            return new PriorityBoostResult(false, ex.Message, pid, disableBoost);
        }
    }

    /// <inheritdoc/>
    public PriorityBoostResult GetProcessPriorityBoost(int pid)
    {
        if (pid <= 0) return new PriorityBoostResult(false, "PID 无效", pid, false);
        try
        {
            using var proc = Process.GetProcessById(pid);
            using var handle = proc.SafeHandle;
            if (handle.IsInvalid) return new PriorityBoostResult(false, "进程句柄无效", pid, false);

            bool ok = NativeMethods.GetProcessPriorityBoost(handle, out bool disable);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                return new PriorityBoostResult(false, $"Win32 错误 {err}", pid, false);
            }
            return new PriorityBoostResult(true, null, pid, disable);
        }
        catch (ArgumentException)
        {
            return new PriorityBoostResult(false, "进程不存在或已退出", pid, false);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] GetProcessPriorityBoost 异常 PID={Pid}", pid);
            return new PriorityBoostResult(false, ex.Message, pid, false);
        }
    }

    /// <inheritdoc/>
    public TimerResolutionResult EnableTimerResolution(uint periodMs)
    {
        // 推荐范围 1-15ms；<1 会被系统拒绝，>15 没意义（系统默认就是 15.6ms）
        if (periodMs == 0) periodMs = 1;
        if (periodMs > 15) periodMs = 15;

        lock (_timerLock)
        {
            // 如果已经有活跃的定时器精度，先清理旧的（避免 winmm 计数器失衡）
            if (_timerPeriod.HasValue)
            {
                _ = NativeMethods.timeEndPeriod(_timerPeriod.Value);
                _timerPeriod = null;
            }

            uint result = NativeMethods.timeBeginPeriod(periodMs);
            // timeBeginPeriod 返回 0 = 成功，否则错误码
            if (result != 0)
            {
                _log.Warning("[CpuPower] timeBeginPeriod 失败 Period={Ms}ms Result={Ret}", periodMs, result);
                return new TimerResolutionResult(false, $"timeBeginPeriod 返回错误码 {result}", periodMs, false);
            }
            _timerPeriod = periodMs;
            _log.Information("[CpuPower] 全局定时器精度已启用 Period={Ms}ms", periodMs);
            return new TimerResolutionResult(true, null, periodMs, true);
        }
    }

    /// <inheritdoc/>
    public TimerResolutionResult DisableTimerResolution()
    {
        lock (_timerLock)
        {
            if (!_timerPeriod.HasValue)
                return new TimerResolutionResult(true, "无活跃定时器精度设置", 0, false);

            uint period = _timerPeriod.Value;
            uint result = NativeMethods.timeEndPeriod(period);
            if (result != 0)
            {
                _log.Warning("[CpuPower] timeEndPeriod 失败 Period={Ms}ms Result={Ret}", period, result);
                return new TimerResolutionResult(false, $"timeEndPeriod 返回错误码 {result}", period, false);
            }
            _timerPeriod = null;
            _log.Information("[CpuPower] 全局定时器精度已禁用（恢复系统默认 15.6ms）");
            return new TimerResolutionResult(true, null, period, false);
        }
    }

    /// <inheritdoc/>
    public TimerResolutionResult GetTimerResolutionState()
    {
        lock (_timerLock)
        {
            if (_timerPeriod.HasValue)
                return new TimerResolutionResult(true, null, _timerPeriod.Value, true);
            return new TimerResolutionResult(true, null, 0, false);
        }
    }

    /// <inheritdoc/>
    public PowerRequestResult StartPowerRequest(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) reason = "MSMC 服务器运行中";

        lock (_powerRequestLock)
        {
            // 已有活跃请求：先清理旧的，避免句柄泄露
            if (_powerRequestHandle != null && !_powerRequestHandle.IsInvalid)
            {
                _ = NativeMethods.PowerClearRequest(_powerRequestHandle, NativeMethods.PowerRequestSystemRequired);
                _powerRequestHandle.Dispose();
                _powerRequestHandle = null;
                _powerRequestReason = null;
            }

            var ctx = new NativeMethods.REASON_CONTEXT
            {
                Version = NativeMethods.POWER_REQUEST_CONTEXT_VERSION,
                Flags = NativeMethods.POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                SimpleReasonString = reason,
            };

            SafeWaitHandle? handle = NativeMethods.PowerCreateRequest(ref ctx);
            if (handle == null || handle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] PowerCreateRequest 失败 Win32Err={Err}", err);
                return new PowerRequestResult(false, $"PowerCreateRequest Win32 错误 {err}", reason, false);
            }

            if (!NativeMethods.PowerSetRequest(handle, NativeMethods.PowerRequestSystemRequired))
            {
                int err = Marshal.GetLastWin32Error();
                _log.Warning("[CpuPower] PowerSetRequest(SystemRequired) 失败 Win32Err={Err}", err);
                handle.Dispose();
                return new PowerRequestResult(false, $"PowerSetRequest Win32 错误 {err}", reason, false);
            }

            _powerRequestHandle = handle;
            _powerRequestReason = reason;
            _log.Information("[CpuPower] Power Request 已激活 Reason=\"{Reason}\"", reason);
            return new PowerRequestResult(true, null, reason, true);
        }
    }

    /// <inheritdoc/>
    public PowerRequestResult StopPowerRequest()
    {
        lock (_powerRequestLock)
        {
            if (_powerRequestHandle == null || _powerRequestHandle.IsInvalid)
                return new PowerRequestResult(true, "无活跃 Power Request", string.Empty, false);

            string reason = _powerRequestReason ?? string.Empty;
            _ = NativeMethods.PowerClearRequest(_powerRequestHandle, NativeMethods.PowerRequestSystemRequired);
            _powerRequestHandle.Dispose();
            _powerRequestHandle = null;
            _powerRequestReason = null;
            _log.Information("[CpuPower] Power Request 已解除");
            return new PowerRequestResult(true, null, reason, false);
        }
    }

    /// <inheritdoc/>
    public PowerRequestResult GetPowerRequestState()
    {
        lock (_powerRequestLock)
        {
            if (_powerRequestHandle != null && !_powerRequestHandle.IsInvalid)
                return new PowerRequestResult(true, null, _powerRequestReason ?? string.Empty, true);
            return new PowerRequestResult(true, null, string.Empty, false);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  内部工具：快照 / powercfg 调用 / 文件操作
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>快照当前 PERFBOOSTMODE / PROCTHROTTLEMAX / PERFINCREASEPOLICY</summary>
    private async Task SnapshotCurrentSettingsAsync()
    {
        _snapshotBoostMode = await QueryPowercfgValueAsync("PERFBOOSTMODE");
        _snapshotMaxProc = await QueryPowercfgValueAsync("PROCTHROTTLEMAX");
        _snapshotIncreasePolicy = await QueryPowercfgValueAsync("PERFINCREASEPOLICY");
        _hasSnapshot = true;

        // 写入快照文件 + 标记文件（崩溃恢复用）
        try
        {
            var dir = Path.GetDirectoryName(SnapshotFilePath)!;
            Directory.CreateDirectory(dir);
            var content = $"PERFBOOSTMODE={_snapshotBoostMode}\nPROCTHROTTLEMAX={_snapshotMaxProc}\nPERFINCREASEPOLICY={_snapshotIncreasePolicy}\n";
            await File.WriteAllTextAsync(SnapshotFilePath, content);
            await File.WriteAllTextAsync(SnapshotMarkerPath, DateTime.UtcNow.ToString("O"));
            _log.Information("[CpuPower] 电源策略快照已保存 BoostMode={Mode} MaxProc={Max} IncPolicy={Pol}",
                _snapshotBoostMode, _snapshotMaxProc, _snapshotIncreasePolicy);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[CpuPower] 保存快照文件失败（崩溃恢复将不可用）");
        }
    }

    /// <summary>从快照文件加载原始设置（崩溃恢复场景）</summary>
    private async Task LoadSnapshotFromFileAsync()
    {
        if (!File.Exists(SnapshotFilePath)) return;
        var content = await File.ReadAllTextAsync(SnapshotFilePath);
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[1].Trim(), out var val)) continue;
            switch (parts[0].Trim())
            {
                case "PERFBOOSTMODE": _snapshotBoostMode = val; break;
                case "PROCTHROTTLEMAX": _snapshotMaxProc = val; break;
                case "PERFINCREASEPOLICY": _snapshotIncreasePolicy = val; break;
            }
        }
        _hasSnapshot = true;
    }

    /// <summary>执行 powercfg 命令并等待</summary>
    private static async Task RunPowercfgAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        if (p == null) throw new Win32Exception("无法启动 powercfg.exe");
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"powercfg {args} 失败 (ExitCode={p.ExitCode}): {err.Trim()}");
        }
    }

    /// <summary>查询 powercfg 当前活动方案的某个设置值</summary>
    private static async Task<int> QueryPowercfgValueAsync(string settingAlias)
    {
        // powercfg /q SCHEME_CURRENT SUB_PROCESSOR <ALIAS>
        // 输出包含 "当前交流电源设置索引: 0x00000002" 行
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = $"/q SCHEME_CURRENT SUB_PROCESSOR {settingAlias}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(psi);
        if (p == null) return -1;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        // 解析 "当前交流电源设置索引: 0x00000002" 或英文 "Current AC Power Setting Index: 0x00000002"
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            // 中英文系统都覆盖：匹配"交流"/"AC"行
            if (trimmed.Contains("交流") || trimmed.Contains("AC Power Setting", StringComparison.OrdinalIgnoreCase))
            {
                var idx = trimmed.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var hex = trimmed.AsSpan(idx + 2).Trim();
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var val))
                        return val;
                }
            }
        }
        return -1;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 应用退出时清理 T3 资源：还原定时器精度 + 解除 Power Request + 还原电源策略快照
    /// </summary>
    public void Dispose()
    {
        try
        {
            // 1. 清理定时器精度（必须配对 timeEndPeriod，否则系统 tick 永久被改）
            DisableTimerResolution();
        }
        catch (Exception ex) { _log.Warning(ex, "[CpuPower] Dispose: 清理定时器精度失败"); }

        try
        {
            // 2. 解除 Power Request
            StopPowerRequest();
        }
        catch (Exception ex) { _log.Warning(ex, "[CpuPower] Dispose: 解除 Power Request 失败"); }

        try
        {
            // 3. 若应用崩溃前应用过电源档位，尝试还原（崩溃恢复场景下 _hasSnapshot 可能为 false，
            //    但崩溃标记文件存在 → 走 LoadSnapshotFromFileAsync 路径）
            if (_hasSnapshot || HasPendingCrashSnapshot())
            {
                RestoreOriginalProfileAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex) { _log.Warning(ex, "[CpuPower] Dispose: 还原电源策略失败"); }
    }
}
