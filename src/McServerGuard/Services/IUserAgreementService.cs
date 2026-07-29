// -----------------------------------------------------------------------------
// 文件名: IUserAgreementService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 定义用户协议服务接口契约，包含协议同意状态的查询与操作
// 依赖组件: 无
// 设计模式: 接口隔离原则
// -----------------------------------------------------------------------------
namespace McServerGuard.Services;

/// <summary>
/// 用户协议服务接口
/// 定义用户协议同意状态的查询与操作契约
/// </summary>
public interface IUserAgreementService
{
    /// <summary>
    /// 用户是否已同意协议
    /// </summary>
    bool IsAgreed { get; }

    /// <summary>
    /// 协议同意时间
    /// </summary>
    DateTime? AgreedAt { get; }

    /// <summary>
    /// 已同意的协议版本号
    /// </summary>
    string? AgreedVersion { get; }

    /// <summary>
    /// 当前协议版本号（随协议内容更新而递增，作为是否需要重新同意的判定基准）
    /// </summary>
    string CurrentAgreementVersion { get; }

    /// <summary>
    /// 是否需要用户重新同意协议
    /// </summary>
    /// <remarks>
    /// 当用户从未同意、或已同意版本与当前版本不一致时返回 true，
    /// 用于在协议内容发生重大变更后强制用户重新阅读并同意新条款。
    /// </remarks>
    bool RequiresReagreement { get; }

    /// <summary>
    /// 标记用户已同意协议
    /// </summary>
    /// <param name="version">协议版本号</param>
    void SetAgreed(string version);

    /// <summary>
    /// 从本地存储加载协议状态
    /// </summary>
    void Load();

    /// <summary>
    /// 保存当前协议状态到本地存储
    /// </summary>
    void Save();
}
