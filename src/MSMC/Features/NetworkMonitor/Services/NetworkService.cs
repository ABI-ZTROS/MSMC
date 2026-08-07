using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using io.NET.ZTR_OS.Features.NetworkMonitor.Models;
using io.NET.ZTR_OS.Features.ServerDetection.Services;
using Serilog;

namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

public class NetworkService
{
    private readonly PortToProcessMapper _portMapper;

    private Dictionary<int, string>? _pidNameCache;
    private DateTime _pidNameCacheTime;
    private readonly TimeSpan _pidNameCacheInterval = TimeSpan.FromSeconds(10);
    private readonly object _pidCacheLock = new();

    /// <summary>TCP/UDP 端口号理论最大值（0-65535，共 65536 个端口）。</summary>
    private const int MaxPortValue = 65535;

    /// <summary>理论系统承载端口极限 = 65536（0 到 65535 共 65536 个端口）。</summary>
    private const int TotalPortCapacity = MaxPortValue + 1;

    public NetworkService(PortToProcessMapper portMapper)
    {
        _portMapper = portMapper;
    }

    public List<PortInfo> GetAllListeningPorts()
    {
        try
        {
            var portPidMap = _portMapper.GetAllListeningPortToPidMap();
            var pidNames = GetPidNameMap();
            var ports = new List<PortInfo>(portPidMap.Count);

            foreach (var ((port, protocol), pids) in portPidMap)
            {
                var pid = pids.FirstOrDefault(p => p > 0);
                if (pid == 0) continue;

                var portInfo = new PortInfo
                {
                    Port = port,
                    Protocol = protocol,
                    ProcessId = pid,
                    IsOpen = true,
                    PortRange = GetPortRange(port),
                    LastUpdated = DateTime.Now
                };

                pidNames.TryGetValue(pid, out var name);
                portInfo.ProcessName = name;

                ports.Add(portInfo);
            }

            ports.Sort((a, b) =>
            {
                var cmp = a.Port.CompareTo(b.Port);
                return cmp != 0 ? cmp : string.Compare(a.Protocol, b.Protocol, StringComparison.Ordinal);
            });
            return ports;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取端口列表失败（将返回空列表）");
            return [];
        }
    }

    /// <summary>
    /// 获取当前所有被占用的端口（TCP 所有状态 + UDP）。
    /// TCP 包含 LISTEN/ESTABLISHED/TIME_WAIT/CLOSE_WAIT 等全部状态，
    /// UDP 包含所有绑定的 UDP 端口。
    /// </summary>
    /// <returns>去重后的端口集合（TCP+UDP 合并去重）</returns>
    public HashSet<int> GetAllOccupiedPorts()
    {
        try
        {
            var allTcp = _portMapper.GetAllTcpConnectionsPortToPidMap();
            var allUdp = _portMapper.GetUdpListeningPortToPidMapMulti();

            var ports = new HashSet<int>();
            foreach (var kv in allTcp)
                ports.Add(kv.Key);
            foreach (var kv in allUdp)
                ports.Add(kv.Key);
            return ports;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取全部占用端口失败");
            return [];
        }
    }

    /// <summary>
    /// 获取当前已占用端口数量（TCP 所有状态 + UDP）。
    /// 用于计算端口占用率，分母为 65536（理论端口极限）。
    /// </summary>
    public int GetAllOccupiedPortCount()
    {
        return GetAllOccupiedPorts().Count;
    }

    private Dictionary<int, string> GetPidNameMap()
    {
        lock (_pidCacheLock)
        {
            if (_pidNameCache is not null
                && DateTime.Now - _pidNameCacheTime < _pidNameCacheInterval)
            {
                return _pidNameCache;
            }
        }

        var map = new Dictionary<int, string>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    map[p.Id] = p.ProcessName;
                }
                catch (Exception ex)
                {
                    Log.Debug("进程 PID={Pid} 枚举失败（可能已退出）: {Message}", p.Id, ex.Message);
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "构建 PID→进程名映射失败");
        }

        lock (_pidCacheLock)
        {
            _pidNameCache = map;
            _pidNameCacheTime = DateTime.Now;
        }
        return map;
    }

    public PortInfo? GetPortInfo(int port)
    {
        var ports = GetAllListeningPorts();
        return ports.FirstOrDefault(p => p.Port == port);
    }

    public bool KillProcessByPort(int port)
    {
        try
        {
            var portInfo = GetPortInfo(port);
            if (portInfo == null || !portInfo.ProcessId.HasValue)
            {
                Log.Debug("端口 {Port} 无关联进程，无需终止", port);
                return false;
            }

            var pid = portInfo.ProcessId.Value;
            var processName = portInfo.ProcessName ?? "unknown";

            using var process = Process.GetProcessById(pid);

            try
            {
                if (process.CloseMainWindow())
                {
                    Log.Information("已请求优雅停止端口 {Port} 的进程 {Name} (PID={Pid})，等待 3 秒",
                        port, processName, pid);
                    if (process.WaitForExit(3000))
                    {
                        Log.Information("进程已优雅退出: {Name} (PID={Pid})", processName, pid);
                        return true;
                    }
                    Log.Warning("优雅停止超时，强杀进程: {Name} (PID={Pid})", processName, pid);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "CloseMainWindow 失败，降级到强杀: {Name} (PID={Pid})", processName, pid);
            }

            process.Kill();
            Log.Information("已强杀占用端口 {Port} 的进程 {Name} (PID={Pid})",
                port, processName, pid);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            Log.Warning(ex, "结束端口 {Port} 的进程失败：权限不足，请以管理员身份运行程序", port);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "结束端口 {Port} 的进程失败", port);
            return false;
        }
    }

    private PortRangeType GetPortRange(int port)
    {
        if (port <= 1023) return PortRangeType.System;
        if (port <= 49151) return PortRangeType.Registered;
        return PortRangeType.Dynamic;
    }

    /// <summary>理论系统承载端口极限 = 65536。</summary>
    public int GetTotalPortCount() => TotalPortCapacity;

    /// <summary>当前监听端口数（LISTEN 状态）。</summary>
    public int GetListeningPortCount() => GetAllListeningPorts().Count;

    /// <summary>当前已占用端口数（TCP 所有状态 + UDP）。</summary>
    public int GetOccupiedPortCount() => GetAllOccupiedPortCount();

    public (int System, int Registered, int Dynamic) GetPortDistribution()
    {
        var ports = GetAllListeningPorts();
        return (
            ports.Count(p => p.PortRange == PortRangeType.System),
            ports.Count(p => p.PortRange == PortRangeType.Registered),
            ports.Count(p => p.PortRange == PortRangeType.Dynamic)
        );
    }

    /// <summary>
    /// 一次性获取端口使用情况快照 —— 复用单次端口枚举结果，避免重复枚举。
    /// 已占用端口 = TCP 所有状态端口 + UDP 端口（去重）。
    /// 总端口 = 65536（0-65535 理论极限）。
    /// </summary>
    public (List<PortInfo> Ports, int OccupiedCount, int TotalCount,
        (int System, int Registered, int Dynamic) Distribution)
        GetPortSnapshot()
    {
        var ports = GetAllListeningPorts();
        var occupied = GetAllOccupiedPortCount();
        var total = TotalPortCapacity;
        var distribution = (
            ports.Count(p => p.PortRange == PortRangeType.System),
            ports.Count(p => p.PortRange == PortRangeType.Registered),
            ports.Count(p => p.PortRange == PortRangeType.Dynamic)
        );
        return (ports, occupied, total, distribution);
    }
}
