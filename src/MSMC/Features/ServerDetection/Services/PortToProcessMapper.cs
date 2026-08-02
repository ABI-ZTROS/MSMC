// -----------------------------------------------------------------------------
// 文件名: PortToProcessMapper.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Services
// 功能描述: 端口→进程 PID 反向绑定器 —— 网络套件组件
//           通过手写 P/Invoke 调用 iphlpapi.dll 的 GetExtendedTcpTable / GetExtendedUdpTable
//           查询 TCP/UDP 监听端口的归属进程 PID（IPv4 + IPv6 双栈）
// 依赖组件: System.Runtime.InteropServices, Serilog
// 设计模式: 适配器模式 (封装 Windows IP Helper API)
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

using System.Runtime.InteropServices;
using Serilog;

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
    /// <summary>IPv4 地址族常量（Windows AF_INET）。</summary>
    private const uint AfInet = 2;

    /// <summary>IPv6 地址族常量（Windows AF_INET6）。</summary>
    private const uint AfInet6 = 23;

    /// <summary>TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER —— 仅 LISTEN 状态。</summary>
    private const int TcpTableOwnerPidListener = 3;

    /// <summary>TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL —— 所有状态（LISTEN/ESTABLISHED/TIME_WAIT/...）。</summary>
    private const int TcpTableOwnerPidAll = 5;

    /// <summary>UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID。</summary>
    private const int UdpTableOwnerPid = 1;

    /// <summary>Win32 NO_ERROR。</summary>
    private const uint NoError = 0;

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

    /// <summary>
    /// 获取所有 TCP 连接（含 LISTEN/ESTABLISHED/TIME_WAIT 等所有状态，IPv4 + IPv6）的 PID 映射。
    /// 用于计算端口占用率时统计所有被 TCP 连接占用的端口（不仅限于监听状态）。
    /// </summary>
    /// <returns>字典：端口 → PID 列表（去重）。</returns>
    public Dictionary<int, List<int>> GetAllTcpConnectionsPortToPidMap()
    {
        var map = new Dictionary<int, List<int>>();
        FillAllTcpMap(map, AfInet);
        FillAllTcpMap(map, AfInet6);
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
            NativeMethods.GetExtendedTcpTable(IntPtr.Zero, ref size, false, af, TcpTableOwnerPidListener, 0);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = NativeMethods.GetExtendedTcpTable(buffer, ref size, false, af, TcpTableOwnerPidListener, 0);
                if (err != NoError)
                {
                    Log.Warning("[WARN] GetExtendedTcpTable (AF={Af}) 调用失败: {Error}", af, err);
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
            Log.Warning(ex, "[WARN] PortToProcessMapper TCP 查询失败 (AF={Af})", af);
        }
    }

    /// <summary>
    /// 查询所有 TCP 连接（不仅 LISTEN）的端口→PID 映射。
    /// 使用 TCP_TABLE_OWNER_PID_ALL 枚举 ESTABLISHED/TIME_WAIT/CLOSE_WAIT 等所有状态。
    /// </summary>
    private static void FillAllTcpMap(Dictionary<int, List<int>> map, uint af)
    {
        try
        {
            var size = 0u;
            NativeMethods.GetExtendedTcpTable(IntPtr.Zero, ref size, false, af, TcpTableOwnerPidAll, 0);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = NativeMethods.GetExtendedTcpTable(buffer, ref size, false, af, TcpTableOwnerPidAll, 0);
                if (err != NoError)
                {
                    Log.Warning("[WARN] GetExtendedTcpTable(ALL) (AF={Af}) 调用失败: {Error}", af, err);
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
            Log.Warning(ex, "[WARN] PortToProcessMapper 全量 TCP 查询失败 (AF={Af})", af);
        }
    }

    private static void FillUdpMap(Dictionary<int, List<int>> map, uint af)
    {
        try
        {
            var size = 0u;
            NativeMethods.GetExtendedUdpTable(IntPtr.Zero, ref size, false, af, UdpTableOwnerPid, 0);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var err = NativeMethods.GetExtendedUdpTable(buffer, ref size, false, af, UdpTableOwnerPid, 0);
                if (err != NoError)
                {
                    Log.Warning("[WARN] GetExtendedUdpTable (AF={Af}) 调用失败: {Error}", af, err);
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
            Log.Warning(ex, "[WARN] PortToProcessMapper UDP 查询失败 (AF={Af})", af);
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

    // ───────────────────────────────────────────────────────────────────
    // 📦 Windows IP Helper API P/Invoke 封装
    // ───────────────────────────────────────────────────────────────────
    // 原先用 Vanara.PInvoke.IpHlpApi（NuGet 包，传递依赖 ~1-2 MB），
    // 现改为手写 P/Invoke 以减小发布产物体积。
    // 函数签名与结构体布局严格按 Microsoft Win32 文档定义：
    //   https://learn.microsoft.com/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable
    //   https://learn.microsoft.com/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedudptable
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Win32 IP Helper API 的 P/Invoke 封装。
    /// 放在独立内部类里以符合 CA1060 规范（P/Invoke 应放 NativeMethods 类）。
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// 检索包含 TCP 端点表的 P/Invoke。
        /// 第一次调用 pTcpTable=IntPtr.Zero 用于查询所需缓冲区大小，返回 ERROR_INSUFFICIENT_BUFFER (122)；
        /// 第二次调用传入实际缓冲区，返回 NO_ERROR (0) 表示成功。
        /// </summary>
        [DllImport("iphlpapi.dll", EntryPoint = "GetExtendedTcpTable", SetLastError = false)]
        internal static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref uint pdwSize,
            [MarshalAs(UnmanagedType.Bool)] bool bOrder,
            uint ulAf,
            int tableClass,
            uint reserved);

        /// <summary>
        /// 检索包含 UDP 端点表的 P/Invoke。语义同 GetExtendedTcpTable。
        /// </summary>
        [DllImport("iphlpapi.dll", EntryPoint = "GetExtendedUdpTable", SetLastError = false)]
        internal static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable,
            ref uint pdwSize,
            [MarshalAs(UnmanagedType.Bool)] bool bOrder,
            uint ulAf,
            int tableClass,
            uint reserved);
    }

    // ───────────────────────────────────────────────────────────────────
    // 📐 Win32 结构体定义（按 Microsoft 文档布局）
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// IPv4 TCP 行（含 PID）。布局严格对应 Win32 MIB_TCPROW_OWNER_PID，共 24 字节。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    /// <summary>
    /// IPv6 TCP 行（含 PID）。布局严格对应 Win32 MIB_TCP6ROW_OWNER_PID，共 56 字节。
    /// IN6_ADDR 为 16 字节数组。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucLocalAddr;
        public uint dwLocalScopeId;
        public uint dwLocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucRemoteAddr;
        public uint dwRemoteScopeId;
        public uint dwRemotePort;
        public uint dwState;
        public uint dwOwningPid;
    }

    /// <summary>
    /// IPv4 UDP 行（含 PID）。布局严格对应 Win32 MIB_UDPROW_OWNER_PID，共 12 字节。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwOwningPid;
    }

    /// <summary>
    /// IPv6 UDP 行（含 PID）。布局严格对应 Win32 MIB_UDP6ROW_OWNER_PID，共 28 字节。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucLocalAddr;
        public uint dwLocalScopeId;
        public uint dwLocalPort;
        public uint dwOwningPid;
    }
}
