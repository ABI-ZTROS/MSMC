// ═══════════════════════════════════════════════════════════════════════════════
// 🧩 NativeServiceCollectionExtensions — Win32 原生服务的统一注册入口
// ═══════════════════════════════════════════════════════════════════════════════
// 设计意图：
//   App.xaml.cs 里的注册列表已经 40+ 项，不要再把 Win32 相关的注册散落在里面。
//   这里统一封装所有 NativeServices 注册，并自动处理「非 Windows 平台降级」。
//
// 用法：
//   services.AddNativeServices();  // 一行搞定所有 Win32 服务注册
//
// 非 Windows 平台策略（跨平台兜底）：
//   每个服务都带 [SupportedOSPlatform("windows")]，Linux/macOS 下调用会 PlatformNotSupported。
//   AddNativeServices 提供了「平台感知注册」：
//     - Windows: 注册真实实现（IProcessSupervisorService → ProcessSupervisorService）
//     - 非 Windows: 注册 Null-Object 模式的降级实现，调用不抛但返回默认值
//     → 保证前端/上层逻辑在任何 OS 下都不会因为注入了不存在的服务而崩溃
// ═══════════════════════════════════════════════════════════════════════════════

using System.Runtime.Versioning;
using io.NET.ZTR_OS.Features.Shared.Native.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using Microsoft.Extensions.DependencyInjection;

namespace io.NET.ZTR_OS.Features.Shared.Native;

public static class NativeServiceCollectionExtensions
{
    /// <summary>
    /// 注册所有基于 Win32 的原生能力服务（平台感知：Windows 注册真实现，非 Windows 注册降级桩）。
    /// </summary>
    public static IServiceCollection AddNativeServices(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            // 真实实现
            services.AddSingleton<IProcessSupervisorService, ProcessSupervisorService>();
            services.AddSingleton<IWindowEffectsService, WindowEffectsService>();
        }
        else
        {
            // 跨平台降级桩（不抛 PlatformNotSupportedException，返回空/默认值）
            services.AddSingleton<IProcessSupervisorService, NullProcessSupervisorService>();
            services.AddSingleton<IWindowEffectsService, NullWindowEffectsService>();
        }
        return services;
    }
}

/// <summary>
/// 非 Windows 平台的 IProcessSupervisorService 降级实现（Null Object Pattern）
/// </summary>
internal sealed class NullProcessSupervisorService : IProcessSupervisorService
{
    public Task<SupervisedProcessHandle> LaunchSupervisedAsync(
        string executablePath, string arguments, string workingDirectory,
        ProcessSupervisorOptions options, CancellationToken ct = default)
    {
        // 非 Windows 平台：退回标准 Process.Start，不提供崩溃重启/亲和性等高级能力
        var process = new ProcessStartInfo(executablePath, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        var p = Process.Start(process)!;
        return Task.FromResult(new SupervisedProcessHandle(
            null!, p, new CancellationTokenSource(), Serilog.Log.ForContext<NullProcessSupervisorService>()));
    }

    public SupervisedProcessHandle AttachExisting(int pid, ProcessSupervisorOptions options) =>
        throw new PlatformNotSupportedException("AttachExisting 仅在 Windows 平台可用");

    public bool SetProcessAffinity(int pid, IEnumerable<int> coreNumbers) => false;
    public bool SetProcessPriorityClass(int pid, ProcessPriorityClass priority) => false;
    public (long PrivateBytes, long WorkingSet, long PagefileUsage) QueryProcessMemory(int pid) => default;
    public bool PreventSystemSleep(bool enabled, bool alsoKeepDisplayOn = false) => false;
    public void FlashMainWindowTaskbar(IntPtr hWnd, uint count = 5, uint intervalMs = 250) { /* no-op */ }
}

/// <summary>
/// 非 Windows 平台的 IWindowEffectsService 降级实现（Null Object Pattern）
/// </summary>
internal sealed class NullWindowEffectsService : IWindowEffectsService
{
    public bool IsCompositionEnabled => false;
    public bool SupportsMica => false;
    public bool SupportsDarkTitleBar => false;
    public bool ApplySystemBackdrop(IntPtr hWnd, SystemBackdropType type) => false;
    public bool ClearSystemBackdrop(IntPtr hWnd) => false;
    public bool ApplyDarkTitleBar(IntPtr hWnd, bool? darkMode) => false;
    public bool ApplyCornerPreference(IntPtr hWnd, WindowCornerPreference corner) => false;
    public bool SetDisplayAffinity(IntPtr hWnd, DisplayAffinity affinity) => false;
    public void ApplyColorOSVisualPack(IntPtr hWnd, bool darkTitleBar = true) { /* no-op */ }
}
