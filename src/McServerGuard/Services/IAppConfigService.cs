// -----------------------------------------------------------------------------
// 文件名: IAppConfigService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 应用配置服务接口契约，定义已知服务器配置的持久化与查询能力
// 依赖组件: KnownServer, AppConfig
// 设计模式: 仓储模式 + 服务接口契约
// -----------------------------------------------------------------------------
using McServerGuard.Models;

namespace McServerGuard.Services;

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
    /// 启用后启动服务器时优先使用 javaw.exe 避免弹出命令行窗口。
    /// </summary>
    public bool PreferJavaw { get; set; } = true;
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
