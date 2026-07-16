using System;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;

namespace McServerGuard.Services;

public interface IToastNotificationService
{
    void Initialize();
    void ShowInfo(string title, string message, Action<string>? onActivated = null);
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);
    void ShowWarning(string title, string message, Action<string>? onActivated = null);
    void ShowError(string title, string message, Action<string>? onActivated = null);
    void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null);
    void ClearAll();
}

public class ToastNotificationService : IToastNotificationService
{
    private const string AppId = "McServerGuard";
    private Action<string>? _onActivated;

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

    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/660/660806.png", onActivated);
    }

    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/3379/3379866.png", onActivated);
    }

    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/1012/1012926.png", onActivated);
    }

    public void ShowError(string title, string message, Action<string>? onActivated = null)
    {
        ShowToast(title, message, "https://cdn-icons-png.flaticon.com/512/1012/1012926.png", onActivated);
    }

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