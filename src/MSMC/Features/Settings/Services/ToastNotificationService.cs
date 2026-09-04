// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: 提供 Windows Toast 通知服务，支持多种类型的系统通知推送
// 依赖组件: Microsoft.Toolkit.Uwp.Notifications, Serilog
// 设计模式: 单例模式（DI容器注册）、策略模式（通知类型分派）
// -----------------------------------------------------------------------------
using System;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// Toast 通知服务接口
/// 定义各类系统通知的发送与清理契约
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// 初始化通知服务
    /// </summary>
    void Initialize();

    /// <summary>
    /// 显示信息类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调</param>
    void ShowInfo(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示成功类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调</param>
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示警告类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调</param>
    void ShowWarning(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示错误类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调</param>
    void ShowError(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示自定义图标通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="icon">图标类型标识</param>
    /// <param name="onActivated">通知激活回调</param>
    void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null);

    /// <summary>
    /// 清除所有已发送的通知
    /// </summary>
    void ClearAll();
}

/// <summary>
/// Toast 通知服务
/// 基于 Windows Toast 通知系统，提供多种类型的桌面通知推送能力
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    /// <summary>
    /// 应用程序标识
    /// </summary>
    private const string AppId = "io.NET.ZTR_OS";

    /// <summary>
    /// 当前通知激活回调
    /// </summary>
    private Action<string>? _onActivated;

    /// <summary>
    /// 初始化通知服务
    /// </summary>
    /// <remarks>
    /// Microsoft.Toolkit.Uwp.Notifications 的 ToastNotificationManagerCompat 不需要显式初始化，
    /// 调用 Show() 时会自动处理。此处保留方法以满足接口契约，并记录应用标识供调试参考。
    /// </remarks>
    public void Initialize()
    {
        try
        {
            Log.Information("[TOAST] Toast 通知服务已就绪 (AppId={AppId})", AppId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WARN] Toast 通知初始化失败，可能是 Windows 版本不支持");
        }
    }

    /// <inheritdoc />
    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/660/660806.png", onActivated);
    }

    /// <inheritdoc />
    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/3379/3379866.png", onActivated);
    }

    /// <inheritdoc />
    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/1012/1012926.png", onActivated);
    }

    /// <inheritdoc />
    public void ShowError(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/1012/1012926.png", onActivated);
    }

    /// <inheritdoc />
    public void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null)
    {
        string iconUrl = icon switch
        {
            "Success" => "https://cdn-icons-png.flaticon.com/512/3379/3379866.png",
            "Warning" => "https://cdn-icons-png.flaticon.com/512/1012/1012926.png",
            "Error" => "https://cdn-icons-png.flaticon.com/512/1012/1012926.png",
            _ => "https://cdn-icons-png.flaticon.com/512/660/660806.png"
        };

        ShowToast(title, message, iconUrl, onActivated);
    }

    /// <summary>
    /// 发送 Toast 通知
    /// Win10/11 最佳实践: 用 WinRT 原生 ToastNotificationManager.CreateToastNotifier(AppId)
    /// 让通知正确归档到 MSMC 应用名下（而不是散落成"未知应用"）。
    /// Microsoft.Toolkit.Uwp.Notifications 7.x 的 .Show() 无参数 CreateToastNotifier 不支持 AppId，
    /// 所以这里绕过 Toolkit，直接用原生 WinRT API + Toolkit 的 XML 构建器。
    /// </summary>
    private void ShowToast(string title, string message, string iconUrl, Action<string>? onActivated = null)
    {
        try
        {
            _onActivated = onActivated;

            // 用 Toolkit 的 ToastContentBuilder 构建 XML，用原生 WinRT API 发送（带 AppId 归档）
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddButton(new ToastButton()
                    .SetContent("打开 MSMC")
                    .AddArgument("action", "open"));

            var toastContent = builder.GetToastContent();
            var toastXml = toastContent.GetXml();  // Toolkit 7.x 返回 XmlDocument
            var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            notifier.Show(new ToastNotification(toastXml));

            Log.Information("[TOAST] Toast 通知已发送 (AppId={AppId}): {Title}", AppId, title);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WARN] Toast 通知发送失败 (AppId={AppId})", AppId);
        }
    }

    /// <summary>
    /// 清除所有已发送的通知
    /// </summary>
    public void ClearAll()
    {
        try
        {
            // 用原生 WinRT History.Clear(AppId) 只清除 MSMC 自己的通知，不影响其他应用
            ToastNotificationManager.History.Clear(AppId);
            Log.Information("[TOAST] Toast 通知历史已清除 (AppId={AppId})", AppId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 清除 Toast 通知失败 (AppId={AppId})", AppId);
        }
    }
}
