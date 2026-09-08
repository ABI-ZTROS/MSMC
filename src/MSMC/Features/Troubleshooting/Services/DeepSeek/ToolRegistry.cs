// -----------------------------------------------------------------------------
// 文件名: ToolRegistry.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek
// 功能描述: AI 工具注册表 —— 统一注册、路由、封装 JSON schema 供 DeepSeek Function Calling 使用
// 设计原则: P4 诚实返回链（异常被捕获并转为 ToolResult.Fail）
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools = new();
    private readonly ILogger<ToolRegistry> _log;

    public ToolRegistry(ILogger<ToolRegistry> log)
    {
        _log = log;
    }

    /// <summary>DI 启动时一次性注册所有工具</summary>
    public void RegisterAll(IEnumerable<IAiTool> tools)
    {
        foreach (var t in tools)
        {
            if (_tools.ContainsKey(t.Name))
                throw new InvalidOperationException($"Duplicate tool name: {t.Name}");
            _tools[t.Name] = t;
            _log.LogDebug("[DIAG-AI] 注册工具: {ToolName}", t.Name);
        }
        _log.LogInformation("[DIAG-AI] ToolRegistry 已注册 {Count} 个工具", _tools.Count);
    }

    public IReadOnlyCollection<IAiTool> All => _tools.Values;

    /// <summary>执行工具，路由到对应实现，异常被捕获并转为 ToolResult.Fail</summary>
    public async Task<ToolResult> ExecuteAsync(string toolName, JsonElement args, CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            _log.LogWarning("[DIAG-AI] DeepSeek 调了不存在的工具: {ToolName}", toolName);
            return ToolResult.Fail($"未知工具: {toolName}");
        }
        try
        {
            _log.LogDebug("[DIAG-AI] 执行工具 {ToolName}", toolName);
            return await tool.ExecuteAsync(args, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("工具执行被取消");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[DIAG-AI] 工具 {ToolName} 抛异常", toolName);
            return ToolResult.Fail($"工具执行异常: {ex.Message}");
        }
    }

    /// <summary>组装 DeepSeek tools JSON schema 数组（SystemPrompt 的 tools 字段）</summary>
    public string BuildToolsSchemaJson()
    {
        var list = new List<object>();
        foreach (var tool in _tools.Values)
        {
            list.Add(new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = JsonSerializer.Deserialize<object>(
                        tool.GetParametersSchema().RootElement.GetRawText())
                }
            });
        }
        return JsonSerializer.Serialize(list);
    }
}
