// -----------------------------------------------------------------------------
// 文件名: CrashTrackerService.cs
// 命名空间: io.NET.ZTR_OS.Features.SafeModeKeeper.Services
// 功能描述: 连续崩溃追踪服务（<10s 存活视为崩溃）
// 核心规则: 连续 3 次 < 10s 崩溃 → 触发安全模式 (SafeModeTriggered = true)
//           正常退出(存活≥10s 或 exitExpected=true) → 计数器清零
// 持久化: {serverDir}/.msmc/safemode.json
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SafeModeKeeper.Services;

using System;
using System.IO;
using System.Text.Json;
using io.NET.ZTR_OS.Features.SafeModeKeeper.Models;

/// <summary>
/// 崩溃追踪器：记录最近启动存活时长，判断是否触发安全模式
/// </summary>
public class CrashTrackerService
{
    /// <summary>崩溃判定阈值（ms）：小于此时长视为崩溃</summary>
    private const long CrashThresholdMs = 10_000L;

    /// <summary>连续崩溃触发阈值：达到此次数触发安全模式</summary>
    private const int TriggerStreak = 3;

    /// <summary>最多保留最近 N 次启动记录</summary>
    private const int MaxRecentRecords = 5;

    private readonly string _serverDir;
    private readonly string _stateDir;
    private readonly string _stateFile;
    private SafeModeState _state;

    /// <summary>
    /// 当前连续崩溃 streak（累计 <10s 的次数）
    /// </summary>
    public int CurrentCrashStreak => _state.CurrentCrashStreak;

    /// <summary>
    /// 安全模式是否已被触发（streak ≥ 3）
    /// </summary>
    public bool SafeModeTriggered => _state.SafeModeTriggered;

    /// <summary>
    /// 最近启动存活时长记录（只读视图）
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<long> RecentUptimesMs => _state.RecentUptimesMs;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serverDir">服务器目录（.msmc 子目录将创建在这里）</param>
    public CrashTrackerService(string serverDir)
    {
        _serverDir = serverDir ?? throw new ArgumentNullException(nameof(serverDir));
        _stateDir = Path.Combine(serverDir, ".msmc");
        _stateFile = Path.Combine(_stateDir, "safemode.json");
        _state = LoadState();
    }

    /// <summary>
    /// 记录一次启动/退出
    /// </summary>
    /// <param name="uptimeMs">本次存活时长（毫秒）</param>
    /// <param name="exitExpected">是否为预期正常退出（如用户手动 Stop）</param>
    public void Record(long uptimeMs, bool exitExpected = false)
    {
        // 1) 先把本次 uptime 写入最近记录（滚动窗口，最多 5 条）
        _state.RecentUptimesMs.Add(uptimeMs);
        if (_state.RecentUptimesMs.Count > MaxRecentRecords)
        {
            _state.RecentUptimesMs.RemoveRange(0, _state.RecentUptimesMs.Count - MaxRecentRecords);
        }

        // 2) 判断本次是否算「正常」
        bool isNormalExit = exitExpected || uptimeMs >= CrashThresholdMs;

        if (isNormalExit)
        {
            // 正常退出 → streak 清零
            _state.CurrentCrashStreak = 0;
        }
        else
        {
            // 崩溃 streak++
            _state.CurrentCrashStreak++;

            // 达到阈值 → 标记触发
            if (_state.CurrentCrashStreak >= TriggerStreak)
            {
                _state.SafeModeTriggered = true;
            }
        }

        SaveState();
    }

    /// <summary>
    /// 手动重置 streak 和触发状态（通常在 ExitSafeMode 成功还原后调用）
    /// </summary>
    public void Reset()
    {
        _state.CurrentCrashStreak = 0;
        _state.SafeModeTriggered = false;
        _state.RecentUptimesMs.Clear();
        SaveState();
    }

    // ═══════════════════════════════════════════════════════
    // 持久化
    // ═══════════════════════════════════════════════════════

    private SafeModeState LoadState()
    {
        try
        {
            if (!File.Exists(_stateFile))
                return new SafeModeState();

            var json = File.ReadAllText(_stateFile);
            var parsed = JsonSerializer.Deserialize<SafeModeState>(json);
            return parsed ?? new SafeModeState();
        }
        catch
        {
            // 文件损坏 → 回退空状态（不影响用户）
            return new SafeModeState();
        }
    }

    private void SaveState()
    {
        try
        {
            if (!Directory.Exists(_stateDir))
                Directory.CreateDirectory(_stateDir);

            var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(_stateFile, json);
        }
        catch
        {
            // 持久化失败不抛异常（避免反向让用户更崩溃）
        }
    }
}
