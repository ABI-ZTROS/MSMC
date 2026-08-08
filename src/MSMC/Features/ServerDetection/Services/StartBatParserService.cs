// -----------------------------------------------------------------------------
// 文件名: StartBatParserService.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Services
// 功能描述: 解析服务器目录下的 start.bat / run.bat 等启动脚本，提取 Java 启动参数
// 依赖组件: System.IO, System.Text.RegularExpressions
// 设计模式: 静态服务 + 正则提取
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

/// <summary>
/// start.bat 解析服务 —— 从启动脚本中提取 Java 命令行参数
/// </summary>
public static class StartBatParserService
{
    private static readonly string[] BatPatterns = ["start.bat", "run.bat", "start.cmd", "run.cmd"];

    /// <summary>
    /// 解析结果
    /// </summary>
    public sealed class ParseResult
    {
        public bool Success { get; set; }
        public string? JarPath { get; set; }
        public long? MaxHeapBytes { get; set; }
        public long? InitialHeapBytes { get; set; }
        public List<string> JvmArguments { get; set; } = [];
        public List<string> UnknownArgs { get; set; } = [];
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 从服务器目录解析 start.bat
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <returns>解析结果</returns>
    public static ParseResult ParseFromDirectory(string workingDirectory)
    {
        var result = new ParseResult();

        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            result.ErrorMessage = "目录不存在";
            return result;
        }

        // 查找 bat 文件
        string? batPath = null;
        foreach (var pattern in BatPatterns)
        {
            var candidate = Path.Combine(workingDirectory, pattern);
            if (File.Exists(candidate))
            {
                batPath = candidate;
                break;
            }
        }

        // 如果没有标准命名，尝试找目录下第一个 .bat/.cmd
        if (batPath == null)
        {
            batPath = Directory.GetFiles(workingDirectory, "*.bat", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(workingDirectory, "*.cmd", SearchOption.TopDirectoryOnly))
                .FirstOrDefault();
        }

        if (batPath == null)
        {
            result.ErrorMessage = "未找到启动脚本";
            return result;
        }

        return ParseFile(batPath, workingDirectory);
    }

    /// <summary>
    /// 解析指定 bat 文件
    /// </summary>
    public static ParseResult ParseFile(string batPath, string workingDirectory)
    {
        var result = new ParseResult { Success = true };

        try
        {
            var lines = File.ReadAllLines(batPath);
            var javaCommand = new List<string>();
            var inJavaCommand = false;

            // 逐行扫描，提取 java 命令（可能跨多行，用 ^ 续行符）
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("::") || line.StartsWith("@") || line.StartsWith("rem", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 移除行内注释
                var commentIdx = line.IndexOf("::", StringComparison.Ordinal);
                if (commentIdx > 0) line = line[..commentIdx].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 检测 java 命令
                if (line.StartsWith("java ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("javaw ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("%JAVA_HOME%", StringComparison.OrdinalIgnoreCase))
                {
                    inJavaCommand = true;
                }
                else if (inJavaCommand && line.EndsWith("^"))
                {
                    // 续行符，继续
                    line = line[..^];
                }
                else if (inJavaCommand)
                {
                    // java 命令结束
                    javaCommand.AddRange(Tokenize(line));
                    break;
                }

                if (inJavaCommand)
                {
                    javaCommand.AddRange(Tokenize(line));
                }
            }

            if (javaCommand.Count == 0)
            {
                result.ErrorMessage = "未找到 java 命令";
                result.Success = false;
                return result;
            }

            // 跳过 java/javaw 关键字
            int idx = 0;
            while (idx < javaCommand.Count && !javaCommand[idx].StartsWith("-"))
                idx++;

            // 解析参数
            var jvmArgs = new List<string>();
            var knownFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "-Xmx", "-Xms", "-Xss", "-Xmn", "-Xms", "-Xss",
                "-XX:+", "-XX:-", "-XX:",
                "-D", "-Djava",
                "-jar", "-cp", "-classpath",
                "-nogui", "-version", "-help",
                "-ea", "-da",
                "--add-opens", "--add-exports", "--add-reads",
                "-?m", "-?d"
            };

            while (idx < javaCommand.Count)
            {
                var token = javaCommand[idx];

                // -Xmx / -Xms（可能不带空格连写，如 -Xmx1024M）
                if (token.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase))
                {
                    var val = token.Length > 4 ? token[4..] : (idx + 1 < javaCommand.Count ? javaCommand[++idx] : null);
                    if (val != null)
                    {
                        result.MaxHeapBytes = ParseMemoryToBytes(val);
                        jvmArgs.Add($"-Xmx{val}");
                    }
                    idx++;
                    continue;
                }

                if (token.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase)
                    && !token.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase))
                {
                    var val = token.Length > 4 ? token[4..] : (idx + 1 < javaCommand.Count ? javaCommand[++idx] : null);
                    if (val != null)
                    {
                        result.InitialHeapBytes = ParseMemoryToBytes(val);
                        jvmArgs.Add($"-Xms{val}");
                    }
                    idx++;
                    continue;
                }

                // -jar
                if (token.Equals("-jar", StringComparison.OrdinalIgnoreCase))
                {
                    idx++;
                    if (idx < javaCommand.Count)
                    {
                        var jarName = javaCommand[idx];
                        // 如果 jarName 是相对路径，相对于 bat 所在目录解析
                        if (Path.IsPathRooted(jarName))
                            result.JarPath = jarName;
                        else if (!string.IsNullOrEmpty(workingDirectory))
                            result.JarPath = Path.GetFullPath(Path.Combine(workingDirectory, jarName));
                        else
                            result.JarPath = jarName;
                    }
                    idx++;
                    continue;
                }

                // -cp / -classpath（跳过值）
                if (token.Equals("-cp", StringComparison.OrdinalIgnoreCase) || token.Equals("-classpath", StringComparison.OrdinalIgnoreCase))
                {
                    jvmArgs.Add(token);
                    idx++;
                    if (idx < javaCommand.Count) jvmArgs.Add(javaCommand[idx]);
                    idx++;
                    continue;
                }

                // 已知 flags 或 value 型参数
                if (knownFlags.Any(f => token.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                    || knownFlags.Contains(token))
                {
                    jvmArgs.Add(token);
                    // value 型参数（-jar 已处理，-cp 已处理，其余尝试跳过下一个 token）
                    var valueFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "-D", "-Djava", "--add-opens", "--add-exports", "--add-reads",
                        "-XX:", "-XX:+", "-XX:-"
                    };
                    var needsValue = valueFlags.Any(f => token.StartsWith(f, StringComparison.OrdinalIgnoreCase));
                    if (needsValue && idx + 1 < javaCommand.Count)
                    {
                        jvmArgs.Add(javaCommand[++idx]);
                    }
                    idx++;
                    continue;
                }

                // 未知参数
                result.UnknownArgs.Add(token);
                idx++;
            }

            result.JvmArguments = jvmArgs;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// 将内存字符串转为字节数（支持 1024M, 1G, 512k 等格式）
    /// </summary>
    public static long ParseMemoryToBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        value = value.Trim().ToUpperInvariant();
        long multiplier = 1;

        if (value.EndsWith("K"))
        {
            multiplier = 1024;
            value = value[..^1];
        }
        else if (value.EndsWith("M"))
        {
            multiplier = 1024L * 1024;
            value = value[..^1];
        }
        else if (value.EndsWith("G"))
        {
            multiplier = 1024L * 1024 * 1024;
            value = value[..^1];
        }

        if (long.TryParse(value, out var num))
            return num * multiplier;

        // 尝试直接解析（可能已经是纯数字字节）
        return long.TryParse(value, out var direct) ? direct : 0;
    }

    /// <summary>
    /// 将命令行字符串拆分为 token（支持引号）
    /// </summary>
    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                if (inQuote && current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                inQuote = !inQuote;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
