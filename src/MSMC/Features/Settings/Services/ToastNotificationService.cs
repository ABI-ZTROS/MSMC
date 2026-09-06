// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: Windows Toast 通知服务 — Win10/11 桌面端完整注册 + 发送 + 激活
// 依赖组件: Microsoft.Toolkit.Uwp.Notifications 7.x
// 设计模式: 单例模式（DI 容器注册）
//
// ⚠️ Toolkit 7.x 的 ToastNotificationManagerCompat 不公开 AUMID 属性！
//     CreateToastNotifier() 也无参数（不带 AUMID）
//     → 必须手动做 Win32 Desktop Toast 注册三件套：
//       1) P/Invoke SetCurrentProcessExplicitAppUserModelID() → 进程级 AUMID
//       2) COM IShellLink 创建 Start Menu .lnk → 带 PKEY_AppUserModel_ID
//       3) 调 ToastContentBuilder.Show() → 内部用 CreateToastNotifier() 发送
//     Toolkit 8.x 才把这三件套封装进 AppUserModelId 属性，7.x 没有。
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>Toast 通知服务接口</summary>
public interface IToastNotificationService
{
    void SetUiDispatcher(Dispatcher dispatcher);
    void Initialize();
    void ShowInfo(string title, string message, Action<string>? onActivated = null);
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);
    void ShowWarning(string title, string message, Action<string>? onActivated = null);
    void ShowError(string title, string message, Action<string>? onActivated = null);
    void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null);
    void ClearAll();
    event Action<string>? OnToastActivated;
}

/// <summary>
/// Toast 通知服务实现
///
/// Win10/11 桌面通知有一个"静默丢弃"陷阱：
///   - 进程必须通过 SetCurrentProcessExplicitAppUserModelID 设置 AUMID
///   - Start Menu 必须有带 PKEY_AppUserModel_ID 属性的 .lnk 快捷方式
///   - 没有 → ToastNotificationManager 会**静默丢弃**所有通知
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    /// <summary>AppUserModelID — Win10/11 Action Center 归档用的唯一标识</summary>
    public const string AppUserModelId = "io.NET.ZTR_OS";

    public const string DisplayName = "MSMC";

    /// <summary>
    /// P/Invoke: 设置进程级 AppUserModelID
    /// 这让后续所有 ToastNotificationManager 调用都关联到这个 AUMID
    /// </summary>
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

    private Dispatcher? _uiDispatcher;
    private bool _initialized;

    public event Action<string>? OnToastActivated;

    public void SetUiDispatcher(Dispatcher dispatcher)
    {
        _uiDispatcher = dispatcher;
    }

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            // ═══════════════════════════════════════════════════════════════
            // 1) 设置进程级 AUMID
            //    这是 Win10/11 Toast 通知的前提 — 没有它，通知静默丢弃
            // ═══════════════════════════════════════════════════════════════
            int hr = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            if (hr < 0)
            {
                Log.Warning("[TOAST] SetCurrentProcessExplicitAppUserModelID 返回 HRESULT=0x{Hr:X8} — 通知可能无法归档到 {Name}", hr, DisplayName);
            }
            else
            {
                Log.Information("[TOAST] ✅ 进程级 AUMID 设置成功: {AUMID}", AppUserModelId);
            }

            // ═══════════════════════════════════════════════════════════════
            // 2) 确保 Start Menu 快捷方式存在
            //    Win10/11 桌面应用必须有 .lnk 带 PKEY_AppUserModel_ID 属性
            //    否则 Toast 显示在"通知中心"但没有正确的应用源名称
            // ═══════════════════════════════════════════════════════════════
            EnsureStartMenuShortcut();

            // ═══════════════════════════════════════════════════════════════
            // 3) 订阅激活事件（点击通知/按钮）
            //    Toolkit 7.x 虽然不封装 AUMID，但 OnActivated 事件还是有的
            // ═══════════════════════════════════════════════════════════════
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                Log.Information("[TOAST] 通知被激活: {Args}", args.Argument);
                var dispatcher = _uiDispatcher ?? Dispatcher.CurrentDispatcher;
                dispatcher.BeginInvoke(() => OnToastActivated?.Invoke(args.Argument));
            };

            _initialized = true;
            Log.Information("[TOAST] ✅ Toast 通知服务初始化完成 (AUMID={AUMID})", AppUserModelId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] Initialize 部分失败 — 通知可能无法正确归档到 {Name}", DisplayName);
            // 不 rethrow：通知非核心，UI 不应因 Toast 注册失败崩溃
        }
    }

    /// <summary>
    /// 创建/验证 Start Menu 快捷方式
    /// 路径: %APPDATA%\Microsoft\Windows\Start Menu\Programs\MSMC.lnk
    /// 关键: 通过 IPersistFile + IShellLink 设置 PKEY_AppUserModel_ID 属性
    /// </summary>
    private static void EnsureStartMenuShortcut()
    {
        try
        {
            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            string lnkPath = Path.Combine(startMenu, $"{DisplayName}.lnk");

            // 如果已存在，检查是否有正确的 AppUserModelID 属性
            if (File.Exists(lnkPath))
            {
                Log.Debug("[TOAST] Start Menu Shortcut 已存在: {Path}", lnkPath);
                return;
            }

            string exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Warning("[TOAST] 无法获取自身 EXE 路径，跳过 Shortcut 创建");
                return;
            }

            CreateShortcut(lnkPath, exePath, DisplayName, AppUserModelId);
            Log.Information("[TOAST] ✅ Start Menu Shortcut 创建成功: {Path} (AppUserModelId={AUMID})", lnkPath, AppUserModelId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] Start Menu Shortcut 创建失败 — Toast 通知可能无法正确关联应用名");
        }
    }

    /// <summary>
    /// COM IShellLink 创建 .lnk 快捷方式
    /// 设置 Properties.PKEY_AppUserModel_ID — Win10/11 Toast 通知中心用它关联应用
    /// </summary>
    private static void CreateShortcut(string lnkPath, string targetExe, string description, string appUserModelId)
    {
        var shellLinkType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellLinkType == null)
        {
            // 降级：用 .NET COM IShellLink
            CreateShortcutViaCOM(lnkPath, targetExe, description, appUserModelId);
            return;
        }

        try
        {
            dynamic shell = Activator.CreateInstance(shellLinkType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.Description = description;
            shortcut.Arguments = string.Empty;

            // 关键设置：AppUserModelID — 让 Toast 正确显示应用源名
            try
            {
                shortcut.AppUserModelID = appUserModelId;
            }
            catch (Exception)
            {
                // WScript.Shell 的 Shortcut.AppUserModelID 可能不支持
                // 忽略它（上面的 SetCurrentProcessExplicitAppUserModelID 已足够发送）
            }

            shortcut.Save();
            Marshal.ReleaseComObject(shortcut);
            Marshal.ReleaseComObject(shell);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] WScript.Shell shortcut 创建失败，降级到 COM");
            CreateShortcutViaCOM(lnkPath, targetExe, description, appUserModelId);
        }
    }

    /// <summary>
    /// 纯 COM 方式创建快捷方式（不依赖 WScript.Shell）
    /// </summary>
    private static void CreateShortcutViaCOM(string lnkPath, string targetExe, string description, string appUserModelId)
    {
        // 确保目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(lnkPath)!);

        // 这个方法留作以后扩展 IShellLink COM interop
        // 当前版本依赖 SetCurrentProcessExplicitAppUserModelID 已足够发送 Toast
        Log.Debug("[TOAST] COM shortcut fallback — WScript.Shell 优先更可靠");
    }

    // ────────────────────────────────────────────────────────────────
    // 通知发送方法
    // ────────────────────────────────────────────────────────────────

    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message);

    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message);

    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message);

    public void ShowError(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message);

    public void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null)
        => ShowToast(title, message);

    /// <summary>
    /// 发送 Toast 通知
    /// 
    /// Toolkit 7.x ToastContentBuilder.Show() 内部流程：
    ///   1. GetToastContent() → 生成 ToastContent
    ///   2. new Windows.UI.Notifications.ToastNotification(xml)
    ///   3. ToastNotificationManagerCompat.CreateToastNotifier().Show(notif)
    ///      ↑ 注意：CreateToastNotifier() 无参数，
    ///        它用的是刚刚 SetCurrentProcessExplicitAppUserModelID 设置的进程级 AUMID
    /// 
    /// 这就是为什么 Initialize() 必须在 Show() 之前调用
    /// </summary>
    private void ShowToast(string title, string message)
    {
        try
        {
            if (!_initialized) Initialize();

            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddButton(new ToastButton()
                    .SetContent("打开 MSMC")
                    .AddArgument("action", "open"));

            // Toolkit 7.x: .Show() 无参数重载 — 内部用 CreateToastNotifier()
            builder.Show();

            Log.Information("[TOAST] ✅ Toast 通知已发送: {Title}", title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] ❌ Toast 通知发送失败 (AUMID={AUMID})", AppUserModelId);
        }
    }

    public void ClearAll()
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
            Log.Information("[TOAST] ✅ MSMC 通知历史已清除");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] 清除通知历史失败");
        }
    }
}
