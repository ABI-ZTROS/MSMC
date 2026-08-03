// -----------------------------------------------------------------------------
// 文件名: ServerPortResolver.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Services
// 功能描述: 服务器配置端口解析器 —— 网络套件组件
//           从工作目录的 server.properties 解析 server-port 配置项，
//           为端口探测提供目标端口数据源
// 依赖组件: PropertiesParser, ServerConstants, System.IO, Serilog
// 设计模式: 解析器模式, 容错回退模式
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

using System.IO;
using io.NET.ZTR_OS.Features.JavaInstallation.Constants;
using io.NET.ZTR_OS.Features.ConfigEditor.Services;
using Serilog;

/// <summary>
/// 服务器配置端口解析器 —— 网络套件组件
/// </summary>
/// <remarks>
/// <para>从工作目录的 <c>server.properties</c> 文件解析 <c>server-port</c> 配置项，
/// 为 <see cref="PortScanner"/> 提供目标端口数据源。</para>
/// <para>采用容错回退策略：文件不存在、解析失败、端口非法等情况都回退到
/// <see cref="ServerConstants.DefaultServerPort"/>（25565），确保检测管道不中断。</para>
/// </remarks>
public sealed class ServerPortResolver
{
    /// <summary>
    /// 从工作目录的 server.properties 解析配置端口
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录（server.properties 所在目录）</param>
    /// <returns>配置端口；解析失败返回默认 25565</returns>
    /// <remarks>
    /// <para>处理流程：</para>
    /// <list type="number">
    /// <item>拼接 <c>server.properties</c> 完整路径</item>
    /// <item>用 <see cref="FileStream"/> + <see cref="FileShare.ReadWrite"/> 读取，避免与服务器进程冲突</item>
    /// <item>调用 <see cref="PropertiesParser.Parse"/> 解析为键值对字典</item>
    /// <item>查找 <c>server-port</c> 键并 <c>int.TryParse</c> 转换</item>
    /// <item>任何失败都回退到 <see cref="ServerConstants.DefaultServerPort"/></item>
    /// </list>
    /// </remarks>
    public int ResolveConfiguredPort(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            Log.Debug("[BRDG] ServerPortResolver: 工作目录为空，使用默认端口 {Port}", ServerConstants.DefaultServerPort);
            return ServerConstants.DefaultServerPort;
        }

        var propertiesPath = Path.Combine(workingDirectory, ServerConstants.ServerValidationFile);

        if (!File.Exists(propertiesPath))
        {
            Log.Debug("[BRDG] ServerPortResolver: {Path} 不存在，使用默认端口 {Port}",
                propertiesPath, ServerConstants.DefaultServerPort);
            return ServerConstants.DefaultServerPort;
        }

        try
        {
            // 用 FileShare.ReadWrite 打开，避免与正在写入的服务器进程产生文件锁冲突
            string content;
            using (var fs = new FileStream(propertiesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                content = sr.ReadToEnd();
            }

            var properties = PropertiesParser.Parse(content);

            if (properties.TryGetValue("server-port", out var portStr)
                && int.TryParse(portStr, out var port)
                && port is > 0 and <= 65535)
            {
                Log.Debug("[BRDG] ServerPortResolver: 解析到配置端口 {Port}", port);
                return port;
            }

            Log.Debug("[BRDG] ServerPortResolver: server-port 配置无效，使用默认端口 {Port}",
                ServerConstants.DefaultServerPort);
            return ServerConstants.DefaultServerPort;
        }
        catch (Exception ex)
        {
            // PropertiesParser 采用降级策略（坏行转注释），不再抛 FormatException；
            // 此处兜底捕获文件读取等意外异常
            Log.Debug(ex, "[BRDG] ServerPortResolver: 读取 server.properties 失败，使用默认端口");
            return ServerConstants.DefaultServerPort;
        }
    }
}
