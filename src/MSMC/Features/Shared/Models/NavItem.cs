using MahApps.Metro.IconPacks;

namespace io.NET.ZTR_OS.Features.Shared.Models;

/// <summary>
/// 导航项模型 —— 数据驱动的侧边栏导航项
/// </summary>
/// <param name="IconKind">FontAwesome6 图标类型</param>
/// <param name="Title">显示标题</param>
/// <param name="PageIndex">对应页面索引</param>
public record NavItem(PackIconFontAwesome6Kind IconKind, string Title, int PageIndex);
