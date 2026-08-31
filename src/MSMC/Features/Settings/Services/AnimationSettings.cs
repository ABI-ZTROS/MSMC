// -----------------------------------------------------------------------------
// 文件名: AnimationSettings.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: ColorOS 动画系统 —— 全量 11 条缓动曲线（与前端 theme.css 一一对应）
//          每条曲线精确匹配前端 cubic-bezier 坐标，实现 Aquario 量子引擎效果。
// 设计模式: 静态工厂、IEasingFunction 自定义实现、贝塞尔曲线数值求解
// 缓动曲线谱系（来自前端 globals.css ColorOS 动画缓动部分）:
//   Standard            cubic-bezier(0.2, 0.9, 0.2, 1)     标准减速
//   StandardDecelerate  cubic-bezier(0, 0, 0.2, 1)          仅出向
//   StandardAccelerate  cubic-bezier(0.4, 0, 1, 1)          仅入向
//   Emphasized          cubic-bezier(0.15, 1, 0.3, 1)       ColorOS 标志性曲线
//   EmphasizedIn        cubic-bezier(0.4, 0, 1, 1)          Emphasized 入向
//   Spring              cubic-bezier(0.34, 1.56, 0.64, 1)   按钮过冲 12%
//   SpringSoft          cubic-bezier(0.22, 1.05, 0.36, 1)   卡片轻微回弹
//   Aquario             cubic-bezier(0.16, 1, 0.3, 1)       量子引擎默认（ColorOS 14）
//   Snap                cubic-bezier(0.4, 0, 0.2, 1)         按下急停
//   Oscillate           cubic-bezier(0.68, -0.3, 0.32, 1.3) 侧栏滑入负值过冲
//   BounceBack          cubic-bezier(0.18, 0.89, 0.32, 1.28) 物理弹回
//   Pop                 cubic-bezier(0.5, 1.6, 0.4, 0.85)   图标 micro 动画（强过冲）
//   Drift               cubic-bezier(0.37, 0, 0.63, 1)      对称缓入缓出
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.Settings.Services;

using System.Windows.Media.Animation;

/// <summary>
/// 自定义 cubic-bezier 缓动函数 —— WPF 没有内置贝塞尔控制点可配的 ease 类，
/// 所以我们实现 IEasingFunction 接口，用 De Casteljau 算法在 t∈[0,1] 上求 P_x(t) = t，
/// 再返回 P_y(t) 作为插值结果。支持过冲（ControlPoint > 1 或 < 0）。
/// </summary>
public sealed class CubicBezierEase : IEasingFunction
{
    private readonly double _p1x, _p1y, _p2x, _p2y;

    public CubicBezierEase(double p1x, double p1y, double p2x, double p2y)
    {
        _p1x = p1x;
        _p1y = p1y;
        _p2x = p2x;
        _p2y = p2y;
    }

    /// <summary>
    /// 对归一化时间 t (0→1) 求缓动后的值。
    /// 先在 x 维求解 B_x(t) = t（因为 CSS cubic-bezier 要求 x 单调递增 0→1），
    /// 再把同一个 t 代入 B_y 得到 y 值。
    /// </summary>
    public double Ease(double normalizedTime)
    {
        // Newton-Raphson 法求 B_x(t) = normalizedTime 的根
        double t = SolveBezier(normalizedTime, _p1x, _p2x);
        return Bezier(t, _p1y, _p2y);
    }

    /// <summary>
    /// 三次贝塞尔曲线公式（起点固定 (0,0)，终点固定 (1,1)）。
    /// B(t) = 3(1-t)²·t·p1 + 3(1-t)·t²·p2 + t³
    /// </summary>
    private static double Bezier(double t, double p1, double p2)
    {
        double oneMinusT = 1 - t;
        return 3 * oneMinusT * oneMinusT * t * p1
             + 3 * oneMinusT * t * t * p2
             + t * t * t;
    }

    /// <summary>
    /// 贝塞尔曲线对 t 的导数。
    /// B'(t) = 3(1-t)²·p1 + 6(1-t)·t·(p2-p1) + 3t²·(1-p2)
    /// </summary>
    private static double BezierDerivative(double t, double p1, double p2)
    {
        double oneMinusT = 1 - t;
        return 3 * oneMinusT * oneMinusT * p1
             + 6 * oneMinusT * t * (p2 - p1)
             + 3 * t * t * (1 - p2);
    }

    /// <summary>
    /// 用 Newton-Raphson 在 x 维求解 B_x(t) = x。
    /// 初始猜测用 Taylor 近似，最多迭代 8 次保证收敛到 1e-6 精度。
    /// </summary>
    private static double SolveBezier(double x, double p1, double p2)
    {
        // 边界保护
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        double t = x; // 初始猜测：线性
        for (int i = 0; i < 8; i++)
        {
            double currentX = Bezier(t, p1, p2) - x;
            if (Math.Abs(currentX) < 1e-7) break;

            double derivative = BezierDerivative(t, p1, p2);
            if (Math.Abs(derivative) < 1e-10) break; // 导数过小，停止迭代

            t -= currentX / derivative;
            t = Math.Clamp(t, 0, 1);
        }
        return t;
    }
}

/// <summary>
/// ColorOS 动画配置 —— 全量 11+ 条缓动曲线（与前端 theme.css 一一对应）
/// 每条曲线精确匹配前端 cubic-bezier 坐标，支持 Aquario 量子引擎、弹簧过冲、
/// 物理弹回等 ColorOS 14 标志性动画风格。
/// </summary>
public static class AnimationSettings
{
    // ── ColorOS 11+ 标准曲线（基础减速曲线）──
    // cubic-bezier(0.2, 0.9, 0.2, 1)
    private static readonly CubicBezierEase _standard = new(0.2, 0.9, 0.2, 1);

    // cubic-bezier(0, 0, 0.2, 1) —— 入向动画用（快速起步 → 慢慢停）
    private static readonly CubicBezierEase _standardDecelerate = new(0, 0, 0.2, 1);

    // cubic-bezier(0.4, 0, 1, 1) —— 出向动画用（慢慢起 → 快速收尾）
    private static readonly CubicBezierEase _standardAccelerate = new(0.4, 0, 1, 1);

    // ── ColorOS 强调曲线（Emphasized）──
    // cubic-bezier(0.15, 1, 0.3, 1) —— ColorOS 标志性曲线，更明显的初始粘滞感 + 快速收尾
    private static readonly CubicBezierEase _emphasized = new(0.15, 1, 0.3, 1);

    // cubic-bezier(0.4, 0, 1, 1) —— Emphasized 的入向版本
    private static readonly CubicBezierEase _emphasizedIn = new(0.4, 0, 1, 1);

    // ── 弹簧 / 过冲曲线（Spring / Overshoot）──
    // cubic-bezier(0.34, 1.56, 0.64, 1) —— 按钮按压回弹，过冲 12%
    private static readonly CubicBezierEase _spring = new(0.34, 1.56, 0.64, 1);

    // cubic-bezier(0.22, 1.05, 0.36, 1) —— 卡片进场轻微回弹（过冲 5%，不过分）
    private static readonly CubicBezierEase _springSoft = new(0.22, 1.05, 0.36, 1);

    // ── Aquario 量子引擎曲线（ColorOS 14 品牌曲线）──
    // cubic-bezier(0.16, 1, 0.3, 1) —— 无过冲的平滑减速，量子引擎默认曲线
    private static readonly CubicBezierEase _aquario = new(0.16, 1, 0.3, 1);

    // ── 急停 / 振荡 / 弹回 ──
    // cubic-bezier(0.4, 0, 0.2, 1) —— 按下瞬间反馈的快速阻尼曲线
    private static readonly CubicBezierEase _snap = new(0.4, 0, 0.2, 1);

    // cubic-bezier(0.68, -0.3, 0.32, 1.3) —— 侧栏滑入 / Sheet 弹出，带负值过冲
    private static readonly CubicBezierEase _oscillate = new(0.68, -0.3, 0.32, 1.3);

    // cubic-bezier(0.18, 0.89, 0.32, 1.28) —— 物理弹回（拖拽取消）
    private static readonly CubicBezierEase _bounceBack = new(0.18, 0.89, 0.32, 1.28);

    // cubic-bezier(0.5, 1.6, 0.4, 0.85) —— 图标 micro 动画，强过冲
    private static readonly CubicBezierEase _pop = new(0.5, 1.6, 0.4, 0.85);

    // cubic-bezier(0.37, 0, 0.63, 1) —— 对称缓入缓出（AOD 流动 / 长周期柔和）
    private static readonly CubicBezierEase _drift = new(0.37, 0, 0.63, 1);

    // ── 公开属性：13 条缓动曲线，覆盖所有 ColorOS 动画场景 ──
    public static IEasingFunction Standard => _standard;
    public static IEasingFunction StandardDecelerate => _standardDecelerate;
    public static IEasingFunction StandardAccelerate => _standardAccelerate;
    public static IEasingFunction Emphasized => _emphasized;
    public static IEasingFunction EmphasizedIn => _emphasizedIn;
    public static IEasingFunction Spring => _spring;
    public static IEasingFunction SpringSoft => _springSoft;
    public static IEasingFunction Aquario => _aquario;
    public static IEasingFunction Snap => _snap;
    public static IEasingFunction Oscillate => _oscillate;
    public static IEasingFunction BounceBack => _bounceBack;
    public static IEasingFunction Pop => _pop;
    public static IEasingFunction Drift => _drift;

    public static IThemeService? ThemeService { get; set; }

    public static bool AnimationsEnabled => ThemeService?.EnableAnimations ?? true;

    /// <summary>
    /// 根据 ThemeService 配置返回动画时长（毫秒）。
    /// 动画被禁用时直接返回 0，让调用方跳过所有 BeginAnimation。
    /// </summary>
    public static int DurationMs(int baseMs)
    {
        var configured = ThemeService?.AnimationDuration ?? baseMs;
        return AnimationsEnabled ? configured : 0;
    }
}
