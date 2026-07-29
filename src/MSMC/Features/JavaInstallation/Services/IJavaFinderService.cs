// -----------------------------------------------------------------------------
// 文件名: IJavaFinderService.cs
// 命名空间: io.NET.ZTR_OS.Features.JavaInstallation.Services
// 功能描述: Java 查找服务接口契约，定义系统中 Java 安装的发现、验证与管理能力
// 依赖组件: JavaInstallation
// 设计模式: 服务接口契约、策略模式（多源查找）
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.JavaInstallation.Services;

/// <summary>
/// Java 安装信息实体
/// </summary>
public class JavaInstallation
{
    /// <summary>java.exe 可执行文件完整路径</summary>
    public string JavaPath { get; init; } = string.Empty;

    /// <summary>javaw.exe 可执行文件完整路径（无控制台窗口）</summary>
    public string JavawPath { get; init; } = string.Empty;

    /// <summary>JAVA_HOME 根目录路径</summary>
    public string JavaHome { get; init; } = string.Empty;

    /// <summary>版本号对象</summary>
    public Version? Version { get; init; }

    /// <summary>版本字符串</summary>
    public string VersionString { get; init; } = string.Empty;

    /// <summary>是否为 64 位架构</summary>
    public bool Is64Bit { get; init; }

    /// <summary>发行厂商名称</summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>是否为用户自定义路径添加</summary>
    public bool IsCustom { get; init; }
}

/// <summary>
/// Java 查找服务接口契约
/// </summary>
/// <remarks>
/// 定义系统中 Java 运行时的发现、验证与管理能力，
/// 支持多策略查找、用户自定义路径、默认 Java 选择等功能。
/// </remarks>
public interface IJavaFinderService
{
    /// <summary>
    /// 查找默认的 Java 运行时
    /// </summary>
    /// <returns>默认 Java 安装信息；未找到返回 null</returns>
    JavaInstallation? FindDefault();

    /// <summary>
    /// 查找系统中所有的 Java 安装实例
    /// </summary>
    /// <returns>Java 安装信息列表，按版本号从高到低排序</returns>
    List<JavaInstallation> FindAll();

    /// <summary>
    /// 验证 Java 可执行文件的有效性
    /// </summary>
    /// <param name="javaPath">java.exe 或 javaw.exe 完整路径</param>
    /// <returns>Java 安装信息对象；验证失败返回 null</returns>
    JavaInstallation? Verify(string javaPath);

    /// <summary>
    /// 添加用户自定义的 Java 路径
    /// </summary>
    /// <param name="javaHomePath">JAVA_HOME 根目录路径</param>
    void AddCustomPath(string javaHomePath);

    /// <summary>
    /// 移除用户自定义的 Java 路径
    /// </summary>
    /// <param name="javaHomePath">JAVA_HOME 根目录路径</param>
    void RemoveCustomPath(string javaHomePath);

    /// <summary>
    /// 获取所有用户自定义路径
    /// </summary>
    /// <returns>自定义路径列表</returns>
    List<string> GetCustomPaths();

    /// <summary>
    /// 默认 Java 路径（用户指定）
    /// </summary>
    string? DefaultJavaPath { get; set; }

    /// <summary>
    /// 是否优先使用 javaw.exe（无控制台窗口）
    /// </summary>
    bool PreferJavaw { get; set; }
}
