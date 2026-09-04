#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MSMC 极端垃圾硬件负载仿真（P10 纸面演练）
============================================
目标硬件：初代 Core i3-530 (Clarksdale, 2010) / 2C4T @ 2.93GHz / 6GB DDR3-1333 / 5400rpm 老机械硬盘

方法：枚举每个交互元素 -> 量化(CPU时间 / 内存峰值 / IO量) -> 按场景聚合 -> 判定 🔴/🟠/✅
所有 workload 条目均来自已核验的审计证据（文件:行号），弱机放大系数见硬件模型。

运行：python3 weak_hw_simulation.py
"""
from __future__ import annotations
from dataclasses import dataclass, field
from typing import List, Dict, Tuple

# ─────────────────────────────────────────────────────────────
# 1. 硬件模型（初代 i3-530）
# ─────────────────────────────────────────────────────────────
@dataclass
class Hardware:
    name: str
    # CPU：2C/4T @ 2.93GHz，Westmere IPC≈4，SMT 增益≈15%
    gips_total: float = 13.0      # 4 线程合计可持续吞吐 (十亿指令/秒)
    gips_single: float = 6.0      # 单线程可持续吞吐
    # 内存：6GB DDR3-1333 双通道
    ram_gb: float = 6.0
    ram_gbps: float = 7.0         # 实际带宽
    # 磁盘：5400rpm 老机械
    disk_seq_read_mbps: float = 80.0
    disk_seq_write_mbps: float = 60.0
    disk_random_iops: float = 60.0    # 4K 随机 IOPS
    disk_seek_ms: float = 15.0
    # 常驻基线（OS + WebView2 Chromium + MSMC 宿主），占用内存
    baseline_ram_gb: float = 3.2
    # 常驻后台基线 CPU 占用（OS + WebView2 渲染进程 + GC），占 4 线程总量百分比
    baseline_cpu_pct: float = 12.0

HW = Hardware(name="初代 i3-530 / 6GB DDR3 / 5400rpm HDD")

# 现代开发机对比（用于直观落差，非精确基准）
HW_MODERN = Hardware(name="现代 8C16T / 32GB / NVMe", gips_total=160.0, gips_single=18.0,
                     ram_gb=32.0, ram_gbps=40.0, disk_seq_read_mbps=3500.0,
                     disk_seq_write_mbps=2800.0, disk_random_iops=300000.0,
                     disk_seek_ms=0.1, baseline_ram_gb=3.0, baseline_cpu_pct=3.0)

# 超算级平台：2× AMD Threadripper PRO 7995WX (Zen4, 96C/192T each) = 192C/384T @ ~5.1GHz
# 512GB 8通道 DDR5 ECC ×2 + 企业级 PCIe5 M.2 + 内存缓存盘
# 单核 gips≈18（与现代机同代 IPC，但核心数 24x）→ 每平台单核串行任务提速相同，
# 差别体现在 384 线程并行容量 + 8通道内存带宽 + NVMe/内存盘随机 IO。
HW_TR = Hardware(
    name="2× Threadripper PRO 7995WX / 512GB ECC / NVMe+内存盘",
    gips_total=3400.0,        # 192 物理核 × ~17.7 GIPS/核 可持续
    gips_single=18.0,         # Zen4 单核
    ram_gb=512.0, ram_gbps=900.0,          # 双路 16 通道 DDR5-5600 ECC
    disk_seq_read_mbps=12000.0,            # 企业级 PCIe5 NVMe（读）
    disk_seq_write_mbps=9000.0,
    disk_random_iops=2500000.0,            # NVMe + 内存缓存盘命中（µs 级随机读）
    disk_seek_ms=0.02,
    baseline_ram_gb=3.2, baseline_cpu_pct=1.0)


# ─────────────────────────────────────────────────────────────
# 2. 工作负载条目（来自已核验审计，文件:行号为证据）
# ─────────────────────────────────────────────────────────────
@dataclass
class Workload:
    key: str
    name: str
    evidence: str                 # 证据 文件:行号
    period_s: float               # 触发周期（秒）；0=一次性/按需
    cpu_ms: float                 # 单次执行占用的单线程 CPU 时间（弱机, 毫秒）
    mem_peak_mb: float            # 单次峰值内存（MB）
    disk_bytes: float             # 单次磁盘 IO 字节
    disk_random: bool             # True=随机 IO(计IOPS) False=顺序 IO(计带宽)
    ui_thread: bool               # 是否在 UI 线程执行
    rating: str = "🟠"            # 🔴/🟠/✅
    note: str = ""
    ui_cpu_ms: float = 0.0        # UI 线程部分耗时（0 = 与 cpu_ms 相同）

# 证据缩写映射（审计子代理核验 + 我亲自复核）
E = {
    "sysmon_vm43":  "SystemMonitorViewModel.cs:43 (2s 采样) + :141 (启动即自动常驻)",
    "metrics16":    "MetricsPersistenceService.cs:45,51,63 (16B/2s 落盘) + SystemMonitorViewModel.cs:296-300 (每次采样 Task.Run 写)",
    "fullproc":     "ProcessManagerService.cs:90,98-208 (全进程枚举+MainModule) + ProcessScanner.cs:252-288 (WMI 全量)",
    "gethistory":   "MainWindow.xaml.cs:714-738 (LoadDay 全天) + :753 LoadRecentDays(days) + MetricsPersistenceService.cs:51 (2s/条→43200点/天)",
    "schedsave":    "SchedulerService.cs:235 (每次执行 finally 同步 SaveAll) + SchedulerStorageService.cs:107-116 (WriteIndented+tmp+Delete+Move)",
    "netsh5s":      "NetworkMonitorViewModel.cs:354-362 (1s Timer 常驻) + :374 (每5s RefreshPorts→netsh) + CompositePortBridgeService.cs:97",
    "portscan":     "PortScanner.cs:56,137-162 (TCP connect 30端口/并发50/800ms超时) + ServerConstants.cs:390 (10s TTL) + ServerDetector.cs:1089 (5s循环)",
    "serverdet":    "ServerDetector.cs:174-263 (一次完整检测6步) + :1053-1097 (5s循环) + ProcessScanner.cs:252-288 (WMI)",
    "cfgkeystroke": "ConfigEditorViewModel.cs:1205-1223 → ConfigDescriptorRegistry.cs:196-253 (每键击 2×1590 描述符扫描)",
    "jarident":     "JarCoreIdentifier.cs:152-162 (流式读 MANIFEST) + :266-294,381-389 (6×N 条目前缀遍历)",
    "plugindl":     "PluginManagerService.cs:101,176,378-386 (整包进内存+WriteAllBytes)",
    "bridgeui":     "WebView2BridgeService.cs:754-770 (全部 handler BeginInvoke 到 UI 线程) + MainWindow.xaml.cs:714 (同步读盘)",
    "logstorm":     "WebView2BridgeService.cs:682,693,445,512 (每条桥消息/每个嵌入资源请求 Log.Information → Serilog 写盘)",
    "dualchart":    "DualLineChart.tsx:109-150 (buildLinePath 全量点拼 path) + :186-195 (hover 线性扫描)",
    "histpoll":     "SystemMonitorPage.tsx:353-367 (指标2s/历史10s/亲和5s) + MainWindow.xaml.cs:714 (getHistory)",
    "dashpoll":     "DashboardPage.tsx:1142-1147 (3s×3 invoke) + AppLayout.tsx:45 (1s clock)",
    "netpage":      "NetworkMonitorPage.tsx:304-308 (5s 6连发 invoke)",
    "particle":     "ParticleField.tsx:91,121-183 + AppLayout.tsx:88 + App.tsx:73 (双 rAF 常驻, n²连线)",
    "gaugering":    "GaugeRing.tsx:42-54 (600ms rAF/SVG 重渲染, 4环)",
    "cfgpage":      "ConfigEditorPage.tsx:500-516 (5s 全配置轮询) + :1382-1532 (无虚拟化大列表)",
    "countdown":    "DashboardPage.tsx:200-215 (每个监管重启徽标 250ms interval)",
}

WL: List[Workload] = [
    # ── 常驻后台（无论用户在哪页都跑）──
    Workload("sysmon", "系统监控采样(常驻)", E["sysmon_vm43"], 2.0, 18.0, 1.0, 16, False, False, "🔴",
             "每2s: 6次PerformanceCounter + java枚举 + DriveInfo + 16B写。后台常驻无法关闭"),
    Workload("metrics_write", "指标落盘", E["metrics16"], 2.0, 1.0, 0.0, 16, False, False, "🟠",
             "每2s一个 Flush(false) 系统调用写 HDD，写入频率过高增大磁盘队列"),
    Workload("netsh", "网络监控1s常驻+netsh", E["netsh5s"], 1.0, 12.0, 2.0, 64_000, True, True, "🔴",
             "1s UI Timer永不停止(页面不可见也跑)；每5s spawn netsh 子进程 + 6次端口表 dump"),
    Workload("logstorm", "Serilog 日志风暴", E["logstorm"], 1.0, 3.0, 0.5, 4_000, True, False, "🟠",
             "每条桥消息/资源请求 Log.Information → 持续写 HDD"),
    # ── 页面触发 ──
    Workload("serverdet", "服务器自动检测(5s循环)", E["serverdet"], 5.0, 700.0, 30.0, 600_000, True, False, "🔴",
             "WMI 全量 0.5-3s + 30端口扫描 + 每候选进程深检(JAR/配置/TCP)"),
    Workload("portscan", "端口扫描", E["portscan"], 10.0, 800.0, 2.0, 0, False, False, "🟠",
             "30 端口 TCP connect 并发50 超时800ms；空系统也扫；缓存10s"),
    Workload("gethistory", "历史图表(当天43k点)", E["gethistory"], 10.0, 650.0, 24.0, 700_000, False, False, "🔴",
             "读 675KB + 建43,200对象 + 序列化 ~4-6MB JSON 直发前端；桥 handler 在 UI 线程读盘"),
    Workload("histrange", "多天历史(最多30天130万点)", E["gethistory"], 0.0, 6_000.0, 480.0, 21_000_000, False, False, "🔴",
             "LoadRecentDays(30)=1,296,000点 → 内存峰值~500MB + 序列化 120-180MB JSON；6GB 机险爆"),
    Workload("fullproc", "全进程枚举", E["fullproc"], 0.0, 1_800.0, 40.0, 200_000, True, True, "🔴",
             "150-300进程 × 属性读取(MainModule开句柄极慢) + WMI 全量；缓存2s太短；UI线程同步"),
    Workload("schedsave", "调度器持久化", E["schedsave"], 30.0, 120.0, 4.0, 300_000, True, False, "🔴",
             "每次任务执行 finally 同步全量重写 JSON；WriteIndented+tmp+Delete+Move；并发写无锁竞争"),
    Workload("cfgkeystroke", "配置编辑(每键击)", E["cfgkeystroke"], 0.1, 1.5, 0.1, 0, False, True, "🟠",
             "每键 2×1590 描述符 EndsWith 扫描 + 正则首次编译 50-100ms"),
    Workload("jarident", "JAR 核心识别", E["jarident"], 300.0, 2_500.0, 8.0, 4_000_000, True, False, "🟠",
             "流式读 MANIFEST(好) 但 6×N 条目前缀遍历；首次单 jar 1-3s；缓存5min"),
    Workload("plugindl", "插件安装(整包进内存)", E["plugindl"], 0.0, 400.0, 280.0, 200_000_000, False, False, "🟠",
             "100MB 插件 → byte[]+流+SHA1 副本峰值 ~200-300MB；仅用户操作触发"),
    Workload("bridgeui", "WebView2 桥 UI 封送", E["bridgeui"], 1.0, 5.0, 1.0, 0, False, True, "🟠",
             "所有 handler 一律 BeginInvoke 到 UI 线程；getHistory 同步读盘在 UI 线程"),
    # ── 前端 ──
    Workload("dualchart", "SVG 图表(43k点 path)", E["dualchart"], 10.0, 900.0, 40.0, 0, False, True, "🔴",
             "43k点×4条 = ~4.4MB path 字符串重建 + DOM 光栅化(弱机软件渲染可飙到1-3s)；hover 每帧线性扫 43k"),
    Workload("histpoll", "监控页三路轮询", E["histpoll"], 2.0, 8.0, 4.0, 0, False, True, "🟠",
             "2s/10s/5s 三路 setInterval + 全页 setState 重渲染"),
    Workload("dashpoll", "仪表盘轮询+时钟", E["dashpoll"], 3.0, 6.0, 2.0, 0, False, True, "🟠",
             "3s×3 invoke + AppLayout 1s 时钟全页重渲染"),
    Workload("netpage", "网络页6连发", E["netpage"], 5.0, 10.0, 3.0, 0, False, True, "🟠",
             "5s 一次 Promise.allSettled 并发 6 个 invoke + 数千行端口表无虚拟化"),
    Workload("particle", "双粒子场 rAF", E["particle"], 0.0, 8.0, 2.0, 0, False, False, "🟠",
             "2 个 canvas rAF 全生命周期常驻 + O(n²) 连线；有 reduced-motion 开关但默认不生效"),
    Workload("gaugering", "GaugeRing 圆环动画", E["gaugering"], 2.0, 6.0, 1.0, 0, False, True, "🟠",
             "每次值变化 600ms rAF×4环 → SVG 高频重渲染"),
    Workload("cfgpage", "配置页大列表", E["cfgpage"], 5.0, 15.0, 6.0, 0, False, True, "🟠",
             "5s 全量配置 JSON 轮询 + 500-2000 DOM 节点无虚拟化"),
    Workload("countdown", "重启倒计时 250ms", E["countdown"], 0.25, 4.0, 1.0, 0, False, True, "🟡",
             "每个监管重启徽标 250ms interval → 4Hz 全页重渲染；多服叠加"),
]


# ─────────────────────────────────────────────────────────────
# 3. 仿真引擎
# ─────────────────────────────────────────────────────────────

# 修复后负载模型：5 个已落地修复（文件:行号见修复日志）对弱机参数的覆盖
#   修复A+D MainWindow.xaml.cs:714-784  历史降采样(43k→1.4k点/天) + 重活移出UI线程
#   修复C   MainWindow.xaml.cs:857-873 + ProcessManagerService.cs:38-40  全进程枚举出UI线程+缓存8s
#   修复B   SystemMonitorViewModel.cs:48-56,305-323  落盘批量(2s→10s)
#   修复E   SchedulerStorageService.cs:36-38,110-123  并发写加锁（不降速，消除竞态）
FIX_OVERRIDES = {
    "gethistory": dict(cpu_ms=150, mem_peak_mb=8, ui_cpu_ms=5),     # 43k→1.4k 序列化量 ↓30x；重活后台
    "histrange":  dict(cpu_ms=1200, mem_peak_mb=80, ui_cpu_ms=5),   # 130万→4.3万点；内存480→80MB
    "fullproc":   dict(cpu_ms=1800, mem_peak_mb=40, ui_cpu_ms=5),   # 枚举仍重但已出 UI 线程
    "dualchart":  dict(cpu_ms=40, ui_cpu_ms=40),                    # 1天视图 1.4k 点 path（原43k）
    "metrics_write": dict(period_s=10.0, cpu_ms=1),                 # 落盘批量 2s→10s，唤醒降 5x
}


def build_fixed_workloads() -> List[Workload]:
    """基于 WL 克隆出『修复后』负载表，套用 FIX_OVERRIDES。红线项（动画/服务器检测）一律不覆盖。"""
    fixed = []
    for w in WL:
        ov = FIX_OVERRIDES.get(w.key)
        if ov is None:
            fixed.append(w)
            continue
        fixed.append(Workload(
            key=w.key, name=w.name, evidence=w.evidence,
            period_s=ov.get("period_s", w.period_s),
            cpu_ms=ov.get("cpu_ms", w.cpu_ms),
            mem_peak_mb=ov.get("mem_peak_mb", w.mem_peak_mb),
            disk_bytes=w.disk_bytes, disk_random=w.disk_random,
            ui_thread=w.ui_thread, rating=w.rating, note=w.note,
            ui_cpu_ms=ov.get("ui_cpu_ms", w.ui_cpu_ms)))
    return fixed


WL_FIXED = build_fixed_workloads()


def scenario_fixed(name: str, active_keys: List[str], hw: Hardware = HW) -> Dict:
    """用『修复后』负载表跑同一场景（内部换用 WL_FIXED）"""
    global WL
    saved = WL
    try:
        WL = WL_FIXED
        return scenario(name, active_keys, hw)
    finally:
        WL = saved
def scenario(name: str, active_keys: List[str], hw: Hardware = HW, parallel_threads: int = 4) -> Dict:
    """按场景聚合负载。active_keys 指定哪些 workload 在该场景激活。
    cpu_ms 是弱机(i3)单次耗时基准 → 按目标平台单核算力缩放：scale = 弱机gips_single / 目标gips_single。
    这样超算上的串行任务(JSON序列化/SVG拼path/进程枚举)会按单核性能真实提速，不会把弱机耗时硬套上去。"""
    rows = [w for w in WL if w.key in active_keys]
    scale = HW.gips_single / hw.gips_single   # 弱机 6.0 GIPS 为基准 = 1.0
    # CPU：单线程总时间(ms/s) = Σ(缩放后 cpu_ms / period)；多线程容量 = gips_total/gips_single 等效线程数
    st_ms_per_s = sum(w.cpu_ms * scale / w.period_s for w in rows if w.period_s > 0)
    # 一次性/按需项按"高压并发"折算：假设每 10s 触发一次
    on_demand = [w for w in rows if w.period_s == 0]
    st_ms_per_s += sum(w.cpu_ms * scale / 10.0 for w in on_demand)

    eq_threads = hw.gips_total / hw.gips_single  # 等效全速线程数
    cpu_util_pct = (st_ms_per_s / 1000.0) / eq_threads * 100.0
    cpu_util_pct += hw.baseline_cpu_pct

    # 磁盘（修正单位：bytes/s ÷ (MB/s × 1e6) × 1000 = ms/s）
    seq_bytes_s = sum(w.disk_bytes / w.period_s for w in rows if w.period_s > 0 and not w.disk_random)
    rnd_iops = sum(1.0 / w.period_s for w in rows if w.period_s > 0 and w.disk_random and w.disk_bytes > 0)
    seq_bytes_s += sum(w.disk_bytes / 10.0 for w in on_demand if not w.disk_random)
    rnd_iops += sum(1.0 / 10.0 for w in on_demand if w.disk_random and w.disk_bytes > 0)
    seq_time_ms_s = seq_bytes_s / (hw.disk_seq_read_mbps * 1e6) * 1000.0
    disk_util_pct = min(100.0, seq_time_ms_s / 10.0 + (rnd_iops / hw.disk_random_iops) * 100.0)

    # 内存峰值（并行峰值求和的上限，取 max(基线+峰值, 各峰值和×0.7)）
    baseline_mb = hw.baseline_ram_gb * 1024
    sum_peak = sum(w.mem_peak_mb for w in rows)
    peak_mb = max(baseline_mb + max(w.mem_peak_mb for w in rows or [WL[0]]),
                  baseline_mb + sum_peak * 0.7)
    ram_pct = peak_mb / (hw.ram_gb * 1024) * 100.0

    # UI 线程阻塞项（单核串行，按 scale 缩放；有 ui_cpu_ms 覆盖时用其 UI 部分耗时）
    ui_items = [w for w in rows if w.ui_thread and w.period_s > 0]
    ui_ms_s = sum((w.ui_cpu_ms if w.ui_cpu_ms > 0 else w.cpu_ms) * scale / w.period_s for w in ui_items)

    return dict(name=name, st_ms_per_s=st_ms_per_s, cpu_util_pct=cpu_util_pct,
                seq_bytes_s=seq_bytes_s, rnd_iops=rnd_iops, disk_util_pct=disk_util_pct,
                peak_mb=peak_mb, ram_pct=ram_pct, ui_ms_s=ui_ms_s)


def worst_latency(active_keys: List[str], top: int = 3, hw: Hardware = HW,
                  wl: List[Workload] | None = None) -> List[Tuple[str, float, str]]:
    """最坏单次冻结：一次性/按需项按单次 CPU 时间(按目标平台单核缩放)；周期项按单次 CPU 时间"""
    if wl is None:
        wl = WL
    scale = HW.gips_single / hw.gips_single
    out = []
    for w in wl:
        if w.key in active_keys:
            out.append((w.name, w.cpu_ms * scale / 1000.0, w.rating))
    return sorted(out, key=lambda x: -x[1])[:top]


def fmt(s: Dict, hw: Hardware = HW) -> str:
    r = s["ram_pct"]
    ram_flag = "💥" if r > 90 else ("⚠️" if r > 70 else "ok")
    cpu_flag = "🔴" if s["cpu_util_pct"] > 60 else ("🟠" if s["cpu_util_pct"] > 30 else "🟢")
    disk_flag = "🔴" if s["disk_util_pct"] > 60 else ("🟠" if s["disk_util_pct"] > 25 else "🟢")
    return (f"  场景: {s['name']}\n"
            f"  ├─ CPU   : 等效单线程 {s['st_ms_per_s']:.0f} ms/s | {hw.name.split('/')[0]} 占用 {s['cpu_util_pct']:.0f}%  {cpu_flag}\n"
            f"  ├─ 磁盘  : 顺序 {s['seq_bytes_s']/1024:.0f} KB/s + 随机 {s['rnd_iops']:.1f} IOPS | 占用 {s['disk_util_pct']:.0f}%  {disk_flag}\n"
            f"  ├─ 内存  : 峰值 {s['peak_mb']:.0f} MB / {hw.ram_gb*1024:.0f} MB ({s['ram_pct']:.0f}%)  {ram_flag}\n"
            f"  └─ UI 线程: {s['ui_ms_s']:.0f} ms/s 阻塞性重活")


def main() -> None:
    print("=" * 78)
    print("MSMC 极端垃圾硬件负载仿真  (P10 纸面演练)")
    print(f"目标: {HW.name}")
    print(f"对比: {HW_MODERN.name}  (比值: CPU {HW_MODERN.gips_total/HW.gips_total:.1f}x  "
          f"随机IO {HW_MODERN.disk_random_iops/HW.disk_random_iops:.0f}x  "
          f"顺序IO {HW_MODERN.disk_seq_read_mbps/HW.disk_seq_read_mbps:.0f}x)")
    print("=" * 78)

    # ── 场景 1: 空闲后台（App 启动后, 用户不操作, 监控常驻）──
    idle = scenario("空闲后台(仅常驻: 监控采样+落盘+网络1s+netsh+日志)",
                    ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui"])
    print(f"\n{'─'*70}\n【场景1】{idle['name']}")
    print(fmt(idle))

    # ── 场景 2: 系统监控页打开 ──
    mon = scenario("系统监控页(监控采样+历史图表+轮询+粒子+圆环+桥)",
                   ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                    "gethistory", "dualchart", "histpoll", "particle", "gaugering"])
    print(f"\n{'─'*70}\n【场景2】{mon['name']}")
    print(fmt(mon))
    print("  最坏单次卡顿: ", " | ".join(f"{n}={t:.1f}s({r})" for n, t, r in worst_latency(["gethistory", "dualchart"], top=2)))

    # ── 场景 3: 高压综合（监控页 + 服务器检测 + 全进程枚举 + 调度执行 + 配置编辑）──
    hp = scenario("高压综合(监控页+检测5s+端口扫+全进程+调度执行+配置编辑+插件安装)",
                  ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                   "gethistory", "dualchart", "histpoll", "particle", "gaugering",
                   "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                   "jarident", "plugindl", "cfgpage", "countdown"])
    print(f"\n{'─'*70}\n【场景3】{hp['name']}")
    print(fmt(hp))
    print("  最坏单次阻塞:")
    for n, t, r in worst_latency(["histrange", "fullproc", "serverdet", "plugindl", "jarident"]):
        print(f"    {n}: ~{t:.1f}s {r}")

    # ── 高压 + 多天历史(灾难场景) ──
    cat = scenario("灾难场景(高压 + 打开30天历史 130万点)",
                   ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                    "gethistory", "histrange", "dualchart", "histpoll", "particle", "gaugering",
                    "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                    "jarident", "plugindl", "cfgpage", "countdown"])
    print(f"\n{'─'*70}\n【场景4】{cat['name']}")
    print(fmt(cat))

    # ── 现代机对比（同一高压场景）──
    hp_modern = scenario("高压综合(现代机对比)", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                   "gethistory", "dualchart", "histpoll", "particle", "gaugering",
                   "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                   "jarident", "plugindl", "cfgpage", "countdown"], hw=HW_MODERN)
    print(f"\n{'─'*70}\n【场景3-现代机】{hp_modern['name']}")
    print(fmt(hp_modern, HW_MODERN))
    print("  最坏单次操作: ", " | ".join(f"{n}={t:.1f}s({r})" for n, t, r in worst_latency(["histrange", "fullproc", "serverdet", "plugindl", "jarident"], top=3, hw=HW_MODERN)))

    # ═══════════════════════════════════════════════════════════
    # 超算级平台：2× Threadripper PRO 7995WX / 512GB ECC / NVMe+内存盘
    # ═══════════════════════════════════════════════════════════
    print(f"\n{'='*78}\n超算级平台仿真: {HW_TR.name}")
    print(f"相对弱机: CPU并行容量 {HW_TR.gips_total/HW.gips_total:.0f}x | 单核 {HW_TR.gips_single/HW.gips_single:.0f}x | "
          f"顺序IO {HW_TR.disk_seq_read_mbps/HW.disk_seq_read_mbps:.0f}x | 随机IO {HW_TR.disk_random_iops/HW.disk_random_iops:.0f}x | "
          f"内存 {HW_TR.ram_gb/HW.ram_gb:.0f}x")
    print("=" * 78)

    tr_idle = scenario("空闲后台(超算)", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui"], hw=HW_TR)
    tr_mon = scenario("系统监控页(超算)", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                    "gethistory", "dualchart", "histpoll", "particle", "gaugering"], hw=HW_TR)
    tr_hp = scenario("高压综合(超算)", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                   "gethistory", "dualchart", "histpoll", "particle", "gaugering",
                   "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                   "jarident", "plugindl", "cfgpage", "countdown"], hw=HW_TR)
    tr_cat = scenario("灾难+30天历史(超算)", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                    "gethistory", "histrange", "dualchart", "histpoll", "particle", "gaugering",
                    "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                    "jarident", "plugindl", "cfgpage", "countdown"], hw=HW_TR)

    for s in (tr_idle, tr_mon, tr_hp, tr_cat):
        print(f"\n{'─'*70}\n【超算】{s['name']}")
        print(fmt(s, HW_TR))
    print("  超算最坏单次操作(单核串行): ",
          " | ".join(f"{n}={t:.2f}s({r})" for n, t, r in worst_latency(["histrange", "fullproc", "serverdet", "plugindl", "jarident"], top=4, hw=HW_TR)))

    # ── P10 修复验证：弱机 修复前 vs 修复后（5 项修复均不碰动画/服务器检测红线）──
    print(f"\n{'='*78}\nP10 弱机修复验证：修复前 vs 修复后（红线不动：动画/服务器检测路径零改动）")
    print(f"{'场景':<34}{'CPU(前→后)':>18}{'UI阻塞(前→后)':>18}{'内存峰值(前→后)':>22}{'最坏单次(前→后)':>22}")
    weak_before = []
    weak_after = []
    for tag, keys in (
        ("空闲后台", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui"]),
        ("系统监控页", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                        "gethistory", "dualchart", "histpoll", "particle", "gaugering"]),
        ("高压综合", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                      "gethistory", "dualchart", "histpoll", "particle", "gaugering",
                      "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                      "jarident", "plugindl", "cfgpage", "countdown"]),
        ("灾难+30天历史", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                            "gethistory", "histrange", "dualchart", "histpoll", "particle", "gaugering",
                            "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                            "jarident", "plugindl", "cfgpage", "countdown"]),
    ):
        b = scenario(tag, keys)
        a = scenario_fixed(tag, keys)
        weak_before.append(b)
        weak_after.append(a)
        wl_keys = [w for w in keys if w in ("histrange", "gethistory", "fullproc", "dualchart")]
        wb = worst_latency(wl_keys, top=1)[0][1] if wl_keys else 0.0
        wa = worst_latency(wl_keys, top=1, wl=WL_FIXED)[0][1] if wl_keys else 0.0
        wb_s = f"{wb:.1f}s" if wb else "—"
        wa_s = f"{wa:.1f}s" if wa else "—"
        print(f"{tag:<34}{b['cpu_util_pct']:>6.0f}%→{a['cpu_util_pct']:>3.0f}%{b['ui_ms_s']:>9.0f}→{a['ui_ms_s']:>5.0f} ms/s"
              f"{b['peak_mb']:>9.0f}→{a['peak_mb']:>6.0f} MB   {wb_s:>5}→{wa_s:>4}")
    print("\n说明: 修复A/D 历史降采样+出UI线程 | 修复C 进程枚举出UI线程+缓存8s | 修复B 落盘批量2s→10s | 修复E 调度写锁(防竞态不降速)")
    print("      红线核验: 动画(ParticleField/GaugeRing/Reveal/CSS/WindowEffects)与服务器检测(ServerDetector/ProcessScanner/PortScanner) 0 改动")

    # ── 三平台对比汇总（灾难场景）──
    print(f"\n{'='*78}\n三平台对比汇总（灾难场景: 高压 + 打开30天历史 130万点）")
    rows_cmp = []
    for tag, hw in (("弱机 i3/6GB/HDD", HW), ("现代 8C16T/NVMe", HW_MODERN), ("超算 2×TR/512GB", HW_TR)):
        s = scenario("对比", ["sysmon", "metrics_write", "netsh", "logstorm", "bridgeui",
                             "gethistory", "histrange", "dualchart", "histpoll", "particle", "gaugering",
                             "serverdet", "portscan", "fullproc", "schedsave", "cfgkeystroke",
                             "jarident", "plugindl", "cfgpage", "countdown"], hw=hw)
        rows_cmp.append((tag, s, hw))
    print(f"{'平台':<24}{'CPU占用':>10}{'磁盘占用':>10}{'内存占用':>12}{'UI线程阻塞':>14}")
    for tag, s, hw in rows_cmp:
        print(f"{tag:<24}{s['cpu_util_pct']:>8.0f}%{s['disk_util_pct']:>8.0f}%{s['ram_pct']:>10.0f}%{s['ui_ms_s']:>12.0f} ms/s")
    print("\n注: 单核串行任务(JSON序列化/SVG path/进程枚举)三平台按单核性能缩放; "
          "UI 线程瓶颈在超算上仍是主要体感限制(所有 handler 压 UI 线程)。")

    # ── 单条负载明细 ──
    print(f"\n{'='*78}\n负载明细（按 CPU 单次耗时排序, 弱机值）")
    print(f"{'元素':<22}{'周期':>7}{'单次CPU':>9}{'峰值MB':>8}{'IO':>12}{'评级':>4}")
    for w in sorted(WL, key=lambda x: -x.cpu_ms):
        per = f"{w.period_s:>6.2f}s" if w.period_s > 0 else "按需"
        io = f"{w.disk_bytes/1024:.0f}KB" if w.disk_bytes > 0 else "-"
        print(f"{w.name[:20]:<22}{per:>7}{w.cpu_ms:>7.0f}ms{w.mem_peak_mb:>8.0f}{io:>12}{w.rating:>4}")


if __name__ == "__main__":
    main()
