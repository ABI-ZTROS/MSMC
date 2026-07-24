# UI 重构方案调研与计划

> 调研日期：2026-07-25
> 当前技术栈：WPF + MVVM + CommunityToolkit.Mvvm + MaterialDesignThemes + LiveCharts2

---

## 一、现状调研

### 1.1 当前 UI 架构

| 层级 | 技术 | 行数 | 文件数 |
|------|------|------|--------|
| Views (XAML) | WPF XAML | 4,132 | 13 |
| Views (C# code-behind) | C# | 2,320 | 5 |
| ViewModels | CommunityToolkit.Mvvm | 5,039 | 7 |
| Services (后端) | C# | 17,409 | 41 |
| Models | C# | 1,140 | ~15 |
| **总计 UI 相关** | | **~11,500** | **25** |

### 1.2 页面清单（7个页面 + 1个窗口）

| 页面 | 复杂度 | 功能 |
|------|--------|------|
| MainWindow | 高 | 主窗口 + 导航框架 + 侧边栏 |
| ServerDetectionPage | 高 | 服务器检测 + 导入 + 管理 |
| SystemMonitorPage | 中 | CPU/内存/磁盘仪表盘 + LiveCharts 图表 |
| NetworkMonitorPage | 高 | 网速监控 + 端口桥接 + 网卡列表 |
| ConfigEditorPage | 高 | 配置编辑 + 分组 + 搜索 + 未保存标记 |
| SettingsPage | 中 | 主题 + 语言 + Java 管理 + 各种设置 |
| UserAgreementWindow | 低 | 用户协议（**排除在重构范围外**） |

### 1.3 自定义控件（5个）

| 控件 | 说明 |
|------|------|
| GaugeRingControl | 圆环仪表盘（自绘，性能优化过） |
| ColorPickerControl | 颜色选择器 |
| IndependentLoadingIcon | 独立加载动画 |

### 1.4 后端服务架构

Services 层已经有良好的接口抽象（8个接口），前后端分离度较高：
- `IConfigManager` — 配置管理
- `IPortBridgeService` — 端口桥接
- `ITcpForwarder` — TCP 转发
- `ISystemMonitor` — 系统监控
- `IServerDetector` — 服务器检测
- `IAppConfigService` — 应用配置
- `IJavaFinderService` — Java 查找
- `IUserAgreementService` — 用户协议

---

## 二、可选技术方案对比

### 方案 A：WebView2 + React/Vue/Svelte（推荐 ⭐）

**架构**：WPF 窗口只放一个 `WebView2` 控件，UI 完全用 Web 技术栈实现，通过 JS ↔ C# 互操作调用后端服务。

| 优点 | 缺点 |
|------|------|
| ✅ CSS 生态完整，样式自由度极高 | ❌ 需要维护 Web + WPF 两套构建 |
| ✅ 海量 UI 组件库可选（shadcn/ui, MUI 等） | ❌ 前后端通信需要一层桥接（WebView2 互操作） |
| ✅ 动画效果远超 WPF，性能更好 | ❌ 打包体积增加 ~50-100MB（WebView2 运行时） |
| ✅ 开发效率高，热更新 | ❌ 高频率数据更新（如网速图表）需要优化通信 |
| ✅ 设计资源丰富，容易做出高级感 | ❌ 需要前端技术栈 |
| ✅ 后端 Services 层几乎可以 100% 复用 | |
| ✅ 未来可轻松迁移到纯 Web / 跨平台 | |

**代码量估算**：
- 前端重写：~6,000-8,000 行（React + TypeScript）
- WebView2 桥接层：~500-800 行 C#
- 后端复用：~17,000 行（几乎不动）
- **总计新增/重写：~7,000-9,000 行**

### 方案 B：Blazor Hybrid（WebView2 + Blazor）

**架构**：用 .NET MAUI Blazor 或 WPF + WebView2 + Blazor 组件，UI 用 Razor + CSS，C# 全栈。

| 优点 | 缺点 |
|------|------|
| ✅ 全 C# 技术栈，不用学 JS | ❌ Blazor 组件生态不如 React 丰富 |
| ✅ 后端代码直接复用，无需桥接层 | ❌ 性能比原生 JS 框架差（尤其是动画） |
| ✅ 可以用 CSS，样式比 WPF 自由 | ❌ 调试体验不如纯 Web |
| ✅ 单技术栈维护成本低 | ❌ 高级动画和复杂交互不如 React 灵活 |
| ✅ 与现有 .NET 生态无缝集成 | ❌ 社区资源和教程相对少 |

**代码量估算**：
- Blazor 组件重写：~5,000-7,000 行（Razor + C#）
- 后端复用：~17,000 行
- **总计新增/重写：~5,000-7,000 行**

### 方案 C：Avalonia UI

**架构**：跨平台 XAML 框架，语法和 WPF 类似，但支持 CSS 样式（Avalonia 11+）。

| 优点 | 缺点 |
|------|------|
| ✅ XAML 语法接近 WPF，学习曲线平缓 | ❌ 仍然是 XAML 思维，CSS 支持有限 |
| ✅ 支持跨平台（Windows/Mac/Linux） | ❌ 生态不如 WPF 和 Web |
| ✅ 有 CSS 样式系统（Avalonia Style + CSS 语法） | ❌ MaterialDesign/Avalonia 版可能不完整 |
| ✅ 后端代码 100% 复用 | ❌ LiveCharts2 支持但可能有坑 |
| ✅ 可编译为单文件，体积小 | ❌ 高级 UI 效果实现成本仍高于 Web |

**代码量估算**：
- XAML 迁移 + 样式重写：~4,000-6,000 行
- ViewModel 修改：~500-1,000 行
- 后端复用：~17,000 行
- **总计新增/重写：~4,500-7,000 行**

### 方案 D：继续 WPF，但深度优化样式

**架构**：不换框架，优化现有 WPF 样式系统，引入更现代的设计语言。

| 优点 | 缺点 |
|------|------|
| ✅ 改动最小，风险最低 | ❌ 还是 WPF，样式自由度本质受限 |
| ✅ 所有代码完全复用 | ❌ "人机感" 的根本问题无法解决 |
| ✅ 无需学习新技术 | ❌ 动画和交互效果上限低 |
| ✅ 单文件打包体积小 | ❌ 设计资源少，难以做出高级感 |

**代码量估算**：
- 样式优化：~1,000-2,000 行 XAML
- **总计改动：~1,000-2,000 行**

---

## 三、方案推荐与分析

### 3.1 我的推荐：方案 A（WebView2 + React）

**理由**：
1. **UI 质量上限最高**：CSS + 现代前端框架可以做出任何你想要的视觉效果，彻底摆脱"人机感"
2. **后端几乎零改动**：Services 层已经有完整的接口抽象，只需要加一层 WebView2 桥接
3. **生态碾压**：组件库、动画库、设计资源的丰富程度是 WPF 的百倍以上
4. **技能可迁移**：Web 技术栈的经验价值远超 WPF
5. **未来扩展性**：想做跨平台、想做 Web 版、想做移动端，都有清晰路径

### 3.2 风险点与应对

| 风险 | 概率 | 影响 | 应对措施 |
|------|------|------|----------|
| WebView2 打包体积大 | 高 | 中 | 用 evergreen bootstrapper 或固定版本嵌入 |
| 前后端通信性能（高频率数据） | 中 | 中 | 用 `CoreWebView2.PostWebMessageAsJson` 批量推送 + 防抖 |
| 前端技术栈学习成本 | 中 | 低 | 用 TypeScript + 成熟脚手架，渐进式开发 |
| 调试复杂度增加 | 中 | 低 | 浏览器 DevTools + Visual Studio 双端调试 |
| 图表库迁移成本 | 中 | 中 | 用 ECharts / Chart.js，功能比 LiveCharts2 更强 |

### 3.3 分阶段实施路线

**阶段 1：基础设施搭建（预计 20% 工作量）**
- WebView2 宿主窗口 + 前端项目初始化
- 前后端通信桥接层（C# ↔ JS）
- 前端路由 + 布局框架 + 主题系统
- 构建系统集成（前端打包嵌入 WPF）

**阶段 2：核心页面迁移（预计 50% 工作量）**
- 主窗口框架 + 侧边栏导航
- 系统监控页（仪表盘 + 图表）
- 网络监控页（网速 + 端口桥接）
- 服务器检测页

**阶段 3：高级功能页（预计 25% 工作量）**
- 配置编辑器（复杂交互）
- 设置页
- 全局通知 / Toast / 弹窗系统

**阶段 4：优化 + 打包（预计 5% 工作量）**
- 性能调优
- 单文件打包
- 测试与 Bug 修复

---

## 四、可用技能与插件分析

### 当前可用技能

| 技能 | 用途 | 相关度 |
|------|------|--------|
| `frontend-skill` | 前端 UI 设计最佳实践 | ⭐⭐⭐⭐⭐ |
| `vercel-react-best-practices` | React/Next.js 性能优化 | ⭐⭐⭐⭐ |
| `brainstorming` | 设计头脑风暴 | ⭐⭐⭐⭐ |
| `writing-plans` | 实施计划编写 | ⭐⭐⭐ |
| `mcp-builder` | MCP 服务开发（如果需要） | ⭐⭐ |
| `agent-browser` | 浏览器自动化测试 | ⭐⭐⭐ |
| `dogfood` | UI 质量测试 / 找 Bug | ⭐⭐⭐⭐ |

### 当前可用插件

| 插件 | 用途 | 相关度 |
|------|------|--------|
| GitHub | CI/CD + PR 流程 | ⭐⭐⭐ |
| Lark | 飞书通知（如果需要） | ⭐ |
| Tailtest | 测试生成 | ⭐⭐⭐ |

---

## 五、关键决策点

在开始实施前，需要明确以下问题：

1. **前端框架选择**：React / Vue / Svelte？
   - React 生态最强，组件库最多
   - Vue 上手简单，中文资源多
   - Svelte 性能最好，打包最小

2. **UI 组件库选择**：
   - shadcn/ui（高度可定制，现代感强）
   - Ant Design（功能最全，企业风）
   - Material UI（和现有 MaterialDesign 风格接近）
   - Arco Design（字节出品，中文文档好）

3. **图表库选择**：
   - ECharts（功能最强，中文文档好）
   - Chart.js（轻量简洁）
   - Recharts（React 风格）
   - D3.js（高度定制，学习曲线陡）

4. **构建集成方式**：
   - 前端单独构建，产物作为 WPF 资源嵌入
   - 用 ASP.NET Core 内置 Kestrel 提供本地 HTTP 服务
   - 直接用 file:// 协议加载本地 HTML

5. **是否保留 WPF 窗口 chrome**：
   - 完全自绘窗口（更现代，但工作量大）
   - 保留系统窗口（简单，但效果打折扣）
   - WebView2 提供的窗口扩展

---

## 六、总结

| 方案 | 推荐度 | UI 效果 | 工作量 | 技术风险 | 后端复用率 |
|------|--------|---------|--------|----------|------------|
| A. WebView2 + React | ⭐⭐⭐⭐⭐ | 🌟🌟🌟🌟🌟 | 大 | 中 | 95%+ |
| B. Blazor Hybrid | ⭐⭐⭐ | 🌟🌟🌟 | 中 | 中高 | 95%+ |
| C. Avalonia UI | ⭐⭐⭐ | 🌟🌟🌟 | 中大 | 中 | 90%+ |
| D. 继续 WPF 优化 | ⭐⭐ | 🌟🌟 | 小 | 低 | 100% |

**我的建议**：如果决心彻底改变 UI 风格，选 **方案 A（WebView2 + React）**，一次到位。如果只是想小幅度改进，选方案 D 成本最低。

方案 A 虽然前期工作量大，但：
1. UI 质量有质的飞跃
2. 后端几乎不用动
3. 技术投资长期有价值
4. 我手头的 `frontend-skill` 和 `vercel-react-best-practices` 技能可以直接用上
