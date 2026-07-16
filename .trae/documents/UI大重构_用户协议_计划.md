# UI大重构 + 用户协议 - 详细实施计划

## 一、项目现状分析

### 1.1 当前架构概述

项目为 WPF (.NET 10) + MaterialDesignInXamlToolkit 构建的 Minecraft 服务器管理工具。核心模块：

| 模块 | 说明 | 关键文件 |
|------|------|----------|
| 主题服务 | 管理颜色、字体、动画设置 | [ThemeService.cs](file:///workspace/src/McServerGuard/Services/ThemeService.cs) |
| 全局配置 | 已知服务器、应用配置持久化 | [AppConfigService.cs](file:///workspace/src/McServerGuard/Services/AppConfigService.cs) |
| 主窗口 | 侧边栏导航 + 内容区切换 | [MainWindow.xaml](file:///workspace/src/McServerGuard/Views/MainWindow.xaml) |
| 资源字典 | 自定义样式、笔刷、控件模板 | [AppResources.xaml](file:///workspace/src/McServerGuard/Themes/AppResources.xaml) |
| 配置编辑器 | 服务器配置文件可视化编辑 | [ConfigEditorPage.xaml](file:///workspace/src/McServerGuard/Views/ConfigEditorPage.xaml) |

### 1.2 现存问题

1. **主题色偏差**：当前默认主色为 `#0097A7`（青色/Teal），用户要求改为**深空蓝**
2. **侧边栏样式陈旧**：使用 ListBox + 基础 VisualStateManager，缺少现代感的灵动效果
3. **配置条目动画不足**：当前仅有 Loaded 入场动画，缺少切换配置文件时的淡入淡出过渡
4. **缺少用户协议**：首次启动无用户协议弹窗，不符合合规要求
5. **渲染性能**：部分动画可能触发布局重算，需统一使用 RenderTransform

---

## 二、功能模块详细设计

### 模块一：主题色修正为深空蓝

#### 1. 深空蓝配色方案

参考现代科技感深空蓝配色（类 VS Code / JetBrains 暗色主题）：

| 色值 | 用途 | 说明 |
|------|------|------|
| `#0A192F` | 主背景色 | 深空蓝底色 |
| `#112240` | 卡片/侧边栏背景 | 比背景亮一级 |
| `#1E3A5F` | 悬停/边框 | 中等深度蓝 |
| `#233554` | 选中/高亮背景 | 较亮蓝 |
| `#64FFDA` | 主色/强调色 | 霓虹青绿色（深空蓝经典搭配） |
| `#8892B0` | 次要文字 | 淡蓝灰 |
| `#CCD6F6` | 主要文字 | 亮蓝白 |
| `#FFD740` | 琥珀黄辅助 | 警告/提示 |

#### 2. 修改内容

- **ThemeService.cs**：更新默认颜色字段和 `ThemeSettings` 默认值
- **App.xaml**：MaterialDesign BundledTheme PrimaryColor 改为 `DeepPurple` 或自定义
- **AppResources.xaml**：更新默认笔刷值为深空蓝系
- **SettingsViewModel.cs**：更新预设方案，新增"深空蓝"预设

---

### 模块二：侧边栏现代化改造

#### 1. 设计目标

- 更现代的视觉层次（毛玻璃感、微光效）
- 灵动的交互动画（悬停缩放、选中滑动过渡）
- 可折叠状态的平滑过渡
- 图标与文字的精致排版

#### 2. 技术方案

| 特性 | 实现方式 | 说明 |
|------|----------|------|
| 侧边栏展开/收起动画 | `GridLengthAnimation` + `Storyboard` | 平滑宽度过渡，非 Visibility 切换 |
| 导航项悬停效果 | `ScaleTransform` + `Opacity` + 背景渐变 | 轻微放大 + 微光 |
| 选中项指示器 | `Canvas` + `TranslateTransform` 动画 | 选中竖条平滑滑动到目标位置 |
| 图标颜色过渡 | `ColorAnimation` 直接动画 Foreground | 悬停/选中时图标颜色渐变 |
| 毛玻璃背景 | `BlurEffect` + 半透明背景 | 侧边栏背景微模糊 |

#### 3. 修改文件

- **MainWindow.xaml**：重写侧边栏布局，引入 `NavigationDrawer` 风格
- **AppResources.xaml**：新增 `ModernNavListBoxItemStyle`、`NavIndicatorStyle`
- **MainWindow.xaml.cs**：Code-behind 处理宽度动画、指示器滑动动画

---

### 模块三：渲染管线全面优化

#### 1. 优化策略

| 优化项 | 问题 | 方案 |
|--------|------|------|
| 布局动画 | 部分动画使用 Margin/Width 触发重排 | 全部改为 `RenderTransform` 动画 |
| 画笔冻结 | 动态创建的 SolidColorBrush 未冻结 | 所有静态 Brush 调用 `Freeze()` |
| 虚拟ization | 配置条目列表使用 ItemsControl 无虚拟化 | 保留 ListBox 虚拟化，优化 ItemTemplate |
| 字体渲染 | TextFormattingMode/TextRenderingMode 不统一 | 全局统一设置，启用 ClearTypeHint |
| 动画缓动 | 部分动画缓动函数不一致 | 统一使用 CubicEase / QuadraticEase |

#### 2. 修改文件

- **AnimationHelper.cs**：新增更多动画辅助方法，确保使用 RenderTransform
- **AppResources.xaml**：所有样式动画统一使用 RenderTransform
- **ThemeService.cs**：UpdateResources 中冻结所有 Brush

---

### 模块四：服务器配置条目淡入淡出切换

#### 1. 功能设计

- 切换配置文件时，旧内容淡出（左移+透明）
- 新内容淡入（右移+透明→不透明）
- 单个配置条目保留入场错位动画（staggered animation）
- 切换过程中显示半透明遮罩防止误操作

#### 2. 技术方案

| 功能 | 实现 |
|------|------|
| 页面切换动画 | `ContentControl` + `DataTemplate` + `VisualStateManager` |
| 条目入场动画 | `ItemsControl` 容器 + 逐个 `BeginTime` 递增的 DoubleAnimation |
| 数据切换 | ViewModel 中先清除数据 → 延迟加载 → 逐条添加 |

#### 3. 修改文件

- **ConfigEditorPage.xaml**：新增切换动画 VisualState，条目容器加 `x:Name`
- **ConfigEditorPage.xaml.cs**：Code-behind 处理切换动画触发
- **ConfigEditorViewModel.cs**：优化配置加载流程，支持分批添加

---

### 模块五：用户协议功能（重要！）

#### 1. 功能需求

- 首次启动（通过 `app-config.json` 中 `UserAgreementAccepted` 判断）弹出用户协议窗口
- **强制 120 秒阅读时间**：倒计时结束前"同意"按钮禁用
- **必须浏览到底部**：ScrollViewer 滚动到底部前"同意"按钮禁用
- 协议内容涵盖中国版权、网络安全相关法律法规及两高司法解释
- 用户同意后记录到配置文件，下次启动不再弹出
- 设置页面提供"重新查看用户协议"入口

#### 2. 法律法规内容清单（已通过网络调研确认）

**法律层级：**

1. **《中华人民共和国著作权法》**（2020年第三次修正）
   - 第三条：计算机软件属于作品
   - 第十条：著作权人享有的各项权利（复制权、发行权、信息网络传播权等）
   - 第二十四条：合理使用情形
   - 第四十九条：技术措施保护
   - 第五十三条：侵权行为的法律责任
   - 第五十四条：惩罚性赔偿

2. **《中华人民共和国网络安全法》**（2025年修正，2026.1.1施行）
   - 第十二条：禁止利用网络从事的活动
   - 第二十一条：网络运营者安全保护义务
   - 第二十七条：禁止从事危害网络安全的活动
   - 第四十四条：禁止非法获取、出售、提供个人信息
   - 第六十六条：危害网络安全活动的法律责任
   - 第七十一条：刑事责任

3. **《中华人民共和国刑法》**（刑法修正案十一）
   - 第二百一十七条：侵犯著作权罪（最高10年）
   - 第二百一十八条：销售侵权复制品罪
   - 第二百八十六条：破坏计算机信息系统罪
   - 第二百八十五条：非法侵入计算机信息系统罪
   - 第二百六十六条：诈骗罪

**行政法规层级：**

4. **《计算机软件保护条例》**（2013年修订）
   - 第八条：软件著作权人享有的权利
   - 第二十四条：侵权行为的法律责任（民事+行政+刑事）

5. **《信息网络传播权保护条例》**（2013年修订）
   - 第二条：信息网络传播权保护
   - 第四条：技术措施保护
   - 第五条：权利管理电子信息保护
   - 第十八条、第十九条：法律责任

**司法解释层级：**

6. **《最高人民法院、最高人民检察院关于办理侵犯知识产权刑事案件适用法律若干问题的解释》**（法释〔2025〕5号，2025.4.26施行）
   - 第十三条：侵犯著作权罪"违法所得数额较大"标准（3万元以上）
   - 第十四条："复制发行"的认定
   - 第二十八条：罚金刑适用标准

7. **其他相关**：最高人民法院关于审理侵害信息网络传播权民事纠纷案件适用法律若干问题的规定

#### 3. 用户协议结构

```
《McServerGuard 软件用户协议》
├─ 一、协议概述
├─ 二、知识产权声明
│  ├─ 2.1 软件著作权归属
│  ├─ 2.2 用户权利限制
│  └─ 2.3 法律依据（著作权法、软件保护条例条文引用）
├─ 三、用户行为规范
│  ├─ 3.1 合法使用承诺
│  ├─ 3.2 禁止行为清单
│  └─ 3.3 法律依据（网络安全法条文引用）
├─ 四、免责声明
├─ 五、隐私保护
├─ 六、协议的变更与终止
├─ 七、争议解决
├─ 八、相关法律法规摘录
│  ├─ 8.1 中华人民共和国著作权法（节选）
│  ├─ 8.2 中华人民共和国网络安全法（节选）
│  ├─ 8.3 中华人民共和国刑法（节选）
│  ├─ 8.4 计算机软件保护条例（节选）
│  ├─ 8.5 信息网络传播权保护条例（节选）
│  └─ 8.6 两高知识产权刑事案件司法解释（节选）
└─ 九、用户确认与承诺
```

#### 4. 技术实现

| 功能点 | 实现方案 |
|--------|----------|
| 首次启动检测 | `AppConfig.UserAgreementAccepted` 字段 + `AppConfigService` |
| 弹窗时机 | `App.OnStartup` 中，主窗口 Show 之前 |
| 120秒倒计时 | `DispatcherTimer` + 按钮 IsEnabled 绑定 |
| 滚动到底检测 | `ScrollViewer.ScrollChanged` 事件 + `VerticalOffset == ScrollableHeight` |
| 模态窗口 | `Window.ShowDialog()` 阻塞启动流程 |
| 协议内容存储 | 内嵌资源文件 `UserAgreementContent.txt` 或 XAML FlowDocument |

#### 5. 新增文件

- `Views/UserAgreementWindow.xaml` - 用户协议窗口
- `Views/UserAgreementWindow.xaml.cs` - 窗口逻辑
- `Resources/UserAgreementContent.md` 或内嵌 XAML - 协议文本内容

#### 6. 修改文件

- **AppConfigService.cs / IAppConfigService.cs**：新增 `UserAgreementAccepted` 属性
- **App.xaml.cs**：启动时检查并弹出用户协议
- **SettingsPage.xaml / SettingsViewModel.cs**：新增查看用户协议入口

---

## 三、实施步骤（按顺序）

### Phase 1：主题色修正为深空蓝
1. 修改 `ThemeService.cs` 默认颜色值
2. 更新 `ThemeSettings` 默认值
3. 修改 `App.xaml` MaterialDesign 主题配置
4. 更新 `AppResources.xaml` 默认笔刷
5. `SettingsViewModel` 新增/更新预设方案

### Phase 2：侧边栏现代化改造
1. 设计新的 `ModernNavListBoxItemStyle` 样式
2. 实现导航选中指示器滑动动画
3. 改造 MainWindow.xaml 侧边栏布局
4. 实现侧边栏展开/收起宽度动画
5. 添加图标颜色过渡动画

### Phase 3：渲染管线优化
1. 审查所有动画，统一使用 `RenderTransform`
2. 冻结所有静态 Brush
3. 统一动画缓动函数
4. 优化文字渲染设置

### Phase 4：配置条目淡入淡出切换
1. 优化 ViewModel 数据加载流程
2. 实现切换时的淡出/淡入动画
3. 实现条目错位入场动画（staggered）
4. 添加加载遮罩防止误操作

### Phase 5：用户协议功能
1. 编写用户协议内容（含法律法规条文摘录）
2. 创建 UserAgreementWindow 及 ViewModel
3. 实现 120秒倒计时 + 滚动到底检测
4. 集成到 App.xaml.cs 启动流程
5. AppConfigService 添加用户协议接受状态
6. 设置页面添加入口

---

## 四、风险与注意事项

### 4.1 技术风险

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| WPF 动画性能问题 | 低端设备卡顿 | 使用 `RenderTransform`，启用硬件加速，提供开关 |
| 侧边栏指示器滑动定位 | 计算复杂导致偏移 | 使用 `ListBox.ItemContainerGenerator.ContainerFromIndex` |
| 滚动到底检测不准确 | 用户绕过阅读检测 | 双重检测：ScrollChanged 事件 + 定时器轮询 |
| 配置切换动画与数据加载冲突 | 动画抖动 | 先淡出 → 数据更新 → 再淡入，严格时序控制 |

### 4.2 法律风险

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 用户协议内容不准确 | 法律合规风险 | 严格引用现行有效法律条文，标注"仅供参考" |
| 用户协议未涵盖必要条款 | 维权困难 | 参考主流软件协议结构，包含必要法律条款 |
| 强制阅读时间是否合法 | 用户体验争议 | 明确提示"为保障您的权益，请仔细阅读"，而非强制 |

> **重要声明**：本用户协议内容仅供参考，不构成法律意见。正式商用前建议咨询专业律师。

### 4.3 兼容性注意事项

- 所有改动保持向后兼容，已保存的主题设置自动迁移
- 动画可通过 `EnableAnimations` 设置关闭
- 用户协议可在设置中重新查看

---

## 五、涉及文件清单

### 新增文件
- `Views/UserAgreementWindow.xaml`
- `Views/UserAgreementWindow.xaml.cs`

### 修改文件
- `Services/ThemeService.cs` - 默认色值、预设更新
- `Services/AppConfigService.cs` - 用户协议接受状态
- `Services/IAppConfigService.cs` - 接口扩展
- `App.xaml` - MaterialDesign 主题色
- `App.xaml.cs` - 启动时用户协议检查
- `Themes/AppResources.xaml` - 侧边栏新样式、动画优化
- `Views/MainWindow.xaml` - 侧边栏现代化改造
- `Views/MainWindow.xaml.cs` - 侧边栏动画逻辑
- `Views/ConfigEditorPage.xaml` - 条目切换动画
- `Views/ConfigEditorPage.xaml.cs` - 动画触发逻辑
- `ViewModels/SettingsViewModel.cs` - 新增预设、用户协议入口
- `Views/SettingsPage.xaml` - 用户协议入口按钮
