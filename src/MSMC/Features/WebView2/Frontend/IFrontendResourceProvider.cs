using System.IO;

namespace io.NET.ZTR_OS.Features.WebView2.Frontend;

/// <summary>
/// 前端资源提供器接口
/// 抽象不同的前端资源加载方式（文件夹/嵌入资源/Zip解压）
/// </summary>
public interface IFrontendResourceProvider
{
    /// <summary>
    /// 提供器名称（用于日志）
    /// </summary>
    string ModeName { get; }

    /// <summary>
    /// 是否可用（资源是否存在）
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 获取本地文件夹路径（用于虚拟主机映射）
    /// 如果返回 null，表示使用 WebResourceRequested 拦截模式
    /// </summary>
    /// <returns>本地文件夹路径，或 null（拦截模式）</returns>
    Task<string?> GetBasePathAsync();

    /// <summary>
    /// 获取资源流（拦截模式下使用）
    /// </summary>
    /// <param name="relativePath">相对路径（如 /index.html, /assets/app.js）</param>
    /// <returns>资源流，找不到返回 null</returns>
    Task<Stream?> GetResourceAsync(string relativePath);

    /// <summary>
    /// 获取资源的 MIME 类型
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>MIME 类型字符串</returns>
    string GetMimeType(string relativePath);
}
