using McServerGuard.Constants;
using McServerGuard.Services.ServerDetection;
using Xunit;

namespace McServerGuard.Tests.Services;

/// <summary>🧪 命令行解析器测试 —— 验证我们能把那些乱七八糟的 Java 命令行给理清楚</summary>
public class CommandLineParserTests
{
    // ──────────────── 基础 Vanilla 服务器命令行 ────────────────

    [Fact]
    public void Parse_VanillaServerCommand_ExtractsCorrectFields()
    {
        // 🎮 最经典的 Vanilla 命令行 —— 简单纯粹，就像原版生存模式
        var cmd = @"java -Xms2G -Xmx4G -XX:+UseG1GC -jar minecraft_server.1.21.4.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("java", result.JavaPath);
        Assert.Equal("minecraft_server.1.21.4.jar", result.JarFileName);
        Assert.Equal("minecraft_server.1.21.4.jar", result.JarFilePath);
        Assert.Equal(2L * 1024 * 1024 * 1024, result.InitialHeapMemoryBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.Equal("G1GC", result.GcType);
        Assert.True(result.HasNoGui);
        Assert.False(result.HasClientMarkers);
        Assert.False(result.UsesAikarFlags);
        Assert.Contains("nogui", result.ServerArguments);
    }

    // ──────────────── Paper + Aikar 优化标志 ────────────────

    [Fact]
    public void Parse_PaperAikarCommand_DetectsAikarFlags()
    {
        // ⚡ Paper 服务器配 Aikar 标志 —— 性能优化的"黄金配方"
        var cmd = @"java -Xms4G -Xmx4G -XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1 -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar paper-1.21.4-439.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("paper-1.21.4-439.jar", result.JarFileName);
        Assert.True(result.UsesAikarFlags);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.Equal("G1GC", result.GcType);
        Assert.True(result.HasNoGui);
    }

    // ──────────────── 自定义 Java 路径 ────────────────

    [Fact]
    public void Parse_CustomJavaPath_ExtractsQuotedPath()
    {
        // 📂 Java 装在 Program Files 里 —— 没引号的话命令行就炸了
        var cmd = @"""C:\Program Files\Java\jdk-21\bin\java.exe"" -Xms2G -Xmx4G -jar server.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("C:\\Program Files\\Java\\jdk-21\\bin\\java.exe", result.JavaPath);
        Assert.Equal("server.jar", result.JarFileName);
        Assert.Equal(2L * 1024 * 1024 * 1024, result.InitialHeapMemoryBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── 环境变量路径 ────────────────

    [Fact]
    public void Parse_EnvVarJavaPath_PreservesEnvVarInPath()
    {
        // 🌍 用了 %JAVA_HOME% 环境变量 —— 跨平台脚本的标准操作
        var cmd = @"%JAVA_HOME%\bin\java.exe -Xms1G -Xmx8G -jar spigot-1.21.4.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("%JAVA_HOME%\\bin\\java.exe", result.JavaPath);
        Assert.Equal("spigot-1.21.4.jar", result.JarFileName);
        Assert.Equal(8L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── Fabric 服务器 ────────────────

    [Fact]
    public void Parse_FabricServerCommand_IdentifiesFabricJar()
    {
        // 🧵 Fabric 服务器 —— 轻量级模组加载器，用 fabric-server-launch.jar
        var cmd = @"java -Xms2G -Xmx6G -XX:+UseZGC -jar fabric-server-launch.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("fabric-server-launch.jar", result.JarFileName);
        Assert.Equal("ZGC", result.GcType);
        Assert.True(result.HasNoGui);
        Assert.Equal(6L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── Forge 服务器 ────────────────

    [Fact]
    public void Parse_ForgeServerCommand_IdentifiesForgeJar()
    {
        // 🔨 Forge 服务器 —— 老牌模组加载器，JAR 名字带 forge
        var cmd = @"""C:\Java\bin\java.exe"" -Xms4G -Xmx8G -XX:+UseG1GC -jar forge-1.21.4-52.0.28.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("C:\\Java\\bin\\java.exe", result.JavaPath);
        Assert.Equal("forge-1.21.4-52.0.28.jar", result.JarFileName);
        Assert.Equal(8L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.Equal("G1GC", result.GcType);
    }

    // ──────────────── 没有 nogui 的命令行（还是服务器） ────────────────

    [Fact]
    public void Parse_ServerWithoutNoGui_HasNoGuiIsFalse()
    {
        // 🤷 有些新手开服没写 nogui —— 不加也能跑，只是会弹出个无用的 GUI 窗口
        var cmd = @"java -Xms1G -Xmx2G -jar minecraft_server.1.21.4.jar";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("minecraft_server.1.21.4.jar", result.JarFileName);
        Assert.False(result.HasNoGui);
        Assert.False(result.HasClientMarkers);
    }

    // ──────────────── 客户端命令行检测 ────────────────

    [Fact]
    public void Parse_ClientCommand_DetectsClientMarkers()
    {
        // 🎮 客户端命令行 —— 带有 --accessToken、--version 等标志，一看就是玩家在启动游戏
        var cmd = @"""C:\Program Files\Java\jdk-21\bin\javaw.exe"" --version 1.21.4 --accessToken abcdef123456 --userType mojang --assetsDir ""C:\Users\Player\.minecraft\assets"" --gameDir ""C:\Users\Player\.minecraft"" --assetsIndex 21 -XX:HeapDumpPath=""C:\Users\Player\.minecraft\java_crash.hprof"" -Xmx2G -XX:+UseG1GC -jar ""C:\Users\Player\.minecraft\versions\1.21.4\1.21.4.jar""";

        var result = CommandLineParser.Parse(cmd);

        Assert.True(result.HasClientMarkers);
        // 客户端的 JAR 名称通常不包含服务器关键词，所以不应匹配
        Assert.Equal("1.21.4.jar", result.JarFileName);
    }

    // ──────────────── 空命令行和边界情况 ────────────────

    [Fact]
    public void Parse_EmptyCommandLine_ReturnsEmptyResult()
    {
        // 🕳️ 空命令行 —— 什么都没有，解析出来也是空的（废话）
        var result = CommandLineParser.Parse("");

        Assert.Equal(string.Empty, result.JavaPath);
        Assert.Equal(string.Empty, result.JarFileName);
        Assert.Equal(0, result.MaxHeapMemoryBytes);
        Assert.False(result.HasClientMarkers);
    }

    [Fact]
    public void Parse_NullCommandLine_ReturnsEmptyResult()
    {
        // 👻 null 命令行 —— 防御性编程，不能让人随便传个 null 就炸了
        var result = CommandLineParser.Parse(null!);

        Assert.Equal(string.Empty, result.JavaPath);
        Assert.Equal(0, result.MaxHeapMemoryBytes);
    }

    // ──────────────── ParseMemoryValue 测试 ────────────────

    [Theory]
    [InlineData("4G", 4L * 1024 * 1024 * 1024)]
    [InlineData("8G", 8L * 1024 * 1024 * 1024)]
    [InlineData("1024M", 1024L * 1024 * 1024)]
    [InlineData("512m", 512L * 1024 * 1024)]
    [InlineData("4096K", 4096L * 1024)]
    [InlineData("2KB", 2L * 1024)]
    [InlineData("1GB", 1L * 1024 * 1024 * 1024)]
    [InlineData("16gb", 16L * 1024 * 1024 * 1024)]
    public void ParseMemoryValue_ValidInputs_ReturnsCorrectBytes(string input, long expectedBytes)
    {
        // 📏 各种内存写法都要能正确解析
        Assert.Equal(expectedBytes, CommandLineParser.ParseMemoryValue(input));
    }

    [Fact]
    public void ParseMemoryValue_PureNumber_TreatsAsMegabytes()
    {
        // 🔢 纯数字 → 默认当 MB 处理 —— 这是行业惯例（虽然不太友好）
        Assert.Equal(4096L * 1024 * 1024, CommandLineParser.ParseMemoryValue("4096"));
    }

    [Fact]
    public void ParseMemoryValue_InvalidInput_ReturnsZero()
    {
        // 🚫 胡乱写的内容 → 返回 0（垃圾进，零出）
        Assert.Equal(0L, CommandLineParser.ParseMemoryValue("abc"));
        Assert.Equal(0L, CommandLineParser.ParseMemoryValue(""));
        Assert.Equal(0L, CommandLineParser.ParseMemoryValue("  "));
    }

    // ──────────────── GC 类型检测 ────────────────

    [Fact]
    public void Parse_ShenandoahGC_DetectsCorrectly()
    {
        // 🌲 ShenandoahGC —— 红帽出品的低延迟 GC，适合对延迟敏感的场景
        var cmd = @"java -Xms2G -Xmx4G -XX:+UseShenandoahGC -jar server.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("ShenandoahGC", result.GcType);
    }

    [Fact]
    public void Parse_ParallelGC_DetectsCorrectly()
    {
        // 🏎️ ParallelGC —— 追求吞吐量的老牌 GC
        var cmd = @"java -Xms2G -Xmx4G -XX:+UseParallelGC -jar server.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("ParallelGC", result.GcType);
    }

    [Fact]
    public void Parse_NoGCFlag_DefaultsToEmpty()
    {
        // 🤷 没指定 GC → 返回空字符串（Java 会用默认的，我们也不操心）
        var cmd = @"java -Xms2G -Xmx4G -jar server.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal(string.Empty, result.GcType);
    }

    // ──────────────── 引号内空格处理 ────────────────

    [Fact]
    public void Parse_JarPathWithSpaces_HandlesQuotesCorrectly()
    {
        // 📂 JAR 文件名里有空格 —— 虽然罕见，但我们要处理
        var cmd = @"java -Xms2G -Xmx4G -jar ""my server.jar"" nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("my server.jar", result.JarFileName);
        Assert.True(result.HasNoGui);
    }

    // ──────────────── JVM 参数列表 ────────────────

    [Fact]
    public void Parse_JvmArguments_CollectsAllJvmFlags()
    {
        // 📋 确保所有 JVM 参数都被收集起来（不含 -jar 后面的部分）
        var cmd = @"java -Xms2G -Xmx4G -XX:+UseG1GC -XX:MaxGCPauseMillis=200 -jar paper.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Contains("-Xms2G", result.JvmArguments);
        Assert.Contains("-Xmx4G", result.JvmArguments);
        Assert.Contains("-XX:+UseG1GC", result.JvmArguments);
        Assert.Contains("-XX:MaxGCPauseMillis=200", result.JvmArguments);
        // nogui 和 JAR 路径不应出现在 JVM 参数中
        Assert.DoesNotContain("nogui", result.JvmArguments);
        Assert.DoesNotContain("paper.jar", result.JvmArguments);
    }

    // ──────────────── Metaspace 参数解析 ────────────────

    [Fact]
    public void Parse_MetaspaceArgs_IncludedInJvmArguments()
    {
        // 🧠 Metaspace 参数 —— 装载了太多模组时可能需要调大这个
        var cmd = @"java -Xms2G -Xmx4G -XX:MetaspaceSize=256M -XX:MaxMetaspaceSize=512M -jar forge.jar nogui";

        var result = CommandLineParser.Parse(cmd);

        Assert.Contains("-XX:MetaspaceSize=256M", result.JvmArguments);
        Assert.Contains("-XX:MaxMetaspaceSize=512M", result.JvmArguments);
    }

    // ──────────────── 服务器参数列表 ────────────────

    [Fact]
    public void Parse_ServerArguments_CollectsPostJarArgs()
    {
        // 📋 -jar 之后的所有参数都应该归入 ServerArguments
        var cmd = @"java -Xms2G -Xmx4G -jar server.jar nogui --world myworld --port 25566";

        var result = CommandLineParser.Parse(cmd);

        Assert.Equal(5, result.ServerArguments.Count);
        Assert.Contains("nogui", result.ServerArguments);
        Assert.Contains("--world", result.ServerArguments);
        Assert.Contains("myworld", result.ServerArguments);
        Assert.Contains("--port", result.ServerArguments);
        Assert.Contains("25566", result.ServerArguments);
    }
}
