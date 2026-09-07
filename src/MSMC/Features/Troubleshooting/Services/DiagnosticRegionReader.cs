// -----------------------------------------------------------------------------
// DiagnosticRegionReader.cs — .mca 区块文件解析（Java 版 1.0+ 稳定）
// 设计约束: 4KB sector header; 大端 uint; 流式读取
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public sealed record RegionChunkInfo(
    int ChunkX, int ChunkZ,        // 区块在 .mca 内的坐标 (0-31)
    int RegionX, int RegionZ,      // .mca 所属 region 坐标
    int DataLength,                // 解压后字节数
    int CompressionType,           // 1=GZIP, 2=ZLIB, 3=LZ4
    NbtTag? Root,                  // 区块完整 NBT（null=解析失败）
    bool ParseSucceeded);

public static class DiagnosticRegionReader
{
    /// <summary>解析单个 .mca 文件</summary>
    public static List<RegionChunkInfo> ParseRegionFile(string mcaPath)
    {
        var chunks = new List<RegionChunkInfo>();
        string? fileName = Path.GetFileName(mcaPath);
        var regionCoords = ParseRegionCoords(fileName);

        try
        {
            using var stream = File.OpenRead(mcaPath);
            // .mca = 8KB 头（4KB 位置表 + 4KB 时间戳表）
            if (stream.Length < 8192) return chunks;

            var buf = new byte[8192];
            stream.ReadExactly(buf, 0, 8192);

            // 位置表: 每个 4 字节大端 uint（sector_offset_high + sector_offset_low + sector_count）
            for (int i = 0; i < 1024; i++)
            {
                int slotX = i % 32;
                int slotZ = i / 32;

                uint entry = ((uint)buf[i * 4] << 24) | ((uint)buf[i * 4 + 1] << 16) |
                             ((uint)buf[i * 4 + 2] << 8) | buf[i * 4 + 3];

                // 高位 3 字节 = sector offset (从 4KB 头之后算)，低位 1 字节 = sector count
                int sectorOffset = (int)(entry >> 8);
                int sectorCount = (int)(entry & 0xFF);

                if (sectorOffset == 0 || sectorCount == 0) continue; // 空 slot

                long chunkFilePos = (long)sectorOffset * 4096;
                if (chunkFilePos + 5 > stream.Length) continue;

                try
                {
                    stream.Position = chunkFilePos;

                    // 区块数据头: 4 字节大端长度 + 1 字节压缩类型
                    var hdr = new byte[5];
                    stream.ReadExactly(hdr, 0, 5);
                    int dataLen = ((hdr[0] << 24) | (hdr[1] << 16) | (hdr[2] << 8) | hdr[3]);
                    int compType = hdr[4];

                    if (dataLen <= 0 || compType != 2) continue; // P0 只处理 ZLIB

                    var zlibData = new byte[dataLen];
                    stream.ReadExactly(zlibData, 0, dataLen);

                    NbtTag? root = null;
                    bool ok = false;
                    try
                    {
                        root = DiagnosticNbtReader.ParseZlib(zlibData);
                        ok = true;
                    }
                    catch { /* 损坏区块 — 跳过 */ }

                    chunks.Add(new RegionChunkInfo(
                        slotX, slotZ, regionCoords.X, regionCoords.Z,
                        dataLen, compType, root, ok));
                }
                catch { /* 单个区块读取失败 — 继续下一个 */ }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[REGION] 解析 {File} 失败", fileName);
        }

        return chunks;
    }

    /// <summary>.mca 文件名 → region 坐标: r.X.Z.mca → (X, Z)</summary>
    public static (int X, int Z) ParseRegionCoords(string? mcaFileName)
    {
        if (string.IsNullOrEmpty(mcaFileName)) return (0, 0);
        var match = System.Text.RegularExpressions.Regex.Match(
            mcaFileName, @"r\.(-?\d+)\.(-?\d+)\.mca");
        if (!match.Success) return (0, 0);
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    /// <summary>世界绝对坐标 → region 坐标</summary>
    public static (int RegionX, int RegionZ) WorldToRegion(int worldX, int worldZ)
    {
        return (worldX / 512, worldZ / 512);
    }

    /// <summary>世界绝对坐标 → region 内的区块坐标 (0-31)</summary>
    public static (int LocalX, int LocalZ) RegionLocal(int worldX, int worldZ)
    {
        return ((int)(worldX % 512 + 512) % 512 / 16,
                (int)(worldZ % 512 + 512) % 512 / 16);
    }
}
