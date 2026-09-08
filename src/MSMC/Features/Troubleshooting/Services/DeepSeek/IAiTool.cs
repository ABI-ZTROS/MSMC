// -----------------------------------------------------------------------------
// 文件名: IAiTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek
// 功能描述: AI Function Calling 工具接口 —— DeepSeek 可自主调用的每个工具必须实现此接口
// 设计原则: P4 诚实返回链 —— ToolResult 强制三字段 Success/Error/Data，绝不静默成功
// -----------------------------------------------------------------------------
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek;

/// <summary>AI 工具接口 —— 每个可被 DeepSeek 自主调用的工具必须实现此接口</summary>
public interface IAiTool
{
    /// <summary>函数名（DeepSeek function.name 必须精确匹配）</summary>
    string Name { get; }

    /// <summary>喂给 DeepSeek 的描述（什么时候该调这个工具）</summary>
    string Description { get; }

    /// <summary>喂给 DeepSeek 的 JSON schema（parameters）</summary>
    JsonDocument GetParametersSchema();

    /// <summary>执行工具，返回三字段诚实结果</summary>
    Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct);
}

/// <summary>
/// 工具执行结果 —— 诚实返回链（P4）强制三字段
/// 任何失败都必须 Success=false + Error 非空，绝不静默成功
/// </summary>
public record ToolResult(
    bool Success,
    string? Error,
    string? Data)
{
    /// <summary>成功结果工厂</summary>
    public static ToolResult Ok(string data) => new(true, null, data);

    /// <summary>失败结果工厂</summary>
    public static ToolResult Fail(string error) => new(false, error, null);

    /// <summary>序列化为 JSON 字符串（塞回 DeepSeek 对话历史）</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);
}

/// <summary>DI 标记接口 —— 所有 IAiTool 实现都应同时实现此接口，便于批量注册</summary>
public interface IToolFactory;
