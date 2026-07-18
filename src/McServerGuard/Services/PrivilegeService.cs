// -----------------------------------------------------------------------------
// 文件名: PrivilegeService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 提供应用权限提升与管理员身份检测服务，支持 UAC 提权重启
// 依赖组件: System.Security.Principal, System.Diagnostics, System.Runtime.InteropServices
// 设计模式: 单例模式（DI容器注册）、事件通知模式
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using Serilog;

namespace McServerGuard.Services;

/// <summary>
/// 权限服务接口
/// 定义管理员权限检测与提升操作契约
/// </summary>
public interface IPrivilegeService
{
    /// <summary>
    /// 当前进程是否以管理员身份运行
    /// </summary>
    bool IsRunningAsAdmin { get; }

    /// <summary>
    /// 当前操作系统是否为 Windows
    /// </summary>
    bool IsWindows { get; }

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
    /// 管理员权限状态缓存
    /// </summary>
    private bool _isRunningAsAdmin;

    /// <inheritdoc />
    public bool IsRunningAsAdmin
    {
        get
        {
            if (!IsWindows) return false;
            return _isRunningAsAdmin;
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
    /// 构造时检测当前进程的管理员权限状态
    /// </summary>
    public PrivilegeService()
    {
        _isRunningAsAdmin = CheckIsAdmin();
        Log.Information("🔐 PrivilegeService 初始化，当前权限: {Level}",
            _isRunningAsAdmin ? "管理员" : "普通用户");
    }

    /// <summary>
    /// 检测当前进程是否以管理员身份运行
    /// </summary>
    /// <returns>是否为管理员权限</returns>
    /// <remarks>
    /// 采用双层检测策略：优先通过 <see cref="IsProcessElevated"/> 查询进程令牌的
    /// TokenElevation 信息（最可靠），失败时回退到 <see cref="WindowsPrincipal.IsInRole"/> 方案。
    /// </remarks>
    private static bool CheckIsAdmin()
    {
        try
        {
            // 优先用 TokenElevation 检测真实提权状态（最可靠）
            if (IsProcessElevated()) return true;

            // 回退到 IsInRole（兼容非 Windows 或 P/Invoke 失败场景）
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ 检查管理员权限失败");
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // P/Invoke: TokenElevation 检测（比 IsInRole 更可靠）
    // ═════════════════════════════════════════════════════════════════

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass,
        out int TokenInformation, int TokenInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

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
                return false;
            try
            {
                if (GetTokenInformation(tokenHandle, TokenElevation, out var elevation,
                    sizeof(int), out _))
                {
                    return elevation != 0;
                }
                return false;
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }
        catch
        {
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
            Log.Warning("⚠️ 非 Windows 平台，无法提权");
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
                Log.Error("❌ 无法获取当前进程路径");
                return false;
            }

            Log.Information("🔐 请求 UAC 提权...");

            var startInfo = new ProcessStartInfo
            {
                FileName = processName,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(startInfo);

            Log.Information("✅ 提权请求已发送，当前实例即将退出");

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                ApplicationExit();
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 请求提权失败");
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

        Log.Warning("⚠️ 权限不足: {Reason}", reasonText);
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
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
