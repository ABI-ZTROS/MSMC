using System.IO;
using System.Text.RegularExpressions;
using io.NET.ZTR_OS.Features.PlayerManager.Models;

namespace io.NET.ZTR_OS.Features.PlayerManager.Services;

public static class PlayerLogParser
{
    private static readonly Regex JoinedRegex = new(
        @":\s*(?<name>\S+?)\s+joined the game",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeftRegex = new(
        @":\s*(?<name>\S+?)\s+left the game",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TimestampRegex = new(
        @"\[(?<h>\d{2}):(?<m>\d{2}):(?<s>\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<OnlinePlayer> ParseLogFile(string logPath)
    {
        if (!File.Exists(logPath))
            return [];
        var lines = File.ReadAllLines(logPath);
        return ParseLogLines(lines);
    }

    public static List<OnlinePlayer> ParseLogLines(IEnumerable<string> lines)
    {
        var dict = new Dictionary<string, OnlinePlayer>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var ts = TimeSpan.Zero;
            var tsMatch = TimestampRegex.Match(line);
            if (tsMatch.Success)
            {
                int h = int.Parse(tsMatch.Groups["h"].Value);
                int m = int.Parse(tsMatch.Groups["m"].Value);
                int s = int.Parse(tsMatch.Groups["s"].Value);
                ts = new TimeSpan(h, m, s);
            }

            var joinedMatch = JoinedRegex.Match(line);
            if (joinedMatch.Success)
            {
                var name = joinedMatch.Groups["name"].Value;
                dict[name] = new OnlinePlayer { Name = name, At = ts, Online = true };
                continue;
            }

            var leftMatch = LeftRegex.Match(line);
            if (leftMatch.Success)
            {
                var name = leftMatch.Groups["name"].Value;
                if (dict.TryGetValue(name, out var existing))
                {
                    existing.At = ts;
                    existing.Online = false;
                }
                else
                {
                    dict[name] = new OnlinePlayer { Name = name, At = ts, Online = false };
                }
            }
        }

        return dict.Values.ToList();
    }
}
