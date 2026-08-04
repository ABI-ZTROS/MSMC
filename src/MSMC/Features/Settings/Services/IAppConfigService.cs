// -----------------------------------------------------------------------------
// 文件名: IAppConfigService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: 应用配置服务接口契约，定义已知服务器配置的持久化与查询能力
// 依赖组件: KnownServer, AppConfig
// 设计模式: 仓储模式 + 服务接口契约
// -----------------------------------------------------------------------------
using io.NET.ZTR_OS.Features.ServerDetection.Models;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// 进程监管策略（崩溃重启 + 防睡眠 + 优先级 + 内存上限）。
/// 可同时存在于 <see cref="AppConfig"/>（全局默认）
/// 和 <see cref="KnownServer.Supervisor"/>（服务器级覆盖，未设置字段走全局）。
/// </summary>
public class ProcessSupervisorPolicy
{
    /// <summary>是否启用崩溃自动重启（Job Object 监控 + 冷却退避）。</summary>
    public bool EnableCrashRestart { get; set; } = true;

    /// <summary>每小时最多重启次数（0 表示不限制，超过则冷却 1 小时）。</summary>
    public int MaxRestartAttemptsPerHour { get; set; } = 10;

    /// <summary>连续重启之间的最小冷却秒数（指数退避：实际 = CooldownSeconds × 2^attempt，上限 300s）。</summary>
    public int RestartCooldownSeconds { get; set; } = 30;

    /// <summary>
    /// 有服务器运行时阻止系统睡眠/休眠。
    /// true 时调用 SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED)
    /// 防止 Windows 进入 Modern Standby / S3 睡眠。
    /// </summary>
    public bool PreventSystemSleepWhenRunning { get; set; } = true;

    /// <summary>
    /// 默认进程优先级类：Idle / BelowNormal / Normal / AboveNormal / High / RealTime。
    /// 推荐 Normal；大服推荐 AboveNormal；不建议 RealTime（会抢占鼠标键盘响应）。
    /// </summary>
    public System.Diagnostics.ProcessPriorityClass ProcessPriority { get; set; } =
        System.Diagnostics.ProcessPriorityClass.Normal;

    /// <summary>
    /// 单个服务器进程提交内存上限（字节），0 = 不限制。
    /// 限制通过 Job Object 的 JOBOBJECT_EXTENDED_LIMIT_INFORMATION.JobMemoryLimit 实现，
    /// 超过会在 Windows 内核层直接杀掉进程（比 JVM -Xmx 更外层，防内存泄漏打爆整机）。
    /// </summary>
    public long MaxProcessMemoryBytes { get; set; }

    /// <summary>
    /// 崩溃后尝试重启的总次数上限（-1 表示无限；0 表示永不重启）。
    /// 独立于每小时窗口计数；主要用来防止一次性故障（如地图损坏）进入无限循环。
    /// </summary>
    public int MaxTotalRestartAttempts { get; set; } = -1;
}

/// <summary>
/// 应用配置数据模型，承载应用全局持久化配置。
/// 包含已知服务器列表与当前活动服务器标识。
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 已知服务器集合。
    /// 存储用户导入的所有服务器配置条目。
    /// </summary>
    public List<KnownServer> KnownServers { get; set; } = [];

    /// <summary>
    /// 最后激活的服务器标识符。
    /// 用于应用重启后恢复上次选中的服务器上下文。
    /// </summary>
    public string LastActiveServerId { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用 Windows 通知中心。
    /// 控制重要信息是否通过系统通知弹出。
    /// </summary>
    public bool EnableWindowsNotifications { get; set; } = true;

    /// <summary>
    /// 用户自定义的 Java 安装路径列表。
    /// 用于查找默认路径外的 Java 运行时。
    /// </summary>
    public List<string> CustomJavaPaths { get; set; } = [];

    /// <summary>
    /// 用户指定的默认 Java 路径。
    /// 为空时自动选择版本最高的 64 位 Java。
    /// </summary>
    public string DefaultJavaPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否优先使用 javaw.exe（无控制台窗口）。
    /// 默认 false，因为 Minecraft 服务器需要控制台窗口来查看日志和输入命令。
    /// javaw.exe 属于 GUI 子系统，会丢弃 stdout/stderr，导致服务器输出完全丢失。
    /// </summary>
    public bool PreferJavaw { get; set; } = false;

    /// <summary>
    /// 全局进程监管策略 —— 所有服务器启动时默认采用。
    /// 服务器级可通过 <see cref="KnownServer.Supervisor"/> 进行字段级覆盖（null 字段走全局）。
    /// </summary>
    public ProcessSupervisorPolicy Supervisor { get; set; } = new();

    /// <summary>
    /// 是否启用电源管理模块（CPU 电源档位/QoS/睿频管控等实验性能力）。
    /// 默认 false：出于安全与稳定性考虑，电源管理默认关闭。
    /// 启用后需重启 MSMC 生效 —— 启用时才会注册 CpuPowerService 与对应的桥接 API。
    /// </summary>
    public bool EnablePowerManagement { get; set; } = false;
}

/// <summary>
/// 应用配置服务接口契约，定义配置的加载、持久化与已知服务器的增删改查操作。
/// 作为配置仓储层的抽象，解耦业务逻辑与具体持久化实现。
/// </summary>
public interface IAppConfigService
{
    /// <summary>
    /// 当前加载的应用配置实例。
    /// 调用 Load 后可用，修改后需调用 Save 持久化。
    /// </summary>
    AppConfig Config { get; }

    /// <summary>
    /// 从持久化存储加载应用配置。
    /// 加载完成后 Config 属性可用。
    /// </summary>
    void Load();

    /// <summary>
    /// 异步从持久化存储加载应用配置 —— 将文件 I/O 放到线程池执行，避免阻塞 UI 线程。
    /// 加载完成后 Config 属性可用。
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// 将当前配置保存至持久化存储。
    /// </summary>
    void Save();

    /// <summary>
    /// 异步将当前配置保存至持久化存储 —— 将文件 I/O 放到线程池执行，避免阻塞 UI 线程。
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 向已知服务器列表中添加一条新记录。
    /// 添加后需调用 Save 方可持久化。
    /// </summary>
    /// <param name="server">待添加的服务器实例</param>
    void AddKnownServer(KnownServer server);

    /// <summary>
    /// 从已知服务器列表中移除指定标识的记录。
    /// 移除后需调用 Save 方可持久化。
    /// </summary>
    /// <param name="id">待移除服务器的唯一标识</param>
    void RemoveKnownServer(string id);

    /// <summary>
    /// 更新已知服务器列表中的指定记录。
    /// 更新后需调用 Save 方可持久化。
    /// </summary>
    /// <param name="server">待更新的服务器实例</param>
    void UpdateKnownServer(KnownServer server);

    /// <summary>
    /// 根据 JAR 文件路径查找对应的已知服务器记录。
    /// </summary>
    /// <param name="jarPath">服务器核心 JAR 文件的绝对路径</param>
    /// <returns>匹配的服务器记录，未找到时返回 null</returns>
    KnownServer? FindByJarPath(string jarPath);

    /// <summary>
    /// 获取所有已知服务器的列表副本。
    /// </summary>
    /// <returns>全部已知服务器的列表</returns>
    List<KnownServer> GetAllKnownServers();
}
