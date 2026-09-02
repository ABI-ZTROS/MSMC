# Minecraft 服务器核心配置翻译——全量版本覆盖与增量补齐

> 日期：2026-09-02
> 状态：已批准设计
> 目标：为 MSMC 配置编辑器补全所有 Minecraft 服务器核心的配置描述符，做到全历史版本溯源

***

## 1. 背景与动机

MSMC 现有 35 种服务器核心、1161 个配置描述符、37 个配置文件的中文翻译，均通过 [ConfigDescriptorRegistry.cs](../../src/MSMC/Features/ConfigEditor/Services/ConfigDescriptorRegistry.cs) 注册。但存在以下问题：

1. **覆盖不全**——市面上还有 Geyser、DragonWell、Pterodactyl 衍生端等核心未收录
2. **版本滞后**——翻译基于某次静态快照，核心跨版本键名漂移、默认值变化时未同步更新
3. **无版本溯源**——Registry 不知道某个键是在哪个版本引入、哪个版本移除的
4. **配置生成源头不透明**——部分核心运行时动态生成配置文件（Purpur/Paper 1.19+），仅解压 JAR 拿不到

***

## 2. 设计总览

```
┌──────────────────────────────────────────────────────────────────┐
│                        核心清单 (YAML)                            │
│  tools/core-fetcher/core-registry.yaml                           │
│  每种核心: name / github / artifact-api / java-min / launch-cmd   │
└──────────────────────────────┬───────────────────────────────────┘
                               │
              ┌────────────────▼────────────────┐
              │  Stage 1: 下载层 fetch.py       │
              │  · GitHub Releases              │
              │  · PaperMC Hangar API            │
              │  · GetBukkit (Bukkit/Spigot)    │
              │  · CircleCI (Nukkit/PowerNukkit)│
              │  · Forge/Fabric 安装器           │
              │  输出: cache/jars/<core>/<ver>.jar │
              └────────────────┬────────────────┘
                               │
              ┌────────────────▼────────────────┐
              │  Stage 2: 运行层 run.py         │
              │  · 按 Java-min 选择 JDK          │
              │  · 写入 eula.txt=true            │
              │  · nogui + max 1GB RAM           │
              │  · 等 ready (日志 match)          │
              │  · /stop 优雅关闭                 │
              │  输出: generated-configs/<core>/<ver>/ │
              └────────────────┬────────────────┘
                               │
              ┌────────────────▼────────────────┐
              │  Stage 3: 源码辅助层 src.py     │
              │  · git clone 浅克隆源码仓库      │
              │  · 扫 @Config / @Value 注解      │
              │  · 扫描 ConfigKey / Bukkit YAML  │
              │  · 输出: sources/<core>/<ver>/config-hints.json │
              └────────────────┬────────────────┘
                               │
              ┌────────────────▼────────────────┐
              │  Stage 4: 比对层 diff.py        │
              │  · 标准化 YAML/Properties/TOML  │
              │  · 扁平化点号路径                 │
              │  · 跨版本键树构建                  │
              │  · 与 ConfigDescriptorRegistry 比对 │
              │  输出: diffs/<core>.diff.json    │
              └────────────────┬────────────────┘
                               │
              ┌────────────────▼────────────────┐
              │  Stage 5: 翻译注入 + 文档生成    │
              │  · 差异 JSON 喂给 AI 翻译         │
              │  · 更新 ConfigDescriptorRegistry │
              │  · 更新 docs/server-cores/*.md   │
              │  · git commit + push              │
              └─────────────────────────────────┘
```

***

## 3. 核心清单格式（YAML）

每种核心一条记录，定义下载方式、Java 需求、启动参数。示例：

```yaml
cores:
  paper:
    display: Paper
    type: paper-fork
    github: PaperMC/Paper
    java-min: 21          # Minecraft 1.21+ 推荐 Java 21
    java-max: 25
    source-github: PaperMC/Paper
    versions-api:
      kind: hangar        # PaperMC Hangar 专用 API
      owner: PaperMC
      project: Paper
    artifact:
      kind: standard      # 标准 paper-<ver>.jar
    launch:
      cmd: ["java", "-Xms256M", "-Xmx1024M", "-jar", "{jar}", "nogui"]
      ready-match: "Done \\(.*s\\)! For help, type"
      config-files:
        - config/paper-global.yml
        - config/paper-world-defaults.yml
        - spigot.yml
        - bukkit.yml
        - server.properties

  purpur:
    display: Purpur
    type: paper-fork
    github: PurpurMC/Purpur
    java-min: 21
    versions-api:
      kind: github-releases
    launch:
      cmd: ["java", "-Xms256M", "-Xmx1024M", "-jar", "{jar}", "nogui"]
      ready-match: "Done \\(.*s\\)! For help, type"
      config-files:
        - purpur.yml
        - config/paper-global.yml

  nuakit:
    display: Nukkit
    type: bedrock-java
    github: CloudburstTeam/Nukkit
    java-min: 17
    versions-api:
      kind: circleci-workflow
      workflow: build
    launch:
      cmd: ["java", "-Xms256M", "-Xmx1024M", "-jar", "{jar}"]
      ready-match: "Done"
      config-files:
        - nukkit.yml
        - nukkit-server.properties

  velocity:
    display: Velocity
    type: proxy
    github: PaperMC/Velocity
    java-min: 21
    versions-api:
      kind: github-releases
    launch:
      cmd: ["java", "-Xms256M", "-Xmx512M", "-jar", "{jar}"]
      ready-match: "Done \\(.*s\\)! Type"
      config-files:
        - velocity.toml
    eula-required: false  # 代理端不需要 EULA

  forge:
    display: Forge
    type: modloader
    java-min: 17           # Minecraft 1.20.1 Forge
    versions-api:
      kind: forge-installer
      mc-versions: [1.16.5, 1.17.1, 1.18.2, 1.19.4, 1.20.1, 1.20.4, 1.21.1]
    launcher-kind: installer  # 不是单一 JAR，要跑安装器
    source-github: MinecraftForge/MinecraftForge
    launch:
      cmd: ["java", "-Xms256M", "-Xmx1024M", "-jar", "{jar}", "nogui"]
      ready-match: "Done \\(.*s\\)!"
      config-files:
        - forge-server.toml
    special-notes: 需要先运行安装器生成 run 目录

  glowstone:
    display: Glowstone
    type: bukkit-api-impl
    github: GlowstoneMC/Glowstone
    java-min: 8
    versions-api:
      kind: maven-central
      group: net.glowstone
      artifact: glowstone-server
      packaging: jar
    launch:
      cmd: ["java", "-Xms128M", "-Xmx512M", "-jar", "{jar}", "nogui"]
      ready-match: "Done \\(.*s\\)!"
      config-files:
        - config/glowstone/glowstone.yml
```

***

## 4. 下载层（fetch.py）实现细节

### 4.1 版本发现策略

按 `versions-api.kind` 选择策略：

| kind                | 说明                                                          | 示例核心                                                |
| ------------------- | ----------------------------------------------------------- | --------------------------------------------------- |
| `hangar`            | PaperMC Hangar v2 API `/api/v2/projects/{project}/versions` | Paper、Folia、Waterfall                               |
| `github-releases`   | 扫描 GitHub Releases tags + assets                            | Purpur、Leaves、Leaf、Mohist、FlameCord、HexaCord、Banner |
| `circleci-workflow` | CircleCI Pipeline API 扫 artifacts                           | Nukkit、PowerNukkit                                  |
| `getbukkit`         | GetBukkit Build API                                         | Bukkit、Spigot                                       |
| `forge-installer`   | Forge Maven 元数据找 installer JAR                              | Forge                                               |
| `fabric-installer`  | Fabric Meta API + Intermediary                              | Fabric                                              |
| `maven-central`     | Maven Central 元数据                                           | Glowstone                                           |
| `builtin`           | 安装器型核心，不直接下 JAR，标记 `launcher-kind=installer`                | Forge、Fabric、NeoForge                               |

### 4.2 版本筛选

对每个核心获取到的版本列表做筛选：

```
1. 过滤 pre-release / snapshot（YAML 里 override 可保留）
2. 版本语义化排序（vercmp）
3. 保留全部（用户明确要求"全部历史版本"）
4. 跳过明显坏掉的（发布页里直接有红色 broken 标记）
```

### 4.3 下载缓存

```
cache/
├── cores/
│   ├── paper/
│   │   ├── paper-1.21.1-133.jar        # 原始文件名
│   │   ├── paper-1.21.1-133.jar.sha256  # 完整性校验
│   │   └── paper-meta.json              # 下载 metadata (source url, release date, 作者)
│   ├── purpur/
│   └── ...
└── runtimes/
    ├── jdk-8/
    ├── jdk-11/
    ├── jdk-17/
    ├── jdk-21/
    └── jdk-25/          # 已有
```

***

## 5. 运行层（run.py）实现细节

### 5.1 JDK 版本选择

用 mise 或脚本自带的选择器：

```python
def pick_jdk(core: dict) -> str:
    available = {
        "1.8": "/root/.local/share/mise/installs/openjdk@8/bin/java",
        "11":  "/root/.local/share/mise/installs/openjdk@11/bin/java",
        "17":  "/root/.local/share/mise/installs/openjdk@17/bin/java",
        "21":  "/root/.local/share/mise/installs/openjdk@21/bin/java",
        "25":  "/root/.local/share/mise/shims/java",
    }
    required = core["java-min"]
    # 选 >= required 的最低版本
    for v, path in sorted(available.items(), key=lambda x: _parse_ver(x[0])):
        if _parse_ver(v) >= _parse_ver(required):
            return path
    raise RuntimeError(f"No JDK >= {required}")
```

### 5.2 启动流程

```python
def run_core(jar_path: str, workdir: Path, core: dict) -> RunResult:
    # 1. 准备工作目录（全新 copy）
    shutil.rmtree(workdir, ignore_errors=True)
    workdir.mkdir(parents=True)

    # 2. 写入 eula.txt=true（代理端例外）
    if core.get("eula-required", True):
        (workdir / "eula.txt").write_text("eula=true\n")

    # 3. 如果是 installer 类型，先跑安装器
    if core.get("launcher-kind") == "installer":
        _run_installer(jar_path, workdir, core)

    # 4. 组装启动命令
    cmd = [jdk_path, "-Xms256M", "-Xmx1024M"]
    cmd += ["-XX:+UseSerialGC"]  # 沙盒 GC 优化
    cmd += ["-jar", str(jar_path), "nogui"]

    # 5. 启动子进程 + 日志捕获
    proc = subprocess.Popen(cmd, cwd=workdir, ...)

    # 6. 等待 ready-match 出现（正则匹配日志）
    ready_re = re.compile(core["launch"]["ready-match"])
    start = time.time()
    timeout = 60  # 秒；老版本可能更快
    while time.time() - start < timeout:
        line = proc.stdout.readline()
        if ready_re.search(line):
            break
        if proc.poll() is not None:
            raise RuntimeError(f"Process exited early with code {proc.returncode}")
    else:
        proc.kill()
        raise TimeoutError(f"Timeout waiting for ready-match in {jar_path}")

    # 7. 优雅关闭
    proc.stdin.write(b"stop\n")
    proc.stdin.flush()
    try:
        proc.wait(timeout=15)
    except subprocess.TimeoutExpired:
        proc.kill()

    # 8. 收集生成的配置文件
    generated = {}
    for rel_path in core["launch"]["config-files"]:
        full = workdir / rel_path
        if full.exists():
            generated[rel_path] = full.read_text(errors="replace")
    return RunResult(generated_configs=generated, jar_name=Path(jar_path).name)
```

### 5.3 启动失败分类（便于报告）

| 类别             | 原因                 | 处理               |
| -------------- | ------------------ | ---------------- |
| timeout        | 60 秒内没打 ready 日志   | 标记失败，报告日志尾部      |
| early-exit     | 进程启动即崩             | 抓 stderr         |
| java-mismatch  | Wrong Java version | 自动降级/升级 JDK 重试一次 |
| native-missing | 需要 LWJGL/图形库       | 永久跳过，标记为需要 GUI   |
| config-corrupt | 作者脑子抽风             | 解压 JAR 里的默认配置代替  |

***

## 6. 源码辅助层（src.py）

用户明确说"源码比配置文件好看"。核心类有 `@Config` 注解、`ServerConfiguration` 类、`BukkitConfig` 注册等结构，比运行时吐出来的 YAML 更完整、有类型信息。

```python
def extract_config_from_source(source_root: Path, core: dict) -> list[SourceConfigHint]:
    """
    扫描源码仓库，从 Java/Kotlin 文件中提取配置定义。
    
    Paper/Purpur 系: 扫 io/papermc/paper/configuration 包下所有类的 @Config
    Bukkit 系: 扫 BukkitYaml / SpigotYaml / CraftServer 中的 YAMLDefaults
    Forge: 扫 ModLoadingContext.registerConfig() 调用
    Velocity: 扫 @Config / VelocityConfiguration 类
    Glowstone: 扫 GlowstoneConfiguration 类
    """
```

输出 `config-hints.json`：

```json
{
  "source": "PaperMC/Paper",
  "commit": "abc1234",
  "configs": [
    {
      "file": "config/paper-global.yml",
      "keys": [
        {
          "path": "chunk-loading.basic-maximizer-chunk-limit",
          "field": "basicMaximizerChunkLimit",
          "type": "int",
          "default": 4,
          "annotation": "@Min(0)",
          "comment": "The maximum number of chunks per player per tick for the basic chunk loader"
        }
      ]
    }
  ]
}
```

源码分析与运行时配置取并集，源码提供**类型 + 注释 + 约束**，运行时提供**真实默认值 + 实际生成的结构**。

***

## 7. 比对层（diff.py）

### 7.1 标准化输入

* **YAML**：PyYAML → 扁平化（`a.b.c: val`）

* **Properties**：按点号拆分层级 → 扁平化

* **TOML**：tomllib → 扁平化

* **HOCON**：需要专门解析（Sponge 用），如不支持就当普通文本

### 7.2 跨版本键树构建

```python
def build_key_evolution(core: str, config_file: str, versions: list[Path]) -> list[KeyEvent]:
    """
    遍历所有版本的配置，构建每个键的生命周期：
    - 在哪个版本首次出现 → introduced_in
    - 在哪个版本改名 → renamed_to (保留原键的生命周期链)
    - 在哪个版本移除 → removed_in
    - 默认值 / 类型 在哪些版本变化
    """
```

输出 `diffs/<core>.diff.json`：

```json
{
  "core": "purpur",
  "config_file": "purpur.yml",
  "keys": [
    {
      "path": "settings.player-clip-plane",
      "introduced_in": "1.16.5-999",
      "type_history": [
        {"version": "1.16.5-999", "type": "bool", "default": "false"},
        {"version": "1.19.4-1500", "type": "double", "default": "0.0", "changed": "bool→double，新增 plane 尺寸参数"}
      ],
      "removed_in": null,
      "rename_chain": null,
      "existing_in_registry": false,
      "needs_translation": true
    },
    {
      "path": "settings.banner-item",
      "introduced_in": "1.17.1-927",
      "removed_in": "1.19.0-1300",
      "existing_in_registry": true,
      "existing_desc": "玩家旗帜物品设置",
      "needs_translation": false
    }
  ]
}
```

### 7.3 与 ConfigDescriptorRegistry 比对

从 Registry 中解析 `(ConfigFileName, Key)` 复合键，与 diff JSON 逐键比对，标记：

* **新增**：diff 有、Registry 没有 → 需翻译

* **已存在**：两者都有 → 跳过

* **已废弃**：Registry 有、diff 没有（在任何版本） → 标记 deprecated，加 VersionRemoved

* **变化**：Registry 有但默认值 / 类型在新版本变了 → 更新

***

## 8. 翻译注入 + 文档生成

### 8.1 ConfigDescriptorRegistry 结构扩展

在 `ServerConfigDescriptor` 类上加字段：

```csharp
/// <summary>配置项的 Minecraft 版本中引入的版本（语义化字符串，如 "1.16.5-927"）</summary>
public string? IntroducedIn { get; init; }

/// <summary>配置项被移除的版本（null 表示仍存在）</summary>
public string? RemovedIn { get; init; }

/// <summary>默认值的历史变更记录（可选，数组形式）</summary>
public ConfigValueChange[]? ValueHistory { get; init; }
```

### 8.2 翻译内容

对 `needs_translation: true` 的键，喂给 AI 翻译：

* 中文显示名（10–20 字，小白友好）

* 详细描述（2–3 句话，解释用途、修改影响、推荐取值）

* 枚举值翻译（如果是 enum）

* 取值范围（如果有）

* 重启要求（如果能推断）

产出是一段可以直接贴进 Registry.cs 的 C# 代码片段。

### 8.3 文档更新

同步更新 `docs/server-cores/*.md` 的表格。

***

## 9. 目录结构变更

```
/workspace/
├── tools/
│   └── core-fetcher/
│       ├── core-registry.yaml     # 核心清单（人工维护，越全越好）
│       ├── fetch.py               # Stage 1: 下载层
│       ├── run.py                 # Stage 2: 运行层
│       ├── src.py                 # Stage 3: 源码辅助
│       ├── diff.py                # Stage 4: 比对层
│       ├── inject.py              # Stage 5: 翻译注入
│       ├── README.md              # 使用说明
│       └── requirements.txt       # PyYAML, requests, toml, ...
│
├── cache/                         # 全部 .gitignore
│   ├── cores/<core>/<ver>.jar
│   ├── cores/<core>/<ver>-meta.json
│   └── runtimes/jdk-8/ jdk-11/ jdk-17/ jdk-21/
│
├── generated-configs/             # 全部 .gitignore
│   └── <core>/<ver>/<files>
│
├── source-hints/                  # 全部 .gitignore
│   └── <core>/<ver>/config-hints.json
│
├── diffs/
│   ├── <core>.diff.json           # 版本间键演化
│   ├── <core>.new-keys.json       # 翻译候选
│   └── summary.json               # 全量汇总
│
└── failures.json                  # 启动失败清单，入库
```

***

## 10. 执行顺序

为了尽快拿到增量价值，分 Phase 跑：

### Phase 1：基础设施 + 最活跃核心（先做）

* 装 JDK 8/11/17/21

* 写 core-registry.yaml 前 10 种核心（Paper/Folia/Purpur/Leaves/Leaf/Luminol/Velocity/Bungee/Nukkit/Glowstone）

* 实现完整 5 个 Stage 的脚本

* 跑通 Paper 全历史作为 demo

### Phase 2：批量跑全部已知核心

* 剩余 25 种核心

* 代理端 + 模组端 + 混合端 + 已停更核心

* 记录失败清单

### Phase 3：扫新核心补漏

* GitHub Search API（`language:Java minecraft server core`）

* Modrinth / CurseForge / Hangar 平台扫

* 发现的新核心追加进 core-registry.yaml，跑一遍

### Phase 4：翻译 + 代码集成

* 全部 diff JSON 喂给 AI 翻译

* 更新 ConfigDescriptorRegistry + .md 文档

* 提交

***

## 11. 风险与缓解

| 风险                           | 概率 | 影响                   | 缓解                                        |
| ---------------------------- | -- | -------------------- | ----------------------------------------- |
| 某个核心某版本启动即崩                  | 高  | 跳过 1 个版本             | 解压 JAR 默认配置兜底；或直接读源码注解                    |
| 磁盘不够                         | 中  | 核心历史版本加起来可能 50–100GB | 1.2TB 够用；事后清理                             |
| Forge/Fabric 安装器流程复杂         | 高  | 模组端跑不起来              | 写死专门的 installer 适配函数；实在不行只做源码分析           |
| GitHub API rate limit        | 中  | 下载受限                 | Token 已配置，5000 req/h；分页缓存元数据              |
| 运行时网络超时                      | 中  | 代理端可能需要网络            | 60s timeout + 重试 1 次                      |
| AI 翻译质量                      | 中  | 小白看不懂                | 翻译后人工审 1 轮；枚举值重点审                         |
| C# Registry 文件 50k+ 字中文，持续膨胀 | 高  | 单文件 1000+ 行字段注册      | 考虑按核心拆分 Registry.cs（一个核心一个 partial class） |

***

## 12. 验收标准

1. `cache/cores/` 下存在 ≥ 40 种核心的至少 1 个历史版本 JAR
2. 每个核心至少 1 个版本的运行配置 + 源码 hints 合并后与 Registry diff 输出
3. `diffs/summary.json` 汇总所有新键数（预期 500–1500 个）
4. ConfigDescriptorRegistry.cs 更新，新增 IntroducedIn/RemovedIn 字段 + 全部新键
5. `docs/server-cores/*.md` 同步更新
6. 1 次完整脚本可重跑（下一个核心发布时）

***

## 13. 不做什么（Scope Boundary）

* **不下载 world 数据**——只要配置

* **不运行模组/插件**——纯核心裸启动

* **不做翻译质量自动化打分**——翻译完跑通编译就算过

* **不做 GUI 启动器**——CLI 脚本足够

* **不处理 Fornax / Pterodactyl 这类启动管理端**——它们不是核心，有独立配置体系

* **不跑完整单元测试**——脚本本身用 pytest 自测；C# 端跑 `dotnet test` 即可

