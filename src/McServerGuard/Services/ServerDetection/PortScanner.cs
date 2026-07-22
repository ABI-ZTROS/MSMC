// -----------------------------------------------------------------------------
// 文件名: PortScanner.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: TCP 端口连通性探测器 —— 网络套件核心组件
//           通过 TcpClient + Task.WhenAny 实现带超时的本地端口探测，
//           支持 SemaphoreSlim 控制的并发范围扫描
// 依赖组件: System.Net.Sockets, Serilog, ServerConstants
// 设计模式: 工具类模式, 异步并发模式
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.Net.Sockets;
using System.Threading;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// TCP 端口连通性探测器 —— 网络套件核心组件
/// </summary>
/// <remarks>
/// <para>通过 <see cref="TcpClient"/> + <c>Task.WhenAny</c> 实现带超时的本地端口 TCP connect 探测。</para>
/// <para>仅探测 127.0.0.1（本地服务器），不涉及远程主机扫描，避免防火墙/权限问题。</para>
/// <para>并发扫描采用 <see cref="SemaphoreSlim"/> 控制最大并发数，防止端口耗尽。</para>
/// </remarks>
public sealed class PortScanner
{
    /// <summary>
    /// 本地回环地址 —— 所有探测的目标主机
    /// </summary>
    private const string LocalHost = "127.0.0.1";

    /// <summary>
    /// 探测单个端口的 TCP 连通性
    /// </summary>
    /// <param name="port">目标端口</param>
    /// <param name="timeoutMs">超时毫秒数，默认取 <see cref="ServerConstants.PortScanTimeoutMs"/></param>
    /// <returns>端口是否开放（可建立 TCP 连接）；超时或异常都返回 <c>false</c></returns>
    /// <remarks>
    /// 采用 <c>TcpClient.ConnectAsync</c> + <c>Task.WhenAny(connectTask, Task.Delay(timeout))</c>
    /// 实现非阻塞超时控制。超时或连接拒绝都不抛异常，仅返回 <c>false</c>。
    /// </remarks>
    public async Task<bool> ProbePortAsync(int port, int timeoutMs = ServerConstants.PortScanTimeoutMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        Log.Debug("🔌 PortScanner: 探测端口 {Port} (超时 {Timeout}ms)", port, timeoutMs);

        using var client = new TcpClient();

        try
        {
            var connectTask = client.ConnectAsync(LocalHost, port);
            var timeoutTask = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(connectTask, timeoutTask);

            if (completed == timeoutTask)
            {
                // 超时后 connectTask 仍在飞行中，方法末尾 using 会 Dispose client，
                // 这会中止挂起的连接并让 connectTask 故障为 SocketException 995。
                // 必须主动观察该异常，否则成为未观察异常被 finalizer 线程重抛。
                _ = connectTask.ContinueWith(
                    t => { var _ = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted);
                Log.Debug("⏱️ 端口 {Port} 探测超时", port);
                return false;
            }

            // 等待 connectTask 完成以观察可能的异常
            await connectTask;
            Log.Debug("✅ 端口 {Port} 开放", port);
            return true;
        }
        catch (SocketException ex)
        {
            // ConnectionRefused 是最常见的正常失败情况，降级为 Debug 避免日志刷屏
            Log.Debug("❌ 端口 {Port} 未开放: {SocketError}", port, ex.SocketErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "❌ 端口 {Port} 探测异常", port);
            return false;
        }
    }

    /// <summary>
    /// 并发扫描端口范围，返回所有开放端口
    /// </summary>
    /// <param name="startPort">起始端口（含）</param>
    /// <param name="endPort">结束端口（含）</param>
    /// <returns>开放的端口列表（升序）</returns>
    /// <remarks>
    /// 采用 <see cref="SemaphoreSlim"/> 控制最大并发数（<see cref="ServerConstants.PortScanMaxConcurrency"/>），
    /// <see cref="Task.WhenAll"/> 等待全部探测任务完成后聚合结果。
    /// </remarks>
    public async Task<List<int>> ScanRangeAsync(int startPort, int endPort)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startPort, endPort);

        Log.Information("📡 PortScanner: 扫描端口范围 {Start}-{End}", startPort, endPort);

        var openPorts = new List<int>();
        var semaphore = new SemaphoreSlim(ServerConstants.PortScanMaxConcurrency);
        var tasks = new List<Task>();

        for (var port = startPort; port <= endPort; port++)
        {
            await semaphore.WaitAsync();
            var currentPort = port;

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (await ProbePortAsync(currentPort))
                    {
                        lock (openPorts)
                        {
                            openPorts.Add(currentPort);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        openPorts.Sort();
        Log.Information("📡 端口扫描完成，发现 {Count} 个开放端口: [{Ports}]",
            openPorts.Count, string.Join(", ", openPorts));

        return openPorts;
    }
}
