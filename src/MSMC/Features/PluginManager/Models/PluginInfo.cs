// -----------------------------------------------------------------------------
// 文件名: PluginInfo.cs
// 命名空间: io.NET.ZTR_OS.Features.PluginManager.Models
// 功能描述: 插件信息模型
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.PluginManager.Models;

/// <summary>
/// 插件信息
/// </summary>
public class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Main { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool IsValid { get; set; }
}
