// 📊 系统监控服务接口 —— 定义"监视系统健康状况"的契约
// 你的服务器是健康还是亚健康，问它就对了 🩺
namespace McServerGuard.Services.SystemMonitoring;

using McServerGuard.Models;

/// <summary>
/// 系统监控服务接口
/// 采集 CPU、内存、磁盘、Java 进程等指标，是整个监控体系的"体检中心"
/// </summary>
public interface ISystemMonitor
{
    /// <summary>
    /// 采集一次系统指标快照
    /// </summary>
    /// <returns>当前的系统指标数据</returns>
    public SystemMetrics CollectSnapshot();

    /// <summary>
    /// 启动持续监控，通过回调函数推送指标
    /// 调用者传入一个回调，每隔 interval 就会收到一个新的 SystemMetrics
    /// 通过 CancellationToken 控制停止
    /// </summary>
    /// <param name="interval">采样间隔</param>
    /// <param name="callback">指标更新回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public void StartMonitoring(TimeSpan interval, Action<SystemMetrics> callback, CancellationToken cancellationToken);

    /// <summary>
    /// 停止持续监控
    /// 记得用完要关掉，不然它会一直跑下去的（虽然不会炸但很浪费）⚡
    /// </summary>
    public void StopMonitoring();
}
