using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
    private readonly object _pidCacheLock = new();

    /// <summary>系统+注册端口段上限（0-49151），动态端口段不计入"可占用"分母。</summary>
    private const int RegisteredPortRangeMax = 49151;

    public NetworkService(PortToProcessMapper portMapper)
    {
        _portMapper = portMapper;
    }

    public List<PortInfo> GetAllListeningPorts()
    {
        try
        {
            // 同时查询 TCP（IPv4+IPv6）与 UDP（IPv4+IPv6），覆盖 Bedrock(UDP 19132) 等场景
            var portPidMap = _portMapper.GetAllListeningPortToPidMap();
            var pidNames = GetPidNameMap();
            var ports = new List<PortInfo>(portPidMap.Count);

            foreach (var ((port, protocol), pids) in portPidMap)
            {
                // 同端口多 PID 场景（SO_REUSEPORT），取第一个非空 PID 显示
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

                // 从缓存字典查进程名，O(1)
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
            Log.Error(ex, "获取端口列表失败");
            return [];
        }
    }

    /// <summary>
    /// 获取 PID→进程名映射（带缓存，线程安全）。
    /// 一次 Process.GetProcesses() 枚举全表构建字典，替代逐个 Process.GetProcessById。
    /// 缓存 10 秒，降低进程表枚举频率。
    /// </summary>
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

    /// <summary>
    /// 结束占用指定端口的进程。先尝试 CloseMainWindow（优雅停止，触发 Java shutdown hook），
    /// 3 秒未退出则强杀。
    /// </summary>
    public bool KillProcessByPort(int port)
    {
        try
        {
            var portInfo = GetPortInfo(port);
            if (portInfo?.ProcessId == null)
                return false;

            using var process = Process.GetProcessById(portInfo.ProcessId.Value);

            // 先优雅停止：CloseMainWindow 在 Windows 下相当于发送 WM_CLOSE，
            // Java 服务器收到后能触发 shutdown hook 完成 save-all
            try
            {
                if (process.CloseMainWindow())
                {
                    Log.Information("已请求优雅停止端口 {Port} 的进程 {Name} (PID={Pid})，等待 3 秒",
                        port, portInfo.ProcessName, portInfo.ProcessId);
                    if (process.WaitForExit(3000))
                    {
                        Log.Information("进程已优雅退出: {Name} (PID={Pid})",
                            portInfo.ProcessName, portInfo.ProcessId);
                        return true;
                    }
                    Log.Warning("优雅停止超时，强杀进程: {Name} (PID={Pid})",
                        portInfo.ProcessName, portInfo.ProcessId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "CloseMainWindow 失败，降级到强杀");
            }

            // 优雅停止失败或不可用，强杀兜底
            process.Kill();
            Log.Information("已强杀占用端口 {Port} 的进程 {Name} (PID={Pid})",
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

    /// <summary>系统+注册端口段上限（动态端口段不计入"可占用"分母）。</summary>
    public int GetTotalPortCount() => RegisteredPortRangeMax;

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
