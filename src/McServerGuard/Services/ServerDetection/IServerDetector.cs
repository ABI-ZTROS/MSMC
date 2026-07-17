// -----------------------------------------------------------------------------
// 文件名: IServerDetector.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 服务器检测服务接口契约，定义服务器自动发现与持续检测的统一规范
// 依赖组件: McServerGuard.Models
// 设计模式: 接口契约模式、观察者模式（事件通知）、轮询模式
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using McServerGuard.Models;

/// <summary>
/// 服务器检测器接口契约
/// </summary>
/// <remarks>
/// <para>定义 Minecraft 服务器自动发现服务的统一抽象层。
/// 实现该接口即具备在宿主系统中自动发现 Minecraft 服务器实例的能力。</para>
/// <para>核心能力：
///   - 全量服务器检测（进程扫描 + 工作目录解析 + 配置文件扫描）
///   - 启动脚本扫描（.bat / .sh 文件识别）
///   - 周期性自动检测（后台轮询）
///   - 检测完成事件通知（观察者模式）
/// </para>
/// </remarks>
public interface IServerDetector : IDisposable
{
    /// <summary>
    /// 自动检测完成事件（无论结果是否变化均触发）
    /// </summary>
    /// <remarks>
    /// ViewModel 层订阅此事件以刷新运行中服务器列表。
    /// </remarks>
    event EventHandler<DetectionResult>? DetectionCompleted;

    /// <summary>
    /// 执行完整的服务器检测流程
    /// </summary>
    /// <returns>检测结果对象，包含所有发现的服务器实例</returns>
    /// <remarks>
    /// 检测范围包括：进程扫描、工作目录解析、配置文件扫描等全套流程。
    /// </remarks>
    public Task<DetectionResult> DetectAllAsync();

    /// <summary>
    /// 扫描指定目录中的启动脚本
    /// </summary>
    /// <param name="directory">待扫描的目录路径</param>
    /// <returns>检测到的启动脚本信息列表</returns>
    /// <remarks>
    /// 启动脚本中通常包含 JAR 文件名、JVM 参数等重要配置信息。
    /// </remarks>
    public Task<List<StartupScriptInfo>> ScanStartupScriptsAsync(string directory);

    /// <summary>
    /// 获取一个值，指示自动检测是否正在运行
    /// </summary>
    bool IsAutoDetectRunning { get; }

    /// <summary>
    /// 启动自动检测（每秒执行一次）
    /// </summary>
    void StartAutoDetect();

    /// <summary>
    /// 停止自动检测
    /// </summary>
    void StopAutoDetect();
}
