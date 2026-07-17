// -----------------------------------------------------------------------------
// 文件名: ISystemMonitor.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 系统监控服务接口契约，定义系统指标采集与持续监控的统一规范
// 依赖组件: McServerGuard.Models
// 设计模式: 接口契约模式、观察者模式（回调推送）、发布-订阅模式
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using McServerGuard.Models;

/// <summary>
/// 系统监控服务接口契约
/// </summary>
/// <remarks>
/// 定义系统健康指标采集的统一抽象层，涵盖 CPU、内存、磁盘、Java 进程等
/// 多维度指标的快照采集与持续监控能力。作为监控体系的核心接口契约，
/// 为上层业务提供一致的指标访问入口。
/// </remarks>
public interface ISystemMonitor
{
    /// <summary>
    /// 采集一次系统指标快照
    /// </summary>
    /// <returns>当前时刻的系统指标数据对象</returns>
    public SystemMetrics CollectSnapshot();

    /// <summary>
    /// 启动持续监控，通过回调函数周期性推送指标数据
    /// </summary>
    /// <param name="interval">采样间隔时间</param>
    /// <param name="callback">指标更新回调函数</param>
    /// <param name="cancellationToken">取消令牌，用于停止监控</param>
    public void StartMonitoring(TimeSpan interval, Action<SystemMetrics> callback, CancellationToken cancellationToken);

    /// <summary>
    /// 停止持续监控
    /// </summary>
    public void StopMonitoring();
}
