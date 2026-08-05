// 🧪 插件管理单元测试
namespace io.NET.ZTR_OS.Tests.Services;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using io.NET.ZTR_OS.Features.PluginManager.Models;
using io.NET.ZTR_OS.Features.PluginManager.Services;
using Xunit;

/// <summary>
/// PluginManager 的单元测试 🎯
/// </summary>
public class PluginManagerTests
{
    [Fact]
    public void PluginYmlParser_ValidJar_ReturnsPluginInfo()
    {
        // Arrange —— 创建临时目录并生成含 plugin.yml 的 jar
        var tempDir = Path.Combine(Path.GetTempPath(), $"msmc_plugin_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var jarPath = Path.Combine(tempDir, "TestPlugin.jar");
            const string ymlContent = """
                name: TestPlugin
                version: 1.0.0
                author: ABI
                main: com.test.Main
                description: '测试插件'
                """;
            CreateJarWithPluginYml(jarPath, ymlContent);

            // Act
            var info = PluginYmlParser.ParseJar(jarPath);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.IsValid);
            Assert.Equal("TestPlugin", info.Name);
            Assert.Equal("1.0.0", info.Version);
            Assert.Equal("ABI", info.Author);
            Assert.Equal("com.test.Main", info.Main);
            Assert.Equal("测试插件", info.Description);
            Assert.Equal(jarPath, info.FilePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PluginManagerService_EnableDisabledJar_RenamesCorrectly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"msmc_plugin_enable_{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            var disabledPath = Path.Combine(pluginsDir, "A.jar.disabled");
            File.WriteAllText(disabledPath, "dummy");
            var svc = new PluginManagerService();

            // Act
            var result = svc.TogglePlugin(disabledPath, enable: true);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "A.jar"));
            Assert.False(File.Exists(disabledPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PluginManagerService_DisableEnabledJar_AddsDisabledSuffix()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"msmc_plugin_disable_{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            var jarPath = Path.Combine(pluginsDir, "B.jar");
            File.WriteAllText(jarPath, "dummy");
            var svc = new PluginManagerService();

            // Act
            var result = svc.TogglePlugin(jarPath, enable: false);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "B.jar.disabled")));
            Assert.False(File.Exists(jarPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PluginManagerService_Toggle_Idempotent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"msmc_plugin_idem_{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            var jarPath = Path.Combine(pluginsDir, "A.jar");
            File.WriteAllText(jarPath, "dummy");
            var svc = new PluginManagerService();

            // Act —— Toggle 两次
            svc.TogglePlugin(jarPath);
            var afterFirst = File.Exists(Path.Combine(pluginsDir, "A.jar.disabled"));

            var disabledPath = Path.Combine(pluginsDir, "A.jar.disabled");
            svc.TogglePlugin(disabledPath);
            var afterSecond = File.Exists(Path.Combine(pluginsDir, "A.jar"));

            // Assert —— 回到原状态
            Assert.True(afterFirst, "第一次 Toggle 后应为 .disabled");
            Assert.True(afterSecond, "第二次 Toggle 后应恢复为 .jar");
            Assert.False(File.Exists(disabledPath), "第二次 Toggle 后 .disabled 应不存在");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PluginManagerService_Scan_MultipleJarsAndDisabled()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"msmc_plugin_scan_{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            // A.jar —— 真 plugin，含 plugin.yml
            var aYml = """
                name: PluginA
                version: 2.0.0
                author: DevA
                main: com.a.Main
                description: A 插件
                """;
            CreateJarWithPluginYml(Path.Combine(pluginsDir, "A.jar"), aYml);

            // B.jar —— 无 plugin.yml
            File.WriteAllText(Path.Combine(pluginsDir, "B.jar"), "not a plugin");

            // C.jar.disabled —— 禁用状态
            File.WriteAllText(Path.Combine(pluginsDir, "C.jar.disabled"), "disabled plugin");

            var svc = new PluginManagerService();

            // Act
            var items = svc.ScanPlugins(pluginsDir);

            // Assert
            Assert.Equal(3, items.Count);
            var a = items.FirstOrDefault(i => i.FilePath.EndsWith("A.jar") || items.FirstOrDefault(i => i.Name == "PluginA");
            var b = items.FirstOrDefault(i => i.FilePath.EndsWith("B.jar"));
            var c = items.FirstOrDefault(i => i.FilePath.EndsWith("C.jar.disabled"));
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.NotNull(c);
            Assert.True(a.Enabled, "A.Enabled=true");
            Assert.True(a.IsValid, "A.IsValid=true");
            Assert.True(b.Enabled, "B.Enabled=true (无法解析但启用)");
            Assert.False(b.IsValid, "B.IsValid=false (无 yml)");
            Assert.False(c.Enabled, "C.Enabled=false");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// 辅助方法：用 ZipArchive 创建含 plugin.yml 的最小 jar
    /// </summary>
    private static void CreateJarWithPluginYml(string jarPath, string ymlContent)
    {
        using var fs = File.Create(jarPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("plugin.yml");
        using var sw = new StreamWriter(entry.Open());
        sw.Write(ymlContent);
    }
}
