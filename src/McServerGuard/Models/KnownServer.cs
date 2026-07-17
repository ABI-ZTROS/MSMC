// -----------------------------------------------------------------------------
// 文件名: KnownServer.cs
// 命名空间: McServerGuard.Models
// 功能描述: 已知服务器数据契约，持久化存储用户导入的服务器配置元数据
// 依赖组件: System.Guid
// 设计模式: POCO 数据模型 + 纯贫血模型
// -----------------------------------------------------------------------------
namespace McServerGuard.Models;

/// <summary>
/// 已知服务器 POCO 数据模型，表示用户已导入并保存至配置中的服务器条目。
/// 作为应用配置持久化层的数据契约，不包含业务逻辑与属性变更通知。
/// </summary>
public class KnownServer
{
    /// <summary>
    /// 服务器唯一标识符，采用 GUID 字符串形式。
    /// 默认值为 Guid.NewGuid() 生成的随机标识。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 用户自定义的服务器显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务器核心 JAR 文件的绝对路径。
    /// </summary>
    public string ServerJarPath { get; set; } = string.Empty;

    /// <summary>
    /// 服务器工作目录绝对路径，即 server.properties 所在目录。
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Java 虚拟机可执行文件的绝对路径。
    /// </summary>
    public string JavaPath { get; set; } = string.Empty;

    /// <summary>
    /// 初始堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xms。
    /// </summary>
    public long InitialHeapMemoryBytes { get; set; }

    /// <summary>
    /// 最大堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xmx。
    /// </summary>
    public long MaxHeapMemoryBytes { get; set; }

    /// <summary>
    /// 服务器监听端口号。
    /// 默认值为 Minecraft 标准端口 25565。
    /// </summary>
    public int Port { get; set; } = 25565;

    /// <summary>
    /// 用户配置的 JVM 参数列表。
    /// 用于启动服务器时组装命令行。
    /// </summary>
    public List<string> JvmArguments { get; set; } = [];

    /// <summary>
    /// 用户备注信息，可存储任意文本说明。
    /// 为 null 表示未设置备注。
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 服务器分组名称，用于 UI 中服务器列表的分类展示。
    /// 默认值为 "默认"。
    /// </summary>
    public string Group { get; set; } = "默认";

    /// <summary>
    /// 服务器添加时间戳，即首次导入配置的时刻。
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 服务器最后一次被检测到运行的时间戳。
    /// 用于判断服务器活跃度。
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 指示是否为收藏的服务器。
    /// 收藏的服务器在 UI 中置顶或高亮显示。
    /// </summary>
    public bool IsFavorite { get; set; }
}
