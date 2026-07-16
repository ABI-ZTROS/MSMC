using McServerGuard.Services.ServerDetection;
using Xunit;

namespace McServerGuard.Tests.Services;

/// <summary>🧪 启动脚本检测器测试 —— 验证我们通过各种"脚本指纹"来识别 MC 服务器启动脚本</summary>
public class StartupScriptDetectorTests
{
    // ──────────────── 基础服务器启动脚本 ────────────────

    [Fact]
    public void Analyze_BasicServerScript_DetectsAsStartupScript()
    {
        // 📜 最基本的启动脚本 —— java + -jar + nogui，三件套齐活
        var script = """
            #!/bin/bash
            java -Xms2G -Xmx4G -XX:+UseG1GC -jar minecraft_server.1.21.4.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.MatchedRules.Count >= 2);
        Assert.Equal("minecraft_server.1.21.4.jar", result.ServerJarName);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.False(result.HasAutoRestart);
        Assert.False(result.UsesAikarFlags);
    }

    // ──────────────── 自动重启循环 ────────────────

    [Fact]
    public void Analyze_RestartLoopScript_DetectsAutoRestart()
    {
        // 🔄 经典的 while true 重启脚本 —— 服务器崩溃了？不怕，我们拉起来继续干
        var script = """
            #!/bin/bash
            while true
            do
                java -Xms4G -Xmx4G -XX:+UseG1GC -jar paper-1.21.4-439.jar nogui
                echo "Server crashed, restarting..."
                sleep 10
            done
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.HasAutoRestart);
        Assert.Equal("paper-1.21.4-439.jar", result.ServerJarName);
    }

    // ──────────────── Aikar 优化标志检测 ────────────────

    [Fact]
    public void Analyze_AikarFlagsScript_DetectsAikarFlags()
    {
        // ⚡ 带 Aikar 优化标志的脚本 —— Paper 服务器的标准配置
        var script = """
            #!/bin/bash
            java -Xms4G -Xmx4G -XX:+UseG1GC -XX:+ParallelRefProcEnabled -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar paper-1.21.4.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.UsesAikarFlags);
        Assert.Equal("paper-1.21.4.jar", result.ServerJarName);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── 非服务器脚本 ────────────────

    [Fact]
    public void Analyze_NonServerScript_NotDetectedAsStartupScript()
    {
        // 📄 这就是个普通的系统脚本 —— 跟 MC 服务器八竿子打不着
        var script = """
            #!/bin/bash
            echo "Hello World"
            date
            ls -la
            echo "This is just a maintenance script, nothing to see here"
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.False(result.IsServerStartupScript);
        // 最多可能命中 0 条规则
        Assert.True(result.MatchedRules.Count < 2);
    }

    // ──────────────── 自定义 Java 路径 ────────────────

    [Fact]
    public void Analyze_CustomJavaPath_ExtractsQuotedPath()
    {
        // 📂 Java 装在 Program Files 里，用引号包裹路径
        var script = """
            @echo off
            "C:\Program Files\Java\jdk-21\bin\java.exe" -Xms2G -Xmx8G -XX:+UseG1GC -jar forge-1.21.4.jar nogui
            pause
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.Contains("java.exe", result.JavaPath);
        Assert.Equal("forge-1.21.4.jar", result.ServerJarName);
        Assert.Equal(8L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── 环境变量 Java 路径 ────────────────

    [Fact]
    public void Analyze_EnvVarJavaPath_ExtractsEnvVarPath()
    {
        // 🌍 使用 %JAVA_HOME% 环境变量的 Windows bat 脚本
        var script = """
            @echo off
            %JAVA_HOME%\bin\java.exe -Xms2G -Xmx4G -jar spigot-1.21.4.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.Contains("JAVA_HOME", result.JavaPath);
        Assert.Equal("spigot-1.21.4.jar", result.ServerJarName);
    }

    [Fact]
    public void Analyze_LinuxEnvVarJavaPath_ExtractsDollarPath()
    {
        // 🐧 Linux 下用 $JAVA_HOME 的写法
        var script = """
            #!/bin/bash
            $JAVA_HOME/bin/java -Xms2G -Xmx4G -jar spigot-1.21.4.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.Contains("JAVA_HOME", result.JavaPath);
        Assert.Equal("spigot-1.21.4.jar", result.ServerJarName);
    }

    // ──────────────── Fabric 服务器脚本 ────────────────

    [Fact]
    public void Analyze_FabricServerScript_IdentifiesFabricJar()
    {
        // 🧵 Fabric 服务器的启动脚本
        var script = """
            #!/bin/bash
            java -Xms2G -Xmx6G -XX:+UseZGC -jar fabric-server-launch.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("fabric-server-launch.jar", result.ServerJarName);
        Assert.Equal(6L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── Forge 服务器脚本 ────────────────

    [Fact]
    public void Analyze_ForgeServerScript_IdentifiesForgeJar()
    {
        // 🔨 Forge 服务器启动脚本 —— 参数通常比 Vanilla 多不少
        var script = """
            #!/bin/bash
            java -Xms4G -Xmx8G -XX:+UseG1GC -XX:MetaspaceSize=256M -XX:MaxMetaspaceSize=512M -jar forge-1.21.4-52.0.28.jar nogui
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("forge-1.21.4-52.0.28.jar", result.ServerJarName);
        Assert.Equal(8L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
    }

    // ──────────────── 空内容 ────────────────

    [Fact]
    public void Analyze_EmptyContent_ReturnsNonStartupScript()
    {
        // 🕳️ 空内容 → 不是启动脚本（废话）
        var result = StartupScriptDetector.Analyze("");

        Assert.False(result.IsServerStartupScript);
        Assert.Empty(result.MatchedRules);
    }

    // ──────────────── 仅命中一条规则 ────────────────

    [Fact]
    public void Analyze_SingleRuleMatch_NotDetectedAsStartupScript()
    {
        // 🔍 只命中一条规则 —— 不足以认定是启动脚本（可能只是巧合）
        var script = """
            echo "This script mentions java in passing"
            """;

        var result = StartupScriptDetector.Analyze(script);

        // 最多命中 "java-command" 一条，不够 2 条的门槛
        // （其实这个脚本连 java 都不算真正调用，但保险起见我们还是测一下）
        Assert.True(result.MatchedRules.Count < 2);
        Assert.False(result.IsServerStartupScript);
    }

    // ──────────────── 复杂多行脚本 ────────────────

    [Fact]
    public void Analyze_ComplexStartupScript_ExtractsAllInfo()
    {
        // 📋 复杂的多行脚本 —— 有注释、有判断、有重启
        var script = """
            #!/bin/bash
            # Minecraft Paper Server Startup Script
            # Author: Some Server Admin
            while true
            do
                echo "Starting Paper server..."
                java -Xms4G -Xmx8G \
                  -XX:+UseG1GC \
                  -XX:+ParallelRefProcEnabled \
                  -Dusing.aikars.flags=https://mcflags.emc.gs \
                  -Daikars.new.flags=true \
                  -jar paper-1.21.4-439.jar \
                  nogui
                echo "Server stopped. Restarting in 10 seconds..."
                sleep 10
            done
            """;

        var result = StartupScriptDetector.Analyze(script);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.HasAutoRestart);
        Assert.True(result.UsesAikarFlags);
        Assert.Equal("paper-1.21.4-439.jar", result.ServerJarName);
        Assert.Equal(8L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.True(result.MatchedRules.Count >= 4);
    }
}
