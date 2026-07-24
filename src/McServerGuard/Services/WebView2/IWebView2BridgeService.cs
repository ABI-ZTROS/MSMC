// -----------------------------------------------------------------------------
// 文件名: IWebView2BridgeService.cs
// 命名空间: McServerGuard.Services.WebView2
// 功能描述: WebView2 桥接服务接口契约，定义 C# 与 JavaScript 双向通信能力
// 依赖组件: Microsoft.Web.WebView2.Wpf, System.Text.Json
// 设计模式: 服务接口契约 + 观察者模式 + 消息模式
// -----------------------------------------------------------------------------
using Microsoft.Web.WebView2.Wpf;

namespace McServerGuard.Services.WebView2;

/// <summary>
/// 请求处理程序委托，处理来自 JS 的请求并返回响应
/// </summary>
/// <param name="payload">请求负载数据</param>
/// <returns>响应负载数据</returns>
public delegate Task<object?> RequestHandler(object? payload);

/// <summary>
/// 事件处理程序委托，处理来自 C# 的事件推送
/// </summary>
/// <param name="action">事件动作名</param>
/// <param name="payload">事件负载数据</param>
public delegate void EventHandler(string action, object? payload);

/// <summary>
/// WebView2 桥接服务接口契约
/// 提供 C# 与 JavaScript 之间的双向通信能力，支持请求/响应模式和事件推送模式
/// </summary>
public interface IWebView2BridgeService
{
    /// <summary>
    /// 是否已初始化并连接到 WebView2 控件
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 初始化桥接服务，绑定到指定的 WebView2 控件
    /// </summary>
    /// <param name="webView">要绑定的 WebView2 控件</param>
    Task InitializeAsync(WebView2 webView);

    /// <summary>
    /// 注册请求处理程序（JS 调用 C# 方法）
    /// </summary>
    /// <param name="action">动作名称</param>
    /// <param name="handler">处理程序</param>
    void RegisterRequestHandler(string action, RequestHandler handler);

    /// <summary>
    /// 注销请求处理程序
    /// </summary>
    /// <param name="action">动作名称</param>
    void UnregisterRequestHandler(string action);

    /// <summary>
    /// 向 JS 发送事件推送（C# → JS，单向）
    /// </summary>
    /// <param name="action">事件动作名</param>
    /// <param name="payload">事件负载数据</param>
    Task SendEventAsync(string action, object? payload = null);

    /// <summary>
    /// 向 JS 发送请求并等待响应（C# → JS → C#）
    /// </summary>
    /// <param name="action">请求动作名</param>
    /// <param name="payload">请求负载数据</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>JS 返回的响应负载</returns>
    Task<object?> SendRequestAsync(string action, object? payload = null, int timeoutMs = 30000);

    /// <summary>
    /// 订阅来自 JS 的事件推送（JS → C#，单向）
    /// </summary>
    /// <param name="handler">事件处理程序</param>
    void SubscribeToEvents(EventHandler handler);

    /// <summary>
    /// 取消事件订阅
    /// </summary>
    /// <param name="handler">事件处理程序</param>
    void UnsubscribeFromEvents(EventHandler handler);

    /// <summary>
    /// 释放资源
    /// </summary>
    void Shutdown();
}
