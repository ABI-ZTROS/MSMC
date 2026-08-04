using io.NET.ZTR_OS.Features.Startup.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

/// <summary>🧪 TDD GREEN: MetricsPersistenceService 降采样 + 24h 1440 点窗口测试</summary>
public class MetricsDownsamplingTests
{
    [Fact]
    public void DownsampleToOneMinuteBuckets_24Hours_ReturnsExactly1440PointsOrLess()
    {
        var svc = new MetricsPersistenceService(new TimeService());

        // 伪造 24h * 3600s = 86400 秒每秒一个点 —— 降采样到每分钟 = 1440 点
        var raw = GenerateRawPoints(86400, TimeSpan.FromSeconds(1));

        var down = MetricsDownsampler.DownsampleToOneMinuteBuckets(raw);

        Assert.True(down.Count <= 1440,
            $"24h 按分钟降采样后最多 1440 点，实际: {down.Count}");
    }

    [Fact]
    public void OneMinuteBucket_AggregatesMeanCpuAndMemory()
    {
        // 1 分钟 60 个点: CPU 0..59 线性 → 平均值 = (0+59)/2 = 29.5
        var points = new List<MetricsSample>(60);
        for (int i = 0; i < 60; i++)
            points.Add(new MetricsSample(
                DateTime.UnixEpoch.AddSeconds(i),
                CpuPercent: i,
                MemoryPercent: 100 - i));

        var down = MetricsDownsampler.DownsampleToOneMinuteBuckets(points);

        Assert.Single(down); // 60 秒 = 1 分钟
        var bucket = down[0];
        Assert.Equal(29.5, bucket.CpuPercent, precision: 1);
        Assert.Equal(70.5, bucket.MemoryPercent, precision: 1);
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyDownsample()
    {
        var down = MetricsDownsampler.DownsampleToOneMinuteBuckets([]);
        Assert.Empty(down);
    }

    // 🧪 辅助生成器
    private static List<MetricsSample> GenerateRawPoints(int count, TimeSpan step)
    {
        var list = new List<MetricsSample>(count);
        var start = DateTime.UtcNow.Date;
        var rnd = new Random(42);
        for (int i = 0; i < count; i++)
        {
            list.Add(new MetricsSample(
                Timestamp: start.AddTicks(step.Ticks * i),
                CpuPercent: Math.Clamp(50 + Math.Sin(i * 0.01) * 30 + rnd.NextDouble() * 5, 0, 100),
                MemoryPercent: Math.Clamp(60 + Math.Cos(i * 0.005) * 15, 0, 100)));
        }
        return list;
    }
}
