// 🧵 线程分析器 —— 你的 Java 服务器到底开了多少线程？是不是在开线程工厂？
// Minecraft 服务器是出了名的线程大户，动辄几百上千个线程
// 不过也别太担心，人家 Java 本来就是线程池的忠实粉丝 🏊
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

/// <summary>
/// 线程分析结果 record —— 分析一个 Java 进程的线程使用情况
/// </summary>
/// <param name="TotalThreads">该进程的总线程数</param>
/// <param name="ThreadPercent">占系统总线程数的百分比</param>
/// <param name="ThreadsPerCore">每核线程数</param>
/// <param name="Assessment">一句话评估结论（比如"线程数正常"或者"你这是在开线程工厂吗？"）</param>
public record ThreadAnalysisResult(
    int TotalThreads,
    double ThreadPercent,
    double ThreadsPerCore,
    string Assessment
);

/// <summary>
/// 线程分析器 —— 分析 Java 进程的线程使用情况
/// 结合逻辑核心数进行评估，告诉你"线程数是否正常"还是"线程数超标了"
/// </summary>
public class ThreadAnalyzer
{
    /// <summary>
    /// 获取逻辑处理器核心数
    /// 使用 kernel32.dll 的 GetSystemInfo —— 在 Windows 上最准确
    /// 退而求其次用 Environment.ProcessorCount（跨平台）
    /// </summary>
    public int GetLogicalProcessorCount()
    {
        // 日志：方法入口
        Log.Debug("🧵 ThreadAnalyzer: GetLogicalProcessorCount");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                GetSystemInfo(out var sysInfo);
                return (int)sysInfo.dwNumberOfProcessors;
            }
            catch (Exception ex)
            {
                // 日志：线程分析失败
                Log.Error(ex, "💥 fuck: 线程分析失败: {Message}", ex.Message);
            }
        }

        // 降级方案：使用 .NET 内置属性（跨平台）
        return Environment.ProcessorCount;
    }

    /// <summary>
    /// 获取系统总线程数
    /// 使用 PerformanceCounter —— Linux 上不可用
    /// </summary>
    public int GetTotalThreadCount()
    {
        // 日志：方法入口
        Log.Debug("🧵 ThreadAnalyzer: GetTotalThreadCount");

        try
        {
            using var threadCounter = new PerformanceCounter(
                "System", "Threads", readOnly: true);

            return (int)threadCounter.NextValue();
        }
        catch (Exception ex)
        {
            // 日志：线程分析失败
            Log.Error(ex, "💥 fuck: 线程分析失败: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// 分析指定 Java 进程的线程使用情况
    /// 返回一个 ThreadAnalysisResult，包含线程数、占比、评估等信息
    /// </summary>
    /// <param name="processId">Java 进程的 PID</param>
    /// <returns>线程分析结果</returns>
    public ThreadAnalysisResult AnalyzeJavaThreads(int processId)
    {
        // 日志：分析 Java 进程线程入口
        Log.Debug("🧵 分析 Java 进程线程: PID={Pid}", processId);

        try
        {
            var process = Process.GetProcessById(processId);
            var threadCount = process.Threads.Count;
            var logicalCores = GetLogicalProcessorCount();
            var totalThreads = GetTotalThreadCount();

            // 计算占比
            var threadPercent = totalThreads > 0
                ? Math.Round((double)threadCount / totalThreads * 100, 2)
                : 0;

            // 每核线程数
            var threadsPerCore = logicalCores > 0
                ? Math.Round((double)threadCount / logicalCores, 2)
                : 0;

            // 评估结论
            var assessment = GenerateAssessment(threadCount, threadsPerCore, logicalCores);

            // 日志：分析结果
            Log.Debug("📊 Java 进程 PID={Pid}: {Threads} 线程", processId, threadCount);

            return new ThreadAnalysisResult(
                TotalThreads: threadCount,
                ThreadPercent: threadPercent,
                ThreadsPerCore: threadsPerCore,
                Assessment: assessment
            );
        }
        catch (ArgumentException)
        {
            // 日志：进程不存在
            Log.Warning("⚠️ fuck: 进程 PID={Pid} 不存在或已退出", processId);
            return new ThreadAnalysisResult(0, 0, 0, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            // 日志：线程分析失败
            Log.Error(ex, "💥 fuck: 线程分析失败: {Message}", ex.Message);
            return new ThreadAnalysisResult(0, 0, 0, $"分析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据线程数和每核线程数生成评估结论
    /// 以下阈值是基于经验的，仅供参考 —— 每个服务器的情况不同
    /// </summary>
    private static string GenerateAssessment(int threadCount, double threadsPerCore, int logicalCores)
    {
        return threadsPerCore switch
        {
            < 10 => $"线程数正常 ({threadCount} 个线程，{logicalCores} 核)",
            < 30 => $"线程数偏高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)，可能存在线程泄漏风险",
            < 60 => $"线程数过高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)！建议检查是否有插件导致的线程泄漏",
            _ => $"线程数异常偏高 ({threadCount} 个线程，平均每核 {threadsPerCore:F1} 个)！！这是在开线程工厂吗？🧵🏭 立即检查！"
        };
    }

    #region P/Invoke 声明

    /// <summary>
    /// SYSTEM_INFO 结构体 —— Windows 系统信息
    /// 实际上我们只关心 dwNumberOfProcessors，但 GetSystemInfo 会填充整个结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO
    {
        /// <summary>处理器架构</summary>
        internal ushort wProcessorArchitecture;

        /// <summary>保留</summary>
        internal ushort wReserved;

        /// <summary>页面大小</summary>
        internal uint dwPageSize;

        /// <summary>最小地址</summary>
        internal IntPtr lpMinimumApplicationAddress;

        /// <summary>最大地址</summary>
        internal IntPtr lpMaximumApplicationAddress;

        /// <summary>地址对齐粒度</summary>
        internal IntPtr dwActiveProcessorMask;

        /// <summary>逻辑处理器数量 —— 这就是我们要的！</summary>
        internal uint dwNumberOfProcessors;

        /// <summary>处理器类型</summary>
        internal uint dwProcessorType;

        /// <summary>虚拟地址位数</summary>
        internal uint dwAllocationGranularity;

        /// <summary>处理器级别</summary>
        internal ushort wProcessorLevel;

        /// <summary>处理器修订版</summary>
        internal ushort wProcessorRevision;
    }

    /// <summary>
    /// kernel32.dll 的 GetSystemInfo
    /// 获取系统硬件信息 —— 这里我们只用来拿处理器核心数
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    #endregion
}
