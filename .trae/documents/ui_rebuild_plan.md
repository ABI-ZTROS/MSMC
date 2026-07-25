# UI 复刻 & 重构方案

## 一、现状调研结论

### 1.1 当前问题

| 问题分类 | 具体表现 |
|---------|---------|
| **视觉风格偏差** | 当前前端是深色玻璃态（slate 色系），WPF 原版是 Material Design 风格，配色/圆角/阴影/间距体系完全不同 |
| **图标体系混乱** | 前端使用 emoji 图标，WPF 使用 FontAwesome 6 Solid/Regular |
| **硬编码泛滥** | 颜色、尺寸、间距全是 Tailwind 原子类硬编码，无主题变量系统 |
| **假数据充斥** | 图表数据、服务器列表、配置项等均为 mock 数据，未接后端真实数据 |
| **页面完整性不足** | 5 个页面中仅系统监控和服务器管理有部分实现，网络监控/配置编辑/设置基本为空壳 |
| **外层冲突** | WPF 有标题栏（正确），但底部状态栏风格与 WPF 不一致 |
| **组件化程度低** | 缺少可复用的基础组件（卡片、按钮、输入框等），每个页面重复写样式 |

### 1.2 WPF 原版结构梳理

**主窗口布局**（MainWindow.xaml）：
- 自定义标题栏（40px 高，可拖动，最小化/最大化/关闭按钮）
- WebView2 内容区（占满剩余空间）

**Material Design 主题资源**（AppResources.xaml）：
- 颜色体系：PrimaryHueMid / AccentText / CardBackground / MaterialDesignBody 等
- 圆角体系：AppCornerRadius / AppCornerRadiusSmall
- 动画体系：StandardEase / EmphasizedEase / PageEnterStoryboard
- 按钮样式：OptimizedButtonBase / OptimizedOutlinedButton / OptimizedFlatButton
- 卡片样式：AnimatedCardStyle（悬停上浮 + 边框高亮）

**5 个页面结构**：

| 页面 | 核心布局 | 关键组件 |
|------|---------|---------|
| **系统监控** | 垂直滚动 StackPanel | 控制按钮行 + 4列UniformGrid圆环卡片 + CPU折线图 + 内存折线图 |
| **服务器管理** | DockPanel（顶操作条 + 底启动命令 + 中左列表右详情） | 顶部操作条（刷新/自动检测/导入）+ 左侧搜索+分组列表（运行中/已知）+ 右侧Tab（控制台/JVM/命令预览）+ 底部启动命令预览 |
| **网络监控** | 两行Grid（顶仪表盘 + 底内容） | 顶部统计卡片+速度仪表盘 + 端口占用列表 + 桥接管理 |
| **配置编辑** | 左右布局（左分类 + 右配置项） | 分类 Expander 分组 + 配置项卡片（名称+描述+输入框）+ 顶部保存/刷新 |
| **设置** | 垂直分组卡片 | 主题设置（颜色选择器）+ 常规设置 + 关于信息 |

---

## 二、需要修改/新增的文件和模块

### 2.1 C# 后端（WPF / 桥接层）

| 文件 | 修改内容 |
|------|---------|
| `Views/MainWindow.xaml` | 保留自定义标题栏，确认 WebView2 占满内容区 |
| `Views/MainWindow.xaml.cs` | 补齐所有页面需要的桥接 API（见下方 API 清单） |
| `Services/WebView2/WebView2BridgeService.cs` | 如有需要，新增事件推送机制 |
| `Themes/AppResources.xaml` | （参考用，不改）提取颜色变量到前端 |

### 2.2 前端（React + TypeScript）

| 文件/目录 | 修改内容 |
|----------|---------|
| `src/styles/globals.css` | 建立 Material Design 主题变量系统（CSS 变量） |
| `src/styles/theme.css` | （新增）明/暗主题变量定义 |
| `src/components/AppLayout.tsx` | 调整布局：去标题栏、改状态栏为 MD 风格、侧边栏 MD 风格 |
| `src/components/Sidebar.tsx` | 重写为 Material Design 风格，hover 展开/折叠 |
| `src/components/ui/` | 新增/重写基础组件：MDButton、MDCard、MDTab、MDExpander、MDTextBox 等 |
| `src/components/ui/GaugeRing.tsx` | 重写为 WPF GaugeRingControl 风格 |
| `src/pages/SystemMonitorPage.tsx` | 像素级复刻系统监控页 + 接真实数据 |
| `src/pages/DashboardPage.tsx` | 像素级复刻服务器管理页 + 接真实数据 |
| `src/pages/NetworkMonitorPage.tsx` | 像素级复刻网络监控页 + 接真实数据 |
| `src/pages/ConfigEditorPage.tsx` | 像素级复刻配置编辑页 + 接真实数据 |
| `src/pages/SettingsPage.tsx` | 像素级复刻设置页 + 接真实数据 |
| `src/types/bridge.ts` | 补齐所有接口类型定义 |
| `src/utils/bridge.ts` | 统一桥接调用封装 |
| `src/stores/appStore.ts` | 扩展状态管理（主题、全局状态等） |

### 2.3 新增依赖

| 依赖 | 用途 |
|------|------|
| `react-icons` | FontAwesome 6 图标（替换 emoji） |
| `echarts` / `recharts` | 折线图、仪表盘（复刻 LiveCharts2 效果） |
| `clsx` | 已存在，继续使用 |

---

## 三、具体实施步骤

### 阶段 1：基础设施搭建

**目标**：建立与 WPF Material Design 一致的设计系统和布局框架

#### 步骤 1.1：主题变量系统
- 从 `AppResources.xaml` 提取所有颜色、圆角、间距、字号、阴影变量
- 在 CSS 中定义 `:root` 下的暗色主题变量（与 WPF 默认暗色主题一致）
- 变量命名与 WPF 资源 Key 对应（如 `--primary-hue-mid`、`--card-background`）
- 支持亮色主题切换（预留）

#### 步骤 1.2：基础组件库
- **MDCard**：Material Design 卡片（圆角、边框、悬停上浮动画）
- **MDButton**：三种按钮样式（实心/描边/扁平），与 `OptimizedButtonBase` 等一致
- **MDTab**：Material Design 风格标签页
- **MDExpander**：可折叠分组，与 WPF Expander 一致
- **MDTextBox**：Material Design 输入框（Outlined 风格）
- **GaugeRing**：圆环仪表盘，复刻 WPF `GaugeRingControl`

#### 步骤 1.3：布局框架调整
- **AppLayout**：
  - 移除标题栏相关代码（WPF 负责）
  - 底部状态栏改为 Material Design PrimaryDark 风格
  - 整体背景色与 WPF `MaterialDesignPaper` 一致
- **Sidebar**：
  - 重写为 Material Design 风格
  - hover 展开/折叠（展开 ~240px，折叠 ~56px）
  - 选中项高亮样式与 WPF `NavItemSelectedBrush` 一致
  - 图标改用 FontAwesome 6（react-icons/fa6）

#### 步骤 1.4：桥接 API 梳理与补齐
- 梳理 5 个页面所需的全部数据接口
- C# 端在 `MainWindow.xaml.cs` 中注册所有桥接 API
- 前端封装统一的 `bridge.request()` / `bridge.on()` 方法

**桥接 API 清单**：

| API 名称 | 用途 | 所属页面 |
|----------|------|---------|
| `app:getTheme` | 获取当前主题（明/暗 + 颜色配置） | 全局 |
| `app:setStatus` | 设置状态栏文字 | 全局 |
| `systemMonitor:getMetrics` | 获取当前系统指标（CPU/内存/磁盘/线程） | 系统监控 |
| `systemMonitor:getTrendData` | 获取趋势图表数据 | 系统监控 |
| `systemMonitor:start` | 开始监控 | 系统监控 |
| `systemMonitor:stop` | 停止监控 | 系统监控 |
| `server:list` | 获取服务器列表（运行中 + 已知） | 服务器管理 |
| `server:refresh` | 刷新检测 | 服务器管理 |
| `server:start` | 启动服务器 | 服务器管理 |
| `server:stop` | 停止服务器 | 服务器管理 |
| `server:getDetail` | 获取选中服务器详情 | 服务器管理 |
| `server:getConsoleLog` | 获取控制台日志 | 服务器管理 |
| `server:saveAsKnown` | 保存为已知服务器 | 服务器管理 |
| `network:getPorts` | 获取端口占用列表 | 网络监控 |
| `network:getSpeed` | 获取上传/下载速度 | 网络监控 |
| `network:getBridges` | 获取端口桥接列表 | 网络监控 |
| `network:addBridge` | 添加端口桥接 | 网络监控 |
| `network:removeBridge` | 移除端口桥接 | 网络监控 |
| `config:load` | 加载 server.properties 配置 | 配置编辑 |
| `config:save` | 保存配置 | 配置编辑 |
| `config:getCategories` | 获取配置分类 | 配置编辑 |
| `settings:get` | 获取设置项 | 设置 |
| `settings:set` | 修改设置项 | 设置 |
| `settings:updateTheme` | 更新主题颜色 | 设置 |

---

### 阶段 2：5 个页面像素级复刻

**总原则**：每个页面的布局结构、组件顺序、间距、配色都与 WPF XAML 一一对应。

#### 步骤 2.1：系统监控页（SystemMonitorPage）

布局结构（从上到下）：
1. **控制按钮行**：开始监控（实心按钮）+ 停止监控（描边按钮）
2. **4 列圆环卡片行**（UniformGrid）：
   - CPU：圆环仪表盘 + "CPU" 标签
   - 内存：圆环仪表盘 + 容量明细（已用/总共）
   - 磁盘：圆环仪表盘 + 磁盘明细（盘符+已用/总共）
   - 线程：三角感叹号图标 + 大号数字
3. **CPU 使用率趋势卡片**：标题 + LiveCharts 折线图
4. **内存使用率趋势卡片**：标题 + LiveCharts 折线图

数据接入：
- 每 2 秒调用 `systemMonitor:getMetrics` 更新当前值
- 每 5 秒调用 `systemMonitor:getTrendData` 更新图表
- 开始/停止按钮调用对应 API

#### 步骤 2.2：服务器管理页（DashboardPage）

布局结构（DockPanel 风格）：
1. **顶部操作条**（固定）：
   - 左侧：刷新按钮 + 自动检测按钮 + 导入服务器按钮
   - 中间：选中服务器状态点 + 状态文字 + 副标题
   - 右侧：忙碌提示（加载动画 + 文字）
2. **底部启动命令预览**（固定）：
   - 终端图标 + "启动命令" 标签
   - 等宽字体显示完整命令
   - 复制按钮
3. **中间内容区**（左右分栏，左 280px）：
   - **左侧列表**：
     - 搜索框（带搜索图标和清除按钮）
     - 运行中分组（Expander，带数字徽章）
     - 已知服务器分组（Expander，带数字徽章）
     - 空状态提示
   - **右侧 Tab 区域**：
     - Tab1 控制台：服务器控制按钮（启动/停止/保存）+ 服务器详情卡片 + 检测日志
     - Tab2 JVM 参数：内存设置（初始/最大）+ 快速预设（Aikar/G1GC/ZGC）+ 参数分类 + 参数列表
     - Tab3 命令预览：完整启动命令 + 复制按钮

数据接入：
- 每 3 秒调用 `server:list` 刷新列表
- 选中服务器后调用 `server:getDetail` 获取详情
- 控制台日志通过事件推送或轮询获取

#### 步骤 2.3：网络监控页（NetworkMonitorPage）

布局结构（上下两行）：
1. **顶部仪表盘行**：
   - 统计卡片：总端口数 / 已占用 / 占用率
   - 上传速度仪表盘（GaugeRing）
   - 下载速度仪表盘（GaugeRing）
   - 自动刷新指示器（旋转图标 + 文字）
2. **底部内容区**：
   - Tab 切换：端口占用 / 端口桥接
   - 端口占用列表（表格形式）
   - 端口桥接管理（添加/删除/列表）

数据接入：
- 每 2 秒刷新端口和速度数据
- 桥接操作调用对应 API

#### 步骤 2.4：配置编辑页（ConfigEditorPage）

布局结构：
1. **顶部操作条**：保存按钮 + 刷新按钮 + 文件路径显示
2. **主内容区**（垂直滚动）：
   - 多个分类 Expander 分组（如"通用设置"、"游戏设置"、"网络设置"等）
   - 每个配置项卡片：配置名（加粗）+ 配置描述（灰色小字）+ 输入框
   - 不同类型配置项：文本框 / 数字框 / 下拉选择 / 复选框

数据接入：
- 加载时调用 `config:load` 获取所有配置
- 保存时调用 `config:save` 提交修改
- 支持 Ctrl+S 快捷键保存

#### 步骤 2.5：设置页（SettingsPage）

布局结构（垂直分组卡片）：
1. **主题设置卡片**：
   - 明/暗模式切换
   - 主题色选择器（多个颜色方块）
   - 实时预览
2. **常规设置卡片**：
   - 自动启动
   - 最小化到托盘
   - 语言设置
   - 监控刷新频率
3. **关于卡片**：
   - 版本号
   - 项目链接
   - 许可证信息

数据接入：
- 加载时调用 `settings:get`
- 修改时调用 `settings:set` / `settings:updateTheme`

---

### 阶段 3：去硬编码 + 优化提升

#### 步骤 3.1：去硬编码
- 审查所有页面代码，确保所有颜色走 CSS 变量
- 所有间距/圆角/字号走主题变量（或 Tailwind 配置）
- 抽取重复的组件模式为可复用组件
- 魔法数字（如卡片高度、列表宽度）提取为常量

#### 步骤 3.2：动效系统
- **页面入场动画**：淡入 + 从右滑入（复刻 WPF `PageEnterStoryboard`，时长 300-350ms，CubicEase Out）
- **卡片悬停动效**：上浮 2px + 边框高亮（200ms，CubicEase Out）
- **按钮状态过渡**：hover/active/pressed 状态平滑过渡
- **数据更新动效**：数字变化时的过渡动画

#### 步骤 3.3：交互体验优化
- 滚动条样式美化（Material Design 风格）
- 输入框 focus 状态动画
- Tab 切换过渡
- Expander 展开/折叠动画
- 列表项 hover 效果

#### 步骤 3.4：性能优化
- 图表数据节流，避免频繁重绘
- 列表虚拟化（长列表时）
- 组件 memo 优化
- 减少不必要的重渲染

---

## 四、潜在依赖和注意事项

### 4.1 技术依赖

| 依赖项 | 说明 | 风险 |
|--------|------|------|
| `react-icons/fa6` | FontAwesome 6 图标 | 低，成熟稳定 |
| `echarts` 或 `recharts` | 图表库 | 中，需要调试样式复刻 LiveCharts |
| WebView2 桥接稳定性 | C# ↔ JS 通信 | 低，已有成熟封装 |

### 4.2 注意事项

1. **WPF 主题颜色准确性**：必须从 `AppResources.xaml` 中精确提取颜色值，不能凭感觉写
2. **字体一致性**：前端字体需与 WPF 的 `AppFontFamily` 一致（大概率是 Segoe UI / 微软雅黑）
3. **DPI 缩放**：WebView2 在高 DPI 下的表现需与 WPF 一致
4. **数据接口对齐**：前端接口定义需与 C# ViewModel 属性精确对应
5. **不要引入多余依赖**：保持技术栈简洁，能不用的库就不用

---

## 五、风险处理

| 风险 | 影响 | 应对方案 |
|------|------|---------|
| 图表库样式难以 100% 复刻 LiveCharts | 视觉略有差异 | 优先用 ECharts，配置项多，调整空间大；实在不行接受 95% 相似度 |
| C# 端某些数据 API 不存在 | 页面功能缺失 | 先盘点现有 ViewModel 能力，缺少的 API 同步在 C# 端实现 |
| 像素级复刻工作量超预期 | 延期 | 优先保证核心页面（系统监控+服务器管理），其他页面可适当降低精度 |
| 主题切换复杂 | 明暗主题不一致 | 先做好暗色主题（WPF 默认），亮色主题后补 |
| WebView2 性能问题 | 动画卡顿 | 用 CSS transform/opacity 做动画，避免触发重排；必要时开启硬件加速 |

---

## 六、验收标准

1. ✅ **视觉一致性**：非专业用户无法区分 Web UI 与 WPF UI 的视觉差异
2. ✅ **数据真实性**：所有页面数据均来自 C# 后端，无任何 mock 数据
3. ✅ **去硬编码**：颜色、间距、圆角、字号全部走主题变量，无魔法数字
4. ✅ **功能完整性**：5 个页面功能与 WPF 版一一对应
5. ✅ **交互一致性**：按钮状态、hover 效果、动画曲线与 WPF 版一致
6. ✅ **CI 通过**：GitHub Actions 编译通过，无错误无警告
