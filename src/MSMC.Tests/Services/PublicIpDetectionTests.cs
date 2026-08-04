using io.NET.ZTR_OS.Features.NetworkMonitor.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

/// <summary>🧪 TDD RED: 公网 IP 检测（HTTP + STUN 双策略） —— README 写了但没有实现</summary>
public class PublicIpDetectionTests
{
    [Fact]
    public void PublicIpDetector_ClassExists_AndExposesDualStrategies()
    {
        // 🟥 RED: 目前完全不存在此类
        var detector = new PublicIpDetector();

        // 应有两种策略
        Assert.True(detector.AvailableStrategies.Length >= 2,
            $"README 要求 HTTP + STUN 双策略，实际只有 {detector.AvailableStrategies.Length} 种");
        Assert.Contains("HttpJson", detector.AvailableStrategies);
        Assert.Contains("StunUdp", detector.AvailableStrategies);
    }

    [Fact]
    public void ParseHttpJsonIpv4_CanParseTypicalResponses()
    {
        // 典型 ipify / ip-api 响应
        Assert.Equal("203.0.113.42",
            PublicIpDetector.TryParseIPv4FromJson("{\"ip\":\"203.0.113.42\"}"));
        // 包含多余字段也能提取
        Assert.Equal("198.51.100.8",
            PublicIpDetector.TryParseIPv4FromJson(
                "{\"status\":\"success\",\"country\":\"CN\",\"query\":\"198.51.100.8\",\"city\":\"Chengdu\"}"));
        // 纯文本响应
        Assert.Equal("192.0.2.1",
            PublicIpDetector.TryParseIPv4FromJson("  192.0.2.1  \n"));
    }

    [Fact]
    public void ParseHttpJsonIpv4_GarbageInput_ReturnsNull()
    {
        Assert.Null(PublicIpDetector.TryParseIPv4FromJson("not an ip"));
        Assert.Null(PublicIpDetector.TryParseIPv4FromJson("{\"ip\":\"999.999.999.999\"}"));
        Assert.Null(PublicIpDetector.TryParseIPv4FromJson(""));
    }
}
