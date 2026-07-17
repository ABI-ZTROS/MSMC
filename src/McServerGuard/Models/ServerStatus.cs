// 🚦 服务器状态枚举 + 操作类型枚举 —— 整个 ServerDetection 模块的"状态机"

namespace McServerGuard.Models;

/// <summary>
/// 🚦 服务器运行状态
/// 用于驱动 UI 状态色、按钮启用、文案展示
/// </summary>
public enum ServerStatus
{
    /// <summary>❓ 未知（未检测）</summary>
    Unknown,

    /// <summary>🟢 运行中</summary>
    Running,

    /// <summary>🟡 启动中（正在执行启动流程）</summary>
    Starting,

    /// <summary>🟠 停止中（正在执行停止流程）</summary>
    Stopping,

    /// <summary>⚫ 已停止</summary>
    Stopped,

    /// <summary>🔴 异常（启动失败、进程崩溃等）</summary>
    Error
}

/// <summary>
/// 🔒 当前正在执行的操作（用于功能锁 + UI 状态显示）
/// 任何时候只能有一个非 None 的操作在进行
/// </summary>
public enum ServerOperation
{
    /// <summary>无操作（空闲）</summary>
    None,

    /// <summary>正在扫描服务器进程</summary>
    Detecting,

    /// <summary>正在导入服务器</summary>
    Importing,

    /// <summary>正在启动服务器</summary>
    Starting,

    /// <summary>正在停止服务器</summary>
    Stopping,

    /// <summary>正在保存到已知服务器列表</summary>
    SavingConfig,

    /// <summary>正在删除已知服务器</summary>
    Deleting
}
