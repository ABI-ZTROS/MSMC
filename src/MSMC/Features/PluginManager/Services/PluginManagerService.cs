// -----------------------------------------------------------------------------
// 文件名: PluginManagerService.cs
// 命名空间: io.NET.ZTR_OS.Features.PluginManager.Services
// 功能描述: 插件管理服务：扫描、启用/禁用、删除
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.PluginManager.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.NET.ZTR_OS.Features.PluginManager.Models;

/// <summary>
/// 插件管理服务
/// </summary>
public class PluginManagerService
{
    private const string DisabledSuffix = ".disabled";
    private const string TrashFolderName = ".trash";

    /// <summary>
    /// 扫描插件目录，返回所有插件条目
    /// </summary>
    public List<PluginInfo> ScanPlugins(string pluginsDir)
    {
        var result = new List<PluginInfo>();
        if (!Directory.Exists(pluginsDir))
            return result;

        try
        {
            // 扫描 .jar 和 .jar.disabled
            var jarFiles = Directory.GetFiles(pluginsDir, "*.jar", SearchOption.TopDirectoryOnly);
            var disabledFiles = Directory.GetFiles(pluginsDir, "*.jar.disabled", SearchOption.TopDirectoryOnly);

            // 处理 .jar
            foreach (var jar in jarFiles)
            {
                // 避免和 .jar.disabled 重复（GetFiles *.jar 不会匹配 *.jar.disabled，这里保险起见）
                if (jar.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = PluginYmlParser.ParseJar(jar);
                info.Enabled = true;
                // 如果解析不出 Name，用文件名兜底
                if (string.IsNullOrEmpty(info.Name))
                    info.Name = Path.GetFileNameWithoutExtension(jar);
                result.Add(info);
            }

            // 处理 .jar.disabled
            foreach (var dis in disabledFiles)
            {
                // 先尝试还原路径去解析（ParseJar 要求文件存在，所以直接传 disabled 路径，
                // 但解析器只看 zip 内容，不管后缀名，因此仍可正常读取）
                var info = PluginYmlParser.ParseJar(dis);
                info.Enabled = false;
                if (string.IsNullOrEmpty(info.Name))
                    info.Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dis)); // 去两次后缀
                result.Add(info);
            }

            return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// 切换插件启用状态
    /// - filePath 是 .jar → 重命名为 .jar.disabled（enable=false 时）
    /// - filePath 是 .jar.disabled → 重命名为 .jar（enable=true 时）
    /// - 若 enable=true 且文件已是 .jar，不改名返回 true
    /// - 若 enable=false 且文件已是 .jar.disabled，不改名返回 true
    /// </summary>
    public bool TogglePlugin(string filePath, bool? enable = null)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var ext = Path.GetExtension(filePath);
            var isDisabled = filePath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            var isJar = string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase) && !isDisabled;

            bool targetEnable;
            if (enable.HasValue)
            {
                targetEnable = enable.Value;
            }
            else
            {
                targetEnable = isDisabled; // Toggle：当前 disabled → 启用；当前 jar → 禁用
            }

            // 已是目标状态，无需改名
            if (targetEnable && isJar)
                return true;
            if (!targetEnable && isDisabled)
                return true;

            string newPath;
            if (targetEnable)
            {
                // .jar.disabled → .jar
                newPath = filePath.Substring(0, filePath.Length - DisabledSuffix.Length);
            }
            else
            {
                // .jar → .jar.disabled
                newPath = filePath + DisabledSuffix;
            }

            // 目标存在则失败（避免覆盖）
            if (File.Exists(newPath))
                return false;

            File.Move(filePath, newPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 删除插件：优先移动到回收站，否则移到 plugins/.trash/，最后才 File.Delete
    /// </summary>
    public bool DeletePlugin(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            // 1. 尝试系统回收站（Windows 上 Microsoft.VisualBasic.FileIO 可用但不一定引用；
            //    这里退而求其次：移到 plugins/.trash/ 目录，作为"软删除回收站"）
            var pluginsDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(pluginsDir))
            {
                var trashDir = Path.Combine(pluginsDir, TrashFolderName);
                try
                {
                    if (!Directory.Exists(trashDir))
                        Directory.CreateDirectory(trashDir);

                    var dest = Path.Combine(trashDir, Path.GetFileName(filePath));
                    // 重名加时间戳
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(trashDir,
                            $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(filePath)}");
                    }
                    File.Move(filePath, dest);
                    return true;
                }
                catch
                {
                    // 回收站移动失败，硬删除兜底
                }
            }

            // 2. 硬删除
            File.Delete(filePath);
            return !File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }
}
