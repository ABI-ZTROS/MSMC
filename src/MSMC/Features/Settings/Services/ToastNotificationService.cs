// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: Windows Toast 通知服务 —— 完整的 Win10/11 桌面通知注册 + 发送 + 激活
// 依赖组件: Microsoft.Toolkit.Uwp.Notifications (含 Win32 Desktop 自动注册)
// 设计模式: 单例模式（DI容器注册）
//   核心: ToastNotificationManagerCompat.AppUserModelId + .Show() + .OnActivated
//   自动完成: 进程级 AUMID + 注册表 + Start Menu Shortcut + COM Activator
// -----------------------------------------------------------------------------
using System;
using System.Windows.Threading;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// Toast 通知服务接口
/// </summary>
public interface IToastNotificationService
{
    /// <summary>绑定 UI Dispatcher（OnActivated 回调封送用）</summary>
    void SetUiDispatcher(System.Windows.Threading.Dispatcher dispatcher);

    /// <summary>初始化（注册 AUMID + Shortcut + 激活回调）</summary>
    void Initialize();

    void ShowInfo(string title, string message, Action<string>? onActivated = null);
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);
    void ShowWarning(string title, string message, Action<string>? onActivated = null);
    void ShowError(string title, string message, Action<string>? onActivated = null);
    void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null);

    /// <summary>清除所有 MSMC 专属的通知历史</summary>
    void ClearAll();

    /// <summary>订阅 Toast 激活事件（点击/按钮）</summary>
    event Action<string>? OnToastActivated;
}

/// <summary>
/// Toast 通知服务实现
/// 
/// Win10/11 桌面通知三件套（Toolkit 7.x 自动完成）：
///   1. ToastNotificationManagerCompat.AppUserModelId = AUMID
///      → SetCurrentProcessExplicitAppUserModelID (进程级)
///      → HKCU\Software\Classes\AppUserModelId\AUMID 注册表
///   2. 首次 .Show() 时自动创建 Start Menu Shortcut
///      → %APPDATA%\Microsoft\Windows\Start Menu\Programs\MSMC.lnk
///      → PKEY_AppUserModel_ID = AUMID
///      → 无此 Shortcut → Win10/11 会**静默丢弃**通知
///   3. 订阅 .OnActivated 时自动注册 COM Activator
///      → 通知按钮点击 → 启动进程 (带 -ToastActivated) → 触发 OnActivated
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    /// <summary>
    /// AppUserModelID — Win10/11 Action Center 归档用的唯一标识
    /// 格式: Company.Application 或 Reverse-DNS
    /// </summary>
    public const string AppUserModelId = "io.NET.ZTR_OS";

    /// <summary>MSMC 显示名（通知中心里显示的应用名）</summary>
    public const string DisplayName = "MSMC";

    /// <summary>CLI 参数 — 用于 Toolkit 检测"是否由 Toast 激活启动"</summary>
    private const string ToastActivatedLaunchArg = "-ToastActivated";

    private Dispatcher? _uiDispatcher;
    private bool _initialized;

    public event Action<string>? OnToastActivated;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            // 1) 设置进程级 AUMID — 所有后续 CreateToastNotifier 都会用它
            ToastNotificationManagerCompat.AppUserModelId = AppUserModelId;
            Log.Information("[TOAST] AUMID 设置: {AUMID}", AppUserModelId);

            // 2) 订阅激活事件 — Toolkit 自动注册 COM activator (首次订阅时)
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                Log.Information("[TOAST] 通知被激活: {Args}", args.Argument);
                // 封送到 UI 线程（OnActivated 在 COM 线程触发）
                var dispatcher = _uiDispatcher ?? Dispatcher.CurrentDispatcher;
                dispatcher.BeginInvoke(() => OnToastActivated?.Invoke(args.Argument));
            };

            // 3) 检查是否是由 Toast 激活启动的进程
            //    Toolkit 7.x: 如果进程带 -ToastActivated 参数启动，
            //    需要在 Initialize 里处理：注册 OnActivated 后自动触发
            var isToastActivated = Environment.GetCommandLineArgs()
                .Any(a => a.Equals(ToastActivatedLaunchArg, StringComparison.OrdinalIgnoreCase));
            if (isToastActivated)
            {
                Log.Information("[TOAST] 检测到 -ToastActivated 启动参数，等待 OnActivated 事件...");
                // Toolkit 会在其内部处理这个流程（见 Toolkit 源码 OnActivatedInternal）
            }

            _initialized = true;
            Log.Information("[TOAST] Toast 通知服务初始化完成 (AUMID={AUMID}, DisplayName={Name})",
                AppUserModelId, DisplayName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] Initialize 部分失败 — 通知可能无法正确归档到 {Name}", DisplayName);
            // 不抛异常：通知非核心功能，UI 不应因 Toast 注册失败而崩溃
        }
    }

    /// <summary>
    /// 设置 UI Dispatcher — 用于 OnActivated 回调封送到 UI 线程
    /// MainWindow 构造完成后调用一次
    /// </summary>
    public void SetUiDispatcher(Dispatcher dispatcher)
    {
        _uiDispatcher = dispatcher;
    }

    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message, onActivated);

    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message, onActivated);

    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message, onActivated);

    public void ShowError(string title, string message, Action<string>? onActivated = null)
        => ShowToast(title, message, onActivated);

    public void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null)
        => ShowToast(title, message, onActivated);

    /// <summary>
    /// 发送 Toast 通知
    /// 
    /// Toolkit 7.x .Show() 内部做了：
    ///   a. 检查并创建 HKCU\Software\Classes\AppUserModelId\{AUMID} 注册表项
    ///   b. 检查并创建 Start Menu Shortcut (.lnk) 带 PKEY_AppUserModel_ID
    ///   c. 调用 ToastNotificationManagerCompat.CreateToastNotifier() (带 AUMID)
    ///   d. Show(toast)
    /// 
    /// 我们只需要：ToastNotificationManagerCompat.AppUserModelId 在 Show() 之前设置好
    /// </summary>
    private void ShowToast(string title, string message, Action<string>? onActivated = null)
    {
        try
        {
            if (!_initialized)
            {
                Log.Warning("[TOAST] 服务未初始化，先调 Initialize()");
                Initialize();
            }

            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                // Toast 头部显示 "MSMC" — Toolkit 用 AUMID + 注册表 DisplayName
                // 不硬编码 flaticon 网络图标（离线变成红叉；Win11 自动用应用图标）
                .AddButton(new ToastButton()
                    .SetContent("打开 MSMC")
                    .AddArgument("action", "open"));

            // Toolkit 7.x: .Show() 自动用 AppUserModelId + 自动创建 Shortcut
            builder.Show();

            Log.Information("[TOAST] ✅ Toast 通知已发送: {Title}", title);

            // 记录 onActivated 回调（当前 Toolkit 的 OnActivated 事件已在 Initialize 里统一订阅）
            // 这里的 onActivated 参数保留签名兼容性，实际通过 OnToastActivated 事件传递
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] ❌ Toast 通知发送失败 — AUMID={AUMID}", AppUserModelId);
        }
    }

    /// <summary>
    /// 清除所有 MSMC 专属的通知历史（只清 AUMID=MSMC 的，不影响其他应用）
    /// </summary>
    public void ClearAll()
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
            Log.Information("[TOAST] ✅ MSMC 通知历史已清除 (AUMID={AUMID})", AppUserModelId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] 清除通知历史失败");
        }
    }
}
