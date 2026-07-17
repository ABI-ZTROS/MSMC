// 🔐 管理员权限服务 —— 检查当前是否以管理员身份运行，以及UAC提权重启
namespace McServerGuard.Services.Privilege;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Serilog;

/// <summary>
/// 管理员权限服务
/// 提供管理员权限检查、UAC提权重启等功能
/// </summary>
public class AdminPrivilegeService
{
    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 检查当前进程是否以管理员身份运行
    /// </summary>
    public bool IsRunningAsAdmin()
    {
        if (!IsWindows)
        {
            Log.Debug("非 Windows 平台，跳过管理员权限检查");
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            Log.Information("🔐 管理员权限检查: {Result}", isAdmin ? "是" : "否");
            return isAdmin;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "🔐 管理员权限检查失败");
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份重启当前程序
    /// </summary>
    /// <returns>是否成功发起重启（注意：此方法返回后当前进程会退出）</returns>
    public bool RestartAsAdmin()
    {
        if (!IsWindows)
        {
            Log.Warning("非 Windows 平台，无法进行 UAC 提权");
            return false;
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("🔐 无法获取当前程序路径");
                return false;
            }

            Log.Information("🔐 正在以管理员身份重启: {ExePath}", exePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };

            Process.Start(startInfo);

            Log.Information("🔐 UAC 提权请求已发起，当前进程即将退出");

            // 延迟一点退出，让日志写入
            Task.Delay(500).ContinueWith(_ =>
            {
                try
                {
                    Log.CloseAndFlush();
                    Environment.Exit(0);
                }
                catch { /* 忽略退出时的异常 */ }
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "🔐 以管理员身份重启失败");
            return false;
        }
    }

    /// <summary>
    /// 获取 Windows 版本信息
    /// </summary>
    public static string GetWindowsVersion()
    {
        if (!IsWindows)
            return "非 Windows 系统";

        try
        {
            var os = Environment.OSVersion;
            var version = os.Version;

            // Windows 11: 内部版本号 >= 22000
            // Windows 10: 内部版本号 10240 - 21996
            string versionName;
            if (version.Major >= 10 && version.Build >= 22000)
                versionName = "Windows 11";
            else if (version.Major >= 10 && version.Build >= 10240)
                versionName = "Windows 10";
            else if (version.Major >= 6 && version.Minor >= 3)
                versionName = "Windows 8.1";
            else if (version.Major >= 6 && version.Minor >= 2)
                versionName = "Windows 8";
            else if (version.Major >= 6 && version.Minor >= 1)
                versionName = "Windows 7";
            else
                versionName = $"Windows {version.Major}.{version.Minor}";

            return $"{versionName} (build {version.Build})";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取 Windows 版本失败");
            return "未知 Windows 版本";
        }
    }

    /// <summary>
    /// 检查是否是 Windows 11
    /// </summary>
    public static bool IsWindows11()
    {
        if (!IsWindows) return false;

        try
        {
            var version = Environment.OSVersion.Version;
            return version.Major >= 10 && version.Build >= 22000;
        }
        catch
        {
            return false;
        }
    }
}
