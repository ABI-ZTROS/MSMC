using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using Serilog;

namespace McServerGuard.Services;

public interface IPrivilegeService
{
    bool IsRunningAsAdmin { get; }
    bool IsWindows { get; }
    bool RequestElevation();
    bool EnsureAdminPrivileges(string? reason = null);
    event EventHandler<bool>? PrivilegeChanged;
}

public class PrivilegeService : IPrivilegeService
{
    private bool _isRunningAsAdmin;

    public bool IsRunningAsAdmin
    {
        get
        {
            if (!IsWindows) return false;
            return _isRunningAsAdmin;
        }
    }

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public event EventHandler<bool>? PrivilegeChanged;

    public PrivilegeService()
    {
        _isRunningAsAdmin = CheckIsAdmin();
        Log.Information("🔐 PrivilegeService 初始化，当前权限: {Level}",
            _isRunningAsAdmin ? "管理员" : "普通用户");
    }

    private static bool CheckIsAdmin()
    {
        try
        {
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

    public bool EnsureAdminPrivileges(string? reason = null)
    {
        if (IsRunningAsAdmin) return true;

        var reasonText = string.IsNullOrEmpty(reason)
            ? "需要管理员权限才能完整使用所有功能"
            : reason;

        Log.Warning("⚠️ 权限不足: {Reason}", reasonText);
        return false;
    }

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
