// -----------------------------------------------------------------------------
// 文件名: JavaInstallationViewModel.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.ViewModels
// 功能描述: Java 安装项视图模型，用于在设置页面展示 Java 版本列表
// 依赖组件: CommunityToolkit.Mvvm, io.NET.ZTR_OS.Services
// 设计模式: MVVM 模式
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;

namespace io.NET.ZTR_OS.Features.Settings.ViewModels;

/// <summary>
/// Java 安装项视图模型
/// </summary>
public partial class JavaInstallationViewModel : ObservableObject
{
    /// <summary>
    /// 原始 Java 安装信息
    /// </summary>
    private readonly JavaInstallation _installation;

    /// <summary>
    /// Java 安装信息
    /// </summary>
    public JavaInstallation Installation => _installation;

    /// <summary>
    /// 版本显示文本
    /// </summary>
    public string VersionDisplay => string.IsNullOrEmpty(_installation.VersionString)
        ? "未知版本"
        : $"Java {_installation.VersionString}";

    /// <summary>
    /// 厂商显示文本
    /// </summary>
    public string VendorDisplay => string.IsNullOrEmpty(_installation.Vendor)
        ? "未知厂商"
        : _installation.Vendor;

    /// <summary>
    /// 架构显示文本
    /// </summary>
    public string ArchitectureDisplay => _installation.Is64Bit ? "64 位" : "32 位";

    /// <summary>
    /// 路径显示文本
    /// </summary>
    public string PathDisplay => _installation.JavaHome;

    /// <summary>
    /// 是否为默认 Java
    /// </summary>
    [ObservableProperty]
    private bool _isDefault;

    /// <summary>
    /// 是否为用户自定义路径
    /// </summary>
    public bool IsCustom => _installation.IsCustom;

    /// <summary>
    /// 是否有 javaw.exe
    /// </summary>
    public bool HasJavaw => !string.IsNullOrEmpty(_installation.JavawPath);

    /// <summary>
    /// 初始化 Java 安装项视图模型
    /// </summary>
    /// <param name="installation">Java 安装信息</param>
    /// <param name="isDefault">是否为默认 Java</param>
    public JavaInstallationViewModel(JavaInstallation installation, bool isDefault = false)
    {
        _installation = installation;
        _isDefault = isDefault;
    }
}
