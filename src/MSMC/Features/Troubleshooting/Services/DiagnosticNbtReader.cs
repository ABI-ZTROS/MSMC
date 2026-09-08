// -----------------------------------------------------------------------------
// DiagnosticNbtReader.cs — 自研 NBT 解析器（零外部依赖）
// 设计约束: 大端序；GZIP/ZLIB 解压；流式读取
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>NBT Tag 类型（MC Wiki 定义，Java 版 1.7+ 稳定）</summary>
public enum NbtTagType : byte
{
    End = 0, Byte = 1, Short = 2, Int = 3, Long = 4,
    Float = 5, Double = 6, ByteArray = 7, String = 8,
    List = 9, Compound = 10, IntArray = 11, LongArray = 12
}

/// <summary>NBT Tag — 不可变 record</summary>
public sealed record NbtTag(NbtTagType Type, string? Name, object? Value)
{
    /// <summary>便捷索引：按路径取值（如 "Items/0/Count"）</summary>
    public NbtTag? this[string path] => DiagnosticNbtReader.GetByPath(this, path);
}

public static class DiagnosticNbtReader
{
    // ─── 健壮性上限（防损坏/恶意存档拖垮解析） ───
    private const int MaxListCount = 100_000;        // 单 List 元素数上限
    private const int MaxCompoundCount = 100_000;    // 单 Compound 键数上限
    private const int MaxStringLength = 32_767;      // NBT 规范字符串长度上限（short 范围）
    private const int MaxArrayLength = 10_000_000;   // Byte/Int/Long 数组元素上限（10M）
    private const int MaxDepth = 128;                // 嵌套深度上限（防栈溢出）

    // ─── 入口 ───

    /// <summary>从 .dat 文件（GZIP 压缩）根节点开始解析</summary>
    public static NbtTag? ParseDatFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ParseGzip(stream);
    }

    /// <summary>从任意流解析 GZip 压缩的 NBT（.dat / .nbt）</summary>
    public static NbtTag? ParseGzip(Stream input)
    {
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        return ParseNbt(gzip);
    }

    /// <summary>从任意流解析 Zlib 压缩的 NBT（.mca 区块内部）</summary>
    public static NbtTag? ParseZlib(byte[] zlibData)
    {
        if (zlibData.Length < 6) return null; // ZLIB = 2-byte header + DEFLATE + 4-byte Adler32
        // ZLIB = 2-byte header + DEFLATE + 4-byte Adler32
        using var ms = new MemoryStream(zlibData, 2, zlibData.Length - 6); // 跳过 header 和 Adler32
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        return ParseNbt(deflate);
    }

    /// <summary>解析原始（未压缩）NBT 流</summary>
    public static NbtTag? ParseNbt(Stream stream) => ParseNbt(stream, 0);

    private static NbtTag? ParseNbt(Stream stream, int depth)
    {
        if (depth > MaxDepth) throw new InvalidDataException($"NBT 嵌套深度超限（>{MaxDepth}）");
        var br = new BigEndianBinaryReader(stream);
        byte typeByte = br.ReadByte();
        if (typeByte == 0) return null; // TAG_End
        var type = (NbtTagType)typeByte;
        string? name = ReadStringSafe(br);
        return ReadTag(br, type, name, depth);
    }

    // ─── 路径查询 ───

    /// <summary>按路径取值（如 "Items/0/Count"）</summary>
    public static NbtTag? GetByPath(NbtTag root, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        NbtTag current = root;
        foreach (var part in parts)
        {
            if (current is null) return null;
            if (current.Type == NbtTagType.Compound && current.Value is Dictionary<string, NbtTag> dict)
            {
                if (!dict.TryGetValue(part, out var next)) return null;
                current = next;
            }
            else if (current.Type == NbtTagType.List && current.Value is NbtList list)
            {
                if (!int.TryParse(part, out int idx) || idx < 0 || idx >= list.Items.Count) return null;
                current = list.Items[idx];
            }
            else return null;
        }
        return current;
    }

    // ─── 内部解析 ───

    private static NbtTag? ReadTag(BigEndianBinaryReader br, NbtTagType type, string? name, int depth)
    {
        return type switch
        {
            NbtTagType.End => null,
            NbtTagType.Byte => new NbtTag(type, name, (int)br.ReadByte()),
            NbtTagType.Short => new NbtTag(type, name, (int)br.ReadBEInt16()),
            NbtTagType.Int => new NbtTag(type, name, (int)br.ReadBEInt32()),
            NbtTagType.Long => new NbtTag(type, name, (long)br.ReadBEInt64()),
            NbtTagType.Float => new NbtTag(type, name, BitConverter.Int32BitsToSingle(br.ReadBEInt32())),
            NbtTagType.Double => new NbtTag(type, name, BitConverter.Int64BitsToDouble(br.ReadBEInt64())),
            NbtTagType.ByteArray => new NbtTag(type, name, ReadByteArray(br)),
            NbtTagType.String => new NbtTag(type, name, ReadStringSafe(br)),
            NbtTagType.List => ReadList(br, name, depth),
            NbtTagType.Compound => ReadCompound(br, name, depth),
            NbtTagType.IntArray => ReadIntArray(br, name),
            NbtTagType.LongArray => ReadLongArray(br, name),
            _ => null
        };
    }

    private static NbtTag ReadList(BigEndianBinaryReader br, string? name, int depth)
    {
        byte elemType = br.ReadByte();
        int count = br.ReadBEInt32();
        if (count < 0 || count > MaxListCount)
            throw new InvalidDataException($"NBT List 元素数异常: {count}");
        var items = new List<NbtTag>(Math.Min(count, 1024));
        for (int i = 0; i < count; i++)
        {
            var tag = ReadTag(br, (NbtTagType)elemType, null, depth + 1);
            if (tag != null) items.Add(tag);
        }
        return new NbtTag(NbtTagType.List, name, new NbtList((NbtTagType)elemType, items));
    }

    private static NbtTag ReadCompound(BigEndianBinaryReader br, string? name, int depth)
    {
        var dict = new Dictionary<string, NbtTag>();
        while (true)
        {
            byte typeByte = br.ReadByte();
            if (typeByte == 0) break; // TAG_End
            var type = (NbtTagType)typeByte;
            string? childName = ReadStringSafe(br);
            // 名字读取失败说明流已损坏，继续读只会死循环，直接终止
            if (childName == null) break;
            var child = ReadTag(br, type, childName, depth + 1);
            if (child != null)
            {
                dict[childName] = child;
                if (dict.Count > MaxCompoundCount)
                    throw new InvalidDataException($"NBT Compound 键数超限（>{MaxCompoundCount}）");
            }
        }
        return new NbtTag(NbtTagType.Compound, name, dict);
    }

    private static byte[] ReadByteArray(BigEndianBinaryReader br)
    {
        int n = br.ReadBEInt32();
        if (n < 0 || n > MaxArrayLength) throw new InvalidDataException($"NBT ByteArray 长度异常: {n}");
        return br.ReadBytes(n);
    }

    private static NbtTag ReadIntArray(BigEndianBinaryReader br, string? name)
    {
        int count = br.ReadBEInt32();
        if (count < 0 || count > MaxArrayLength) throw new InvalidDataException($"NBT IntArray 长度异常: {count}");
        var arr = new int[count];
        for (int i = 0; i < count; i++) arr[i] = br.ReadBEInt32();
        return new NbtTag(NbtTagType.IntArray, name, arr);
    }

    private static NbtTag ReadLongArray(BigEndianBinaryReader br, string? name)
    {
        int count = br.ReadBEInt32();
        if (count < 0 || count > MaxArrayLength) throw new InvalidDataException($"NBT LongArray 长度异常: {count}");
        var arr = new long[count];
        for (int i = 0; i < count; i++) arr[i] = br.ReadBEInt64();
        return new NbtTag(NbtTagType.LongArray, name, arr);
    }

    private static string? ReadStringSafe(BigEndianBinaryReader br)
    {
        try
        {
            int len = br.ReadBEInt16();
            if (len <= 0) return string.Empty;
            if (len > MaxStringLength) throw new InvalidDataException($"NBT String 长度超限: {len}");
            var bytes = br.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }
}

/// <summary>NBT List 容器（带元素类型信息）</summary>
public sealed record NbtList(NbtTagType ElementType, List<NbtTag> Items);

/// <summary>大端序 BinaryReader — NBT 所有数值都是大端</summary>
internal sealed class BigEndianBinaryReader
{
    private readonly BinaryReader _reader;
    public BigEndianBinaryReader(Stream stream) { _reader = new BinaryReader(stream); }
    public byte ReadByte() => _reader.ReadByte();
    public byte[] ReadBytes(int n) => _reader.ReadBytes(n);

    public short ReadBEInt16()
    {
        var bytes = _reader.ReadBytes(2);
        return (short)((bytes[0] << 8) | bytes[1]);
    }
    public int ReadBEInt32()
    {
        var bytes = _reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
    public long ReadBEInt64()
    {
        var bytes = _reader.ReadBytes(8);
        long val = 0;
        for (int i = 0; i < 8; i++) val = (val << 8) | bytes[i];
        return val;
    }
}
