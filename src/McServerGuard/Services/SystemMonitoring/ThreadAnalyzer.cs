// -----------------------------------------------------------------------------
// 文件名: ThreadAnalyzer.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 线程分析器，分析 Java 进程的线程使用情况并提供评估结论
// 依赖组件: System.Diagnostics, System.Runtime.InteropServices, Serilog
// 设计模式: 分析器模式、分级评估、策略模式（多方案降级）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

/// <summary>
/// 线程分析结果记录 —— Java 进程线程使用情况的分析快照
/// </summary>
/// <param name="TotalThreads">该进程的总线程数</param>
/// <param name="ThreadPercent">占系统总线程数的百分比</param>
/// <param name="ThreadsPerCore">每逻辑核平均线程数</param>
/// <param name="Assessment">评估结论文本</param>
public record ThreadAnalysisResult(
    int TotalThreads,
    double ThreadPercent,
    double ThreadsPerCore,
    string Assessment
);

/// <summary>
/// 线程分析器
/// </summary>
/// <remarks>
/// <para>分析 Java 进程的线程使用密度，结合逻辑核心数与系统总线程数
/// 提供多维度的线程健康度评估。</para>
/// <para>核心能力：
///   - 逻辑处理器核心数获取（Windows 平台使用 GetSystemInfo，跨平台回退至 Environment）
///   - 系统总线程数统计（PerformanceCounter）
///   - 基于每核线程数阈值的分级评估机制
/// </para>
/// </remarks>
public class ThreadAnalyzer
{
    /// <summary>
    /// 获取逻辑处理器核心数量
    /// </summary>
    /// <returns>逻辑处理器核心数</returns>
    /// <remarks>
    /// Windows 平台优先使用 kernel32.dll 的 GetSystemInfo 以保证精度；
    /// 非 Windows 平台或调用失败时回退至 <see cref="Environment.ProcessorCount"/>。
    /// </remarks>
    public int GetLogicalProcessorCount()
    {
        Log.Debug("ThreadAnalyzer: GetLogicalProcessorCount");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                GetSystemInfo(out var sysInfo);
                return (int)sysInfo.dwNumberOfProcessors;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "线程分析失败: {Message}", ex.Message);
            }
        }

        return Environment.ProcessorCount;
    }

    /// <summary>
    /// 获取系统总线程数
    /// </summary>
    /// <returns>系统总线程数；获取失败返回 0</returns>
    /// <remarks>
    /// 使用 System.Threading 性能计数器实现，Linux 平台不可用。
    /// </remarks>
    public int GetTotalThreadCount()
    {
        Log.Debug("ThreadAnalyzer: GetTotalThreadCount");

        try
        {
            using var threadCounter = new PerformanceCounter(
                "System", "Threads", readOnly: true);

            return (int)threadCounter.NextValue();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "线程分析失败: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// 分析指定 Java 进程的线程使用情况
    /// </summary>
    /// <param name="processId">Java 进程的 PID</param>
    /// <returns>线程分析结果对象</returns>
    public ThreadAnalysisResult AnalyzeJavaThreads(int processId)
    {
        Log.Debug("分析 Java 进程线程: PID={Pid}", processId);

        try
        {
            var process = Process.GetProcessById(processId);
            var threadCount = process.Threads.Count;
            var logicalCores = GetLogicalProcessorCount();
            var totalThreads = GetTotalThreadCount();

            var threadPercent = totalThreads > 0
                ? Math.Round((double)threadCount / totalThreads * 100, 2)
                : 0;

            var threadsPerCore = logicalCores > 0
                ? Math.Round((double)threadCount / logicalCores, 2)
                : 0;

            var assessment = GenerateAssessment(threadCount, threadsPerCore, logicalCores);

            Log.Debug("Java 进程 PID={Pid}: {Threads} 线程", processId, threadCount);

            return new ThreadAnalysisResult(
                TotalThreads: threadCount,
                ThreadPercent: threadPercent,
                ThreadsPerCore: threadsPerCore,
                Assessment: assessment
            );
        }
        catch (ArgumentException)
        {
            Log.Warning("进程 PID={Pid} 不存在或已退出", processId);
            return new ThreadAnalysisResult(0, 0, 0, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "线程分析失败: {Message}", ex.Message);
            return new ThreadAnalysisResult(0, 0, 0, $"分析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据线程数和每核线程数生成分级评估结论
    /// </summary>
    /// <param name="threadCount">总线程数</param>
    /// <param name="threadsPerCore">每核线程数</param>
    /// <param name="logicalCores">逻辑核心数</param>
    /// <returns>评估结论文本</returns>
    /// <remarks>
    /// 阈值基于经验设定，仅供参考：
    ///   - &lt; 10 线程/核：正常
    ///   - &lt; 30 线程/核：偏高，可能存在线程泄漏风险
    ///   - &lt; 60 线程/核：过高，建议检查插件线程泄漏
    ///   - ≥ 60 线程/核：异常偏高，需立即排查
    /// </remarks>
    private static string GenerateAssessment(int threadCount, double threadsPerCore, int logicalCores)
    {
        return threadsPerCore switch
        {
            < 10 => $"线程数正常 ({threadCount} 个线程，{logicalCores} 核)",
            < 30 => $"线程数偏高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)，可能存在线程泄漏风险",
            < 60 => $"线程数过高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)！建议检查是否有插件导致的线程泄漏",
            _ => $"线程数异常偏高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)！！这是在开线程工厂吗？立即检查！"
        };
    }

    #region P/Invoke 声明

    /// <summary>
    /// SYSTEM_INFO 结构体 —— Windows 系统硬件信息
    /// </summary>
    /// <remarks>
    /// 本实现仅使用 dwNumberOfProcessors 字段，但结构体必须完整声明以匹配
    /// GetSystemInfo 的输出格式。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO
    {
        /// <summary>处理器架构</summary>
        internal ushort wProcessorArchitecture;

        /// <summary>保留字段</summary>
        internal ushort wReserved;

        /// <summary>页面大小</summary>
        internal uint dwPageSize;

        /// <summary>最小应用程序地址</summary>
        internal IntPtr lpMinimumApplicationAddress;

        /// <summary>最大应用程序地址</summary>
        internal IntPtr lpMaximumApplicationAddress;

        /// <summary>活动处理器掩码</summary>
        internal IntPtr dwActiveProcessorMask;

        /// <summary>逻辑处理器数量</summary>
        internal uint dwNumberOfProcessors;

        /// <summary>处理器类型</summary>
        internal uint dwProcessorType;

        /// <summary>虚拟地址分配粒度</summary>
        internal uint dwAllocationGranularity;

        /// <summary>处理器级别</summary>
        internal ushort wProcessorLevel;

        /// <summary>处理器修订版</summary>
        internal ushort wProcessorRevision;
    }

    /// <summary>
    /// kernel32.dll 的 GetSystemInfo 函数
    /// </summary>
    /// <param name="lpSystemInfo">SYSTEM_INFO 结构输出参数</param>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    #endregion
}
