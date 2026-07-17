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
