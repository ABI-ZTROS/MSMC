// ═══════════════════════════════════════════════════════════════════════════════
// 🔧 ProcessSupervisorService — 基于 Win32 Job Objects 的服务器进程监管者
// ═══════════════════════════════════════════════════════════════════════════════
// 核心能力：
//   1. ✅ Job Object 包装 — 关闭 MSMC 主程序时，所有服务器 Java 进程被一起杀死（不僵尸）
//   2. ✅ 崩溃自动重启 — 指定「崩溃重启次数上限 + 冷却时间」（:start ... goto start 替代）
//   3. ✅ CPU 亲和性 + 优先级 — 大核优先绑定 + ABOVE_NORMAL 调度
//   4. ✅ 防止系统睡眠 — SetThreadExecutionState(Continuous | SystemRequired)
//   5. ✅ 任务栏闪烁 — FLASHW_TRAY (崩溃/异常时吸引注意)
//   6. ✅ psapi 精确内存查询 — PrivateBytes / WorkingSet / Pagefile 三位一体
//
// 与现有 ServerManagerService 的关系：
//   本服务是 ServerManagerService 的「下一层」：ServerManagerService 决定「启动哪台服」，
//   本服务负责「把进程创建好、关了随主程序死、崩溃自动拉、亲和性绑、电源不让睡」。
// ═══════════════════════════════════════════════════════════════════════════════

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using io.NET.ZTR_OS.Features.Shared.Native;
using Serilog;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

/// <summary>
/// 一个被监管的子进程的生命周期句柄。
/// 实现了 IAsyncDisposable：await using (var svc = supervisor.Launch(...)) { ... } 自动清理。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SupervisedProcessHandle : IAsyncDisposable, IDisposable
{
    private SafeJobHandle _job;
    private Process _process;
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly ILogger _log;
    private int _crashCount;
    private int _disposed;

    public int ProcessId => _process.Id;
    public bool HasExited => _process.HasExited;
    public int CrashCount => Volatile.Read(ref _crashCount);
    /// <summary>监管生命周期是否已被取消（用户主动 Stop / Dispose 后为 true）</summary>
    public bool IsCancellationRequested => _lifetimeCts.IsCancellationRequested;
    public event EventHandler<int>? ProcessExited;
    public event EventHandler<int>? ProcessCrashedAndWillRestart;

    /// <summary>启动时复制的监管策略（用于 UI 展示 & 序列化）</summary>
    public ProcessSupervisorOptions Options { get; }

    /// <summary>
    /// 崩溃后计划下次重启的 UTC 时间戳；
    /// 不在重启等待阶段（例如进程在运行中 / 已放弃重启 / 重启循环中）则为 null。
    /// </summary>
    public DateTime? ScheduledRestartAtUtc { get; internal set; }

    /// <summary>从 psapi 查询的最新 WorkingSet 字节数（0 表示不可用/未刷新）。</summary>
    public long LastWorkingSetBytes { get; internal set; }

    /// <summary>近 1 秒 CPU 百分比估算（0-100，-1 表示不可用）。</summary>
    public double LastCpuPercent { get; internal set; } = -1;

    internal SupervisedProcessHandle(
        SafeJobHandle job,
        Process process,
        CancellationTokenSource lifetimeCts,
        ILogger log,
        ProcessSupervisorOptions options)
    {
        _job = job;
        _process = process;
        _lifetimeCts = lifetimeCts;
        _log = log;
        Options = options;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        ProcessExited?.Invoke(this, _process.ExitCode);
        if (_process.ExitCode != 0 && !_lifetimeCts.IsCancellationRequested)
        {
            Interlocked.Increment(ref _crashCount);
            ProcessCrashedAndWillRestart?.Invoke(this, _process.ExitCode);
            _log.Warning("[Supervisor] PID={Pid} 异常退出 code={Code}（第 N={Count} 次崩溃）",
                _process.Id, _process.ExitCode, CrashCount);
        }
    }

    public void Terminate(uint exitCode = 0)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        try
        {
            _lifetimeCts.Cancel();
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[Supervisor] Terminate PID={Pid} 失败（可能已退出）", _process.Id);
        }
    }

    /// <summary>
    /// 内部方法：用新进程和新 Job 替换当前句柄（崩溃自动重启时调用）。
    /// 会取消旧进程的 Exited 事件订阅、Dispose 旧 Job，
    /// 并为新进程启用 EnableRaisingEvents + 订阅 Exited 事件。
    /// crashCount 保持累加，不会重置。
    /// </summary>
    internal void UpdateProcess(Process newProcess, SafeJobHandle newJob)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            // 已释放：直接清理新传入的资源，避免泄漏
            newProcess.Dispose();
            newJob.Dispose();
            return;
        }

        // 取消旧进程的事件订阅
        _process.Exited -= OnProcessExited;
        var oldProc = _process;
        var oldJob = _job;

        // 替换为新进程和新 Job
        _process = newProcess;
        _job = newJob;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        // 释放旧资源（旧进程可能已经退出，但仍需 Dispose 释放句柄）
        try { oldProc.Dispose(); } catch { /* ignore */ }
        try { oldJob.Dispose(); } catch { /* ignore */ }

        _log.Debug("[Supervisor] 进程句柄已更新：旧 PID={OldPid} → 新 PID={NewPid}", oldProc.Id, newProcess.Id);
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
        _process.Exited -= OnProcessExited;
        _lifetimeCts.Dispose();
        _process.Dispose();
        _job.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
        _process.Exited -= OnProcessExited;
        try { _lifetimeCts.Cancel(); } catch { /* ignore */ }
        // 给子进程 500ms 自己优雅退出（比如 java 的 shutdown hook）
        try
        {
            if (!_process.HasExited)
            {
                // .NET 9 的 Process.WaitForExitAsync 没有 TimeSpan 重载，用 CTS 模拟超时
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await _process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch { /* ignore */ }
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch { /* ignore */ }
        _lifetimeCts.Dispose();
        _process.Dispose();
        _job.Dispose();
    }
}

[SupportedOSPlatform("windows")]
public interface IProcessSupervisorService
{
    /// <summary>在 Job Object 下启动 Java 子进程，崩溃自动重启（策略可配）</summary>
    Task<SupervisedProcessHandle> LaunchSupervisedAsync(
        string executablePath,
        string arguments,
        string workingDirectory,
        ProcessSupervisorOptions options,
        CancellationToken ct = default);

    /// <summary>把一个已经运行的 PID 挂入监管（用于 ProcessScanner 导入）</summary>
    SupervisedProcessHandle AttachExisting(int pid, ProcessSupervisorOptions options);

    /// <summary>设置进程 CPU 亲和性（coreNumbers：逻辑核编号 0..N-1）</summary>
    bool SetProcessAffinity(int pid, IEnumerable<int> coreNumbers);

    /// <summary>获取 psapi 级精确内存（PrivateUsage + WorkingSet + Pagefile）</summary>
    (long PrivateBytes, long WorkingSet, long PagefileUsage) QueryProcessMemory(int pid);

    /// <summary>
    /// 设置「防止系统睡眠」状态（跑服/备份/下载时用）。
    /// 传 false 解除。返回 true 表示成功。
    /// </summary>
    bool PreventSystemSleep(bool enabled, bool alsoKeepDisplayOn = false);

    /// <summary>
    /// 闪烁主窗口任务栏（服务器崩溃 / 关键异常时吸引注意）。count=闪几次；ms=间隔。
    /// </summary>
    void FlashMainWindowTaskbar(IntPtr hWnd, uint count = 5, uint intervalMs = 250);
}

/// <summary>监管策略配置</summary>
public sealed record ProcessSupervisorOptions
{
    /// <summary>崩溃最大自动重启次数（0 = 关闭自动重启；int.MaxValue = 无限）</summary>
    public int MaxAutoRestartCount { get; init; } = 5;
    /// <summary>重启前冷却时间（毫秒），避免崩溃循环把系统打爆</summary>
    public int RestartCooldownMs { get; init; } = 3000;
    /// <summary>允许进程组 breakaway（Java 的子进程，如 Windows java.exe 偶尔需要）</summary>
    public bool AllowBreakaway { get; init; } = true;
    /// <summary>进程优先级</summary>
    public ProcessPriorityClass Priority { get; init; } = ProcessPriorityClass.AboveNormal;
    /// <summary>启动后设置 CPU 亲和性；null = 不设置</summary>
    public IReadOnlyList<int>? PreferredCores { get; init; }
    /// <summary>单进程内存上限（字节），0 = 不限（和 JVM -Xmx 无关，这是 OS 级别硬上限）</summary>
    public long MaxProcessMemoryBytes { get; init; }
    /// <summary>防止系统自动睡眠（长任务）</summary>
    public bool PreventSystemSleep { get; init; } = true;
    /// <summary>true = 启动时把此进程放入 Job（主程序关 → 一起杀）；false 只做崩溃重启</summary>
    public bool BindToJobObject { get; init; } = true;
}

[SupportedOSPlatform("windows")]
public sealed class ProcessSupervisorService : IProcessSupervisorService
{
    private readonly ILogger _log = Log.ForContext<ProcessSupervisorService>();
    private int _sleepRefCount;

    // ───────────────────────────────────────────────────────────────────────
    public async Task<SupervisedProcessHandle> LaunchSupervisedAsync(
        string executablePath,
        string arguments,
        string workingDirectory,
        ProcessSupervisorOptions options,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        options ??= new ProcessSupervisorOptions();

        if (options.PreventSystemSleep) PreventSystemSleep(enabled: true);

        SafeJobHandle? job = null;
        Process? process = null;
        var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            if (options.BindToJobObject)
            {
                job = CreateBoundJob(options);
                _log.Information("[Supervisor] Job Object 已创建，策略：Breakaway={B}, MemCap={M}MB",
                    options.AllowBreakaway, options.MaxProcessMemoryBytes >> 20);
            }

            var psi = new ProcessStartInfo(executablePath, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
            _log.Information("[Supervisor] 子进程启动 PID={Pid}: {Exe} {Args} @ {Cwd}",
                process.Id, Path.GetFileName(executablePath), arguments[..Math.Min(80, arguments.Length)], workingDirectory);

            // Job 绑定要尽早 — 在子进程可能再 fork 孙子进程之前
            if (job != null)
            {
                if (!NativeMethods.AssignProcessToJobObject(job, process.Handle))
                {
                    var err = Marshal.GetLastWin32Error();
                    // ERROR_ACCESS_DENIED (5) 很常见：子进程已被自己的 Job 套住了（例如管理员权限制约）
                    if (err != 5)
                        throw new Win32Exception(err, $"AssignProcessToJobObject 失败 (PID={process.Id})");
                    _log.Warning("[Supervisor] Job 绑定遭拒绝 (win32=5)，将跳过 Job，但仍保留崩溃重启策略");
                    job.Dispose();
                    job = null;
                }
                else _log.Debug("[Supervisor] Job 绑定成功 PID={Pid}", process.Id);
            }

            if (options.PreferredCores?.Count > 0)
                SetProcessAffinity(process.Id, options.PreferredCores);

            try { process.PriorityClass = options.Priority; }
            catch (Exception ex) { _log.Warning(ex, "[Supervisor] 设置进程优先级失败 PID={Pid}", process.Id); }

            var handle = new SupervisedProcessHandle(
                job ?? new SafeJobHandle(), // 没绑 Job 时给个空句柄（Dispose 不会空引用）
                process, lifetimeCts, _log, options);
            handle.ProcessCrashedAndWillRestart += (_, exitCode) =>
            {
                _ = OnRestartAsync(handle, executablePath, arguments, workingDirectory, options, exitCode)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _log.Error(t.Exception, "[Supervisor] 重启任务发生未观察异常");
                    }, TaskContinuationOptions.OnlyOnFaulted);
            };
            return handle;
        }
        catch
        {
            process?.Dispose();
            job?.Dispose();
            lifetimeCts.Dispose();
            // 发生异常时，如果我们之前加了睡眠锁，释放一次
            if (options.PreventSystemSleep) PreventSystemSleep(enabled: false);
            throw;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    private async Task OnRestartAsync(
        SupervisedProcessHandle handle,
        string exe, string args, string cwd, ProcessSupervisorOptions opts, int exitCode)
    {
        // 超过最大重启次数：解锁睡眠，发通知，不再拉
        if (handle.CrashCount > opts.MaxAutoRestartCount)
        {
            handle.ScheduledRestartAtUtc = null;
            _log.Error("[Supervisor] PID={Pid} 累计崩溃 {N} 次，超过上限 {Max}，放弃自动重启",
                handle.ProcessId, handle.CrashCount, opts.MaxAutoRestartCount);
            if (opts.PreventSystemSleep) PreventSystemSleep(enabled: false);
            return;
        }

        try
        {
            // ✅ 先写入「计划下次重启时间」，UI 层可显示倒数
            handle.ScheduledRestartAtUtc = DateTime.UtcNow.AddMilliseconds(opts.RestartCooldownMs);

            // 冷却时间：避免 1s 10 次重启打爆磁盘/CPU
            await Task.Delay(opts.RestartCooldownMs).ConfigureAwait(false);

            // 如果此时用户已主动 Stop/Dispose（生命周期取消），立刻退出，不再拉起
            if (handle.IsCancellationRequested)
            {
                handle.ScheduledRestartAtUtc = null;
                return;
            }

            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var newProc = Process.Start(psi);
            if (newProc == null)
            {
                _log.Error("[Supervisor] 重启失败：Process.Start 返回 null");
                handle.ScheduledRestartAtUtc = null;
                return;
            }
            _log.Information("[Supervisor] 第 {N} 次重启成功 → 新 PID={NewPid}", handle.CrashCount, newProc.Id);

            // 新进程也进 Job（注意：不要 using，handle.UpdateProcess 会接管所有权）
            SafeJobHandle? job = null;
            if (opts.BindToJobObject)
            {
                job = CreateBoundJob(opts);
                if (!NativeMethods.AssignProcessToJobObject(job, newProc.Handle))
                {
                    var err = Marshal.GetLastWin32Error();
                    _log.Warning("[Supervisor] 重启进程 Job 绑定失败 win32={E}", err);
                    job.Dispose();
                    job = null;
                }
            }

            // 用新进程和新 Job 更新句柄（替换 _process/_job、转移 Exited 事件订阅）
            handle.UpdateProcess(newProc, job ?? new SafeJobHandle());

            // 重启成功，清除「计划重启时间」
            handle.ScheduledRestartAtUtc = null;
        }
        catch (Exception ex)
        {
            handle.ScheduledRestartAtUtc = null;
            _log.Error(ex, "[Supervisor] 自动重启失败 code={Code}", exitCode);
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    private static SafeJobHandle CreateBoundJob(ProcessSupervisorOptions opts)
    {
        SECURITY_ATTRIBUTES sa = default;
        sa.nLength = Marshal.SizeOf(sa);
        var job = NativeMethods.CreateJobObjectW(ref sa, lpName: null);
        if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject 失败");

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = default;
        info.BasicLimitInformation.LimitFlags =
            JobObjectLimits.KillOnJobClose |
            JobObjectLimits.LimitDieOnUnhandledException;
        if (opts.AllowBreakaway)
            info.BasicLimitInformation.LimitFlags |= JobObjectLimits.BreakawayOk;
        if (opts.MaxProcessMemoryBytes > 0)
        {
            info.BasicLimitInformation.LimitFlags |= JobObjectLimits.LimitProcessMemory;
            info.ProcessMemoryLimit = (UIntPtr)opts.MaxProcessMemoryBytes;
        }

        if (!NativeMethods.SetInformationJobObject(
                job,
                NativeMethods.JobObjectExtendedLimitInformation,
                ref info,
                Marshal.SizeOf(info)))
        {
            job.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject 失败");
        }
        return job;
    }

    // ───────────────────────────────────────────────────────────────────────
    public SupervisedProcessHandle AttachExisting(int pid, ProcessSupervisorOptions options)
    {
        if (options.PreventSystemSleep) PreventSystemSleep(enabled: true);
        var proc = Process.GetProcessById(pid);
        SafeJobHandle job = CreateBoundJob(options);
        if (!NativeMethods.AssignProcessToJobObject(job, proc.Handle))
        {
            var err = Marshal.GetLastWin32Error();
            if (err != 5) // 5=已在其它Job中，可忽略
                _log.Warning("[Supervisor] Attach PID={Pid} → Job 失败 win32={E}", pid, err);
            job.Dispose();
            job = new SafeJobHandle();
        }
        if (options.PreferredCores?.Count > 0) SetProcessAffinity(pid, options.PreferredCores);
        try { proc.PriorityClass = options.Priority; } catch { /* ignore */ }
        return new SupervisedProcessHandle(job, proc, new CancellationTokenSource(), _log, options);
    }

    // ───────────────────────────────────────────────────────────────────────
    public bool SetProcessAffinity(int pid, IEnumerable<int> coreNumbers)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            ulong mask = 0;
            foreach (var c in coreNumbers)
            {
                if (c is < 0 or >= 64) throw new ArgumentOutOfRangeException(nameof(coreNumbers), c, "core 必须在 [0,64)");
                mask |= 1UL << c;
            }
            if (mask == 0) return false;
            proc.ProcessorAffinity = (IntPtr)mask;
            _log.Debug("[Supervisor] PID={Pid} 亲和性掩码=0x{M:X16}", pid, mask);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[Supervisor] SetProcessAffinity 失败 PID={Pid}", pid);
            return false;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public (long PrivateBytes, long WorkingSet, long PagefileUsage) QueryProcessMemory(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            NativeMethods.GetProcessMemoryInfo(proc.SafeHandle, out var counters, Marshal.SizeOf<NativeMethods.PROCESS_MEMORY_COUNTERS_EX>());
            return ((long)counters.PrivateUsage, (long)counters.WorkingSetSize, (long)counters.PagefileUsage);
        }
        catch (Exception ex)
        {
            _log.Verbose(ex, "[Supervisor] QueryProcessMemory 失败 PID={Pid}", pid);
            return default;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public bool PreventSystemSleep(bool enabled, bool alsoKeepDisplayOn = false)
    {
        // 参考计数：多次 enable = 只发一次 ES_CONTINUOUS；最后一次 disable 才真的释放
        ExecutionState state;
        if (enabled)
        {
            Interlocked.Increment(ref _sleepRefCount);
            state = ExecutionState.Continuous | ExecutionState.SystemRequired;
            if (alsoKeepDisplayOn) state |= ExecutionState.DisplayRequired;
        }
        else
        {
            var count = Interlocked.Decrement(ref _sleepRefCount);
            if (count > 0) return true; // 还有人持有锁
            if (count < 0) { Interlocked.Increment(ref _sleepRefCount); return false; }
            state = ExecutionState.Continuous; // 单纯 Continuous 解除所有锁
        }

        var result = NativeMethods.SetThreadExecutionState(state);
        if (result == 0)
        {
            _log.Warning("[Supervisor] SetThreadExecutionState({State}) 失败", state);
            return false;
        }
        _log.Debug("[Supervisor] 电源策略 → {State}, refCount={N}", state, Volatile.Read(ref _sleepRefCount));
        return true;
    }

    // ───────────────────────────────────────────────────────────────────────
    public void FlashMainWindowTaskbar(IntPtr hWnd, uint count = 5, uint intervalMs = 250)
    {
        if (hWnd == IntPtr.Zero) return;
        NativeMethods.FLASHWINFO f = default;
        f.cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>();
        f.hwnd = hWnd;
        f.dwFlags = NativeMethods.FLASHW_TRAY | NativeMethods.FLASHW_TIMERNOFG;
        f.uCount = count;
        f.dwTimeout = intervalMs;
        _ = NativeMethods.FlashWindowEx(ref f);
    }
}
