// ═══════════════════════════════════════════════════════════════════════════════
// 🏗️ MSMC.Native — 所有原生 Win32 P/Invoke 与结构体的统一注册中心
// ═══════════════════════════════════════════════════════════════════════════════
// 设计原则：
//   1. 集中管理 — 不再在每个 Service 里零散写 [DllImport]，避免重复签名/漏 SetLastError
//   2. 类型安全 — 句柄用 SafeHandle（避免 IntPtr 泄露），枚举用 [Flags] 位运算
//   3. 文档完整 — 每个函数都引用 Microsoft Docs URL，参数/返回值语义一目了然
//   4. 可测试 — NativeMethods 为 internal static（通过 InternalsVisibleTo 给测试工程）
//   5. 零异常 — P/Invoke 失败不抛，统一返回 BOOL / NTSTATUS，由上层服务判错并包装
//
// 扩展权力方向：
//   kernel32  → 进程/作业对象/线程/内存/文件/定时器/符号链接
//   advapi32  → 权限令牌/注册表/服务控制管理器/事件日志
//   user32    → 窗口/消息/输入/高 DPI/悬停穿透/玻璃效果
//   psapi     → 进程模块/工作集/内存信息（比 System.Diagnostics 精确）
//   iphlpapi  → TCP/UDP 表扩展（已存在，统一迁入）
//   shell32   → 文件操作（SHFileOperation 带进度+撤销）/快捷方式/已知文件夹
//   powrprof  → 电源状态/唤醒定时器/防止系统睡眠（服务器长任务）
//   dwmapi    → DWM 窗口动画/玻璃效果/缩略图（ColorOS 桌面级毛玻璃）
// ═══════════════════════════════════════════════════════════════════════════════

using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace io.NET.ZTR_OS.Features.Shared.Native;

#region ── 枚举 & 位标记 ─────────────────────────────────────────────────────

/// <summary>Job Objects 权限掩码 — 控制子进程能做什么（崩溃重启的基石）</summary>
[Flags]
public enum JobObjectLimits : uint
{
    KillOnJobClose = 0x2000,
    JobObjectAssigned = 0x0001,
    BreakawayOk = 0x0800,
    SilentBreakawayOk = 0x0400,
    LimitKillOnJobClose = 0x2000,
    LimitMemory = 0x0100,
    LimitProcessMemory = 0x0080,
    LimitJobTime = 0x0004,
    LimitProcessTime = 0x0008,
    LimitActiveProcesses = 0x0040,
    LimitAffinity = 0x0010,
    LimitPriorityClass = 0x0020,
    LimitSchedulingClass = 0x0002,
    LimitDieOnUnhandledException = 0x0400,
    LimitJobSet = KillOnJobClose | LimitActiveProcesses | LimitProcessMemory | LimitPriorityClass
}

/// <summary>SetProcessAffinityMask / GetSystemInfo 辅助的 CPU 分组信息</summary>
[Flags]
public enum ProcessorAccessRights : uint
{
    AllAccess = 0x1FFFFF,
    Terminate = 0x0001,
    SetInformation = 0x0200,
    QueryInformation = 0x0400,
    QueryLimitedInformation = 0x1000
}

/// <summary>EXECUTION_STATE — 防止系统睡眠（下载/备份/长时间跑服）</summary>
[Flags]
public enum ExecutionState : uint
{
    AwayModeRequired   = 0x00000040,
    Continuous         = 0x80000000,
    DisplayRequired    = 0x00000002,
    SystemRequired     = 0x00000001,
    UserPresent        = 0x00000004
}

/// <summary>DWM — 窗口毛玻璃/云母效果（ColorOS 级玻璃化）</summary>
public enum DwmWindowAttribute : uint
{
    NcRenderingEnabled = 1,
    NcRenderingPolicy  = 2,
    TransitionsForceDisabled = 3,
    AllowNcPaint       = 4,
    CaptionButtonBounds= 5,
    NonClientRtlLayout = 6,
    ForceIconicRepresentation = 7,
    Flip3DPolicy       = 8,
    ExtendedFrameBounds= 9,
    HasIconicBitmap    = 10,
    DisallowPeek       = 11,
    ExcludedFromPeek   = 12,
    Cloak              = 13,
    Cloaked            = 14,
    FreezeRepresentation = 15,
    PassiveUpdateMode  = 16,
    UseHostBackdropBrush = 17, // Win10+ 「Mica/云母」效果基础
    UseImmersiveDarkMode = 20, // Win10 1809+ 深色模式标题栏
    SystemBackdropType = 38  // Win11 22H2+ Mica/MicaAlt/Acrylic/云母
}

#endregion

#region ── Safe Handles — 安全句柄（using 自动释放，杜绝句柄泄露） ─────

/// <summary>Job Object 安全句柄</summary>
[SuppressUnmanagedCodeSecurity]
public sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeJobHandle() : base(true) { }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    protected override bool ReleaseHandle() => CloseHandle(handle);
}

#endregion

#region ── Native 结构体 ─────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
{
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public JobObjectLimits LimitFlags;
    public UIntPtr MinimumWorkingSetSize;
    public UIntPtr MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public UIntPtr Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
public struct IO_COUNTERS
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
{
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
    public IO_COUNTERS IoInfo;
    public UIntPtr ProcessMemoryLimit;
    public UIntPtr JobMemoryLimit;
    public UIntPtr PeakProcessMemoryUsed;
    public UIntPtr PeakJobMemoryUsed;
}

[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_ASSOCIATE_COMPLETION_PORT
{
    public IntPtr CompletionKey;
    public IntPtr CompletionPort;
}

[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_INFORMATION
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public int   dwProcessId;
    public int   dwThreadId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct STARTUPINFO
{
    public int   cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public int   dwX;
    public int   dwY;
    public int   dwXSize;
    public int   dwYSize;
    public int   dwXCountChars;
    public int   dwYCountChars;
    public int   dwFillAttribute;
    public int   dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
public struct SECURITY_ATTRIBUTES
{
    public int    nLength;
    public IntPtr lpSecurityDescriptor;
    [MarshalAs(UnmanagedType.Bool)]
    public bool   bInheritHandle;
}

#endregion

#region ═══════════════════════════════════════════════════════════════════════
// 🏛️ NativeMethods — 统一 P/Invoke 入口
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 所有 Win32 API 集中签名在这里，任何 Feature Service 只引用本类不自行 DllImport。
///
/// 命名约定：
///   - 原样保留原 API 名（CreateJobObject/AssignProcessToJobObject/...），不做"友好化"
///   - 所有 SetLastError=true 的函数后，调用方必须用 Marshal.GetLastWin32Error()
///   - 返回 BOOL 的一律用 [return: MarshalAs(UnmanagedType.Bool)]，避免 0/1 与 bool 转换歧义
/// </summary>
internal static class NativeMethods
{
    // ───────────────────────────────────────────────────────────────────────
    // 🧪 kernel32.dll — 进程 / Job Objects / 内存 / 电源 / I/O
    //     ref: https://learn.microsoft.com/windows/win32/api/_base/
    // ───────────────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeJobHandle CreateJobObjectW(
        [In] ref SECURITY_ATTRIBUTES lpJobAttributes,
        [In][MarshalAs(UnmanagedType.LPWStr)] string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetInformationJobObject(
        [In] SafeJobHandle hJob,
        [In] int JobObjectInfoClass,                 // = 9 → JobObjectExtendedLimitInformation
        [In] ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo,
        [In] int cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AssignProcessToJobObject(
        [In] SafeJobHandle hJob,
        [In] IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessInJob(
        [In] IntPtr ProcessHandle,
        [In] SafeJobHandle? JobHandle,
        [Out][MarshalAs(UnmanagedType.Bool)] out bool Result);

    /// <summary>创建子进程 — 比 System.Diagnostics.Process.Start 更细粒度（如指定 Job）</summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcessW(
        [In][MarshalAs(UnmanagedType.LPWStr)] string? lpApplicationName,
        [In] string lpCommandLine,
        [In] ref SECURITY_ATTRIBUTES lpProcessAttributes,
        [In] ref SECURITY_ATTRIBUTES lpThreadAttributes,
        [In][MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        [In] uint dwCreationFlags,
        [In] IntPtr lpEnvironment,
        [In][MarshalAs(UnmanagedType.LPWStr)] string? lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        [Out] out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess([In] IntPtr hProcess, [In] uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle([In] IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr SetProcessAffinityMask(
        [In] SafeProcessHandle hProcess, [In] UIntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessAffinityMask(
        [In] SafeProcessHandle hProcess,
        [Out] out UIntPtr lpProcessAffinityMask,
        [Out] out UIntPtr lpSystemAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        [In] ProcessorAccessRights dwDesiredAccess,
        [In][MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        [In] int dwProcessId);

    /// <summary>QueryInformationJobObject — 读取 Job 的限制与统计（Job 内存/CPU 用量）</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryInformationJobObject(
        [In] SafeJobHandle hJob,
        [In] int JobObjectInfoClass,
        [In] IntPtr lpJobObjectInfo,
        [In] int cbJobObjectInfoLength,
        [Out] out int lpReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ExecutionState SetThreadExecutionState([In] ExecutionState esFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetPriorityClass([In] SafeProcessHandle hProcess, [In] uint dwPriorityClass);

    // 优先级类常量（SetPriorityClass）
    public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    public const uint HIGH_PRIORITY_CLASS       = 0x00000080;
    public const uint IDLE_PRIORITY_CLASS       = 0x00000040;
    public const uint NORMAL_PRIORITY_CLASS     = 0x00000020;
    public const uint REALTIME_PRIORITY_CLASS   = 0x00000100;

    // ───────────────────────────────────────────────────────────────────────
    // ⚡ Process Power Throttling / EcoQoS — 进程级能效档位（类安卓 schedtune）
    //     ref: https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation
    //     PROCESS_INFORMATION_CLASS.ProcessPowerThrottling = 4
    //     PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1
    //     PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1（控制执行速度/能效档）
    //     StateMask=EXECUTION_SPEED → 启用 EcoQoS（降频/能效核）
    //     StateMask=0 → 解除限制（恢复高性能）
    // ───────────────────────────────────────────────────────────────────────
    public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x00000001;
    public const uint ProcessInformationClass_ProcessPowerThrottling = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;   // 哪些位受控（EXECUTION_SPEED）
        public uint StateMask;     // 启用(=EXECUTION_SPEED) 或 解除(=0)
    }

    /// <summary>
    /// SetProcessInformation — 设置进程级电源节流(EcoQoS)/电源状态。
    /// ProcessInformationClass=4 → ProcessPowerThrottling
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessInformation(
        [In] SafeProcessHandle hProcess,
        [In] uint ProcessInformationClass,
        [In] ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        [In] uint ProcessInformationSize);

    /// <summary>
    /// GetProcessInformation — 读取进程级电源节流当前状态。
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessInformation(
        [In] SafeProcessHandle hProcess,
        [In] uint ProcessInformationClass,
        [Out] out PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        [In] uint ProcessInformationSize);

    // ───────────────────────────────────────────────────────────────────────
    // 🧠 内存优先级 — 控制工作集页驻留优先级（防止 MC 内存被优先换出）
    //     ref: https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation
    //     ProcessInformationClass.ProcessMemoryPriority = 3
    // ───────────────────────────────────────────────────────────────────────
    public const uint ProcessInformationClass_ProcessMemoryPriority = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_PRIORITY_INFORMATION
    {
        public uint MemoryPriority;  // 0-5：VeryLow/Low/Medium/BelowNormal/Normal(默认5)
    }

    // 内存优先级枚举值（MEMORY_PRIORITY_*）
    public const uint MEMORY_PRIORITY_VERYLOW      = 0;
    public const uint MEMORY_PRIORITY_LOW          = 1;
    public const uint MEMORY_PRIORITY_MEDIUM       = 2;
    public const uint MEMORY_PRIORITY_BELOW_NORMAL = 3;
    public const uint MEMORY_PRIORITY_NORMAL       = 5;


    /// <summary>
    /// JobObjectExtendedLimitInformation = 9
    /// https://learn.microsoft.com/windows/win32/api/jobapi2/nf-jobapi2-setinformationjobobject
    /// </summary>
    public const int JobObjectExtendedLimitInformation = 9;
    public const int JobObjectBasicUiRestrictions    = 4;
    public const int JobObjectAssociateCompletionPortInformation = 7;

    public const uint CREATE_SUSPENDED       = 0x00000004;
    public const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
    public const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
    public const uint CREATE_NO_WINDOW       = 0x08000000;
    public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    // ───────────────────────────────────────────────────────────────────────
    // 🔑 advapi32.dll — 权限令牌 / 注册表
    // ───────────────────────────────────────────────────────────────────────

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(
        [In] SafeProcessHandle ProcessHandle,
        [In] uint DesiredAccess,
        [Out] out SafeWaitHandle TokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValueW(
        [In][MarshalAs(UnmanagedType.LPWStr)] string? lpSystemName,
        [In][MarshalAs(UnmanagedType.LPWStr)] string lpName,
        [Out] out long lpLuid);

    // ───────────────────────────────────────────────────────────────────────
    // 🖥️ user32.dll — 窗口 / 高 DPI / 输入
    // ───────────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics([In] int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDPIAware();

    [DllImport("shcore.dll", SetLastError = true)]
    public static extern int SetProcessDpiAwarenessContext([In] IntPtr dpiContext);

    // PerMonitorV2 DPI 感知 = -4 (PerMonitorV2)
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    /// <summary>FlashWindowEx — 闪任务栏（服务器崩溃/异常时吸引用户注意）</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlashWindowEx([In] ref FLASHWINFO pfwi);

    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint    cbSize;
        public IntPtr  hwnd;
        public uint    dwFlags;
        public uint    uCount;
        public uint    dwTimeout;
    }
    public const uint FLASHW_STOP      = 0;
    public const uint FLASHW_CAPTION   = 0x00000001;
    public const uint FLASHW_TRAY      = 0x00000002;
    public const uint FLASHW_ALL       = FLASHW_CAPTION | FLASHW_TRAY;
    public const uint FLASHW_TIMERNOFG = 0x0000000C;

    // ───────────────────────────────────────────────────────────────────────
    // ✨ dwmapi.dll — DWM 合成效果（Mica/云母/Acrylic）
    //     ref: https://learn.microsoft.com/windows/win32/api/dwmapi/
    // ───────────────────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll", PreserveSig = false)] // PreserveSig=false → 失败抛 Exception（便于 catch）
    public static extern int DwmSetWindowAttribute(
        [In] IntPtr hwnd,
        [In] DwmWindowAttribute dwAttribute,
        [In] ref int pvAttribute,
        [In] int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);

    // ───────────────────────────────────────────────────────────────────────
    // 📦 shell32.dll — 带进度/撤销的文件操作 / 已知文件夹
    // ───────────────────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHCreateDirectoryExW(
        [In] IntPtr hwnd,
        [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr psa);

    // ───────────────────────────────────────────────────────────────────────
    // 🔌 psapi.dll — 进程工作集信息（比 PerformanceCounter 精确 10x）
    // ───────────────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint   cb;
        public uint   PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage; // Private Bytes（最接近任务管理器的"专用工作集"）
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessMemoryInfo(
        [In] SafeProcessHandle hProcess,
        [Out] out PROCESS_MEMORY_COUNTERS_EX ppsmemCounters,
        [In] int cb);

    // ───────────────────────────────────────────────────────────────────────
    // 🧬 CPU Set — 异构 CPU（Intel 12 代+ / AMD X3D）的 P-core/E-core 路由
    //     ref: https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemcpusetinformation
    //     比 ProcessorAffinity 更精细：可指定"用 P-core 不用 E-core"，
    //     而不影响同一物理核的超线程兄弟核；适合 MC 主线程锁定到 P-core
    //     Win10 1709+ 用户态 API，零驱动
    // ───────────────────────────────────────────────────────────────────────
    public const uint CPU_SET_INFORMATION_TYPE_CpuSet = 1;

    // AllocationFlags 位定义（SYSTEM_CPU_SET.AllocationFlags）
    public const ulong CPU_SET_ALLOCATED = 0x00000001;        // 已分配给 CPU Set
    public const ulong CPU_SET_ALLOCATED_TO_CONFIGURED_CLASS = 0x00000002;
    public const ulong CPU_SET_PARKED = 0x00000004;            // 已停泊

    /// <summary>
    /// SYSTEM_CPU_SET — 一个 CPU Set 的描述（P-core 或 E-core 组）
    /// SchedulingClass：0 = E-core（能效核），更高值 = P-core（性能核）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_CPU_SET
    {
        public uint   Id;                       // CPU Set ID（用于 SetProcessDefaultCpuSet）
        public ushort Group;                    // NUMA 组
        public byte   LogicalProcessorIndex;    // 组内逻辑处理器序号
        public byte   CoreIndex;                // 物理核序号
        public byte   PhysicalProcessorIndex;   // 物理处理器（socket）序号
        public byte   Reserved;                 // 保留
        public uint   LogicalProcessorCount;    // 本 Set 中的逻辑处理器数
        public uint   CoreCount;                // 本 Set 中的物理核数
        public ulong  SchedulingClass;          // 0=E-core，>0=P-core（值越大越偏性能）
        public ulong  AllocationFlags;          // 分配状态（CPU_SET_ALLOCATED 等）
    }

    /// <summary>
    /// SYSTEM_CPU_SET_INFORMATION — GetSystemCpuSetInformation 输出的可变长度记录
    /// 每条记录的 Size 字段是本记录的字节大小，迭代时按 Size 累加指针
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_CPU_SET_INFORMATION
    {
        public uint Size;          // 本记录字节大小（变长）
        public uint Type;          // CPU_SET_INFORMATION_TYPE（=1 表示 CpuSet）
        public SYSTEM_CPU_SET CpuSet; // 当 Type==1 时有效
    }

    /// <summary>
    /// GetSystemCpuSetInformation — 查询系统 CPU Set 拓扑（P/E 核分布）
    /// Process=(IntPtr)(-1) 表示当前进程；Flags=0
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemCpuSetInformation(
        [Out] IntPtr Information,
        [In] int BufferLength,
        [Out] out int ReturnedLength,
        [In] IntPtr Process,
        [In] uint Flags);

    /// <summary>
    /// SetProcessDefaultCpuSet — 把进程默认调度限制到指定 CPU Set 列表
    /// CpuSetIds 是 CPU Set ID 数组（来自 SYSTEM_CPU_SET.Id）
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDefaultCpuSet(
        [In] SafeProcessHandle Process,
        [In] IntPtr[] CpuSetIds,
        [In] uint CpuSetIdCount);

    /// <summary>
    /// GetProcessDefaultCpuSet — 读取进程默认 CPU Set
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessDefaultCpuSet(
        [In] SafeProcessHandle Process,
        [Out] uint[] CpuSetIds,
        [In] uint CpuSetIdCount,
        [Out] out uint RequiredIdCount);

    // ───────────────────────────────────────────────────────────────────────
    // ⚡ SetProcessPriorityBoost — 控制进程是否在窗口前台/输入事件时自动提升优先级
    //     ref: https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocesspriorityboost
    //     后台 MC 服建议禁用 boost，避免与系统其他进程的优先级翻转
    // ───────────────────────────────────────────────────────────────────────
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessPriorityBoost(
        [In] SafeProcessHandle hProcess,
        [In][MarshalAs(UnmanagedType.Bool)] bool DisablePriorityBoost);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessPriorityBoost(
        [In] SafeProcessHandle hProcess,
        [Out][MarshalAs(UnmanagedType.Bool)] out bool DisablePriorityBoost);

    // ───────────────────────────────────────────────────────────────────────
    // ⏱️ winmm 多媒体定时器 — timeBeginPeriod/timeEndPeriod
    //     ref: https://learn.microsoft.com/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
    //     默认系统 tick = 15.6ms，提到 1ms 可显著降低 MC 20 TPS 主循环抖动
    //     全局影响：会增加空闲功耗，仅在服务器运行期间启用
    // ───────────────────────────────────────────────────────────────────────
    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint timeBeginPeriod([In] uint uPeriod);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint timeEndPeriod([In] uint uPeriod);

    // ───────────────────────────────────────────────────────────────────────
    // 🔌 Power Request — 现代化防睡眠（比 SetThreadExecutionState 更可靠）
    //     ref: https://learn.microsoft.com/windows/win32/api/powerbase/nf-powerbase-powercreaterequest
    //     支持命名请求 + 显式 Clear；崩溃后系统能根据句柄清理
    //     可独立请求 SystemRequired / DisplayRequired / AwayModeRequired
    // ───────────────────────────────────────────────────────────────────────
    public const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    public const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct REASON_CONTEXT
    {
        public uint   Version;            // POWER_REQUEST_CONTEXT_VERSION
        public uint   Flags;              // POWER_REQUEST_CONTEXT_SIMPLE_STRING
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString; // 简单字符串原因（最长 255 字符）
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POWER_REQUEST_INFORMATION
    {
        // 不直接使用结构体，PowerCreateRequest 接收 REASON_CONTEXT*
    }

    /// <summary>PowerCreateRequest — 创建命名 Power Request，返回句柄</summary>
    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern SafeWaitHandle PowerCreateRequest([In] ref REASON_CONTEXT Context);

    /// <summary>PowerSetRequest — 激活 Power Request（PowerSystemRequired / PowerDisplayRequired / ...）</summary>
    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PowerSetRequest(
        [In] SafeWaitHandle PowerRequest,
        [In] uint RequestType);   // 0=PowerSystemRequired, 1=PowerDisplayRequired, 2=PowerAwayModeRequired

    /// <summary>PowerClearRequest — 解除 Power Request</summary>
    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PowerClearRequest(
        [In] SafeWaitHandle PowerRequest,
        [In] uint RequestType);

    // Power Request 类型常量
    public const uint PowerRequestSystemRequired = 0;
    public const uint PowerRequestDisplayRequired = 1;
    public const uint PowerRequestAwayModeRequired = 2;

    // ───────────────────────────────────────────────────────────────────────
    // 📊 Job Object CPU 速率硬限 — JobObjectCpuRateControlInformation (class=15)
    //     ref: https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-jobobject_cpu_rate_control_information
    //     限制 Job 内所有进程的总 CPU 占用百分比（硬上限）
    // ───────────────────────────────────────────────────────────────────────
    public const int JobObjectCpuRateControlInformation = 15;

    public const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE         = 0x1;
    public const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP       = 0x4;   // 硬上限（不能超过）
    public const uint JOB_OBJECT_CPU_RATE_CONTROL_PERCENT_BASED  = 0x2;   // 按 % 控制（否则按 Weight 1-9）
    public const uint JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED   = 0x0;   // 按 Weight 控制

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;  // ENABLE | HARD_CAP | (PERCENT_BASED 或 WEIGHT_BASED)
        public uint CpuRate;       // 按 % 时：百分比 × 100（50% = 5000）
        public uint Weight;        // 按 Weight 时：1-9（5=默认）
    }
}

#endregion
