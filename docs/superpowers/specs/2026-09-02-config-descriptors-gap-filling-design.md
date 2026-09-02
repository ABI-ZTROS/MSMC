# Minecraft 服务器核心配置翻译——全量版本覆盖与增量补齐

> 日期：2026-09-02
> 状态：已批准设计 v2（用户审阅后修订）
> 目标：为 MSMC 配置编辑器补全所有 Minecraft 服务器核心的配置描述符，做到全历史版本溯源

***

## 1. 背景

MSMC 现有 35 种服务器核心、1161 个配置描述符、37 个配置文件的中文翻译，集中在 [ConfigDescriptorRegistry.cs](../../src/MSMC/Features/ConfigEditor/Services/ConfigDescriptorRegistry.cs) 注册。存在四个缺口：

1. **覆盖不全**——市面上还有 Geyser、DragonWell、LiteBans 衍生端、以及 Modrinth / CurseForge 上活跃的非 Paper 系核心尚未收录
2. **版本滞后**——翻译基于某次静态快照，核心跨版本键名漂移、默认值变化、类型变更时未同步
3. **无版本溯源**——Registry 不知道某个键在哪个版本引入、哪个版本废弃
4. **翻译质量缺乏权威依据**——纯 AI 机翻导致小白看不懂、大佬一眼唾弃。应当基于 MineBBS、中文 Minecraft Wiki、以及项目内已有的高质量 .md 文档做 RAG 增强

***

## 2. 设计总览（带数据流）

```
                 ┌─────────────────────────────┐
                 │  Phase 0: RAG 知识库构建     │
                 │  tools/core-fetcher/rag.py   │
                 │                              │
                 │  · 爬 MineBBS 配置帖          │
                 │  · 抓 zh.minecraft.wiki       │
                 │  · 索引项目内 .md 文档         │
                 │  · 结构化为 knowledge-base/   │
                 │  · 入库 GitHub (长期保存)       │
                 └──────────────┬──────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                                                                           │
│  ┌─────────────────────┐    ┌─────────────────────┐                      │
│  │  Stage 1: 下载层     │    │  Stage 2: 运行层     │                      │
│  │  fetch.py            │    │  run.py              │                      │
│  │                      │    │                       │                      │
│  │  Hangar / GH Release │───▶│  Java 8/11/17/21 自动 │                      │
│  │  CircleCI / Maven    │    │  EULA=true            │                      │
│  │  Forge/Fabric 安装器  │    │  nogui + 1GB          │                      │
│  │                      │    │  ready-match 检测      │                      │
│  │  → cache/jars/      │    │  → generated-configs/  │                      │
│  └─────────────────────┘    └──────────┬────────────┘                      │
│                                        │                                   │
│  ┌─────────────────────┐               ▼                                   │
│  │  Stage 3: 源码辅助层 │    ┌─────────────────────┐                      │
│  │  src.py              │    │  Stage 4: 比对层     │                      │
│  │                      │    │  diff.py              │                      │
│  │  git clone 浅克隆    │───▶│  标准化 YAML/Properties/TOML                 │
│  │  扫 @Config 注解     │    │  跨版本键树构建          │                      │
│  │  提取类型+约束+注释   │    │  与 Registry 比对      │                      │
│  │                      │    │  合并运行时+源码 hints  │                      │
│  │  → source-hints/    │    │  → diffs/*.diff.json   │                      │
│  └─────────────────────┘    └──────────┬────────────┘                      │
│                                        │                                   │
│                                        ▼                                   │
│  ┌──────────────────────────────────────────────────────┐                 │
│  │  Stage 5: RAG 翻译注入 + 文档同步  (translate.py)   │                 │
│  │                                                       │                 │
│  │  diff.json → 逐键查询 RAG 知识库                      │                 │
│  │              ↓                                        │                 │
│  │  翻译 prompt 携带:                                    │                 │
│  │    - 键名、默认值、类型                               │                 │
│  │    - 社区 RAG 匹配到的权威中文资料片段                  │                 │
│  │    - 同核心已翻译的相邻键（保证术语一致）                │                 │
│  │              ↓                                        │                 │
│  │  → 更新 ConfigDescriptorRegistry.cs                  │                 │
│  │  → 更新 docs/server-cores/*.md                       │                 │
│  │  → git commit + push                                 │                 │
│  └──────────────────────────────────────────────────────┘                 │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘
```

***

## 3. RAG 知识库（**Phase 0，最优先**）

### 3.1 目标

构建一个**本地持久化、入库 GitHub** 的权威中文配置知识库，供翻译时 RAG 查询。这个知识库本身就是长期资产，不仅服务本次翻译，以后新核心 / 新版本发布时也能复用。

### 3.2 数据源优先级（按权威性）

| 优先级 | 数据源                                  | 类型                    | 爬取方式                            |
| --- | ------------------------------------ | --------------------- | ------------------------------- |
| 1   | 项目内 `docs/server-cores/*.md`         | 已有高质量中文手册             | 直接读取，结构化抽取                      |
| 2   | MineBBS（minebbs.com）配置帖              | 社区公认最佳实践              | 搜索 API + 帖子正文抓取（需遵守 robots.txt） |
| 3   | 中文 Minecraft Wiki（zh.minecraft.wiki） | 游戏机制权威                | 爬 `/w/配置文件`、`/w/服务器配置` 等页面      |
| 4   | 官方文档本地化                              | PaperMC/Velocity 等有中译 | 官方 docs 镜像 / GitHub 仓库          |
| 5   | GitHub Issues / Discussions 中的中文讨论   | 问题场景下的真实使用            | 搜索 `lang:zh "配置键名"`             |

### 3.3 知识库存储结构（入库 GitHub）

```
knowledge-base/
├── paper/
│   ├── paper-global.yml.json        # 每个配置文件一个 JSON
│   └── paper-world-defaults.yml.json
├── purpur/
│   └── purpur.yml.json
├── velocity/
│   └── velocity.toml.json
└── ...
```

每个 JSON 格式：

```json
{
  "config_file": "config/paper-global.yml",
  "cores": ["paper", "folia", "purpur"],
  "source_docs": [
    {"source": "project-md", "path": "docs/server-cores/04-paper.md"},
    {"source": "minebbs", "url": "https://www.minebbs.com/threads/xxx", "title": "Paper 优化配置全解"},
    {"source": "wiki", "url": "https://zh.minecraft.wiki/...", "title": "Minecraft 服务器配置"}
  ],
  "entries": [
    {
      "key_path": "chunk-loading.basic-maximizer-chunk-limit",
      "chinese_entries": [
        {
          "term": "区块加载器区块上限",
          "context": "单个玩家每 tick 最多加载多少区块，降低可缓解鞘翅飞行卡顿",
          "source": "docs/server-cores/04-paper.md: 区块加载章节"
        },
        {
          "term": "区块加载最大数量",
          "context": "推荐服务器开服默认 4，不要设太大",
          "source": "minebbs.com/threads/1234"
        }
      ],
      "enum_values": {
        "说明": [
          {"value": "true", "chinese": "开启"},
          {"value": "false", "chinese": "关闭"}
        ]
      }
    }
  ],
  "indexed_at": "2026-09-02T08:30:00Z",
  "schema_version": 1
}
```

### 3.4 RAG 查询逻辑

翻译时对每个新键执行：

```python
def query_knowledge(key_path: str, core: str, config_file: str) -> KnowledgeResult:
    """
    1. 精确匹配 knowledge-base/<core>/<config_file>.json 中 key_path
    2. 跨核心模糊匹配（如果 purpur.yml 的键在 paper-global.yml 里有类似语义）
    3. 按 term 关键词全库搜索（如 "chunk-limit" 找到其他核心的同类键）
    4. 按 priority 聚合，取 top 3 相关片段
    """
```

### 3.5 翻译 prompt 模板（带正/反例对照）

```
你是一位有多年 Minecraft 服务器运维经验的中国人，给 MSMC 配置编辑器的小白用户写配置项说明。

[键信息]
核心: {core_name}
配置文件: {config_file}
键路径: {key_path}
类型: {type}
默认值: {default}

[社区权威参考资料 — 必须参考这些，不要自己瞎编]
{priority_1_snippets}    # 项目内 .md 文档的中文描述（最高优先级）
{priority_2_snippets}    # MineBBS 帖子中的用法
{priority_3_snippets}    # 中文 Minecraft Wiki 的术语

[同核心已翻译的相邻键 — 必须与它们术语一致]
{neighbor_translations}

═══════════════════════════════════════════════════════
【硬性风格规定 — 不遵守就是垃圾翻译】
═══════════════════════════════════════════════════════

❌ 绝对禁止（微软味道极重、机翻腔）：
  - 禁止出现 "该参数用于控制"
  - 禁止出现 "此设置用于指定"
  - 禁止出现 "此选项确定是否"
  - 禁止出现 "的值"、"的参数"、"的配置项" 这种机械后缀
  - 禁止把 "Chunk" 直译成 "组块" — Minecraft 社区通用术语是 "区块"
  - 禁止把 "Spawn" 直译成 "产卵" — Minecraft 通用是 "生成" / "刷新"
  - 禁止把 "Ender" 直译成 "安德" — Minecraft 通用是 "末影"
  - 禁止中英夹杂的无意义长词如 "区块负载上限配置"

✅ 必须做到（自然中文、开服者语气）：
  - 显示名像 App 设置的标题，10–20 字，读出来像人话
  - 详细说明 2–4 句，像给朋友推荐参数
  - 可以用 "建议"、"注意"、"注意改了之后"、"一般开服就默认" 这类话
  - 枚举值用 Minecraft 社区约定俗成的译名（生存/创造/冒险/极限；主世界/下界/末地 等）
  - 提到默认值时直接说 "默认是 4"，不要说 "默认值为 4"
  - 能推断出影响范围就直接说，不用问

═══════════════════════════════════════════════════════
【正/反例对照 — 必须参照 GOOD 列的风格】
═══════════════════════════════════════════════════════

键: chunk-loading.basic-maximizer-chunk-limit, 默认 4
  ❌ BAD 显示名: "该配置参数用于控制基础区块加载器的区块上限"
  ❌ BAD 说明: "此参数用于指定单个玩家每 tick 最多加载的区块数量"
  ✅ GOOD 显示名: "玩家区块加载上限"
  ✅ GOOD 说明: "单个玩家每 tick 最多加载多少区块。默认 4，鞘翅飞行卡顿或高频瞬移时适当调低到 2–3，不要设太大。需要重启。"

键: collisions.enable-player-collisions, 默认 true
  ❌ BAD 显示名: "启用玩家碰撞参数的配置选项"
  ❌ BAD 说明: "此选项确定是否启用玩家之间的物理碰撞检测"
  ✅ GOOD 显示名: "玩家间物理碰撞"
  ✅ GOOD 说明: "开启后玩家会互相挡路。小游戏服（起床战争/空岛）通常关掉；生存服建议保留。改了要重启。"

键: spigot.settings.save-user-cache-on-stop-only, 默认 false
  ❌ BAD 显示名: "仅在停止时保存用户缓存的参数"
  ❌ BAD 说明: "此设置指定是否仅在服务器停止时执行 usercache.json 的保存操作"
  ✅ GOOD 显示名: "仅停机时写玩家数据"
  ✅ GOOD 说明: "开启后玩家 UUID→名字的映射只在服务器停止时写磁盘。开服稳定性微提升，但服务器崩了可能丢最近注册的玩家数据。一般保持默认 false 就行。"

键: server.properties.gamemode, 默认 survival
  ❌ BAD 显示名: "服务器游戏模式的初始值"
  ❌ BAD 说明: "此配置项用于指定新玩家加入服务器时的游戏模式"
  ✅ GOOD 显示名: "服务器游戏模式"
  ✅ GOOD 说明: "新玩家进服的初始模式。survival=生存、creative=创造、adventure=冒险、hardcore=极限。需要重启。"

═══════════════════════════════════════════════════════

[输出格式（严格遵守）]

DisplayName: <10-20字自然中文>
Category: <功能分类，参考相邻键已有的分类>
Description: <2-4句开服者语气的说明>
ValueType: <bool/int/double/string/enum/list>
DefaultValue: <原样>
AllowedValues:
  true/false → ["开启", "关闭"]
  survival/creative/adventure/hardcore → ["生存", "创造", "冒险", "极限"]
  其他枚举 → 按社区通用译名
RequiresRestart: <能推断就填 true/false>
IntroducedIn: <从版本溯源结果自动填，你不用自己编>
```

### 3.6 入库策略

* `knowledge-base/` 目录全程入库 GitHub（Git LFS 如遇大文件）

* 每次 RAG 爬完新数据或手动补充后 commit 一次

* 知识库与翻译结果是**两个独立的提交**，便于回溯

***

## 4. 核心清单（YAML）

### 4.1 结构

```yaml
# 核心清单 v1
# 每项定义: 下载方式、Java 版本、启动参数、配置文件列表、源码仓库
# 字段全部必填，无 optional

schema: 1

defaults:
  launch:
    java-args: ["-Xms256M", "-Xmx1024M", "-XX:+UseSerialGC", "-jar"]
    timeout_seconds: 60
    ready-match-flags: ig       # 忽略大小写，多行匹配

cores:
  paper:
    display: Paper
    type: paper-fork
    github: PaperMC/Paper
    java-min: 21
    java-max: 25
    source-repo: PaperMC/Paper
    download:
      kind: hangar
      project: Paper
    launch:
      ready-match: "Done \\(.*s\\)! For help, type"
      eula: true
      config-files:
        - path: config/paper-global.yml
          type: yaml
        - path: config/paper-world-defaults.yml
          type: yaml
    inherits-translations: [spigot.yml, bukkit.yml, server.properties]  # 从已有翻译继承
    notes: Minecraft 1.19.4+ 使用新配置体系

  purpur:
    display: Purpur
    type: paper-fork
    github: PurpurMC/Purpur
    java-min: 21
    download:
      kind: github-releases
      asset-regex: "purpur-[0-9]+\\.jar"
    launch:
      ready-match: "Done \\(.*s\\)! For help, type"
      eula: true
      config-files:
        - path: purpur.yml
          type: yaml
        - path: config/paper-global.yml
          type: yaml
    inherits-translations: [spigot.yml, bukkit.yml, server.properties, paper-global.yml]

  nuakit:
    display: Nukkit
    type: bedrock-java
    github: CloudburstTeam/Nukkit
    java-min: 17
    download:
      kind: circleci-workflow
      workflow: build
    launch:
      ready-match: "Done"
      eula: true
      config-files:
        - path: nukkit.yml
          type: yaml
        - path: nukkit-server.properties
          type: properties

  velocity:
    display: Velocity
    type: proxy
    github: PaperMC/Velocity
    java-min: 21
    download:
      kind: github-releases
      asset-regex: "velocity-[0-9]+(-[a-z]+)?\\.jar"
    launch:
      ready-match: "Done \\(.*s\\)! Type"
      eula: false              # 代理端不需要 EULA
      config-files:
        - path: velocity.toml
          type: toml

  forge:
    display: Forge
    type: modloader
    github: MinecraftForge/MinecraftForge
    java-min: 17              # Minecraft 1.20+ Forge 统一 Java 17+
    download:
      kind: forge-installer
      mc-versions: [1.16.5, 1.17.1, 1.18.2, 1.19.4, 1.20.1, 1.20.4, 1.21.1]
    source-repo: MinecraftForge/MinecraftForge
    special: installer-first   # 先跑安装器生成 run 目录
    launch:
      ready-match: "Done \\(.*s\\)!"
      eula: true
      config-files:
        - path: forge-server.toml
          type: toml

  fabric:
    display: Fabric
    type: modloader
    github: FabricMC
    java-min: 17
    download:
      kind: fabric-installer
      loader-versions: all
    source-repo: FabricMC/fabric-loader
    special: installer-first
    launch:
      ready-match: "Done \\(.*s\\)!"
      eula: true
      config-files:
        - path: fabric-server-launcher.properties
          type: properties
    notes: Fabric 需要 fabric-server-launch.jar + intermediary 映射

  glowstone:
    display: Glowstone
    type: bukkit-api-impl
    github: GlowstoneMC/Glowstone
    java-min: 8
    download:
      kind: maven-central
      group: net.glowstone
      artifact: glowstone-server
    source-repo: GlowstoneMC/Glowstone
    launch:
      ready-match: "Done \\(.*s\\)!"
      eula: true
      config-files:
        - path: config/glowstone/glowstone.yml
          type: yaml

  leaf:
    display: Leaf
    type: paper-fork
    github: LeafMC/Leaf
    java-min: 21
    download:
      kind: github-releases
      asset-regex: "leaf-[0-9]+\\.jar"
    launch:
      ready-match: "Done \\(.*s\\)! For help, type"
      eula: true
      config-files:
        - path: leaf.yml
          type: yaml
        - path: config/leaf-global.yml
          type: yaml
    inherits-translations: [spigot.yml, bukkit.yml, server.properties, paper-global.yml]
    notes: Leaf 有独立 leaf.yml 继承 Purpur 的大部分配置 + 自己的增强项

  # ... 其他 30+ 核心照此格式补充
```

### 4.2 download.kind 枚举

| kind                | 说明                                                          |
| ------------------- | ----------------------------------------------------------- |
| `hangar`            | PaperMC Hangar v2 API `/api/v2/projects/{project}/versions` |
| `github-releases`   | GitHub Releases API（tag + asset 正则匹配）                       |
| `circleci-workflow` | CircleCI Pipeline API 扫 artifacts                           |
| `maven-central`     | Maven Central 元数据（Glowstone）                                |
| `forge-installer`   | Forge Maven XML 找 installer JAR                             |
| `fabric-installer`  | Fabric Meta API                                             |
| `modrinth`          | Modrinth API（可选，扫新核心）                                       |
| `curseforge`        | CurseForge API（可选，扫新核心）                                     |

### 4.3 核心清单完整性补漏（Phase 3）

用 GitHub Search API + 主流平台 API 扫描未收录的核心：

```python
def discover_new_cores() -> list[Candidates]:
    """
    GitHub Search: language:Java minecraft server core stars:>100 pushed:>2025-01-01
    Modrinth search: category=server-modloader
    Hangar projects: active
    CurseForge: Server-Side categories
    排除 Paper/Folia/Purpur 等已收录的
    对候选者做 JAR 可用性 + 配置文件存在性验证
    返回可追加进 core-registry.yaml 的条目建议
    """
```

***

## 5. Stage 1: 下载层（fetch.py）

### 5.1 下载缓存结构

```
cache/                              # .gitignore
├── cores/<core>/
│   ├── <ver>.jar
│   └── <ver>-meta.json             # {url, sha256, release_date, source}
└── runtimes/
    ├── jdk-8/    /jdk-8/bin/java
    ├── jdk-11/   /jdk-11/bin/java
    ├── jdk-17/   /jdk-17/bin/java
    └── jdk-21/   /jdk-21/bin/java
```

### 5.2 版本发现 + 下载流程

```python
def fetch(core_id: str) -> list[Path]:
    core = load_core_registry()[core_id]
    versions = discover_versions(core["download"])  # 按 kind 分派
    jars = []
    for v in versions:
        jar = cache_path(core_id, v)
        if jar.exists():          # 命中缓存
            jars.append(jar)
            continue
        url = resolve_url(core["download"], v)
        download_with_retry(url, jar)
        jars.append(jar)
    return jars
```

### 5.3 完整性校验

* SHA-256 校验（GitHub Release 提供的 artifact hash）

* 无法校验时，检查 JAR 的 Manifest 是否包含 Minecraft 标识

***

## 6. Stage 2: 运行层（run.py）

### 6.1 JDK 版本选择器

```python
JDK_PATHS = {
    "1.8": "/root/.local/share/mise/installs/openjdk@8/bin/java",
    "11":  "/root/.local/share/mise/installs/openjdk@11/bin/java",
    "17":  "/root/.local/share/mise/installs/openjdk@17/bin/java",
    "21":  "/root/.local/share/mise/installs/openjdk@21/bin/java",
    "25":  "/root/.local/share/mise/shims/java",
}

def pick_jdk(java_min: str) -> str:
    v_min = _parse(java_min)
    for ver, path in sorted(JDK_PATHS.items(), key=lambda x: _parse(x[0])):
        if _parse(ver) >= v_min:
            return path
    raise RuntimeError(f"No JDK >= {java_min}")
```

### 6.2 启动流程

```python
def run_one(jar_path: Path, workdir: Path, core: dict) -> RunResult | RunFailure:
    # 1. 全新工作目录
    if workdir.exists():
        shutil.rmtree(workdir)
    workdir.mkdir(parents=True)

    # 2. EULA
    if core["launch"]["eula"]:
        (workdir / "eula.txt").write_text("eula=true\n")

    # 3. 安装器型核心特殊处理
    if core.get("special") == "installer-first":
        run_installer(jar_path, workdir, core)

    # 4. 组装命令
    cmd = [pick_jdk(core["java-min"])] + core.get("defaults", {}).get("launch", {}).get("java-args", []) + \
          ["-jar", str(jar_path), "nogui"]

    # 5. 启动并捕获日志
    proc = subprocess.Popen(cmd, cwd=workdir,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            bufsize=1, text=True)

    # 6. 等待 ready
    ready_re = re.compile(core["launch"]["ready-match"], re.IGNORECASE | re.MULTILINE)
    start = time.time()
    log_tail = deque(maxlen=200)
    while time.time() - start < core.get("defaults", {}).get("launch", {}).get("timeout_seconds", 60):
        line = proc.stdout.readline()
        if not line and proc.poll() is not None:
            break
        log_tail.append(line)
        if ready_re.search(line):
            break
    else:
        proc.kill()
        return RunFailure(category="timeout", log_tail="".join(log_tail))

    if proc.poll() is not None and proc.returncode != 0:
        return RunFailure(category="early-exit", log_tail="".join(log_tail))

    # 7. 优雅停止
    try:
        proc.stdin.write("stop\n")
        proc.stdin.flush()
        proc.wait(timeout=15)
    except Exception:
        proc.kill()

    # 8. 收集生成的配置
    generated = {}
    for cf in core["launch"]["config-files"]:
        full = workdir / cf["path"]
        if full.exists():
            generated[cf["path"]] = full.read_text(errors="replace")
        else:
            # 尝试解压 JAR 默认配置
            jcf = extract_from_jar(jar_path, cf["path"])
            if jcf:
                generated[cf["path"]] = jcf  # 兜底
    return RunResult(generated, jar_path.name)
```

### 6.3 失败分类

| category                           | 处理                          | 记录            |
| ---------------------------------- | --------------------------- | ------------- |
| `timeout`                          | 跳过该版本                       | failures.json |
| `early-exit`                       | 抓 stderr 分析；自动降级 JDK 重试 1 次 | failures.json |
| `java-mismatch`                    | 自动切换 JDK 重试 1 次             | failures.json |
| `native-missing`（LWJGL/图形库）        | 永久跳过该核心，标记 GUI-dependent    | failures.json |
| `config-not-generated`（作者脑子抽风不写文件） | 回退到解压 JAR 默认配置              | warnings      |
| `corrupt-jar`                      | 下载重新拉取 1 次                  | failures.json |

`failures.json` 入库 GitHub：

```json
[
  {"core": "some-core", "version": "2.0.0", "category": "native-missing", "detail": "java.lang.UnsatisfiedLinkError: no lwjgl", "timestamp": "2026-09-02T08:30:00Z"}
]
```

***

## 7. Stage 3: 源码辅助层（src.py）

**核心类有 @Config 注解、类型约束、中文注释（部分核心），比运行时吐出来的 YAML 更完整**。

### 7.1 源码提取策略

| 核心系                       | 扫描目标                                                           |
| ------------------------- | -------------------------------------------------------------- |
| Paper/Folia/Purpur/Leaf 等 | `io/papermc/paper/configuration/**` 下所有 Java 类的 `@Config` 注解   |
| Bukkit/Spigot             | `CraftServer.java` 中的 `BukkitYaml`、`spigot.yml` 的 YAMLDefaults |
| Forge                     | `ModLoadingContext.registerConfig()` 调用                        |
| NeoForge                  | `ModLoadingContext.registerConfig()`                           |
| Velocity                  | `io/velocity/configuration/VelocityConfiguration` 类            |
| BungeeCord                | `net.md_5.bungee.conf.Configuration`                           |
| Nukkit/PowerNukkit        | `cn.nukkit.utils.Config` 注册点                                   |
| Glowstone                 | `net.glowstone.conf.GlowstoneConfiguration`                    |
| Sponge                    | `org.spongepowered.api.config.*` + HOCON 注解                    |

### 7.2 输出格式

```
source-hints/<core>/<ver>/
├── paper-global.yml.json         # 提取的每个键
└── meta.json                     # {commit, source_repo, extractor_version}
```

```jsonc
{
  "key_path": "chunk-loading.basic-maximizer-chunk-limit",
  "field_name": "basicMaximizerChunkLimit",
  "java_type": "int",
  "default_value": 4,
  "annotations": ["@Min(0)", @Description("...")],
  "source_file": "io/papermc/paper/configuration/GlobalConfiguration.java",
  "line": 123,
  "enclosing_class": "GlobalConfiguration.ChunkLoadingSection"
}
```

***

## 8. Stage 4: 比对层（diff.py）

### 8.1 合并策略

运行时配置 + 源码 hints + RAG 知识库 **三者取并集**：

```python
def merge(runtime_config, source_hints, rag_knowledge) -> MergedConfig:
    keys = set(runtime_config.keys()) | set(source_hints.keys())
    merged = {}
    for key in keys:
        entry = MergedKey(key=key)
        if key in runtime_config:
            entry.runtime = runtime_config[key]      # 真实默认值
        if key in source_hints:
            entry.type = source_hints[key].java_type
            entry.annotations = source_hints[key].annotations
            entry.source_default = source_hints[key].default_value  # 源码声明的默认值
        entry.rag_snippets = rag_knowledge.query(key)
        merged[key] = entry
    return MergedConfig(merged)
```

### 8.2 跨版本键树构建

对每个核心的所有版本配置做并集遍历，得到每个键的生命周期：

```python
@dataclass
class KeyEvolution:
    path: str
    introduced_in: str | None          # 首次出现版本
    removed_in: str | None             # 最后出现版本之后的版本（即哪个版本移除了）
    type_changes: list[TypeChange]     # 类型变化记录
    default_changes: list[DefaultChange]
    rename_chain: list[str]            # 重命名历史，["old.path", "new.path"]
```

### 8.3 与 ConfigDescriptorRegistry 比对

解析 Registry.cs 里所有 `(ConfigFileName, Key)` 复合键（正则提取），与 diff JSON 比对：

* **new\_key**：diff 有、Registry 没有 → 需翻译

* **existing**：都有 → 跳过，除非类型/默认值有漂移

* **removed\_key**：Registry 有、diff 里没有（任何版本）→ 标记 deprecated，加 `RemovedIn`

* **drifted**：Registry 有但默认值 / 类型在新版本变了 → 报告

### 8.4 diff.json 输出

```jsonc
{
  "core": "purpur",
  "config_file": "purpur.yml",
  "generated_at": "2026-09-02T08:30:00Z",
  "versions_scanned": ["1.16.5-999", "1.17.1-927", "..."],
  "keys": [
    {
      "path": "settings.player-clip-plane",
      "introduced_in": "1.16.5-999",
      "removed_in": null,
      "type_history": [
        {"version": "1.16.5-999", "type": "bool", "default": "false"},
        {"version": "1.19.4-1500", "type": "double", "default": "0.0", "note": "bool→double，新增 plane 尺寸参数"}
      ],
      "rename_chain": null,
      "state": "new_key",                       // new_key / existing / removed / drifted
      "rag_snippets_count": 3,
      "needs_translation": true
    }
  ],
  "summary": {"total_keys": 187, "new_keys": 52, "existing": 128, "removed": 7}
}
```

***

## 9. Stage 5: RAG 翻译注入 + 文档同步

### 9.1 翻译流程

```python
def translate_and_inject(core_id: str, config_file: str, diff_json: Path):
    # 1. 读取所有 new_key / drifted 条目
    diff = load(diff_json)
    for entry in diff["keys"]:
        if not entry["needs_translation"]:
            continue

        # 2. 从 RAG 知识库取 top 3 片段
        rag = query_knowledge(entry["path"], core_id, config_file)

        # 3. 取同核心已翻译相邻键（保证术语一致）
        neighbors = get_neighbor_translations(core_id, config_file, entry["path"])

        # 4. 构建 prompt 调用 AI
        prompt = build_prompt(entry, rag, neighbors)
        result = ai_call(prompt)

        # 5. 格式化为 C# ServerConfigDescriptor
        cs_snippet = format_as_cs_descriptor(entry, result)

        # 6. 追加到 Registry.cs（按 ConfigFileName 分区）
        inject_into_registry("src/MSMC/Features/ConfigEditor/Services/ConfigDescriptorRegistry.cs", cs_snippet)

        # 7. 更新 .md 文档对应表格行
        update_md_row(f"docs/server-cores/{core_id}.md", entry, result)

    # 8. 单独 commit 翻译结果
    git_commit(f"feat: translate {core_id} {config_file} ({count} keys via RAG)")
```

### 9.2 ServerConfigDescriptor 扩展字段

```csharp
/// <summary>配置项在 Minecraft 版本中首次引入的版本号（语义化，如 "1.16.5-927"）。null 表示未知</summary>
public string? IntroducedIn { get; init; }

/// <summary>配置项被移除的版本号。null 表示仍存在。用于在旧版兼容中隐藏废弃配置</summary>
public string? RemovedIn { get; init; }

/// <summary>默认值的跨版本变更记录。数组中每个条目代表一次有意义的变更</summary>
public ValueHistoryEntry[]? ValueHistory { get; init; }
```

```csharp
public record ValueHistoryEntry(string Version, string OldDefault, string NewDefault, string? Note);
```

### 9.3 防膨胀：Registry.cs 拆分为 partial class

当前 Registry.cs 已 50k+ 字符中文，继续膨胀会单文件超 1500 行。改为：

```csharp
// ConfigDescriptorRegistry.cs          —— 注册表逻辑（查找、比对、初始化调度）
// ConfigDescriptorRegistry.Paper.cs     —— 所有 Paper/Folia/Purpur 系键的注册
// ConfigDescriptorRegistry.ModLoader.cs —— Forge/Fabric/NeoForge/Quilt
// ConfigDescriptorRegistry.Proxy.cs     —— Velocity/BungeeCord/Waterfall/...
// ConfigDescriptorRegistry.Hybrid.cs    —— Mohist/Arclight/CatServer/Magma/Banner
// ConfigDescriptorRegistry.Other.cs     —— Nukkit/PowerNukkit/Glowstone/Sponge
```

***

## 10. 目录结构变更

```
/workspace/
├── tools/core-fetcher/                  # 全部入库
│   ├── core-registry.yaml               # 核心清单（越全越好）
│   ├── rag.py                           # Phase 0: 知识库构建
│   ├── fetch.py                         # Stage 1: 下载
│   ├── run.py                           # Stage 2: 运行
│   ├── src.py                           # Stage 3: 源码辅助
│   ├── diff.py                          # Stage 4: 比对
│   ├── translate.py                     # Stage 5: RAG 翻译注入
│   ├── verify.py                        # 最终校验: dotnet test + C# 编译
│   ├── requirements.txt
│   └── README.md
│
├── knowledge-base/                      # 入库 GitHub（长期资产）
│   ├── paper/paper-global.yml.json
│   ├── purpur/purpur.yml.json
│   └── ...
│
├── diffs/                               # 入库 GitHub（运行产物快照）
│   ├── <core>.diff.json
│   ├── <core>.new-keys.json
│   ├── summary.json
│   └── failures.json                   # 启动失败清单
│
├── cache/                               # .gitignore（几十 GB JAR + JDK）
│   ├── cores/<core>/<ver>.jar
│   └── runtimes/jdk-{8,11,17,21}/
│
├── generated-configs/                   # .gitignore
│   └── <core>/<ver>/
│
└── source-hints/                        # .gitignore
    └── <core>/<ver>/
```

***

## 11. 执行顺序（Phase 重新排）

**Phase 0: RAG 知识库构建（最优先，独立 commit）**

* [ ] 爬 MineBBS 配置帖 + 中文 Minecraft Wiki

* [ ] 索引项目内 `docs/server-cores/*.md`（已有 36 个高质量手册）

* [ ] 生成 `knowledge-base/*.json`

* [ ] commit: `docs: initial RAG knowledge base for config translation`

**Phase 1: 基础设施 + 最活跃 10 种核心跑通**

* [ ] 装 JDK 8/11/17/21（用 mise 或直接 tar 解）

* [ ] core-registry.yaml 前 10 种核心（Paper、Purpur、Leaf、Velocity、Bungee、Nukkit、Glowstone、Forge、Fabric、Folia）

* [ ] fetch.py + run.py + src.py + diff.py 全部写完

* [ ] Paper 全历史版本跑一遍做 demo

* [ ] translate.py 翻译 Paper 新键注入 Registry

**Phase 2: 批量跑全部已知核心**

* [ ] 剩余 25 种已知核心写进 core-registry.yaml

* [ ] 循环跑所有核心 × 全部历史版本

* [ ] diff JSON 批量生成

* [ ] failures.json 记录失败清单

**Phase 3: 扫新核心补漏**

* [ ] GitHub Search API + Modrinth/CurseForge/Hangar 扫活跃核心

* [ ] 候选验证（能否下载？能否运行？有独立配置？）

* [ ] 追加进 core-registry.yaml 跑一遍

**Phase 4: 翻译 + C# 集成 + 文档**

* [ ] 全部 diff 喂 translate.py 翻译注入

* [ ] Registry.cs 按核心拆分为 partial class

* [ ] 新增 IntroducedIn/RemovedIn/ValueHistory 字段

* [ ] `dotnet build` + `dotnet test` 全过

* [ ] docs/server-cores/\*.md 同步更新

* [ ] commit

***

## 12. 风险与缓解

| 风险                    | 概率 | 影响                  | 缓解                                            |
| --------------------- | -- | ------------------- | --------------------------------------------- |
| 某核心某版本启动即崩            | 高  | 跳过 1 个版本            | 解压 JAR 默认配置兜底 / 直接读源码注解                       |
| Forge/Fabric 安装器流程复杂  | 高  | 模组端跑不起来             | 专门的 installer 适配函数；不行就只做源码分析                  |
| 磁盘不够                  | 中  | 50–100GB JAR + 生成目录 | 1.2TB 够用；事后自动清理 cache/cores/                  |
| GitHub API rate limit | 中  | 下载受限                | Token 已配（5000 req/h）；元数据本地缓存                  |
| 运行时网络超时               | 中  | 代理端可能需要网络           | 60s timeout + 重试 1 次                          |
| AI 翻译仍小白不友好           | 中  | 翻译质量不达标             | RAG + prompt 模板双重保险；关键核心人工审 1 轮               |
| Registry.cs 继续膨胀      | 高  | 单文件 2000 行          | Phase 4 拆 partial class                       |
| MineBBS 爬取被反爬         | 低  | RAG 片段少             | 用已有 .md 文档兜底；加 requests.Session + UA          |
| 某个核心某版本 config 键全改了   | 中  | 版本溯源失败              | rename\_chain 记录，diff.py 里标记 `state: renamed` |

***

## 13. 不做什么（Scope Boundary）

* **不下 world 数据 / 插件数据**——只要配置

* **不跑模组 / 插件**——纯核心裸启动

* **不做翻译质量自动打分**——跑通 dotnet test + 人工 spot check 就算过

* **不做 GUI 启动器**——CLI 脚本足够

* **不处理 Pterodactyl / FTB / CurseForge Modpack 这类启动管理端**——不是核心，独立配置体系

* **不跑完整单元测试脚本**——脚本用 pytest 自测；C# 端跑 `dotnet test`

* **不做 Docker 化**——沙盒里直接 python + java 够用

***

## 14. 验收标准

1. `knowledge-base/` 入库 GitHub，每种核心至少 1 个配置文件有 RAG 条目
2. `cache/cores/` 下 ≥ 40 种核心的至少 1 个历史版本 JAR
3. 每种核心至少 1 个版本的运行配置 + 源码 hints 合并后与 Registry diff 输出
4. `diffs/summary.json` 汇总所有新键数（预期 500–2000 个）
5. `diffs/failures.json` 记录所有启动失败的核心+版本
6. ConfigDescriptorRegistry.cs 引入 IntroducedIn/RemovedIn/ValueHistory 字段，按核心拆分 partial class
7. 全部新键注入 + `docs/server-cores/*.md` 同步更新
8. `dotnet build` + `dotnet test` 全过
9. `tools/core-fetcher/` 的脚本可以在**下一个核心发布时一键增量更新**

