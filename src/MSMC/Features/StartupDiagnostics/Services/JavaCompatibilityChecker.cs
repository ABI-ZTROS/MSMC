using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;

namespace io.NET.ZTR_OS.Features.StartupDiagnostics.Services;

public class JavaCompatibilityChecker
{
    private readonly Dictionary<(string Core, string McVersion), (int JavaMajor, string Note)> _versionMap;

    public JavaCompatibilityChecker()
    {
        _versionMap = new Dictionary<(string Core, string McVersion), (int JavaMajor, string Note)>
        {
            { ("paper", "1.21.*"), (21, "Paper 1.21.x 建议 Java 21") },
            { ("purpur", "1.21.*"), (21, "Purpur 1.21.x 建议 Java 21") },
            { ("folia", "1.21.*"), (21, "Folia 1.21.x 建议 Java 21") },
            { ("vanilla", "1.20.5+"), (21, "Vanilla 1.20.5+ 建议 Java 21") },
            { ("fabric", "1.20.5+"), (21, "Fabric 1.20.5+ 建议 Java 21") },
            { ("forge", "1.20.5+"), (21, "Forge 1.20.5+ 建议 Java 21") },
            { ("neoforge", "1.20.5+"), (21, "NeoForge 1.20.5+ 建议 Java 21") },
            { ("any", "1.20.4或更早"), (17, "通用 Java 17 兼容") }
        };
    }

    public StartupDiagnosis? Check(string coreType, string mcVersion, int installedJavaMajor)
    {
        if (string.IsNullOrWhiteSpace(coreType) || string.IsNullOrWhiteSpace(mcVersion))
            return null;

        var core = coreType.ToLowerInvariant();
        var (requiredMajor, note) = FindBestMatch(core, mcVersion);

        if (installedJavaMajor >= requiredMajor)
            return null;

        string coreName = coreType switch
        {
            "paper" => "Paper",
            "purpur" => "Purpur",
            "folia" => "Folia",
            "vanilla" => "Vanilla",
            "fabric" => "Fabric",
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            _ => coreType
        };

        string shortVersion = GetShortVersion(mcVersion);
        string description = $"{coreName} {shortVersion} 建议 Java {requiredMajor}，当前 Java {installedJavaMajor}";

        return new StartupDiagnosis(
            Severity: DiagnosisSeverity.Warning,
            Description: description,
            SuggestedAction: $"切换到 Java {requiredMajor} 以获得最佳兼容性",
            OneClickFixCommandId: requiredMajor == 21 ? "switch-java-21" : $"switch-java-{requiredMajor}");
    }

    private (int JavaMajor, string Note) FindBestMatch(string core, string mcVersion)
    {
        bool is121x = IsMatch(mcVersion, "1.21.*");
        bool is1205Plus = Is1205OrLater(mcVersion);

        var specificKey1 = (core, "1.21.*");
        var specificKey2 = (core, "1.20.5+");

        if (is121x && _versionMap.TryGetValue(specificKey1, out var r1))
            return r1;
        if (is1205Plus && _versionMap.TryGetValue(specificKey2, out var r2))
            return r2;

        var anyKey1 = ("any", "1.21.*");
        var anyKey2 = ("any", "1.20.5+");
        if (is121x && _versionMap.TryGetValue(anyKey1, out var r3))
            return r3;
        if (is1205Plus && _versionMap.TryGetValue(anyKey2, out var r4))
            return r4;

        return _versionMap[("any", "1.20.4或更早")];
    }

    private static bool IsMatch(string version, string pattern)
    {
        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern[..^2];
            return version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(version, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Is1205OrLater(string version)
    {
        if (!Version.TryParse(version, out var ver))
            return false;
        var compare = new Version(1, 20, 5);
        return ver >= compare;
    }

    private static string GetShortVersion(string mcVersion)
    {
        if (mcVersion.StartsWith("1.21"))
            return "1.21.x";
        if (Is1205OrLater(mcVersion))
            return "1.20.5+";
        return "1.20.4或更早";
    }
}
