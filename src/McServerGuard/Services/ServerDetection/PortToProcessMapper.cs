// -----------------------------------------------------------------------------
// 文件名: PortToProcessMapper.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 端口→进程 PID 反向绑定器 —— 网络套件组件
//           通过 Vanara.PInvoke.IpHlpApi 封装的 GetExtendedTcpTable / GetExtendedUdpTable
//           查询 TCP/UDP 监听端口的归属进程 PID（IPv4 + IPv6 双栈）
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
/// <para>查询 TCP（IPv4+IPv6）与 UDP（IPv4+IPv6）监听端口的归属进程 PID，
/// 实现"端口→进程"的反向绑定。覆盖 Bedrock（UDP 19132）与双栈绑定服务器。</para>
/// <para>所有 API 调用失败都返回空集合，不抛异常，避免拖垮检测循环。</para>
/// </remarks>
public sealed class PortToProcessMapper
{
    /// <summary>IPv4 地址族常量（Windows AF_INET）。Vanara GetExtendedTcpTable/UdpTable 的 ulAf 参数为 uint。</summary>
    private const uint AfInet = 2;

    /// <summary>IPv6 地址族常量（Windows AF_INET6）。Vanara GetExtendedTcpTable/UdpTable 的 ulAf 参数为 uint。</summary>
    private const uint AfInet6 = 23;

    /// <summary>
    /// 获取所有 TCP 监听端口（IPv4 + IPv6）的 PID 映射。
    /// 保留原签名以向后兼容；同端口多 PID 场景后者覆盖前者。
    /// </summary>
    /// <returns>字典：端口 → 监听该端口的 PID。查询失败返回空字典。</returns>
    public Dictionary<int, int> GetListeningPortToPidMap()
    {
        var multi = GetTcpListeningPortToPidMapMulti();
        // 折叠为单 PID 字典（取第一个），保持向后兼容
        var result = new Dictionary<int, int>(multi.Count);
        foreach (var kv in multi)
        {
            if (kv.Value.Count > 0)
                result[kv.Key] = kv.Value[0];
        }
        return result;
    }

    /// <summary>
    /// 获取所有 TCP 监听端口（IPv4 + IPv6）的 PID 映射（多 PID 版本）。
    /// </summary>
    /// <returns>字典：端口 → 监听该端口的所有 PID 列表（SO_REUSEPORT 场景下可能有多个）。</returns>
    public Dictionary<int, List<int>> GetTcpListeningPortToPidMapMulti()
    {
        var map = new Dictionary<int, List<int>>();
        FillTcpMap(map, AfInet);
        FillTcpMap(map, AfInet6);
        return map;
    }

    /// <summary>
    /// 获取所有 UDP 监听端口（IPv4 + IPv6）的 PID 映射。
    /// </summary>
    /// <returns>字典：端口 → 监听该端口的所有 PID 列表。</returns>
    public Dictionary<int, List<int>> GetUdpListeningPortToPidMapMulti()
    {
        var map = new Dictionary<int, List<int>>();
        FillUdpMap(map, AfInet);
        FillUdpMap(map, AfInet6);
        return map;
    }

    /// <summary>
    /// 获取所有监听端口（TCP + UDP，IPv4 + IPv6）的 PID 映射，按协议分组。
    /// </summary>
    /// <returns>字典：(端口, 协议) → PID 列表。协议为 "TCP" 或 "UDP"。</returns>
    public Dictionary<(int Port, string Protocol), List<int>> GetAllListeningPortToPidMap()
    {
        var map = new Dictionary<(int, string), List<int>>();

        foreach (var kv in GetTcpListeningPortToPidMapMulti())
            map[(kv.Key, "TCP")] = kv.Value;

        foreach (var kv in GetUdpListeningPortToPidMapMulti())
        {
            // UDP 与 TCP 同端口共存时合并 PID 列表
            if (map.TryGetValue((kv.Key, "UDP"), out var existing))
                existing.AddRange(kv.Value);
            else
                map[(kv.Key, "UDP")] = new List<int>(kv.Value);
        }

        return map;
    }

    /// <summary>查询监听指定 TCP 端口的 PID</summary>
    public int? GetPidByListeningPort(int port)
    {
        var map = GetListeningPortToPidMap();
        return map.TryGetValue(port, out var pid) ? pid : null;
    }

    private static void FillTcpMap(Dictionary<int, List<int>> map, uint af)
    {
        try
        {
            var size = 0u;
            IpHlpApi.GetExtendedTcpTable(IntPtr.Zero, ref size, false, af,
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = IpHlpApi.GetExtendedTcpTable(buffer, ref size, false, af,
                    TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER);
                if (err.Failed)
                {
                    Log.Warning("⚠️ GetExtendedTcpTable (AF={Af}) 调用失败: {Error}", af, err);
                    return;
                }

                var count = Marshal.ReadInt32(buffer);
                var rowSize = af == AfInet6
                    ? Marshal.SizeOf<MIB_TCP6ROW_OWNER_PID>()
                    : Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                var rowPtr = buffer + sizeof(int);

                for (var i = 0; i < count; i++)
                {
                    int port;
                    uint pid;

                    if (af == AfInet6)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCP6ROW_OWNER_PID>(rowPtr);
                        port = ntohs(row.dwLocalPort);
                        pid = row.dwOwningPid;
                    }
                    else
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                        port = ntohs(row.dwLocalPort);
                        pid = row.dwOwningPid;
                    }

                    if (port > 0 && pid > 0)
                    {
                        if (!map.TryGetValue(port, out var list))
                        {
                            list = new List<int>(1);
                            map[port] = list;
                        }
                        if (!list.Contains((int)pid))
                            list.Add((int)pid);
                    }

                    rowPtr += rowSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ PortToProcessMapper TCP 查询失败 (AF={Af})", af);
        }
    }

    private static void FillUdpMap(Dictionary<int, List<int>> map, uint af)
    {
        try
        {
            var size = 0u;
            IpHlpApi.GetExtendedUdpTable(IntPtr.Zero, ref size, false, af,
                UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = IpHlpApi.GetExtendedUdpTable(buffer, ref size, false, af,
                    UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                if (err.Failed)
                {
                    Log.Warning("⚠️ GetExtendedUdpTable (AF={Af}) 调用失败: {Error}", af, err);
                    return;
                }

                var count = Marshal.ReadInt32(buffer);
                var rowSize = af == AfInet6
                    ? Marshal.SizeOf<MIB_UDP6ROW_OWNER_PID>()
                    : Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();
                var rowPtr = buffer + sizeof(int);

                for (var i = 0; i < count; i++)
                {
                    int port;
                    uint pid;

                    if (af == AfInet6)
                    {
                        var row = Marshal.PtrToStructure<MIB_UDP6ROW_OWNER_PID>(rowPtr);
                        port = ntohs(row.dwLocalPort);
                        pid = row.dwOwningPid;
                    }
                    else
                    {
                        var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                        port = ntohs(row.dwLocalPort);
                        pid = row.dwOwningPid;
                    }

                    if (port > 0 && pid > 0)
                    {
                        if (!map.TryGetValue(port, out var list))
                        {
                            list = new List<int>(1);
                            map[port] = list;
                        }
                        if (!list.Contains((int)pid))
                            list.Add((int)pid);
                    }

                    rowPtr += rowSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ PortToProcessMapper UDP 查询失败 (AF={Af})", af);
        }
    }

    /// <summary>
    /// 将网络字节序的端口号转换为主机字节序（ntohs 的 .NET 实现）。
    /// </summary>
    /// <param name="networkPort">网络字节序端口号（存储在 uint 中，仅低 16 位有效，大端序）</param>
    /// <returns>主机字节序端口号（小端序）</returns>
    /// <remarks>
    /// Windows API 的 dwLocalPort 字段是 32 位无符号整数，但只有低 16 位有效，
    /// 且按网络字节序（big-endian）存储。必须交换高低字节得到实际端口。
    /// 例：端口 25565（0x639D）存储为 0x9D63，ntohs 后应得 0x639D。
    /// </remarks>
    private static int ntohs(uint networkPort)
    {
        // 取低16位，交换高低字节
        var lowByte = networkPort & 0xFF;
        var highByte = (networkPort >> 8) & 0xFF;
        return (int)((lowByte << 8) | highByte);
    }
}
