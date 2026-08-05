// 🧪 SafeModeKeeper 单元测试（TDD RED 阶段）
// 覆盖崩溃追踪触发逻辑 + L1/L2/L3 三级降级策略与还原
namespace io.NET.ZTR_OS.Tests.Services;

using io.NET.ZTR_OS.Features.SafeModeKeeper.Services;
using io.NET.ZTR_OS.Features.SafeModeKeeper.Models;
using Xunit;

/// <summary>
/// SafeModeKeeper 安全模式模块测试 🛡️
/// 
/// 覆盖场景：
///   1) 连续 3 次 &lt; 10s 崩溃 → 触发安全模式
///   2) 仅 2 次崩溃 → 不触发
///   3) 正常退出（存活 &gt; 10s）→ 计数器清零
///   4) L1 策略：plugins/*.jar → *.jar.disabled，还原可逆
///   5) L2 策略：server.properties 3 键降级，还原可逆
///   6) L3 策略：写 jvm.args 文件
/// </summary>
public class SafeModeKeeperTests
{
    // ═══════════════════════════════════════════════════════════
    // 1️⃣ CrashTracker 核心逻辑测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CrashTracker_3CrashesUnder10s_TriggersSafeModeTrue()
    {
        // Arrange —— 准备临时目录 + 追踪器
        var serverDir = CreateTempServerDir();
        var tracker = new CrashTrackerService(serverDir);

        // Act —— 连续 3 次短存活崩溃（间隔均在 10s 内）
        tracker.Record(1200);    // 第 1 次：活了 1.2s 就崩
        tracker.Record(3500);    // 第 2 次：活了 3.5s 就崩
        tracker.Record(6500);    // 第 3 次：活了 6.5s 就崩 → 触发

        // Assert —— 确认触发状态
        Assert.True(tracker.SafeModeTriggered, "连续 3 次 <10s 崩溃应触发安全模式");
        Assert.Equal(3, tracker.CurrentCrashStreak);
    }

    [Fact]
    public void CrashTracker_Only2Crashes_NoTrigger()
    {
        // Arrange
        var serverDir = CreateTempServerDir();
        var tracker = new CrashTrackerService(serverDir);

        // Act —— 仅 2 次短存活崩溃，不足阈值
        tracker.Record(1500);
        tracker.Record(4000);

        // Assert —— 不应触发
        Assert.False(tracker.SafeModeTriggered, "仅 2 次崩溃不应触发安全模式");
        Assert.Equal(2, tracker.CurrentCrashStreak);
    }

    [Fact]
    public void CrashTracker_1CrashOver10s_ResetsCounter()
    {
        // Arrange —— 先制造一次崩溃 streak
        var serverDir = CreateTempServerDir();
        var tracker = new CrashTrackerService(serverDir);
        tracker.Record(2000);   // 先崩一次（<10s）
        Assert.Equal(1, tracker.CurrentCrashStreak);

        // Act —— 正常退出（存活 >10s 或显式 exitExpected=true）
        tracker.Record(12000, exitExpected: true);

        // Assert —— streak 应清零
        Assert.Equal(0, tracker.CurrentCrashStreak);
        Assert.False(tracker.SafeModeTriggered);
    }

    // ═══════════════════════════════════════════════════════════
    // 2️⃣ SafeModeBootstrapper L1/L2/L3 降级测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SafeModeBootstrapper_L1Strategy_DisablesPlugins()
    {
        // Arrange —— 造一个带 plugins 目录的服务器
        var serverDir = CreateTempServerDir();
        var pluginsDir = Path.Combine(serverDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        var jarA = Path.Combine(pluginsDir, "A.jar");
        var jarB = Path.Combine(pluginsDir, "B.jar");
        File.WriteAllText(jarA, "fake-jar-a");
        File.WriteAllText(jarB, "fake-jar-b");

        var bootstrapper = new SafeModeBootstrapper();

        // Act —— 应用 L1 降级策略
        bootstrapper.ApplyLevel(SafeModeLevel.L1, serverDir);

        // Assert L1 —— 原 jar 消失，disabled 出现
        Assert.False(File.Exists(jarA), "L1 后 A.jar 应被改名");
        Assert.False(File.Exists(jarB), "L1 后 B.jar 应被改名");
        Assert.True(File.Exists(jarA + ".disabled"), "A.jar.disabled 应存在");
        Assert.True(File.Exists(jarB + ".disabled"), "B.jar.disabled 应存在");

        // Act —— 还原（ExitSafeMode）
        bootstrapper.ExitSafeMode(serverDir);

        // Assert 还原 —— jar 恢复，disabled 消失
        Assert.True(File.Exists(jarA), "还原后 A.jar 应恢复");
        Assert.True(File.Exists(jarB), "还原后 B.jar 应恢复");
        Assert.False(File.Exists(jarA + ".disabled"), "还原后 A.jar.disabled 应消失");
        Assert.False(File.Exists(jarB + ".disabled"), "还原后 B.jar.disabled 应消失");
    }

    [Fact]
    public void SafeModeBootstrapper_L2Strategy_WritesServerProperties()
    {
        // Arrange —— 造默认 server.properties
        var serverDir = CreateTempServerDir();
        var propsPath = Path.Combine(serverDir, "server.properties");
        File.WriteAllLines(propsPath, new[]
        {
            "server-port=25565",
            "view-distance=10",
            "online-mode=true",
            "simulation-distance=10",
        });

        var bootstrapper = new SafeModeBootstrapper();

        // Act —— 应用 L2 降级
        bootstrapper.ApplyLevel(SafeModeLevel.L2, serverDir);

        // Assert L2 —— 3 键被降级
        var afterL2 = ReadProps(propsPath);
        Assert.Equal("2", afterL2["view-distance"]);
        Assert.Equal("2", afterL2["simulation-distance"]);
        Assert.Equal("false", afterL2["online-mode"]);
        Assert.Equal("25565", afterL2["server-port"]);  // 未被改动

        // Act —— 还原
        bootstrapper.ExitSafeMode(serverDir);

        // Assert 还原 —— 3 键回到原值
        var afterRestore = ReadProps(propsPath);
        Assert.Equal("10", afterRestore["view-distance"]);
        Assert.Equal("10", afterRestore["simulation-distance"]);
        Assert.Equal("true", afterRestore["online-mode"]);
        Assert.Equal("25565", afterRestore["server-port"]);
    }

    [Fact]
    public void SafeModeBootstrapper_L3Strategy_WritesJvmArgs()
    {
        // Arrange
        var serverDir = CreateTempServerDir();
        var bootstrapper = new SafeModeBootstrapper();

        // Act —— 应用 L3
        bootstrapper.ApplyLevel(SafeModeLevel.L3, serverDir);

        // Assert L3 —— jvm.args 文件存在且包含保守参数
        var jvmArgsPath = Path.Combine(serverDir, "jvm.args");
        Assert.True(File.Exists(jvmArgsPath), "L3 应生成 jvm.args 文件");
        var content = File.ReadAllText(jvmArgsPath);
        Assert.Contains("-XX:+UseSerialGC", content);
        Assert.Contains("-Xmx1G", content);

        // Act —— 还原（L3 的 jvm.args 文件应被清理或回到备份内容）
        bootstrapper.ExitSafeMode(serverDir);

        // Assert 还原 —— 若原本不存在则还原后应删除
        Assert.False(File.Exists(jvmArgsPath), "原本无 jvm.args 时还原后应删除");
    }

    // ═══════════════════════════════════════════════════════════
    // 🛠️ 辅助方法
    // ═══════════════════════════════════════════════════════════

    private static string CreateTempServerDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "msmc-safemode-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Dictionary<string, string> ReadProps(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!')) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var k = line[..eq].Trim();
            var v = line[(eq + 1)..].Trim();
            dict[k] = v;
        }
        return dict;
    }
}
