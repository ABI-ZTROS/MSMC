// -----------------------------------------------------------------------------
// 文件名: PrivilegeService.cs
// 命名空间: io.NET.ZTR_OS.Features.Startup.Services
// 功能描述: 提供应用权限提升与管理员身份检测服务，支持 UAC 提权重启
// 依赖组件: System.Security.Principal, System.Diagnostics, System.Runtime.InteropServices
// 设计模式: 单例模式（DI容器注册）、事件通知模式
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace io.NET.ZTR_OS.Features.Startup.Services;

/// <summary>
/// 权限服务接口
/// 定义管理员权限检测与提升操作契约
/// </summary>
public interface IPrivilegeService
{
    /// <summary>
    /// 当前进程是否以管理员身份运行（每次访问实时检测，带 5 秒滑动缓存）
    /// </summary>
    bool IsRunningAsAdmin { get; }

    /// <summary>
    /// 当前操作系统是否为 Windows
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// 强制刷新管理员权限状态（跳过缓存，立即重新检测）
    /// </summary>
    /// <returns>刷新后的权限状态</returns>
    bool Refresh();

    /// <summary>
    /// 请求 UAC 权限提升
    /// 成功后当前进程将退出，以管理员权限重启新实例
    /// </summary>
    /// <returns>是否成功发起提权请求</returns>
    bool RequestElevation();

    /// <summary>
    /// 确保当前具有管理员权限
    /// 若权限不足则记录警告（不主动提权）
    /// </summary>
    /// <param name="reason">权限不足的原因说明</param>
    /// <returns>是否具有管理员权限</returns>
    bool EnsureAdminPrivileges(string? reason = null);

    /// <summary>
    /// 权限状态变更事件
    /// </summary>
    event EventHandler<bool>? PrivilegeChanged;
}

/// <summary>
/// 权限提升服务
/// 负责检测当前进程的管理员权限状态，并提供 UAC 提权重启能力
/// </summary>
public class PrivilegeService : IPrivilegeService
{
    /// <summary>
    /// 权限状态缓存（5 秒滑动窗口，避免高频 P/Invoke）
    /// </summary>
    private bool _cachedIsAdmin;
    private long _lastCheckTick;
    private const long CacheWindowMs = 5_000;

    private static readonly TimeSpan CacheTimeout = TimeSpan.FromMilliseconds(CacheWindowMs);
    private int _refreshFlag; // 0 = normal, 1 = force refresh next read

    /// <inheritdoc />
    public bool IsRunningAsAdmin
    {
        get
        {
            if (!IsWindows) return false;

            var now = Environment.TickCount64;
            if (Interlocked.Exchange(ref _refreshFlag, 0) != 0 ||
                now - _lastCheckTick > CacheTimeout.TotalMilliseconds)
            {
                _cachedIsAdmin = CheckIsAdmin();
                _lastCheckTick = now;
            }
            return _cachedIsAdmin;
        }
    }

    /// <inheritdoc />
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <inheritdoc />
    public event EventHandler<bool>? PrivilegeChanged;

    /// <summary>
    /// 触发权限变更事件
    /// </summary>
    /// <param name="isAdmin">是否为管理员权限</param>
    private void OnPrivilegeChanged(bool isAdmin)
    {
        PrivilegeChanged?.Invoke(this, isAdmin);
    }

    /// <summary>
    /// 初始化权限服务
    /// 构造时立即进行首次权限检测
    /// </summary>
    public PrivilegeService()
    {
        _cachedIsAdmin = CheckIsAdmin();
        _lastCheckTick = Environment.TickCount64;
        Log.Information("[SEC] PrivilegeService 初始化，当前权限: {Level} (TokenElevation={El})",
            _cachedIsAdmin ? "管理员" : "普通用户",
            IsProcessElevated());
    }

    /// <inheritdoc />
    public bool Refresh()
    {
        Interlocked.Exchange(ref _refreshFlag, 1);
        var result = IsRunningAsAdmin; // triggers re-check
        Log.Information("[SEC] 权限刷新完成，当前权限: {Level}", result ? "管理员" : "普通用户");
        return result;
    }

    /// <summary>
    /// 检测当前进程是否以管理员身份运行
    /// </summary>
    /// <returns>是否为管理员权限</returns>
    /// <remarks>
    /// 采用三层检测策略：
    /// 1. TokenElevation 检测进程令牌的提升状态（最可靠）
    /// 2. WindowsPrincipal.IsInRole 回退（UAC 关闭或 P/Invoke 异常时）
    /// 3. 综合判断：若 TokenElevation 为 true 或 IsInRole 为 true 均视为管理员
    /// </remarks>
    private static bool CheckIsAdmin()
    {
        // 方式 1: TokenElevation（最可靠，直接查询进程令牌）
        if (IsProcessElevated())
        {
            Log.Debug("[SEC] TokenElevation 检测: 进程已提升");
            return true;
        }

        // 方式 2: WindowsPrincipal 回退
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var inAdminRole = principal.IsInRole(WindowsBuiltInRole.Administrator);
            Log.Debug("[SEC] WindowsPrincipal.IsInRole(Administrator) = {Result}", inAdminRole);
            return inAdminRole;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SEC] WindowsPrincipal 检测失败");
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // P/Invoke: TokenElevation 检测（比 IsInRole 更可靠）
    // ═════════════════════════════════════════════════════════════════

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out SafeAccessTokenHandle TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        int TokenInformationClass,
        IntPtr TokenInformation,
        uint TokenInformationLength,
        out uint ReturnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;  // TOKEN_INFORMATION_CLASS.TokenElevation

    /// <summary>
    /// 用 TokenElevation 检测进程令牌是否真正被提升
    /// </summary>
    /// <returns>进程已提权返回 <c>true</c>，否则返回 <c>false</c></returns>
    private static bool IsProcessElevated()
    {
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out var tokenHandle))
            {
                var err = Marshal.GetLastWin32Error();
                Log.Warning("[SEC] OpenProcessToken 失败，Win32Error={Err}", err);
                return false;
            }

            using (tokenHandle)
            {
                if (tokenHandle.IsInvalid)
                {
                    Log.Warning("[SEC] OpenProcessToken 返回无效令牌句柄");
                    return false;
                }

                // TOKEN_ELEVATION 结构仅 4 字节 (DWORD TokenIsElevated)
                var buffer = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    if (!GetTokenInformation(tokenHandle.DangerousGetHandle(), TokenElevation,
                        buffer, (uint)sizeof(int), out _))
                    {
                        var err = Marshal.GetLastWin32Error();
                        Log.Warning("[SEC] GetTokenInformation(TokenElevation) 失败，Win32Error={Err}", err);
                        return false;
                    }

                    var elevation = Marshal.ReadInt32(buffer);
                    var result = elevation != 0;
                    Log.Debug("[SEC] GetTokenInformation(TokenElevation) 成功，TokenIsElevated={Val}", elevation);
                    return result;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SEC] IsProcessElevated 异常");
            return false;
        }
    }

    /// <summary>
    /// 请求 UAC 权限提升
    /// 以管理员身份重启当前进程，原进程延迟退出
    /// </summary>
    /// <returns>是否成功发起提权请求</returns>
    public bool RequestElevation()
    {
        if (!IsWindows)
        {
            Log.Warning("[WARN] 非 Windows 平台，无法提权");
            return false;
        }

        if (IsRunningAsAdmin)
        {
            Log.Information("已经是管理员权限，无需提权");
            return true;
        }

        try
        {
            var processName = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(processName))
            {
                Log.Error("[ERR] 无法获取当前进程路径");
                return false;
            }

            Log.Information("[SEC] 请求 UAC 提权...");

            var startInfo = new ProcessStartInfo
            {
                FileName = processName,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(startInfo);

            Log.Information("[OK] 提权请求已发送，当前实例即将退出");

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                ApplicationExit();
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 请求提权失败");
            return false;
        }
    }

    /// <summary>
    /// 确保当前具有管理员权限
    /// 仅进行权限校验与日志记录，不主动触发提权流程
    /// </summary>
    /// <param name="reason">权限不足的原因说明</param>
    /// <returns>是否具有管理员权限</returns>
    public bool EnsureAdminPrivileges(string? reason = null)
    {
        if (IsRunningAsAdmin) return true;

        var reasonText = string.IsNullOrEmpty(reason)
            ? "需要管理员权限才能完整使用所有功能"
            : reason;

        Log.Warning("[WARN] 权限不足: {Reason}", reasonText);
        return false;
    }

    /// <summary>
    /// 退出当前应用程序
    /// 优先使用 WPF 关闭机制，失败时回退到环境退出
    /// </summary>
    private static void ApplicationExit()
    {
        try
        {
            _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        catch
        {
            Environment.Exit(0);
        }
    }
}
