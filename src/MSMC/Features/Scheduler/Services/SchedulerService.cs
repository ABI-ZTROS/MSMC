// -----------------------------------------------------------------------------
// 文件名: SchedulerService.cs
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Services
// 功能描述: 计划任务调度服务 —— 任务管理 + 定时触发 + 防重入
// 设计模式: 三链原则 - 执行链：SemaphoreSlim 防并发；返回链：结构化日志
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

/// <summary>
/// 计划任务调度服务
/// </summary>
public class SchedulerService : ISchedulerService
{
    private readonly ConcurrentDictionary<Guid, ScheduledTask> _tasks = new();
    private readonly ConcurrentBag<ExecutionRecord> _executionHistory = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<SchedulerService> _logger;
    private readonly INotificationService _notificationService;
    private Timer? _timer;
    private bool _isRunning;

    public SchedulerService(ILogger<SchedulerService> logger, INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _logger.LogInformation("[Scheduler] Starting task scheduler...");
        _timer = new Timer(TickCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _logger.LogInformation("[Scheduler] Task scheduler stopped.");
    }

    /// <summary>
    /// 定时回调 —— 每 10 秒扫描一次到期任务
    /// </summary>
    private void TickCallback(object? state)
    {
        if (!_isRunning) return;

        var now = DateTimeOffset.UtcNow;
        var dueTasks = _tasks.Values
            .Where(t => t.Enabled && t.NextRunTime.HasValue && t.NextRunTime.Value <= now)
            .ToList();

        foreach (var task in dueTasks)
        {
            _ = ExecuteTaskAsync(task);
        }
    }

    /// <summary>
    /// 执行任务（防重入 + 异常捕获 + 日志）
    /// </summary>
    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        if (!await _semaphore.WaitAsync(0))
        {
            _logger.LogWarning("[Scheduler] Task {TaskName} ({TaskId}) already running, skipping.",
                task.Name, task.Id);
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var record = new ExecutionRecord
        {
            TaskId = task.Id,
            TaskName = task.Name,
            StartedAt = startedAt
        };

        try
        {
            _logger.LogInformation("[Scheduler] Executing task: {TaskName} (Id={TaskId}, Action={Action})",
                task.Name, task.Id, task.Action.Type);

            // 根据动作类型执行
            await task.Action.Type switch
            {
                ActionType.ServerStart => ExecuteServerActionAsync(task, "start"),
                ActionType.ServerStop => ExecuteServerActionAsync(task, "stop"),
                ActionType.ServerRestart => ExecuteServerActionAsync(task, "restart"),
                ActionType.RunCommand => ExecuteCommandAsync(task),
                ActionType.RunBackup => ExecuteBackupAsync(task),
                ActionType.RunScript => ExecuteScriptAsync(task),
                ActionType.SendNotification => ExecuteNotificationTaskAsync(task),
                _ => throw new NotSupportedException($"Action type {task.Action.Type} not supported")
            };

            record.Status = TaskStatus.Completed;
            task.LastStatus = TaskStatus.Completed;
            task.ConsecutiveFailures = 0;
            _logger.LogInformation("[Scheduler] Task {TaskName} completed successfully.", task.Name);
        }
        catch (Exception ex)
        {
            record.Status = TaskStatus.Failed;
            record.ErrorMessage = ex.Message;
            task.LastStatus = TaskStatus.Failed;
            task.ConsecutiveFailures++;
            task.LastErrorMessage = ex.Message;

            _logger.LogError(ex, "[Scheduler] Task {TaskName} failed (Attempt {Attempt}).",
                task.Name, task.ConsecutiveFailures);

            // 失败超过阈值 → 禁用任务
            if (task.ConsecutiveFailures >= task.MaxConsecutiveFailures)
            {
                task.Enabled = false;
                _logger.LogWarning("[Scheduler] Task {TaskName} auto-disabled after {Max} consecutive failures.",
                    task.Name, task.ConsecutiveFailures);
            }
        }
        finally
        {
            record.CompletedAt = DateTimeOffset.UtcNow;
            record.Duration = record.CompletedAt.Value - record.StartedAt;
            _executionHistory.Add(record);

            task.LastRunTime = startedAt;
            task.TotalRunCount++;

            // 计算下次运行时间
            task.NextRunTime = CalculateNextRunTime(task);

            _semaphore.Release();
        }
    }

    /// <summary>
    /// 计算下次运行时间
    /// </summary>
    private DateTimeOffset? CalculateNextRunTime(ScheduledTask task)
    {
        return task.Trigger.Type switch
        {
            TriggerType.Cron => CronParser.GetNextRunTime(
                task.Trigger.CronExpression ?? string.Empty,
                DateTimeOffset.UtcNow),
            TriggerType.Interval => DateTimeOffset.UtcNow + (task.Trigger.Interval ?? TimeSpan.FromHours(1)),
            TriggerType.OneTime => task.Trigger.OneTimeAt.HasValue && task.Trigger.OneTimeAt > DateTimeOffset.UtcNow
                ? task.Trigger.OneTimeAt.Value
                : null,
            _ => null
        };
    }

    #region 动作执行

    private Task ExecuteServerActionAsync(ScheduledTask task, string action)
    {
        // 实际实现将通过 ServerDetection 模块的 ServerManagerService 执行
        _logger.LogDebug("[Scheduler] Would {Action} server {ServerId}", action, task.Action.TargetServerId);
        return Task.CompletedTask;
    }

    private Task ExecuteCommandAsync(ScheduledTask task)
    {
        _logger.LogDebug("[Scheduler] Would execute command: {Command}", task.Action.CommandOrPath);
        return Task.CompletedTask;
    }

    private Task ExecuteBackupAsync(ScheduledTask task)
    {
        _logger.LogDebug("[Scheduler] Would backup server {ServerId}", task.Action.TargetServerId);
        return Task.CompletedTask;
    }

    private Task ExecuteScriptAsync(ScheduledTask task)
    {
        _logger.LogDebug("[Scheduler] Would run script: {Path}", task.Action.CommandOrPath);
        return Task.CompletedTask;
    }

    private async Task ExecuteNotificationTaskAsync(ScheduledTask task)
    {
        await _notificationService.DispatchAsync(new Models.NotificationEvent
        {
            EventType = Models.NotificationEventType.ScheduleCompleted,
            Title = $"计划任务完成: {task.Name}",
            Message = task.Action.CommandOrPath ?? $"任务 {task.Name} 已执行",
            SourceModule = "Scheduler",
            TargetServerId = task.Action.TargetServerId
        });
    }

    #endregion

    #region CRUD

    public IReadOnlyList<ScheduledTask> GetAllTasks() => _tasks.Values.ToList().AsReadOnly();

    public ScheduledTask? GetTask(Guid taskId) => _tasks.GetValueOrDefault(taskId);

    public void AddTask(ScheduledTask task)
    {
        if (_tasks.TryAdd(task.Id, task))
        {
            task.NextRunTime = CalculateNextRunTime(task);
            _logger.LogInformation("[Scheduler] Task added: {TaskName} ({TaskId})", task.Name, task.Id);
        }
    }

    public void UpdateTask(ScheduledTask task)
    {
        task.NextRunTime = CalculateNextRunTime(task);
        _tasks.AddOrUpdate(task.Id, task, (_, __) => task);
        _logger.LogInformation("[Scheduler] Task updated: {TaskName} ({TaskId})", task.Name, task.Id);
    }

    public bool DeleteTask(Guid taskId)
    {
        if (_tasks.TryRemove(taskId, out var task))
        {
            _logger.LogInformation("[Scheduler] Task removed: {TaskName} ({TaskId})", task.Name, task.Id);
            return true;
        }
        return false;
    }

    public async Task<bool> RunNowAsync(Guid taskId)
    {
        var task = _tasks.GetValueOrDefault(taskId);
        if (task == null) return false;
        task.NextRunTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        await ExecuteTaskAsync(task);
        return task.LastStatus == TaskStatus.Completed;
    }

    public IReadOnlyList<ExecutionRecord> GetExecutionHistory(int maxRecords = 100)
    {
        return _executionHistory.OrderByDescending(r => r.StartedAt).Take(maxRecords).ToList().AsReadOnly();
    }

    #endregion
}
