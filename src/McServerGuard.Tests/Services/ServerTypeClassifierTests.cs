using McServerGuard.Constants;
using McServerGuard.Services.ServerDetection;
using Xunit;

namespace McServerGuard.Tests.Services;

/// <summary>🧪 服务器类型分类器测试 —— 验证我们能不能准确判断每个 JAR 文件的"门派"</summary>
public class ServerTypeClassifierTests
{
    // ──────────────── 按 JAR 名称分类 ────────────────

    [Fact]
    public void ClassifyByJarName_VanillaJar_ReturnsVanilla()
    {
        // 🎮 Vanilla 的标准命名 —— minecraft_server.版本号.jar
        Assert.Equal(ServerType.Vanilla, ServerTypeClassifier.ClassifyByJarName("minecraft_server.1.21.4.jar"));
    }

    [Fact]
    public void ClassifyByJarName_VanillaServerJar_ReturnsVanilla()
    {
        // 🎮 也有叫 server.jar 的 Vanilla
        Assert.Equal(ServerType.Vanilla, ServerTypeClassifier.ClassifyByJarName("server.jar"));
    }

    [Fact]
    public void ClassifyByJarName_SpigotJar_ReturnsSpigot()
    {
        // 🔧 Spigot —— 经典的 Bukkit 替代品
        Assert.Equal(ServerType.Spigot, ServerTypeClassifier.ClassifyByJarName("spigot-1.21.4.jar"));
        Assert.Equal(ServerType.Spigot, ServerTypeClassifier.ClassifyByJarName("spigot.jar"));
    }

    [Fact]
    public void ClassifyByJarName_PaperJar_ReturnsPaper()
    {
        // 📄 Paper —— Spigot 的高性能分支，MC 服务器圈的当红炸子鸡
        Assert.Equal(ServerType.Paper, ServerTypeClassifier.ClassifyByJarName("paper-1.21.4-439.jar"));
        Assert.Equal(ServerType.Paper, ServerTypeClassifier.ClassifyByJarName("paper.jar"));
    }

    [Fact]
    public void ClassifyByJarName_ForgeJar_ReturnsForge()
    {
        // 🔨 Forge —— 老牌模组加载器，Mod 生态最庞大
        Assert.Equal(ServerType.Forge, ServerTypeClassifier.ClassifyByJarName("forge-1.21.4-52.0.28.jar"));
        Assert.Equal(ServerType.Forge, ServerTypeClassifier.ClassifyByJarName("forge.jar"));
    }

    [Fact]
    public void ClassifyByJarName_FabricJar_ReturnsFabric()
    {
        // 🧵 Fabric —— 新兴的轻量级模组加载器，加载速度快
        Assert.Equal(ServerType.Fabric, ServerTypeClassifier.ClassifyByJarName("fabric-server-launch.jar"));
        Assert.Equal(ServerType.Fabric, ServerTypeClassifier.ClassifyByJarName("fabric-server.jar"));
    }

    [Fact]
    public void ClassifyByJarName_BukkitJar_ReturnsBukkit()
    {
        // 🧩 Bukkit —— 元老级服务器模组 API（虽然已经不更新了）
        Assert.Equal(ServerType.Bukkit, ServerTypeClassifier.ClassifyByJarName("craftbukkit-1.8.8.jar"));
    }

    // ──────────────── 未知 JAR ────────────────

    [Fact]
    public void ClassifyByJarName_UnknownJar_ReturnsUnknown()
    {
        // 🤷 不知道什么鬼 JAR —— 不在我们的识别范围内
        Assert.Equal(ServerType.Unknown, ServerTypeClassifier.ClassifyByJarName("random-app.jar"));
        Assert.Equal(ServerType.Unknown, ServerTypeClassifier.ClassifyByJarName("totally-not-mc.jar"));
    }

    [Fact]
    public void ClassifyByJarName_EmptyString_ReturnsUnknown()
    {
        // 🕳️ 空字符串 → 未知（空 JAR 名是什么鬼？）
        Assert.Equal(ServerType.Unknown, ServerTypeClassifier.ClassifyByJarName(""));
    }

    [Fact]
    public void ClassifyByJarName_NullInput_ReturnsUnknown()
    {
        // 👻 null 输入 → 也返回 Unknown（防御性编程）
        Assert.Equal(ServerType.Unknown, ServerTypeClassifier.ClassifyByJarName(null!));
    }

    // ──────────────── 通配符匹配 (MatchesAny) ────────────────

    [Fact]
    public void MatchesAny_StarWildcard_MatchesAnySuffix()
    {
        // ⭐ 通配符 * 匹配任意字符序列
        Assert.True(ServerTypeClassifier.MatchesAny("paper-1.21.4-439.jar", "paper-*.jar"));
        Assert.True(ServerTypeClassifier.MatchesAny("minecraft_server.1.21.4.jar", "minecraft_server.*.jar"));
        Assert.True(ServerTypeClassifier.MatchesAny("forge-1.21.4-52.0.28.jar", "forge-*.jar"));
    }

    [Fact]
    public void MatchesAny_ExactMatch_ReturnsTrue()
    {
        // 🎯 精确匹配
        Assert.True(ServerTypeClassifier.MatchesAny("spigot.jar", "spigot.jar"));
        Assert.True(ServerTypeClassifier.MatchesAny("server.jar", "server.jar"));
    }

    [Fact]
    public void MatchesAny_CaseInsensitive_ReturnsTrue()
    {
        // 🔠 大小写不敏感
        Assert.True(ServerTypeClassifier.MatchesAny("PAPER-1.21.4.jar", "paper-*.jar"));
        Assert.True(ServerTypeClassifier.MatchesAny("Spigot.Jar", "spigot.jar"));
    }

    [Fact]
    public void MatchesAny_NoMatch_ReturnsFalse()
    {
        // ❌ 不匹配
        Assert.False(ServerTypeClassifier.MatchesAny("paper.jar", "forge-*.jar"));
        Assert.False(ServerTypeClassifier.MatchesAny("random.jar", "spigot.jar"));
    }

    [Fact]
    public void MatchesAny_EmptyInput_ReturnsFalse()
    {
        // 🕳️ 空输入 → 匹配失败
        Assert.False(ServerTypeClassifier.MatchesAny("", "paper-*.jar"));
        Assert.False(ServerTypeClassifier.MatchesAny("paper.jar", ""));
    }

    [Fact]
    public void MatchesAny_QuestionMarkWildcard_MatchesSingleChar()
    {
        // ❓ 问号通配符匹配单个字符
        Assert.True(ServerTypeClassifier.MatchesAny("paper-1.jar", "paper-?.jar"));
        Assert.False(ServerTypeClassifier.MatchesAny("paper-12.jar", "paper-?.jar"));
    }

    // ──────────────── 通用 JAR + 配置文件推断 ────────────────

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_PaperConfigFiles_ReturnsPaper()
    {
        // 📁 通用 JAR 名 + Paper 配置文件 → 推断为 Paper
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            var configDir = System.IO.Path.Combine(tempDir, "config");
            System.IO.Directory.CreateDirectory(configDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(configDir, "paper-global.yml"), "# Paper config");

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("server.jar", tempDir);
            Assert.Equal(ServerType.Paper, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_SpigotConfigFiles_ReturnsSpigot()
    {
        // 📁 通用 JAR 名 + spigot.yml → 推断为 Spigot
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            System.IO.Directory.CreateDirectory(tempDir); // 👈 忘了创建目录，文件往哪写啊喂
            System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "spigot.yml"), "# Spigot config");
            System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "bukkit.yml"), "# Bukkit config");

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("server.jar", tempDir);
            Assert.Equal(ServerType.Spigot, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_ForgeConfigFiles_ReturnsForge()
    {
        // 📁 通用 JAR 名 + mods 目录 → 推断为 Forge
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tempDir, "mods"));

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("server.jar", tempDir);
            Assert.Equal(ServerType.Forge, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_FabricConfigFiles_ReturnsFabric()
    {
        // 📁 通用 JAR 名 + fabric-server-launch.properties → 推断为 Fabric
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            System.IO.Directory.CreateDirectory(tempDir); // 👈 同上，创建目录先
            System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "fabric-server-launch.properties"), "# Fabric config");

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("server.jar", tempDir);
            Assert.Equal(ServerType.Fabric, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_SpecificJarName_IgnoresConfig()
    {
        // 🏷️ JAR 名称已经很明确了（paper-1.21.4.jar），不需要看配置文件
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            // 即使目录里也有 spigot.yml，但 JAR 名称明确是 Paper
            System.IO.Directory.CreateDirectory(tempDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "spigot.yml"), "# fake spigot config");

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("paper-1.21.4-439.jar", tempDir);
            Assert.Equal(ServerType.Paper, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_NoMatchingConfig_ReturnsJarBasedType()
    {
        // 📁 目录里什么配置文件都没有 —— 只能靠 JAR 名称来判断
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcg_test_{Guid.NewGuid():N}");
        try
        {
            System.IO.Directory.CreateDirectory(tempDir);

            var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("minecraft_server.1.21.4.jar", tempDir);
            Assert.Equal(ServerType.Vanilla, result);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClassifyByJarNameAndConfigFiles_NonExistentDir_ReturnsJarBasedType()
    {
        // 🕳️ 目录不存在 —— 只能靠 JAR 名称
        var result = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles("paper-1.21.4.jar", "/nonexistent/path");
        Assert.Equal(ServerType.Paper, result);
    }
}
