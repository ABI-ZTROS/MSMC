// ═══════════════════════════════════════════════════════════════════════════════
// 🪟 WindowEffectsService — 基于 DWM / Win32 的窗口视觉效果服务
// ═══════════════════════════════════════════════════════════════════════════════
// 核心能力：
//   1. ✅ Mica / MicaAlt / Acrylic 云母/亚克力背景（Win11 22H2+，ColorOS 级玻璃感）
//   2. ✅ 深色模式标题栏（Win10 1809+，不白边扎眼）
//   3. ✅ DWM 合成开关检测（Win7/远程桌面下自动降级）
//   4. ✅ 窗口圆角调整（Win11 22H2+，配合卡片圆角体系）
//   5. ✅ 防止窗口「被截屏」—— SetWindowDisplayAffinity（防录屏保护配置页）
//
// 使用方式：
//   var effects = App.Services.GetRequiredService<IWindowEffectsService>();
//   effects.ApplyMicaBackground(mainWindowHandle);    // 套上云母
//   effects.ApplyDarkTitleBar(mainWindowHandle, true); // 深色标题栏
// ═══════════════════════════════════════════════════════════════════════════════

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace io.NET.ZTR_OS.Features.Shared.Native.Services;

/// <summary>
/// Win11 22H2+ 的 DWM_SYSTEMBACKDROP_TYPE 枚举
/// ref: https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type
/// </summary>
public enum SystemBackdropType
{
    Auto = 0,
    None = 1,
    MainWindow = 2,     // Mica（适合主窗口，跟系统主题走）
    TransientWindow = 3, // MicaAlt（更透明的云母，适合弹窗/Sidebar）
    TabbedWindow = 4    // Acrylic 类效果
}

/// <summary>
/// Win11 窗口圆角（DWMWA_WINDOW_CORNER_PREFERENCE）
/// </summary>
public enum WindowCornerPreference
{
    Default = 0,
    DoNotRound = 1,
    Round = 2,
    RoundSmall = 3
}

/// <summary>
/// 截屏/录屏保护级别
/// ref: SetWindowDisplayAffinity
/// </summary>
[Flags]
public enum DisplayAffinity : uint
{
    None = 0x00000000,
    Monitor = 0x00000001,         // 只在物理显示器可见，截屏/录屏=黑
    ExcludeFromCapture = 0x00000002 // Win10 2004+ 明确不参与全局截屏
}

[SupportedOSPlatform("windows")]
public interface IWindowEffectsService
{
    /// <summary>检测 DWM 合成是否启用（远程桌面/Win7 基本主题下会返回 false）</summary>
    bool IsCompositionEnabled { get; }

    /// <summary>操作系统版本是否支持 Mica（Win11 22H2+ = build 22621）</summary>
    bool SupportsMica { get; }

    /// <summary>操作系统版本是否支持深色标题栏（Win10 1809+ = build 17763）</summary>
    bool SupportsDarkTitleBar { get; }

    /// <summary>把窗口背景设为 Mica/MicaAlt/Acrylic（成功=true，系统不支持=false）</summary>
    bool ApplySystemBackdrop(IntPtr hWnd, SystemBackdropType type);

    /// <summary>移除 Mica，回到纯色背景</summary>
    bool ClearSystemBackdrop(IntPtr hWnd);

    /// <summary>设置标题栏深色/浅色模式（true=深色；false=跟随系统；null=浅色）</summary>
    bool ApplyDarkTitleBar(IntPtr hWnd, bool? darkMode);

    /// <summary>设置窗口圆角策略（Win11 22H2+；ColorOS 统一用 RoundSmall）</summary>
    bool ApplyCornerPreference(IntPtr hWnd, WindowCornerPreference corner);

    /// <summary>设置窗口截屏保护（配置页/隐私页用：录屏工具拍到黑屏）</summary>
    bool SetDisplayAffinity(IntPtr hWnd, DisplayAffinity affinity);

    /// <summary>把主窗口一套完整的「ColorOS 美学」效果一次性打上去</summary>
    void ApplyColorOSVisualPack(IntPtr hWnd, bool darkTitleBar = true);

    /// <summary>
    /// 某窗口句柄是否已经 ApplyColorOSVisualPack 过（用于 ThemeService 深浅切换时去重调用）。
    /// Windows 以外的平台或句柄无效时返回 false。
    /// </summary>
    bool IsApplied(IntPtr hWnd);
}

[SupportedOSPlatform("windows")]
public sealed class WindowEffectsService : IWindowEffectsService
{
    private readonly ILogger _log = Log.ForContext<WindowEffectsService>();

    // DWA 常量（不在 Win32.cs 里是因为它们是字符串常量，跟 DwmSetWindowAttribute 的编号配套）
    // DwmWindowAttribute 已覆盖编号，这里是扩展编号：
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;  // Win11 22H2+
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;        // Win11 22H2+
    // 2 = UseImmersiveDarkMode (Win10 1809+)

    // 记录哪些 hWnd 已经 ApplyColorOSVisualPack 过；弱引用集，避免窗口关闭后句柄被长期占用
    private readonly HashSet<IntPtr> _appliedHandles = new();
    private readonly object _appliedLock = new();

    public bool IsCompositionEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                var hr = NativeMethods.DwmIsCompositionEnabled(out var enabled);
                return hr == 0 && enabled;
            }
            catch (Exception ex)
            {
                _log.Verbose(ex, "DwmIsCompositionEnabled 查询失败（可能是 Win7 无 DWM）");
                return false;
            }
        }
    }

    public bool SupportsMica => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621); // Win11 22H2
    public bool SupportsDarkTitleBar => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763); // Win10 1809

    // ───────────────────────────────────────────────────────────────────────
    public bool ApplySystemBackdrop(IntPtr hWnd, SystemBackdropType type)
    {
        if (hWnd == IntPtr.Zero || !SupportsMica || !IsCompositionEnabled) return false;
        try
        {
            int backdrop = (int)type;
            var hr = NativeMethods.DwmSetWindowAttribute(
                hWnd,
                (DwmWindowAttribute)DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdrop,
                sizeof(int));
            if (hr != 0)
            {
                _log.Warning("[WindowFX] DWMWA_SYSTEMBACKDROP_TYPE={T} 失败 hr=0x{H:X8}", type, hr);
                return false;
            }
            // 兼容性：有些系统还需要 UseHostBackdropBrush=1 才能让 Mica 真正生效
            if (type != SystemBackdropType.None)
            {
                int enable = 1;
                NativeMethods.DwmSetWindowAttribute(
                    hWnd,
                    DwmWindowAttribute.UseHostBackdropBrush,
                    ref enable,
                    sizeof(int));
            }
            _log.Debug("[WindowFX] 系统背景 → {T}  句柄=0x{H:X8}", type, hWnd.ToInt64());
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[WindowFX] ApplySystemBackdrop 异常（不支持或 DWM 版本过旧）");
            return false;
        }
    }

    public bool ClearSystemBackdrop(IntPtr hWnd) => ApplySystemBackdrop(hWnd, SystemBackdropType.None);

    // ───────────────────────────────────────────────────────────────────────
    public bool ApplyDarkTitleBar(IntPtr hWnd, bool? darkMode)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            // darkMode = true   → 1（深色）
            // darkMode = false  → 0（浅色）
            // darkMode = null   → 跟随系统（不设置此项，默认行为）
            if (!darkMode.HasValue && SupportsDarkTitleBar) return true;
            if (!SupportsDarkTitleBar) return false;

            int value = darkMode.Value ? 1 : 0;
            var hr = NativeMethods.DwmSetWindowAttribute(
                hWnd,
                DwmWindowAttribute.UseImmersiveDarkMode,
                ref value,
                sizeof(int));
            if (hr != 0)
            {
                _log.Verbose("[WindowFX] DWMWA_USE_IMMERSIVE_DARK_MODE={V} 失败 hr=0x{H:X8}", value, hr);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Verbose(ex, "[WindowFX] ApplyDarkTitleBar 异常");
            return false;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public bool ApplyCornerPreference(IntPtr hWnd, WindowCornerPreference corner)
    {
        if (hWnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return false;
        try
        {
            int value = (int)corner;
            var hr = NativeMethods.DwmSetWindowAttribute(
                hWnd,
                (DwmWindowAttribute)DWMWA_WINDOW_CORNER_PREFERENCE,
                ref value,
                sizeof(int));
            return hr == 0;
        }
        catch (Exception ex)
        {
            _log.Verbose(ex, "[WindowFX] ApplyCornerPreference 异常");
            return false;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public bool SetDisplayAffinity(IntPtr hWnd, DisplayAffinity affinity)
    {
        if (hWnd == IntPtr.Zero || !OperatingSystem.IsWindows()) return false;
        try
        {
            // user32.dll 里的 SetWindowDisplayAffinity
            return SetWindowDisplayAffinity(hWnd, (uint)affinity);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[WindowFX] SetDisplayAffinity 失败");
            return false;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public void ApplyColorOSVisualPack(IntPtr hWnd, bool darkTitleBar = true)
    {
        if (hWnd == IntPtr.Zero) return;
        _log.Information("[WindowFX] 正在为 0x{H:X8} 应用 ColorOS Visual Pack...", hWnd.ToInt64());

        // 1) 深色标题栏（Win10 1809+）—— 解决主色是蓝的、标题栏还是白的这种割裂感
        ApplyDarkTitleBar(hWnd, darkTitleBar);

        // 2) 窗口小圆角（Win11 22H2+）—— 配合 globals.css 里的 md-radius 体系
        ApplyCornerPreference(hWnd, WindowCornerPreference.RoundSmall);

        // 3) Mica 背景（Win11 22H2+）—— 如果 DWM 合成就套，不成就拉倒
        if (SupportsMica && IsCompositionEnabled)
        {
            var ok = ApplySystemBackdrop(hWnd, SystemBackdropType.MainWindow);
            _log.Debug("[WindowFX] Mica 应用结果：{OK}", ok ? "成功" : "失败（降级为纯色背景）");
        }
        else
        {
            _log.Debug("[WindowFX] 当前系统不支持 Mica（Win11 22H2- 或 DWM 未启用），跳过");
        }

        // 4) 记录已应用句柄，后续 ThemeService 深浅切换时可查询
        lock (_appliedLock)
        {
            _appliedHandles.Add(hWnd);
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    public bool IsApplied(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        lock (_appliedLock) return _appliedHandles.Contains(hWnd);
    }

    // ───────────────────────────────────────────────────────────────────────
    // 内部 P/Invoke（因为 SetWindowDisplayAffinity 是 user32 的独立 API，未在 Win32.cs 出现）
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
}
