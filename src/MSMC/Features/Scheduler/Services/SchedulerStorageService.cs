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

            // 清理运行时状态（不持久化 NextRunTime 等计算值，启动时重新计算）
            foreach (var task in taskList)
            {
                task.NextRunTime = null;
                task.LastRunTime = null;
                task.LastStatus = TaskStatus.Idle;
            }

            var json = JsonSerializer.Serialize(taskList, _jsonOptions);
            var tempPath = _storagePath + ".tmp";
            
            File.WriteAllText(tempPath, json);
            
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
            File.Move(tempPath, _storagePath);
            
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

            foreach (var task in taskList)
            {
                task.NextRunTime = null;
                task.LastRunTime = null;
                task.LastStatus = TaskStatus.Idle;
            }

            var json = JsonSerializer.Serialize(taskList, _jsonOptions);
            var tempPath = _storagePath + ".tmp";
            
            await File.WriteAllTextAsync(tempPath, json, ct);
            
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
            File.Move(tempPath, _storagePath);
            
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
