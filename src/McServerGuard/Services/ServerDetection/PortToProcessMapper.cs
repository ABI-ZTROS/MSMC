// -----------------------------------------------------------------------------
// 文件名: PortToProcessMapper.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 端口→进程 PID 反向绑定器 —— 网络套件组件
//           通过 Vanara.PInvoke.IpHlpApi 封装的 GetExtendedTcpTable 查询
//           TCP 监听端口的归属进程 PID，实现端口到进程的反向绑定
// 依赖组件: Vanara.PInvoke.IpHlpApi (NuGet), System.Runtime.InteropServices, Serilog
// 设计模式: 适配器模式 (封装 Windows IP Helper API)
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.Runtime.InteropServices;
using Serilog;
using Vanara.PInvoke;
using static Vanara.PInvoke.IpHlpApi;

/// <summary>
/// 端口→进程 PID 反向绑定器 —— 网络套件组件
/// </summary>
/// <remarks>
/// <para>通过 <see cref="IpHlpApi.GetExtendedTcpTable"/> 查询系统 TCP 连接表，
/// 获取每个监听端口的归属进程 PID，实现"端口→进程"的反向绑定。</para>
/// <para>用于与 <see cref="ProcessScanner"/> 的"进程→端口"正向绑定做交叉验证，
/// 检测端口被其他程序占用的异常情况。</para>
/// <para>所有 API 调用失败都返回空集合，不抛异常，避免拖垮检测循环。</para>
/// </remarks>
public sealed class PortToProcessMapper
{
    /// <summary>
    /// IPv4 地址族常量（Windows AF_INET）
    /// </summary>
    private const int AfInet = 2;

    /// <summary>
    /// 获取所有 TCP 监听端口的 PID 映射
    /// </summary>
    /// <returns>字典：端口 → 监听该端口的 PID。查询失败返回空字典。</returns>
    /// <remarks>
    /// 调用 <see cref="IpHlpApi.GetExtendedTcpTable"/> 配合
    /// <see cref="TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER"/> 获取所有监听状态的 TCP 行，
    /// 提取端口与 PID 构建映射字典。
    /// </remarks>
    public Dictionary<int, int> GetListeningPortToPidMap()
    {
        try
        {
            // 第一次调用获取所需缓冲区大小（Windows API 标准两步式调用）
            uint size = 0;
            IpHlpApi.GetExtendedTcpTable(IntPtr.Zero, ref size, false, AfInet,
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER);

            if (size == 0)
            {
                Log.Debug("📡 PortToProcessMapper: TCP 监听表为空");
                return [];
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = IpHlpApi.GetExtendedTcpTable(buffer, ref size, false, AfInet,
                    TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER);

                if (err.Failed)
                {
                    Log.Warning("⚠️ GetExtendedTcpTable 调用失败: {Error}", err);
                    return [];
                }

                // 表头前 4 字节是条目数（dwNumEntries）
                var count = Marshal.ReadInt32(buffer);
                var map = new Dictionary<int, int>(count);

                // MIB_TCPROW_OWNER_PID 结构体大小，用 Vanara 提供的结构体
                var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                var rowPtr = buffer + sizeof(int);

                for (var i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);

                    // dwLocalPort 存储为网络字节序，需要转主机字节序
                    // Windows API 端口编码：低16位有效，按网络字节序存储
                    var port = ntohs(row.dwLocalPort);
                    var pid = (int)row.dwOwningPid;

                    if (port > 0 && pid > 0)
                    {
                        map[port] = pid;
                    }

                    rowPtr += rowSize;
                }

                Log.Debug("📡 PortToProcessMapper: 获取到 {Count} 个 TCP 监听端口", map.Count);
                return map;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            // 降级处理：任何异常都返回空字典，不拖垮检测循环
            Log.Warning(ex, "⚠️ PortToProcessMapper 查询失败，返回空映射");
            return [];
        }
    }

    /// <summary>
    /// 查询监听指定端口的 PID
    /// </summary>
    /// <param name="port">目标端口</param>
    /// <returns>监听该端口的 PID；无监听或查询失败返回 <c>null</c></returns>
    public int? GetPidByListeningPort(int port)
    {
        var map = GetListeningPortToPidMap();
        return map.TryGetValue(port, out var pid) ? pid : null;
    }

    /// <summary>
    /// 将网络字节序的端口号转换为主机字节序（ntohs 的 .NET 实现）
    /// </summary>
    /// <param name="networkPort">网络字节序端口号（存储在 uint 中）</param>
    /// <returns>主机字节序端口号</returns>
    /// <remarks>
    /// Windows API 的 dwLocalPort 字段是 32 位无符号整数，但只有低 16 位有效，
    /// 且按网络字节序（big-endian）存储。需要交换高低字节得到实际端口。
    /// </remarks>
    private static int ntohs(uint networkPort)
    {
        // 取低16位，交换高低字节
        var lowByte = networkPort & 0xFF;
        var highByte = (networkPort >> 8) & 0xFF;
        return (int)((highByte << 8) | lowByte);
    }
}
