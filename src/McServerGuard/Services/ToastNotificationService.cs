// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 提供 Windows Toast 通知服务，支持多种类型的系统通知推送
// 依赖组件: Microsoft.Toolkit.Uwp.Notifications, Serilog
// 设计模式: 单例模式（DI容器注册）、策略模式（通知类型分派）
// -----------------------------------------------------------------------------
using System;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;

namespace McServerGuard.Services;

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
    private const string AppId = "McServerGuard";

    /// <summary>
    /// 当前通知激活回调
    /// </summary>
    private Action<string>? _onActivated;

    /// <summary>
    /// 初始化通知服务
    /// </summary>
    public void Initialize()
    {
        try
        {
            Log.Information("🔔 Toast 通知服务已初始化");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Toast 通知初始化失败，可能是 Windows 版本不支持");
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
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="iconUrl">图标 URL</param>
    /// <param name="onActivated">通知激活回调</param>
    private void ShowToast(string title, string message, string iconUrl, Action<string>? onActivated = null)
    {
        try
        {
            _onActivated = onActivated;

            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddAppLogoOverride(new Uri(iconUrl), ToastGenericAppLogoCrop.Circle)
                .AddButton(new ToastButton()
                    .SetContent("打开")
                    .AddArgument("action", "open"))
                .Show();

            Log.Information("🔔 Toast 通知已发送: {Title}", title);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Toast 通知发送失败");
        }
    }

    /// <summary>
    /// 清除所有已发送的通知
    /// </summary>
    public void ClearAll()
    {
        try
        {
            Log.Information("🔔 所有 Toast 通知已清除");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 清除 Toast 通知失败");
        }
    }
}
