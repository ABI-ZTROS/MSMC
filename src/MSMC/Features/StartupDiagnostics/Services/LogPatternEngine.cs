using System.Text.RegularExpressions;
using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;

namespace io.NET.ZTR_OS.Features.StartupDiagnostics.Services;

public class LogPatternEngine
{
    private readonly List<(Regex Regex, DiagnosisSeverity Severity, string HumanMessage, string RepairCommandId, string Action)> _rules;

    public LogPatternEngine()
    {
        _rules =
        [
            (
                new Regex(@"BindException.*Address already in use|Failed to bind to port\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Critical,
                "端口25565被占用",
                "kill-port-25565",
                "关闭占用端口的进程后重试"
            ),
            (
                new Regex(@"UnsupportedClassVersionError.*major version\s*65", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Critical,
                "需要Java 21",
                "switch-java-21",
                "切换到 Java 21 运行环境"
            ),
            (
                new Regex(@"agree to the EULA|EULA.*false|eula\.txt", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Critical,
                "未同意 EULA",
                "agree-eula",
                "设置 eula.txt 中 eula=true"
            ),
            (
                new Regex(@"OutOfMemoryError.*Java heap space", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Warning,
                "堆内存不足，建议增加最大内存",
                "increase-memory-2g",
                "将最大内存 Xmx 增加 2G"
            ),
            (
                new Regex(@"World folder lock detected|files in use.*world|session\.lock", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Warning,
                "世界文件夹被锁定，可能有重复进程运行",
                "kill-duplicate-process",
                "杀掉同目录下重复的服务器进程"
            ),
            (
                new Regex(@"Failed to verify username|authlib.*failed|AuthLib", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Warning,
                "正版验证失败，网络可能不通",
                "temp-disable-online-mode",
                "临时关闭在线模式或检查网络"
            ),
            (
                new Regex(@"JLine|Ansi.*disabled|terminal.*jansi", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                DiagnosisSeverity.Info,
                "终端 ANSI 颜色输出异常",
                "disable-ansi-terminal",
                "添加 -Dterminal.ansi=disabled 参数"
            )
        ];
    }

    public List<StartupDiagnosis> AnalyzeLog(string logTail)
    {
        var results = new List<StartupDiagnosis>();
        if (string.IsNullOrWhiteSpace(logTail))
            return results;

        foreach (var (regex, severity, humanMessage, repairCommandId, action) in _rules)
        {
            var match = regex.Match(logTail);
            if (!match.Success)
                continue;

            string description = humanMessage;
            if (repairCommandId == "kill-port-25565" && match.Groups.Count > 1 && match.Groups[1].Success)
            {
                var port = match.Groups[1].Value;
                description = $"端口{port}被占用";
            }

            results.Add(new StartupDiagnosis(
                Severity: severity,
                Description: description,
                SuggestedAction: action,
                OneClickFixCommandId: repairCommandId));
        }

        return results;
    }
}
