// -----------------------------------------------------------------------------
// 文件名: SchedulerStorageService.cs
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Services
// 功能描述: 调度任务持久化服务 —— JSON 文件读写
// 设计模式: 三链原则 - 因果链：任务变更触发保存；执行链：原子写入；返回链：日志记录
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using Microsoft.Extensions.Logging;
using TaskStatus = io.NET.ZTR_OS.Features.Scheduler.Models.TaskStatus;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

/// <summary>
/// 调度任务持久化服务接口
/// </summary>
public interface ISchedulerStorageService
{
    IReadOnlyList<ScheduledTask> LoadAll();
    void SaveAll(IEnumerable<ScheduledTask> tasks);
    Task SaveAllAsync(IEnumerable<ScheduledTask> tasks, CancellationToken ct = default);
}

/// <summary>
/// 调度任务持久化服务
/// </summary>
public class SchedulerStorageService : ISchedulerStorageService
{
    private readonly ILogger<SchedulerStorageService> _logger;
    private readonly string _storagePath;
    /// <summary>保存写门闸：SchedulerService 在任务执行 finally 中可能并发调 SaveAll/SaveAllAsync，
    /// 多个线程同时写同一 .tmp 路径会互相覆盖/抛异常（因果链竞态）。
    /// 用 SemaphoreSlim 而非 Monitor —— 因为 SaveAllAsync 在临界区内 await，Monitor 跨 await 会在
    /// 非持有线程上 Exit 抛 SynchronizationLockException（执行链 bug）。SemaphoreSlim 是异步安全的。</summary>
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public SchedulerStorageService(ILogger<SchedulerStorageService> logger, string storagePath)
    {
        _logger = logger;
        _storagePath = storagePath;
    }

    /// <summary>
    /// 加载所有已保存的任务
    /// </summary>
    public IReadOnlyList<ScheduledTask> LoadAll()
    {
        _logger.LogInformation("[SchedStorage] Loading tasks from {Path}", _storagePath);
        
        try
        {
            if (!File.Exists(_storagePath))
            {
                _logger.LogInformation("[SchedStorage] No saved tasks found");
                return new List<ScheduledTask>();
            }

            var json = File.ReadAllText(_storagePath);
            var tasks = JsonSerializer.Deserialize<List<ScheduledTask>>(json, _jsonOptions);
            
            if (tasks == null || !tasks.Any())
            {
                _logger.LogInformation("[SchedStorage] No tasks in file");
                return new List<ScheduledTask>();
            }
            
            _logger.LogInformation("[SchedStorage] Loaded {Count} tasks", tasks.Count);
            return tasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SchedStorage] Failed to load tasks");
            return new List<ScheduledTask>();
        }
    }

    /// <summary>
    /// 保存所有任务
    /// </summary>
    public void SaveAll(IEnumerable<ScheduledTask> tasks)
    {
        var taskList = tasks.ToList();
        _logger.LogInformation("[SchedStorage] Saving {Count} tasks to {Path}", taskList.Count, _storagePath);
        
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 深拷贝（JSON 往返）后在副本上清理运行时字段——绝不污染调用方内存对象。
            // 原实现直接 task.NextRunTime = null 修改内存同引用：SchedulerService 刚
            // CalculateNextRunTime 出的 NextRunTime 被抹掉，ScanAndExecute 永不命中，
            // 添加/更新后的任务永远不到点自动执行（仅 RunNow 有效）。
            var snapshotJson = JsonSerializer.Serialize(taskList, _jsonOptions);
            var snapshot = JsonSerializer.Deserialize<List<ScheduledTask>>(snapshotJson, _jsonOptions)
                           ?? new List<ScheduledTask>();
            foreach (var task in snapshot)
            {
                task.NextRunTime = null;
                task.LastRunTime = null;
                task.LastStatus = TaskStatus.Idle;
            }

            // 串行化写入，防止并发 SaveAll 竞争同一 .tmp 路径
            _saveGate.Wait();
            try
            {
                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                var tempPath = _storagePath + ".tmp";

                File.WriteAllText(tempPath, json);

                if (File.Exists(_storagePath))
                {
                    File.Delete(_storagePath);
                }
                File.Move(tempPath, _storagePath);
            }
            finally
            {
                _saveGate.Release();
            }

            _logger.LogInformation("[SchedStorage] Saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SchedStorage] Failed to save tasks");
            throw;
        }
    }

    /// <summary>
    /// 异步保存所有任务
    /// </summary>
    public async Task SaveAllAsync(IEnumerable<ScheduledTask> tasks, CancellationToken ct = default)
    {
        var taskList = tasks.ToList();
        _logger.LogInformation("[SchedStorage] Async saving {Count} tasks...", taskList.Count);
        
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 深拷贝（JSON 往返）后在副本上清理运行时字段——不污染调用方内存对象（同 SaveAll 修复）
            var snapshotJson = JsonSerializer.Serialize(taskList, _jsonOptions);
            var snapshot = JsonSerializer.Deserialize<List<ScheduledTask>>(snapshotJson, _jsonOptions)
                           ?? new List<ScheduledTask>();
            foreach (var task in snapshot)
            {
                task.NextRunTime = null;
                task.LastRunTime = null;
                task.LastStatus = TaskStatus.Idle;
            }

            // 串行化写入，防止并发 SaveAllAsync 竞争同一 .tmp 路径
            // 注意：此处 await 必须在 SemaphoreSlim 内（不能用 Monitor/lock —— 跨 await 会抛
            // SynchronizationLockException）。SemaphoreSlim.WaitAsync 可安全跨异步等待。
            await _saveGate.WaitAsync(ct);
            try
            {
                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                var tempPath = _storagePath + ".tmp";

                await File.WriteAllTextAsync(tempPath, json, ct);

                if (File.Exists(_storagePath))
                {
                    File.Delete(_storagePath);
                }
                File.Move(tempPath, _storagePath);
            }
            finally
            {
                _saveGate.Release();
            }

            _logger.LogInformation("[SchedStorage] Async saved successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[SchedStorage] Save cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SchedStorage] Failed to async save tasks");
            throw;
        }
    }
}
