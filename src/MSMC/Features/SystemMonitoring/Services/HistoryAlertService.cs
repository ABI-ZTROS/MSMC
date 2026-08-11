// -----------------------------------------------------------------------------
// 文件名: HistoryAlertService.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 历史数据告警服务 —— 定时检查指标阈值，触发通知
// 设计模式: 三链原则 - 因果链：指标异常 → 告警通知；执行链：定时检查+阈值判断；返回链：告警日志
// -----------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

/// <summary>
/// 历史数据告警服务
/// </summary>
public class HistoryAlertService
{
    private readonly ILogger<HistoryAlertService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IMetricsPersistenceService _metricsService;
    private Timer? _timer;
    private bool _isRunning;
    
    // 告警阈值配置
    private double _cpuThresholdPercent = 90.0;
    private double _memoryThresholdPercent = 95.0;
    private double _diskThresholdPercent = 95.0;
    private TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private TimeSpan _alertCooldown = TimeSpan.FromHours(1); // 同一告警冷却时间

    private DateTimeOffset _lastCpuAlert = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMemoryAlert = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDiskAlert = DateTimeOffset.MinValue;

    public HistoryAlertService(
        ILogger<HistoryAlertService> logger,
        INotificationService notificationService,
        IMetricsPersistenceService metricsService)
    {
        _logger = logger;
        _notificationService = notificationService;
        _metricsService = metricsService;
    }

    /// <summary>
    /// 启动告警检查
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        
        _logger.LogInformation("[HistoryAlert] Starting alert service...");
        _timer = new Timer(CheckMetrics, null, TimeSpan.Zero, _checkInterval);
        _isRunning = true;
    }

    /// <summary>
    /// 停止告警检查
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        
        _logger.LogInformation("[HistoryAlert] Stopping alert service...");
        _timer?.Dispose();
        _isRunning = false;
    }

    private async void CheckMetrics(object? state)
    {
        try
        {
            var recentPoints = _metricsService.LoadRecentDays(1);
            var metrics = recentPoints.Count > 0 ? recentPoints[recentPoints.Count - 1] : null;
            if (metrics == null) return;

            var now = DateTimeOffset.UtcNow;

            // CPU 告警
            if (metrics.CpuUsagePercent > _cpuThresholdPercent && 
                (now - _lastCpuAlert) > _alertCooldown)
            {
                _logger.LogWarning("[HistoryAlert] CPU usage {Usage}% exceeds threshold {Threshold}%", 
                    metrics.CpuUsagePercent, _cpuThresholdPercent);
                
                await _notificationService.DispatchAsync(new NotificationEvent
                {
                    EventType = NotificationEventType.SystemAlert,
                    Title = "High CPU Usage",
                    Message = $"CPU usage is at {metrics.CpuUsagePercent:F1}% (threshold: {_cpuThresholdPercent}%)",
                    SourceModule = "SystemMonitor"
                });
                
                _lastCpuAlert = now;
            }

            // 内存告警
            if (metrics.MemoryUsagePercent > _memoryThresholdPercent && 
                (now - _lastMemoryAlert) > _alertCooldown)
            {
                _logger.LogWarning("[HistoryAlert] Memory usage {Usage}% exceeds threshold {Threshold}%", 
                    metrics.MemoryUsagePercent, _memoryThresholdPercent);
                
                await _notificationService.DispatchAsync(new NotificationEvent
                {
                    EventType = NotificationEventType.SystemAlert,
                    Title = "High Memory Usage",
                    Message = $"Memory usage is at {metrics.MemoryUsagePercent:F1}% (threshold: {_memoryThresholdPercent}%)",
                    SourceModule = "SystemMonitor"
                });
                
                _lastMemoryAlert = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HistoryAlert] Error during metrics check");
        }
    }

    /// <summary>
    /// 更新告警阈值
    /// </summary>
    public void UpdateThresholds(double cpu, double memory, double disk)
    {
        _cpuThresholdPercent = cpu;
        _memoryThresholdPercent = memory;
        _diskThresholdPercent = disk;
        
        _logger.LogInformation("[HistoryAlert] Thresholds updated: CPU={Cpu}%, Mem={Mem}%, Disk={Disk}%",
            cpu, memory, disk);
    }
}
