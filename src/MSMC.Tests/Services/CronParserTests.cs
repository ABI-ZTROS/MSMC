// -----------------------------------------------------------------------------
// 文件名: CronParserTests.cs
// 项目: MSMC.Tests
// 功能描述: Cron 表达式解析器单元测试 —— 验证因果链（表达式 → 时间）的正确性
// -----------------------------------------------------------------------------

using Xunit;
using io.NET.ZTR_OS.Features.Scheduler.Services;

namespace MSMC.Tests.Services;

public class CronParserTests
{
    [Fact]
    public void GetNextRunTime_EveryMinute_ReturnsOneMinuteLater()
    {
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("* * * * *", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 1, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_SpecificMinuteHour_ReturnsCorrect()
    {
        var from = new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 11 * * *", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_DailyAtMidnight_ReturnsNextDay()
    {
        var from = new DateTimeOffset(2026, 8, 15, 23, 30, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 0 * * *", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_Every15Minutes_ReturnsCorrect()
    {
        var from = new DateTimeOffset(2026, 8, 15, 10, 7, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("*/15 * * * *", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 15, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_Every2Hours_ReturnsCorrect()
    {
        var from = new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 */2 * * *", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 2, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_WeekdaysOnly_WeekendSkipsToMonday()
    {
        // Saturday (2026-08-15) — should skip to Monday
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 9 * * MON-FRI", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_WeekdayAbbreviations_ParseCorrectly()
    {
        // MON = 1
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero); // Saturday
        var result = CronParser.GetNextRunTime("0 9 * * MON", from);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunTime_NeverOnTheSameMinute_ReturnsFuture()
    {
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 30, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 * * * *", from);

        Assert.NotNull(result);
        Assert.True(result > from);
        Assert.Equal(0, result!.Value.Minute);
    }

    [Fact]
    public void GetNextRunTime_InvalidExpression_ReturnsNull()
    {
        var from = DateTimeOffset.UtcNow;
        Assert.Null(CronParser.GetNextRunTime("", from));
        Assert.Null(CronParser.GetNextRunTime("invalid", from));
        Assert.Null(CronParser.GetNextRunTime("1 2 3 4", from)); // only 4 fields
    }

    [Fact]
    public void GetNextRunTime_MultiValueField_ReturnsListMatches()
    {
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        var result = CronParser.GetNextRunTime("0 1,3,5 * * *", from);

        Assert.NotNull(result);
        Assert.Contains(result!.Value.Hour, new[] { 1, 3, 5 });
        Assert.Equal(0, result.Value.Minute);
    }

    [Fact]
    public void IsValid_ValidExpression_ReturnsTrue()
    {
        Assert.True(CronParser.IsValid("* * * * *"));
        Assert.True(CronParser.IsValid("0 0 * * *"));
        Assert.True(CronParser.IsValid("*/5 */2 * * MON"));
    }

    [Fact]
    public void IsValid_InvalidExpression_ReturnsFalse()
    {
        Assert.False(CronParser.IsValid(""));
        Assert.False(CronParser.IsValid("bad expression"));
    }
}
