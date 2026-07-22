using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McServerGuard.Models;
using McServerGuard.Services.ServerDetection;
using Serilog;

namespace McServerGuard.Services.Network;

public class NetworkService
{
    private readonly PortToProcessMapper _portMapper;

    // P1-001: 缓存 PID→进程名映射，避免逐个调用 Process.GetProcessById
    private Dictionary<int, string>? _pidNameCache;
    private DateTime _pidNameCacheTime;
    private readonly TimeSpan _pidNameCacheInterval = TimeSpan.FromSeconds(10);

    public NetworkService(PortToProcessMapper portMapper)
    {
        _portMapper = portMapper;
    }

    public List<PortInfo> GetAllListeningPorts()
    {
        try
        {
            var portToPid = _portMapper.GetListeningPortToPidMap();
            var pidNames = GetPidNameMap();
            var ports = new List<PortInfo>(portToPid.Count);

            foreach (var (port, pid) in portToPid)
            {
                var portInfo = new PortInfo
                {
                    Port = port,
                    Protocol = "TCP",
                    ProcessId = pid,
                    IsOpen = true,
                    PortRange = GetPortRange(port),
                    LastUpdated = DateTime.Now
                };

                // 从缓存字典查进程名，O(1)
                pidNames.TryGetValue(pid, out var name);
                portInfo.ProcessName = name;

                ports.Add(portInfo);
            }

            ports.Sort((a, b) => a.Port.CompareTo(b.Port));
            return ports;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取端口列表失败");
            return [];
        }
    }

    /// <summary>
    /// 获取 PID→进程名映射（带缓存）。
    /// 一次 Process.GetProcesses() 枚举全表构建字典，替代逐个 Process.GetProcessById。
    /// 缓存 10 秒，降低进程表枚举频率。
    /// </summary>
    private Dictionary<int, string> GetPidNameMap()
    {
        if (_pidNameCache is not null
            && DateTime.Now - _pidNameCacheTime < _pidNameCacheInterval)
        {
            return _pidNameCache;
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
                catch
                {
                    // 进程可能已退出
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

        _pidNameCache = map;
        _pidNameCacheTime = DateTime.Now;
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
            if (portInfo?.ProcessId == null)
                return false;

            using var process = Process.GetProcessById(portInfo.ProcessId.Value);
            process.Kill();
            Log.Information("已结束占用端口 {Port} 的进程 {Name} (PID={Pid})",
                port, portInfo.ProcessName, portInfo.ProcessId);
            return true;
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

    public int GetTotalPortCount() => 65535;

    public int GetUsedPortCount() => GetAllListeningPorts().Count;

    public int GetUsedPercentage()
    {
        var used = GetUsedPortCount();
        return (int)((double)used / GetTotalPortCount() * 100);
    }

    public (int System, int Registered, int Dynamic) GetPortDistribution()
    {
        var ports = GetAllListeningPorts();
        return (
            ports.Count(p => p.PortRange == PortRangeType.System),
            ports.Count(p => p.PortRange == PortRangeType.Registered),
            ports.Count(p => p.PortRange == PortRangeType.Dynamic)
        );
    }
}