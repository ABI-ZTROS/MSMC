// 🎯 服务器检测服务接口 —— 定义"怎么才算检测完成"的契约
// 接口就是接口，实现在另一个文件里，分离关注点，优雅 🎩
namespace McServerGuard.Services.ServerDetection;

using McServerGuard.Models;

/// <summary>
/// 服务器检测器接口
/// 实现这个接口，你就拥有了"找到 Minecraft 服务器"的超能力
/// </summary>
public interface IServerDetector : IDisposable
{
    /// <summary>
    /// 执行完整的服务器检测流程
    /// 包括进程扫描、工作目录解析、配置文件扫描等全套服务
    /// </summary>
    /// <returns>检测结果，包含找到的所有服务器实例</returns>
    public Task<DetectionResult> DetectAllAsync();

    /// <summary>
    /// 扫描指定目录中的启动脚本（.bat / .sh 文件）
    /// 启动脚本里通常包含了 JAR 名称、JVM 参数等重要信息
    /// </summary>
    /// <param name="directory">要扫描的目录</param>
    /// <returns>检测到的启动脚本信息列表</returns>
    public Task<List<StartupScriptInfo>> ScanStartupScriptsAsync(string directory);

    /// <summary>
    /// 自动检测是否正在运行
    /// </summary>
    bool IsAutoDetectRunning { get; }

    /// <summary>
    /// 启动自动检测（每秒一次）
    /// </summary>
    void StartAutoDetect();

    /// <summary>
    /// 停止自动检测
    /// </summary>
    void StopAutoDetect();
}
