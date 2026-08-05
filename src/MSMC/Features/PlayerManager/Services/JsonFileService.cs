using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using io.NET.ZTR_OS.Features.PlayerManager.Models;

namespace io.NET.ZTR_OS.Features.PlayerManager.Services;

public class JsonFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string GetFileName(string type) => type switch
    {
        "wl" => "whitelist.json",
        "ops" => "ops.json",
        "ban" => "banned-players.json",
        _ => throw new ArgumentException($"Unknown type: {type}", nameof(type)),
    };

    public T[] ReadJson<T>(string serverDir, string type)
    {
        var filePath = Path.Combine(serverDir, GetFileName(type));
        if (!File.Exists(filePath))
            return [];
        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return [];
            var result = JsonSerializer.Deserialize<T[]>(json, JsonOptions);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void WriteJson<T>(string serverDir, string type, T[] entries)
    {
        var filePath = Path.Combine(serverDir, GetFileName(type));
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static bool MatchByUuidOrName<T>(T entry, T other, Func<T, string?> getUuid, Func<T, string?> getName)
    {
        var eu = getUuid(entry);
        var ou = getUuid(other);
        if (!string.IsNullOrEmpty(eu) && !string.IsNullOrEmpty(ou)
            && string.Equals(eu, ou, StringComparison.OrdinalIgnoreCase))
            return true;
        var en = getName(entry);
        var on = getName(other);
        if (string.IsNullOrEmpty(eu) && string.IsNullOrEmpty(ou)
            && !string.IsNullOrEmpty(en) && !string.IsNullOrEmpty(on)
            && string.Equals(en, on, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public void UpsertWhitelist(string serverDir, WhitelistEntry entry)
    {
        var list = ReadJson<WhitelistEntry>(serverDir, "wl").ToList();
        var idx = list.FindIndex(e => MatchByUuidOrName(e, entry, x => x.Uuid, x => x.Name));
        if (idx >= 0) list[idx] = entry; else list.Add(entry);
        WriteJson(serverDir, "wl", list.ToArray());
    }

    public void UpsertOp(string serverDir, OpEntry entry)
    {
        var list = ReadJson<OpEntry>(serverDir, "ops").ToList();
        var idx = list.FindIndex(e => MatchByUuidOrName(e, entry, x => x.Uuid, x => x.Name));
        if (idx >= 0) list[idx] = entry; else list.Add(entry);
        WriteJson(serverDir, "ops", list.ToArray());
    }

    public void UpsertBan(string serverDir, BanEntry entry)
    {
        var list = ReadJson<BanEntry>(serverDir, "ban").ToList();
        var idx = list.FindIndex(e => MatchByUuidOrName(e, entry, x => x.Uuid, x => x.Name));
        if (idx >= 0) list[idx] = entry; else list.Add(entry);
        WriteJson(serverDir, "ban", list.ToArray());
    }

    public void Upsert<T>(string serverDir, string type, T entry)
    {
        switch (type)
        {
            case "wl" when entry is WhitelistEntry w:
                UpsertWhitelist(serverDir, w);
                break;
            case "ops" when entry is OpEntry o:
                UpsertOp(serverDir, o);
                break;
            case "ban" when entry is BanEntry b:
                UpsertBan(serverDir, b);
                break;
            default:
                throw new ArgumentException($"Unsupported type {type} or entry mismatch", nameof(type));
        }
    }

    public bool RemoveWhitelist(string serverDir, string nameOrUuid)
    {
        var list = ReadJson<WhitelistEntry>(serverDir, "wl").ToList();
        var removed = list.RemoveAll(e =>
            string.Equals(e.Uuid, nameOrUuid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Name, nameOrUuid, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) WriteJson(serverDir, "wl", list.ToArray());
        return removed > 0;
    }

    public bool RemoveOp(string serverDir, string nameOrUuid)
    {
        var list = ReadJson<OpEntry>(serverDir, "ops").ToList();
        var removed = list.RemoveAll(e =>
            string.Equals(e.Uuid, nameOrUuid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Name, nameOrUuid, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) WriteJson(serverDir, "ops", list.ToArray());
        return removed > 0;
    }

    public bool RemoveBan(string serverDir, string nameOrUuid)
    {
        var list = ReadJson<BanEntry>(serverDir, "ban").ToList();
        var removed = list.RemoveAll(e =>
            string.Equals(e.Uuid, nameOrUuid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Name, nameOrUuid, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) WriteJson(serverDir, "ban", list.ToArray());
        return removed > 0;
    }

    public bool Remove(string serverDir, string type, string nameOrUuid)
    {
        if (string.IsNullOrWhiteSpace(nameOrUuid))
            return false;
        return type switch
        {
            "wl" => RemoveWhitelist(serverDir, nameOrUuid),
            "ops" => RemoveOp(serverDir, nameOrUuid),
            "ban" => RemoveBan(serverDir, nameOrUuid),
            _ => throw new ArgumentException($"Unknown type: {type}", nameof(type)),
        };
    }
}
