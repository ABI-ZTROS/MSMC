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

    public NetworkService(PortToProcessMapper portMapper)
    {
        _portMapper = portMapper;
    }

    public List<PortInfo> GetAllListeningPorts()
    {
        try
        {
            var portToPid = _portMapper.GetListeningPortToPidMap();
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

                try
                {
                    using var process = Process.GetProcessById(pid);
                    portInfo.ProcessName = process.ProcessName;
                }
                catch
                {
                    portInfo.ProcessName = null;
                }

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