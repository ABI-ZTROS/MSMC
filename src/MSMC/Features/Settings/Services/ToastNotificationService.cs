// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: Windows Toast 通知服务 — 严格按微软官方 Win32 Desktop Toast 流程
//
// 微软官网必读:
//   learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid
//   learn.microsoft.com/en-us/windows/win32/shell/quickstart-sending-desktop-toast
//
// ⚠️ 关键（微软强调）:
//   1. Start Menu .lnk 必须有 System.AppUserModel.ID 属性
//   2. CreateToastNotifier **必须传入** AppUserModelID — 不传 = Win32 Desktop 静默丢弃
//   3. Toolkit 7.x 的 .Show() 用无参数 CreateToastNotifier() → 必然静默丢弃
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.UI.Notifications;
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
/// Win32 Desktop Toast 通知服务
/// 
/// 微软官方三步流程:
///   SetCurrentProcessExplicitAppUserModelID → 进程级 AUMID
///   Start Menu .lnk with PKEY_AppUserModel_ID → 关联应用源名
///   ToastNotificationManager.CreateToastNotifier(AUMID).Show() → 发送
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    public const string AppUserModelId = "io.NET.ZTR_OS";
    public const string DisplayName = "MSMC";

    private Dispatcher? _uiDispatcher;
    private bool _initialized;

    public event Action<string>? OnToastActivated;

    // ─── P/Invoke（放在 class 内部，避免 namespace-scope 语法问题）───

    /// <summary>shell32.dll: 设置进程级 AppUserModelID</summary>
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    // ─── 公共 API ────────────────────────────────────────────────────────

    public void SetUiDispatcher(Dispatcher dispatcher) => _uiDispatcher = dispatcher;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Step 1: 设置进程级 AUMID
            int hr = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            Log.Information("[TOAST] SetCurrentProcessExplicitAppUserModelID hr=0x{Hr:X8} AUMID={AUMID}", hr, AppUserModelId);

            // Step 2: 创建 Start Menu .lnk + 设置 AUMID 属性
            EnsureStartMenuShortcut();

            // Step 3: 订阅激活回调
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                Log.Information("[TOAST] 激活回调: {Args}", args.Argument);
                var d = _uiDispatcher ?? Dispatcher.CurrentDispatcher;
                d.BeginInvoke(() => OnToastActivated?.Invoke(args.Argument));
            };

            _initialized = true;
            Log.Information("[TOAST] ✅ Toast 通知服务初始化完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] Initialize 异常");
            throw;
        }
    }

    /// <summary>
    /// 创建/验证 Start Menu 快捷方式（WScript.Shell）
    /// 微软要求: 没有带 AppUserModelID 的 .lnk → Toast 静默丢弃
    /// </summary>
    private static void EnsureStartMenuShortcut()
    {
        try
        {
            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            string lnkPath = Path.Combine(startMenu, $"{DisplayName}.lnk");

            if (File.Exists(lnkPath))
            {
                Log.Debug("[TOAST] .lnk 已存在，跳过: {Path}", lnkPath);
                return;
            }

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) { Log.Warning("[TOAST] 无法获取 EXE 路径"); return; }

            Directory.CreateDirectory(startMenu);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) { Log.Warning("[TOAST] WScript.Shell 不可用"); return; }

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = DisplayName;
                shortcut.Arguments = "";
                try { shortcut.AppUserModelID = AppUserModelId; }
                catch (Exception ex) { Log.Debug(ex, "[TOAST] shortcut.AppUserModelID 跳过"); }
                shortcut.Save();
                Log.Information("[TOAST] ✅ Start Menu .lnk 创建: {Path}", lnkPath);
            }
            finally { Marshal.ReleaseComObject(shell); }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] Start Menu Shortcut 创建失败");
        }
    }

    // ─── 发送通知 ────────────────────────────────────────────────────────

    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowError(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null)
        => ShowToastCore(title, message);

    /// <summary>
    /// 微软官方流程:
    ///   ToastNotificationManager.CreateToastNotifier(AUMID) — 必须带参数！
    ///   Toolkit 7.x 的 .Show() 用无参数版 → Win32 Desktop 静默丢弃
    /// </summary>
    private void ShowToastCore(string title, string message)
    {
        try
        {
            if (!_initialized) Initialize();

            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddButton(new ToastButton().SetContent("打开 MSMC").AddArgument("action", "open"));

            var toast = new ToastNotification(builder.GetToastContent().GetXml());

            // ⚠️ 关键: 带 AUMID 参数 —— 微软官方明确要求！
            ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(toast);

            Log.Information("[TOAST] ✅ Toast 已发送 (AUMID={AUMID}, Title={Title})", AppUserModelId, title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] ❌ Toast 发送失败 (AUMID={AUMID})", AppUserModelId);
        }
    }

    public void ClearAll()
    {
        try { ToastNotificationManagerCompat.History.Clear(); }
        catch (Exception ex) { Log.Warning(ex, "[TOAST] 清除通知历史失败"); }
    }
}
