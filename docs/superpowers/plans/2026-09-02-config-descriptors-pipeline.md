# Minecraft 服务器核心配置翻译——实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个可复用的 Python 工具链，下载 + 运行 Minecraft 服务器核心，抓取最新配置文件，与 ConfigDescriptorRegistry 比对，RAG 辅助翻译，增量补入 C# Registry 和 Markdown 文档。

**Architecture:** 5 个独立 Python 脚本 (rag → fetch → run → src → diff → translate) + 一个 YAML 核心清单。中间产物 (cache/, generated-configs/, source-hints/) 进 .gitignore。RAG 知识库和 diff 结果入库 GitHub。C# 侧只改 ConfigDescriptorRegistry.cs（加版本字段 + 拆 partial class）。

**Tech Stack:** Python 3.14 + PyYAML + requests + toml + GitHub REST API；C# (ConfigDescriptorRegistry 扩展)

**Spec:** `docs/superpowers/specs/2026-09-02-config-descriptors-gap-filling-design.md`

---

## 仓库现状速览（执行者必读）

| 项 | 值 |
|---|---|
| ConfigDescriptorRegistry.cs | 16,795 行 / 525KB / 43 个 Register* 方法 |
| Python | 3.14.7，pytest 已装 |
| dotnet | 沙盒里没有，Phase 4 之前不跑 build |
| 已有 JDK | 25 (via mise) |
| 需要装的 JDK | 8 / 11 / 17 / 21 |
| .gitignore | 没有 cache/、generated-configs/、knowledge-base/ 条目，要加 |
| GITHUB_TOKEN | 环境变量已配，完整 repo 权限 |

---

### Task 1: 项目基础设施（.gitignore + 目录 + requirements.txt）

**Files:**
- Modify: `.gitignore`
- Create: `tools/core-fetcher/requirements.txt`

- [ ] **Step 1: 追加 .gitignore 条目**

```bash
cat >> /workspace/.gitignore << 'EOF'

## Cache / Generated (core-fetcher pipeline)
cache/
generated-configs/
source-hints/
*.jar
*.sha256
EOF
```

- [ ] **Step 2: 验证 .gitignore 追加正确**

Run: `tail -10 /workspace/.gitignore`
Expected: 看到 `cache/`、`generated-configs/`、`source-hints/`、`*.jar` 条目

- [ ] **Step 3: 创建 requirements.txt**

```python
# /workspace/tools/core-fetcher/requirements.txt
PyYAML>=6.0
requests>=2.31.0
toml>=0.10.2
beautifulsoup4>=4.12.0
lxml>=5.0.0
```

- [ ] **Step 4: 安装依赖**

```bash
pip install -r /workspace/tools/core-fetcher/requirements.txt 2>&1 | tail -5
```

Expected: `Successfully installed` ...

- [ ] **Step 5: Commit**

```bash
cd /workspace && git add .gitignore tools/core-fetcher/requirements.txt
git commit -m "build: add gitignore entries for core-fetcher pipeline + python requirements"
```

---

### Task 2: 安装 JDK 8/11/17/21

**Files:** (no source files — 纯系统操作)

- [ ] **Step 1: 确认 mise 可用**

```bash
which mise && mise --version
```

- [ ] **Step 2: 列出当前已安装版本**

```bash
mise ls java
```

Expected: `openjdk@25` 至少有

- [ ] **Step 3: 安装 4 个版本（并行）**

```bash
mise use -g java@8 2>&1 | tail -3
mise use -g java@11 2>&1 | tail -3
mise use -g java@17 2>&1 | tail -3
mise use -g java@21 2>&1 | tail -3
```

或如果 `-g` 太慢（系统全局），用 project-local：

```bash
cd /workspace
mise use java@8@global java@11@global java@17@global java@21@global
```

- [ ] **Step 4: 验证安装**

```bash
for j in 8 11 17 21 25; do
  echo -n "JDK $j: "
  java -version 2>&1 | head -1
done
```

Expected: 每个版本都能输出 `openjdk version "$j.x.x"`

- [ ] **Step 5: 记录 JDK 路径（后面脚本要用）**

```bash
mise which java@8 && mise which java@11 && mise which java@17 && mise which java@21 && mise which java@25
```

把输出记下来，后面 fetch.py / run.py 的 `JDK_PATHS` 要用。

---

### Task 3: 核心清单 YAML（core-registry.yaml）

**Files:**
- Create: `tools/core-fetcher/core-registry.yaml`

- [ ] **Step 1: 写完整 YAML（至少先 10 种核心，跑 demo）**

写完整 `tools/core-fetcher/core-registry.yaml`。先写 10 种代表性核心，后续批量阶段再补剩余 25+。

**内容（直接写，不要省略号）：**

```yaml
schema: 1

defaults:
  launch:
    java-args: ["-Xms256M", "-Xmx1024M", "-XX:+UseSerialGC", "-jar"]
    timeout_seconds: 60
    ready-match-flags: ig

cores:
  paper:
    display: Paper
    type: paper-fork
    github: PaperMC/Paper
    java-min: "21"
    download:
      kind: hangar
      project: Paper
      owner: PaperMC
    launch:
      ready-match: "Done \\(.*s\\)! For help, type"
      eula: true
      config-files:
        - path: config/paper-global.yml
          type: yaml
        - path: config/paper-world-defaults.yml
          type: yaml
        - path: spigot.yml
          type: yaml
        - path: bukkit.yml
          type: yaml
        - path: server.properties
          type: properties

  purpur:
    display: Purpur
    type: paper-fork
    github: PurpurMC/Purpur
    java-min: "21"
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

  leaf:
    display: Leaf
    type: paper-fork
    github: LeafMC/Leaf
    java-min: "21"
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

  folia:
    display: Folia
    type: paper-fork
    github: PaperMC/Folia
    java-min: "21"
    download:
      kind: github-releases
      asset-regex: "folia-[0-9]+\\.jar"
    launch:
      ready-match: "Done \\(.*s\\)! For help, type"
      eula: true
      config-files:
        - path: config/paper-global.yml
          type: yaml

  velocity:
    display: Velocity
    type: proxy
    github: PaperMC/Velocity
    java-min: "21"
    download:
      kind: github-releases
      asset-regex: "velocity-[0-9]+(-[a-z]+)?\\.jar"
    launch:
      ready-match: "Done \\(.*s\\)! Type"
      eula: false
      config-files:
        - path: velocity.toml
          type: toml

  bungee:
    display: BungeeCord
    type: proxy
    github: SpigotMC/BungeeCord
    java-min: "11"
    download:
      kind: github-releases
      asset-regex: "BungeeCord-[0-9]+\\.jar"
    launch:
      ready-match: "Listening on /"
      eula: false
      config-files:
        - path: config.yml
          type: yaml

  nukkit:
    display: Nukkit
    type: bedrock-java
    github: CloudburstTeam/Nukkit
    java-min: "17"
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

  glowstone:
    display: Glowstone
    type: bukkit-api-impl
    github: GlowstoneMC/Glowstone
    java-min: "8"
    download:
      kind: maven-central
      group: net.glowstone
      artifact: glowstone-server
    launch:
      ready-match: "Done \\(.*s\\)!"
      eula: true
      config-files:
        - path: config/glowstone/glowstone.yml
          type: yaml

  forge:
    display: Forge
    type: modloader
    github: MinecraftForge
    java-min: "17"
    download:
      kind: forge-installer
      mc-versions: ["1.16.5", "1.17.1", "1.18.2", "1.19.4", "1.20.1", "1.20.4", "1.21.1"]
    special: installer-first
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
    java-min: "17"
    download:
      kind: fabric-installer
    special: installer-first
    launch:
      ready-match: "Done \\(.*s\\)!"
      eula: true
      config-files:
        - path: fabric-server-launcher.properties
          type: properties
```

- [ ] **Step 2: 用 Python 校验 YAML 可解析**

```python
python3 -c "
import yaml
with open('/workspace/tools/core-fetcher/core-registry.yaml') as f:
    data = yaml.safe_load(f)
print('cores:', list(data['cores'].keys()))
print('count:', len(data['cores']))
"
```

Expected: `cores: ['paper', 'purpur', 'leaf', 'folia', 'velocity', 'bungee', 'nukkit', 'glowstone', 'forge', 'fabric']`

- [ ] **Step 3: Commit**

```bash
cd /workspace && git add tools/core-fetcher/core-registry.yaml
git commit -m "feat: core-registry.yaml — 10 representative cores for initial pipeline"
```

---

### Task 4: fetch.py — 下载层

**Files:**
- Create: `tools/core-fetcher/fetch.py`

- [ ] **Step 1: 写 fetch.py**

完整代码见下方。核心功能：加载 YAML → 按 `download.kind` 分发版本发现 → 下载到 `cache/cores/<core>/<ver>.jar` → 写 meta.json。

```python
#!/usr/bin/env python3
"""fetch.py - Download Minecraft server core JARs from various sources.

Usage:
    python fetch.py                    # download all cores (all versions)
    python fetch.py --core paper       # download only paper
    python fetch.py --cores paper,purpur,leaf
    python fetch.py --core paper --latest-only
    python fetch.py --rebuild          # ignore cache, re-download everything
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import time
from pathlib import Path
from typing import Iterable
from urllib.parse import quote

import requests
import yaml

ROOT = Path(__file__).resolve().parent.parent.parent
CACHE_DIR = ROOT / "cache" / "cores"
REGISTRY_PATH = Path(__file__).resolve().parent / "core-registry.yaml"

GH_API = "https://api.github.com"


def _gh_headers():
    token = os.environ.get("GITHUB_TOKEN")
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "MSMC-core-fetcher/1.0"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return headers


def load_registry() -> dict:
    with open(REGISTRY_PATH) as f:
        return yaml.safe_load(f)


def cache_path(core_id: str, version: str) -> Path:
    return CACHE_DIR / core_id / f"{version}.jar"


def meta_path(core_id: str, version: str) -> Path:
    return CACHE_DIR / core_id / f"{version}-meta.json"


def _gh_get(url: str, params: dict | None = None) -> dict | list:
    r = requests.get(url, headers=_gh_headers(), params=params, timeout=30)
    if r.status_code == 403 and "rate limit" in r.text.lower():
        print(f"  [WARN] GitHub rate limit hit. Waiting 60s...")
        time.sleep(60)
        r = requests.get(url, headers=_gh_headers(), params=params, timeout=30)
    r.raise_for_status()
    return r.json()


# ──────────────────────────── Version discovery strategies ────────────────────────────

def discover_hangar(core: dict, latest_only: bool) -> list[str]:
    """PaperMC Hangar v2 API."""
    owner = core["download"]["owner"]
    project = core["download"]["project"]
    url = f"https://hangar.papermc.io/api/v2/projects/{owner}/{quote(project)}/versions"
    r = requests.get(url, params={"offset": 0, "limit": 100}, timeout=30)
    r.raise_for_status()
    versions = [v["version"] for v in r.json()]
    if latest_only and versions:
        return [versions[0]]  # Hangar returns newest first
    return versions


def discover_github_releases(core: dict, latest_only: bool) -> list[str]:
    """GitHub Releases — tags are version identifiers."""
    gh_repo = core["github"]
    url = f"{GH_API}/repos/{gh_repo}/releases"
    releases = _gh_get(url, params={"per_page": 100})
    versions = []
    for r in releases:
        if r.get("draft"):
            continue
        if latest_only and versions:
            versions.append(r["tag_name"])
            continue
        if not latest_only:
            versions.append(r["tag_name"])
    if latest_only and releases:
        versions = [releases[0]["tag_name"]]
    return versions


def discover_forge_installer(core: dict, latest_only: bool) -> list[str]:
    """Forge Maven metadata."""
    versions = []
    for mc_ver in core["download"]["mc-versions"]:
        url = f"https://maven.minecraftforge.net/net/minecraftforge/forge/{mc_ver}/forge-{mc_ver}.pom"
        try:
            r = requests.get(url, timeout=15)
            if r.ok:
                versions.append(mc_ver)
        except Exception:
            pass
    return versions


def discover_fabric_installer(core: dict, latest_only: bool) -> list[str]:
    """Fabric installer — Fabric Meta API."""
    r = requests.get("https://meta.fabricmc.net/v2/versions/installer", timeout=15)
    r.raise_for_status()
    data = r.json()
    if latest_only:
        return [data[0]["version"]]
    return [item["version"] for item in data]


def discover_circleci(core: dict, latest_only: bool) -> list[str]:
    """CircleCI — Nukkit/PowerNukkit."""
    # CircleCI requires auth; skip for now, mark to-do
    print(f"  [SKIP] circleci-workflow: auth required, implement later")
    return []


def discover_maven_central(core: dict, latest_only: bool) -> list[str]:
    """Maven Central metadata."""
    group = core["download"]["group"].replace(".", "/")
    artifact = core["download"]["artifact"]
    url = f"https://repo1.maven.org/maven2/{group}/{artifact}/maven-metadata.xml"
    r = requests.get(url, timeout=15)
    r.raise_for_status()
    # parse versions from metadata
    versions = re.findall(r"<version>([^<]+)</version>", r.text)
    if latest_only and versions:
        return [versions[-1]]
    return versions


def discover_versions(core: dict, latest_only: bool) -> list[str]:
    kind = core["download"]["kind"]
    dispatch = {
        "hangar": discover_hangar,
        "github-releases": discover_github_releases,
        "forge-installer": discover_forge_installer,
        "fabric-installer": discover_fabric_installer,
        "circleci-workflow": discover_circleci,
        "maven-central": discover_maven_central,
    }
    fn = dispatch.get(kind)
    if fn is None:
        print(f"  [SKIP] unknown download kind: {kind}")
        return []
    return fn(core, latest_only)


# ──────────────────────────── URL resolution ────────────────────────────

def resolve_download_url(core: dict, version: str) -> str | None:
    """Given a core + version, return the direct download URL for the JAR."""
    kind = core["download"]["kind"]

    if kind == "hangar":
        owner = core["download"]["owner"]
        project = core["download"]["project"]
        url = (f"https://hangar.papermc.io/api/v2/projects/{owner}/{quote(project)}"
               f"/versions/{quote(version)}/downloads/archetype")
        try:
            r = requests.get(url, timeout=30)
            r.raise_for_status()
            return r.json()["url"]
        except Exception as e:
            print(f"  [WARN] Hangar URL fail for {version}: {e}")
            return None

    if kind == "github-releases":
        gh_repo = core["github"]
        releases = _gh_get(f"{GH_API}/repos/{gh_repo}/releases", params={"per_page": 100})
        for r in releases:
            if r["tag_name"] == version:
                regex = core["download"].get("asset-regex", r"\.jar")
                for asset in r["assets"]:
                    if re.search(regex, asset["name"]):
                        return asset["browser_download_url"]
                # fallback: first asset
                if r["assets"]:
                    return r["assets"][0]["browser_download_url"]
        return None

    if kind == "forge-installer":
        mc_ver = version
        return (f"https://maven.minecraftforge.net/net/minecraftforge/forge/"
                f"{mc_ver}/forge-{mc_ver}-installer.jar")

    if kind == "fabric-installer":
        r = requests.get("https://meta.fabricmc.net/v2/versions/installer", timeout=15)
        r.raise_for_status()
        for item in r.json():
            if item["version"] == version:
                return item.get("url")
        return None

    if kind == "maven-central":
        group = core["download"]["group"].replace(".", "/")
        artifact = core["download"]["artifact"]
        return f"https://repo1.maven.org/maven2/{group}/{artifact}/{version}/{artifact}-{version}.jar"

    return None


# ──────────────────────────── Download with retry ────────────────────────────

def download(url: str, dest: Path, retries: int = 2) -> bool:
    dest.parent.mkdir(parents=True, exist_ok=True)
    for attempt in range(retries + 1):
        try:
            with requests.get(url, stream=True, timeout=60) as r:
                r.raise_for_status()
                with open(dest, "wb") as f:
                    for chunk in r.iter_content(chunk_size=8192):
                        f.write(chunk)
            return True
        except Exception as e:
            if attempt == retries:
                print(f"  [FAIL] {url}: {e}")
                return False
            time.sleep(1)
    return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core", help="single core id")
    parser.add_argument("--cores", help="comma-separated list of core ids")
    parser.add_argument("--latest-only", action="store_true")
    parser.add_argument("--rebuild", action="store_true")
    args = parser.parse_args()

    reg = load_registry()
    all_cores = reg["cores"]

    if args.core:
        target_ids = [args.core]
    elif args.cores:
        target_ids = [c.strip() for c in args.cores.split(",")]
    else:
        target_ids = list(all_cores.keys())

    CACHE_DIR.mkdir(parents=True, exist_ok=True)

    for core_id in target_ids:
        if core_id not in all_cores:
            print(f"[SKIP] unknown core: {core_id}")
            continue
        core = all_cores[core_id]
        print(f"\n[{core_id}] discovering versions...")
        versions = discover_versions(core, latest_only=args.latest_only)
        print(f"[{core_id}] found {len(versions)} version(s): {versions[:5]}{'...' if len(versions) > 5 else ''}")

        for v in versions:
            jar_path = cache_path(core_id, v)
            meta = meta_path(core_id, v)

            if jar_path.exists() and not args.rebuild:
                print(f"  [CACHE] {jar_path.name}")
                continue

            url = resolve_download_url(core, v)
            if url is None:
                print(f"  [SKIP] {v}: no url")
                continue

            size = f"({core_id}/{v})"
            print(f"  [GET] {v} {url[:70]}...")
            if download(url, jar_path):
                # write meta
                meta_data = {
                    "core": core_id,
                    "version": v,
                    "url": url,
                    "downloaded_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                    "size_bytes": jar_path.stat().st_size,
                }
                meta.write_text(json.dumps(meta_data, indent=2))
                print(f"  [OK]  {size} -> {jar_path.name} ({jar_path.stat().st_size} bytes)")
            else:
                print(f"  [FAIL] {size}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 跑 Paper latest 验证 fetch.py 能通**

```bash
cd /workspace/tools/core-fetcher
python3 fetch.py --core paper --latest-only 2>&1 | tail -20
```

Expected: 成功下载至少 1 个 Paper JAR 到 cache/cores/paper/，没有 [SKIP]/[FAIL]。

- [ ] **Step 3: 跑 Purpur latest 再验证**

```bash
python3 fetch.py --core purpur --latest-only 2>&1 | tail -10
```

Expected: 成功。

- [ ] **Step 4: Commit**

```bash
cd /workspace && git add tools/core-fetcher/fetch.py
git commit -m "feat(core-fetcher): add fetch.py — download Minecraft core JARs from Hangar/GitHub/Maven"
```

---

### Task 5: run.py — 运行层

**Files:**
- Create: `tools/core-fetcher/run.py`

- [ ] **Step 1: 写 run.py**

完整代码见下方。核心功能：给定 JAR + YAML core 定义 → 选 JDK → 写 EULA → 启动 → 等 ready → `/stop` → 收集生成的配置文件 → 存 `generated-configs/<core>/<ver>/`。

```python
#!/usr/bin/env python3
"""run.py — Launch Minecraft server cores and capture default config files.

Usage:
    python run.py --core paper --version 1.21.1-133
    python run.py --core paper                 # all downloaded versions
    python run.py --all                        # all cores × all downloaded versions
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from collections import deque
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent.parent
CACHE_DIR = ROOT / "cache" / "cores"
RUNTIME_DIR = ROOT / "cache" / "runtimes"
GENERATED_DIR = ROOT / "generated-configs"
REGISTRY_PATH = Path(__file__).resolve().parent / "core-registry.yaml"
FAILURES_PATH = ROOT / "diffs" / "failures.json"

# Fill these from Task 2 Step 5 output
JDK_PATHS = {
    "1.8":   "/root/.local/share/mise/installs/openjdk@8/bin/java",
    "11":    "/root/.local/share/mise/installs/openjdk@11/bin/java",
    "17":    "/root/.local/share/mise/installs/openjdk@17/bin/java",
    "21":    "/root/.local/share/mise/installs/openjdk@21/bin/java",
    "25":    "/root/.local/share/mise/shims/java",
}


def _parse_ver(s: str) -> tuple[int, ...]:
    """Parse "1.21" -> (21,), "17" -> (17,), "1.8" -> (8,)."""
    parts = re.split(r"[.\-_]", s)
    nums = []
    for p in parts:
        m = re.match(r"(\d+)", p)
        if m:
            nums.append(int(m.group(1)))
    return tuple(nums) or (0,)


def pick_jdk(java_min: str) -> str:
    v_min = _parse_ver(java_min)
    best = None
    for ver_str, path in sorted(JDK_PATHS.items(), key=lambda kv: _parse_ver(kv[0])):
        if Path(path).exists() and _parse_ver(ver_str) >= v_min:
            if best is None or _parse_ver(ver_str) < _parse_ver(best[0]):
                best = (ver_str, path)
    if best is None:
        raise RuntimeError(f"No JDK >= {java_min} found at {list(JDK_PATHS.values())}")
    return best[1]


def load_registry() -> dict:
    with open(REGISTRY_PATH) as f:
        return yaml.safe_load(f)


def find_downloaded_versions(core_id: str) -> list[str]:
    core_dir = CACHE_DIR / core_id
    if not core_dir.exists():
        return []
    jars = sorted(core_dir.glob("*.jar"))
    return [j.stem for j in jars]


def _record_failure(core_id: str, version: str, category: str, detail: str):
    failures = []
    if FAILURES_PATH.exists():
        failures = json.loads(FAILURES_PATH.read_text())
    failures.append({
        "core": core_id,
        "version": version,
        "category": category,
        "detail": detail[:500],
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    })
    FAILURES_PATH.parent.mkdir(parents=True, exist_ok=True)
    FAILURES_PATH.write_text(json.dumps(failures, indent=2, ensure_ascii=False))


def extract_from_jar(jar_path: Path, config_rel_path: str) -> str | None:
    """Try to extract default config from JAR resources as fallback."""
    try:
        proc = subprocess.run(
            ["unzip", "-p", str(jar_path), config_rel_path],
            capture_output=True, text=True, timeout=10
        )
        if proc.returncode == 0 and proc.stdout.strip():
            return proc.stdout
        # also try stripping leading "config/"
        alt = config_rel_path
        if alt.startswith("config/"):
            alt = alt[len("config/"):]
        proc2 = subprocess.run(
            ["unzip", "-p", str(jar_path), alt],
            capture_output=True, text=True, timeout=10
        )
        if proc2.returncode == 0 and proc2.stdout.strip():
            return proc2.stdout
    except Exception:
        pass
    return None


def run_one(core_id: str, version: str, jar_path: Path, core: dict) -> tuple[bool, str]:
    workdir = GENERATED_DIR / core_id / version
    if workdir.exists():
        shutil.rmtree(workdir)
    workdir.mkdir(parents=True)

    # EULA
    if core.get("launch", {}).get("eula", True):
        (workdir / "eula.txt").write_text("eula=true\n")

    jdk = pick_jdk(str(core.get("java-min", "17")))
    jvm_args = core.get("defaults", {}).get("launch", {}).get("java-args",
               ["-Xms256M", "-Xmx1024M", "-XX:+UseSerialGC", "-jar"])
    timeout = core.get("defaults", {}).get("launch", {}).get("timeout_seconds", 60)
    ready_match = core["launch"]["ready-match"]

    cmd = [jdk] + jvm_args + [str(jar_path), "nogui"]
    print(f"  [RUN] {' '.join(cmd[:3])} ... nogui", file=sys.stderr)
    print(f"  [JDK] {jdk}", file=sys.stderr)
    print(f"  [CWD] {workdir}", file=sys.stderr)

    log_tail = deque(maxlen=300)
    try:
        proc = subprocess.Popen(
            cmd, cwd=workdir,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            stdin=subprocess.PIPE, bufsize=1, text=True,
            env={**os.environ, "java.awt.headless": "true"},
        )
    except FileNotFoundError as e:
        return False, f"launch failed: {e}"

    ready_re = re.compile(ready_match, re.IGNORECASE | re.MULTILINE)
    start = time.time()
    while time.time() - start < timeout:
        line = proc.stdout.readline()
        if not line:
            if proc.poll() is not None:
                break
            continue
        line = line.rstrip()
        log_tail.append(line)
        print(f"    {line[:120]}", file=sys.stderr)
        if ready_re.search(line):
            print(f"  [READY] after {time.time() - start:.1f}s", file=sys.stderr)
            break
    else:
        proc.kill()
        return False, "timeout: " + "\n".join(list(log_tail)[-20:])

    if proc.poll() is not None and proc.returncode != 0:
        return False, "early-exit (code " + str(proc.returncode) + "):\n" + "\n".join(list(log_tail)[-20:])

    # Graceful stop
    try:
        if proc.stdin:
            proc.stdin.write("stop\n")
            proc.stdin.flush()
        proc.wait(timeout=20)
    except Exception:
        proc.kill()
        try:
            proc.wait(timeout=5)
        except Exception:
            pass

    if proc.poll() is None:
        proc.kill()

    # Collect generated configs
    collected = {}
    for cf in core["launch"]["config-files"]:
        rel = cf["path"]
        full = workdir / rel
        if full.exists() and full.stat().st_size > 0:
            collected[rel] = full.read_text(errors="replace")
            print(f"  [GOT]  {rel} ({len(collected[rel])} chars)", file=sys.stderr)
        else:
            jcf = extract_from_jar(jar_path, rel)
            if jcf:
                collected[rel] = jcf
                # write to disk too
                fallback_path = workdir / rel
                fallback_path.parent.mkdir(parents=True, exist_ok=True)
                fallback_path.write_text(jcf)
                print(f"  [JAR]  {rel} (fallback, {len(jcf)} chars)", file=sys.stderr)
            else:
                print(f"  [MISS] {rel}", file=sys.stderr)

    # Save manifest
    manifest = {
        "core": core_id,
        "version": version,
        "jar": str(jar_path),
        "jdk": jdk,
        "configs": collected,
        "launched_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    manifest_path = workdir / "_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False))

    return True, f"collected {len(collected)} config files"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core", help="single core id")
    parser.add_argument("--version", help="specific version")
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--retry-failures", action="store_true")
    args = parser.parse_args()

    reg = load_registry()
    all_cores = reg["cores"]

    if args.all:
        target_ids = list(all_cores.keys())
    elif args.core:
        target_ids = [args.core]
    else:
        print("Need --core or --all")
        sys.exit(1)

    success, fail = 0, 0
    for core_id in target_ids:
        if core_id not in all_cores:
            print(f"[SKIP] unknown core: {core_id}")
            continue
        core = all_cores[core_id]

        if args.version:
            versions = [args.version]
        else:
            versions = find_downloaded_versions(core_id)

        if not versions:
            print(f"[{core_id}] no downloaded versions — run fetch.py first")
            continue

        for v in versions:
            jar = CACHE_DIR / core_id / f"{v}.jar"
            if not jar.exists():
                print(f"[{core_id}/{v}] jar missing")
                continue
            print(f"\n{'='*60}")
            print(f"[{core_id}/{v}] starting...")
            ok, msg = run_one(core_id, v, jar, core)
            if ok:
                success += 1
                print(f"[{core_id}/{v}] OK — {msg}")
            else:
                fail += 1
                print(f"[{core_id}/{v}] FAIL — {msg[:200]}")
                _record_failure(core_id, v, "run-fail", msg)

    print(f"\n{'='*60}")
    print(f"Done: {success} OK, {fail} failed")


if __name__ == "__main__":
    main()
```

**IMPORTANT** Run this ONLY AFTER Task 2 is complete. Before running, update the `JDK_PATHS` dict in run.py with actual paths from `mise which java@8`.

- [ ] **Step 2: 更新 run.py 里的 JDK_PATHS**

Task 2 Step 5 输出的真实路径替换进去。

- [ ] **Step 3: 跑 Paper latest 验证**

```bash
cd /workspace/tools/core-fetcher
python3 run.py --core paper 2>&1 | tail -40
```

Expected: JVM 启动日志出现、看到 `[READY]`、收集到 `paper-global.yml` / `paper-world-defaults.yml` / `spigot.yml` / `bukkit.yml` / `server.properties` 全部 5 个文件。

- [ ] **Step 4: 验证输出**

```bash
ls -la /workspace/generated-configs/paper/
```

Expected: 里面有 1 个子目录（版本号命名），子目录里有 5 个 yaml + _manifest.json。

- [ ] **Step 5: Commit**

```bash
cd /workspace && git add tools/core-fetcher/run.py diffs/failures.json
git commit -m "feat(core-fetcher): add run.py — launch cores in sandbox, capture default configs"
```

---

### Task 6: diff.py — 比对层（合并运行时 + Registry 差异）

**Files:**
- Create: `tools/core-fetcher/diff.py`

- [ ] **Step 1: 写 diff.py**

核心功能：
1. 扫描 `generated-configs/<core>/<ver>/` 所有 manifest.json
2. 把 YAML / Properties / TOML 扁平化为点号路径字典
3. 跨版本构建键生命周期（introduced_in / removed_in / default_changes）
4. 解析 ConfigDescriptorRegistry.cs 里已有的 `(ConfigFileName, Key)` 复合键（正则）
5. 输出 `<core>.diff.json` 到 `diffs/`
6. 输出 `diffs/summary.json` 全量汇总

```python
#!/usr/bin/env python3
"""diff.py — Compare runtime-generated configs against ConfigDescriptorRegistry.

Usage:
    python diff.py --core paper
    python diff.py --all
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

import yaml
import toml

ROOT = Path(__file__).resolve().parent.parent.parent
GENERATED_DIR = ROOT / "generated-configs"
DIFFS_DIR = ROOT / "diffs"
REGISTRY_PATH = ROOT / "src" / "MSMC" / "Features" / "ConfigEditor" / "Services" / "ConfigDescriptorRegistry.cs"


# ──────────────────────────── Flatten configs ────────────────────────────

def flatten_yaml(data, prefix="") -> dict[str, str]:
    out = {}
    for k, v in data.items():
        key = f"{prefix}.{k}" if prefix else k
        if isinstance(v, dict):
            out.update(flatten_yaml(v, key))
        elif isinstance(v, list):
            out[key] = json.dumps(v, ensure_ascii=False)
        else:
            out[key] = str(v) if v is not None else ""
    return out


def flatten_properties(text: str) -> dict[str, str]:
    """Java .properties format."""
    out = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or line.startswith("!"):
            continue
        if "=" in line:
            k, v = line.split("=", 1)
        elif ":" in line:
            k, v = line.split(":", 1)
        else:
            continue
        out[k.strip()] = v.strip()
    return out


def flatten_toml(text: str) -> dict[str, str]:
    data = toml.loads(text)
    return flatten_yaml(data)


def parse_config(text: str, fmt: str) -> dict[str, str]:
    fmt = fmt.lower()
    if fmt == "yaml":
        data = yaml.safe_load(text) or {}
        return flatten_yaml(data)
    if fmt == "properties":
        return flatten_properties(text)
    if fmt == "toml":
        return flatten_toml(text)
    return {}


# ──────────────────────────── Parse Registry.cs ────────────────────────────

REGISTRY_RE = re.compile(
    r'ConfigFileName\s*=\s*"([^"]+)"[^}]*?Key\s*=\s*"([^"]+)"',
    re.DOTALL
)


def parse_registry() -> set[tuple[str, str]]:
    """Extract all (ConfigFileName, Key) pairs from Registry.cs via regex."""
    if not REGISTRY_PATH.exists():
        print(f"  [WARN] Registry.cs not found at {REGISTRY_PATH}")
        return set()
    text = REGISTRY_PATH.read_text(errors="replace")
    pairs = set()
    for m in REGISTRY_RE.finditer(text):
        fname, key = m.group(1), m.group(2)
        # normalize leading config/
        if fname.startswith("config/"):
            fname = fname[len("config/"):]
        pairs.add((fname, key))
    return pairs


# ──────────────────────────── Build version history ────────────────────────────

def build_evolution(core_id: str, config_file: str, fmt: str,
                    versions_configs: dict[str, dict[str, str]]) -> dict:
    """versions_configs: {version_str: flat_dict}"""
    sorted_versions = sorted(versions_configs.keys())
    all_keys = set()
    for cfg in versions_configs.values():
        all_keys.update(cfg.keys())

    key_records = {}
    for key in sorted(all_keys):
        present_versions = [v for v in sorted_versions if key in versions_configs[v]]
        if not present_versions:
            continue

        # introduced = first version where key appeared
        introduced = present_versions[0]

        # removed = version after last where it appeared (None if still there)
        removed = None
        last_present = present_versions[-1]
        # check if it disappeared in later versions
        for v in sorted_versions:
            if v > last_present:
                removed = v
                break

        # track default changes
        defaults_seen = {}  # version -> default_value
        for v in present_versions:
            defaults_seen[v] = versions_configs[v][key]

        default_changes = []
        prev = None
        for v in sorted(present_versions):
            cur = versions_configs[v][key]
            if prev is not None and cur != prev:
                default_changes.append({"from_version": None, "to_version": v,
                                         "old_default": prev, "new_default": cur})
            prev = cur

        key_records[key] = {
            "introduced_in": introduced,
            "removed_in": removed,
            "default_changes": default_changes,
            "default_latest": versions_configs[last_present][key],
            "versions_present": present_versions,
            "versions_total": len(sorted_versions),
        }

    return key_records


# ──────────────────────────── Main diff logic ────────────────────────────

def diff_core(core_id: str, registry_pairs: set[tuple[str, str]]) -> dict | None:
    core_dir = GENERATED_DIR / core_id
    if not core_dir.exists():
        return None

    # Group configs by config_file -> {version_str: flat_dict}
    by_config: dict[str, dict[str, dict]] = defaultdict(dict)

    for ver_dir in sorted(core_dir.iterdir()):
        if not ver_dir.is_dir() or ver_dir.name.startswith("."):
            continue
        manifest_path = ver_dir / "_manifest.json"
        if not manifest_path.exists():
            continue
        try:
            manifest = json.loads(manifest_path.read_text())
        except Exception:
            continue

        version = ver_dir.name
        for rel_path, content in manifest.get("configs", {}).items():
            fmt = "yaml"
            if rel_path.endswith(".properties"):
                fmt = "properties"
            elif rel_path.endswith(".toml"):
                fmt = "toml"
            flat = parse_config(content, fmt)
            # normalize: strip "config/" prefix for matching
            normalized_path = rel_path.replace("config/", "")
            by_config[normalized_path][version] = flat

    if not by_config:
        return None

    # Compare each config file
    core_result = {
        "core": core_id,
        "generated_at": __import__("time").strftime("%Y-%m-%dT%H:%M:%SZ", __import__("time").gmtime()),
        "configs": {},
    }

    total_new = 0
    total_existing = 0
    total_removed = 0
    total_drifted = 0

    for config_file, versions_configs in by_config.items():
        fmt = "yaml"
        if config_file.endswith(".properties"):
            fmt = "properties"
        elif config_file.endswith(".toml"):
            fmt = "toml"

        key_records = build_evolution(core_id, config_file, fmt, versions_configs)

        keys_out = []
        for key, info in sorted(key_records.items()):
            reg_has_key = (config_file, key) in registry_pairs

            if not reg_has_key:
                state = "new_key"
                total_new += 1
            elif info["removed_in"] is not None:
                state = "removed"
                total_removed += 1
            elif info["default_changes"]:
                state = "drifted"
                total_drifted += 1
            else:
                state = "existing"
                total_existing += 1

            keys_out.append({
                "path": key,
                "introduced_in": info["introduced_in"],
                "removed_in": info["removed_in"],
                "default_latest": info["default_latest"],
                "default_changes": info["default_changes"],
                "state": state,
                "needs_translation": state in ("new_key", "drifted"),
            })

        core_result["configs"][config_file] = {
            "versions_scanned": sorted(versions_configs.keys()),
            "total_keys": len(key_records),
            "keys": keys_out,
        }

    core_result["summary"] = {
        "new_keys": total_new,
        "existing": total_existing,
        "removed": total_removed,
        "drifted": total_drifted,
    }

    return core_result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core", help="single core id")
    parser.add_argument("--all", action="store_true")
    args = parser.parse_args()

    registry_pairs = parse_registry()
    print(f"[DIFF] Registry has {len(registry_pairs)} (config_file, key) pairs")

    DIFFS_DIR.mkdir(parents=True, exist_ok=True)

    if args.all:
        targets = [d.name for d in GENERATED_DIR.iterdir() if d.is_dir()]
    elif args.core:
        targets = [args.core]
    else:
        print("Need --core or --all")
        sys.exit(1)

    summary = {
        "generated_at": __import__("time").strftime("%Y-%m-%dT%H:%M:%SZ", __import__("time").gmtime()),
        "cores": {},
        "totals": {"new": 0, "existing": 0, "removed": 0, "drifted": 0},
    }

    for core_id in sorted(targets):
        print(f"\n[{core_id}] diffing...")
        result = diff_core(core_id, registry_pairs)
        if result is None:
            print(f"  [SKIP] no generated configs")
            continue

        out_path = DIFFS_DIR / f"{core_id}.diff.json"
        out_path.write_text(json.dumps(result, ensure_ascii=False, indent=2))

        s = result["summary"]
        summary["cores"][core_id] = s
        summary["totals"]["new"] += s["new_keys"]
        summary["totals"]["existing"] += s["existing"]
        summary["totals"]["removed"] += s["removed"]
        summary["totals"]["drifted"] += s["drifted"]

        print(f"  new={s['new_keys']} existing={s['existing']} removed={s['removed']} drifted={s['drifted']}")

    summary_path = DIFFS_DIR / "summary.json"
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"\n{'='*40}")
    print(f"TOTAL: new={summary['totals']['new']} existing={summary['totals']['existing']} "
          f"removed={summary['totals']['removed']} drifted={summary['totals']['drifted']}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 装 toml 库（如果还没装）**

```bash
pip install toml 2>&1 | tail -3
```

- [ ] **Step 3: 跑 diff.py --core paper**

```bash
cd /workspace/tools/core-fetcher
python3 diff.py --core paper 2>&1
```

Expected: 输出 `[DIFF] Registry has X (config_file, key) pairs` 然后 Paper 的 diff 结果，有 `diffs/paper.diff.json` 生成。

- [ ] **Step 4: 看看 diff.json 里面有多少 new_keys**

```bash
python3 -c "import json; d=json.load(open('/workspace/diffs/paper.diff.json')); print('summary:', d['summary'])"
```

- [ ] **Step 5: Commit**

```bash
cd /workspace && git add tools/core-fetcher/diff.py diffs/
git commit -m "feat(core-fetcher): add diff.py — compare generated configs against Registry.cs, version evolution tracing"
```

---

### Task 7: translate.py — RAG 翻译注入（脚手架，不真调 AI）

**Files:**
- Create: `tools/core-fetcher/translate.py`

- [ ] **Step 1: 写 translate.py（脚手架版本）**

这个脚手架只做**格式化输出**和**占位翻译**——把 `diff.json` 里的 `new_key` 条目列出，为每条生成一个 C# ServerConfigDescriptor 片段（DisplayName="TO_BE_TRANSLATED"），不真调 AI。真正的 RAG 翻译在后续迭代里接入。

核心产出：
1. 为每个 `new_key` / `drifted` 生成 C# 代码片段（Display 占位）
2. 输出到 `diffs/<core>.new-keys.snippets.cs`（可审阅）
3. 不修改 ConfigDescriptorRegistry.cs——人工确认后再注入

```python
#!/usr/bin/env python3
"""translate.py — Format diff results into C# ServerConfigDescriptor snippets.

This is scaffolding only — actual AI/RAG translation plugged in later.
Snippets go to diffs/<core>.new-keys.snippets.cs for human review.

Usage:
    python translate.py --core paper
    python translate.py --all
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
DIFFS_DIR = ROOT / "diffs"


def _infer_value_type(key: str, default: str) -> str:
    if default.lower() in ("true", "false"):
        return "bool"
    try:
        float(default)
        if "." in default:
            return "double"
        return "int"
    except ValueError:
        pass
    if "," in default or "[" in default:
        return "list"
    return "string"


def format_cs_descriptor(config_file: str, key: str, default: str, introduced_in: str | None) -> str:
    vtype = _infer_value_type(key, default)
    default_escaped = default.replace("\\", "\\\\").replace("\"", "\\\"")
    default_field = f', DefaultValue = "{default_escaped}"' if default else ""
    introduced = f', IntroducedIn = "{introduced_in}"' if introduced_in else ""
    return (
        f'new ServerConfigDescriptor {{ ConfigFileName = "{config_file}", '
        f'Key = "{key}", DisplayName = "TODO: {key}", '
        f'Description = "TODO: describe what {key} does", '
        f'Category = "TODO: category", ValueType = "{vtype}"'
        f'{default_field}{introduced} }},'
    )


def translate_core(core_id: str, diff_path: Path) -> str | None:
    if not diff_path.exists():
        return None
    diff = json.loads(diff_path.read_text())

    lines = [
        f"// ──────────────────────────────────────────",
        f"// NEW KEYS — {core_id} (generated by translate.py)",
        f"// Review each entry and replace TODO DisplayName/Description/Category",
        f"// before injecting into ConfigDescriptorRegistry.cs",
        f"// ──────────────────────────────────────────",
        "",
    ]

    total_new = 0
    for config_file, cfg in diff.get("configs", {}).items():
        keys = [k for k in cfg["keys"] if k["needs_translation"]]
        if not keys:
            continue
        lines.append(f"// ── {config_file} ({len(keys)} keys) ──")
        for k in keys:
            snippet = format_cs_descriptor(
                config_file,
                k["path"],
                k.get("default_latest", ""),
                k.get("introduced_in"),
            )
            lines.append(snippet)
        lines.append("")
        total_new += len(keys)

    if total_new == 0:
        return None

    return "\n".join(lines), total_new


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core")
    parser.add_argument("--all", action="store_true")
    args = parser.parse_args()

    if args.all:
        diff_files = sorted(DIFFS_DIR.glob("*.diff.json"))
    elif args.core:
        f = DIFFS_DIR / f"{args.core}.diff.json"
        diff_files = [f]
    else:
        print("Need --core or --all")
        sys.exit(1)

    for df in diff_files:
        core_id = df.stem.replace(".diff", "")
        result = translate_core(core_id, df)
        if result is None:
            print(f"[{core_id}] no new keys")
            continue
        content, count = result
        out = df.with_name(f"{core_id}.new-keys.snippets.cs")
        out.write_text(content)
        print(f"[{core_id}] {count} new key snippets → {out.name}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 跑 translate.py --core paper**

```bash
cd /workspace/tools/core-fetcher
python3 translate.py --core paper 2>&1
```

Expected: 输出 new key 数量，生成 `diffs/paper.new-keys.snippets.cs`。

- [ ] **Step 3: 看 snippets.cs 内容**

```bash
head -30 /workspace/diffs/paper.new-keys.snippets.cs
```

Expected: 看到 C# 格式的 ServerConfigDescriptor 条目，DisplayName="TODO: ..."。

- [ ] **Step 4: Commit**

```bash
cd /workspace && git add tools/core-fetcher/translate.py diffs/
git commit -m "feat(core-fetcher): add translate.py — scaffold that formats diffs into C# snippets for human review"
```

---

### Task 8: verify.py — 流水线自检 + 整合入口

**Files:**
- Create: `tools/core-fetcher/verify.py`

- [ ] **Step 1: 写 verify.py**

这是一个整合脚本——给定一个 core_id，依次跑 fetch → run → diff → translate，产出完整流水线输出 + 报告。

```python
#!/usr/bin/env python3
"""verify.py — Run the full pipeline for one core and produce a report.

Usage:
    python verify.py --core paper
    python verify.py --core paper --skip-fetch   # reuse cached jars
    python verify.py --core paper --skip-run      # reuse generated configs
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
TOOLS = Path(__file__).resolve().parent
REPORT_DIR = ROOT / "diffs"


def run_step(name: str, cmd: list[str]) -> tuple[int, str, str]:
    print(f"\n{'='*50}")
    print(f"[{name}] {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=600)
    print(result.stdout[-500:] if result.stdout else "")
    if result.stderr:
        print(result.stderr[-500:] if result.stderr else "", file=sys.stderr)
    return result.returncode, result.stdout, result.stderr


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core", required=True)
    parser.add_argument("--skip-fetch", action="store_true")
    parser.add_argument("--skip-run", action="store_true")
    args = parser.parse_args()

    report = {
        "core": args.core,
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "steps": {},
    }

    # Step 1: fetch
    if not args.skip_fetch:
        rc, _, _ = run_step("fetch", ["python3", str(TOOLS / "fetch.py"),
                                       "--core", args.core, "--latest-only"])
        report["steps"]["fetch"] = "OK" if rc == 0 else f"FAIL({rc})"

    # Step 2: run
    if not args.skip_run:
        rc, _, _ = run_step("run", ["python3", str(TOOLS / "run.py"),
                                     "--core", args.core])
        report["steps"]["run"] = "OK" if rc == 0 else f"FAIL({rc})"

    # Step 3: diff
    rc, out, _ = run_step("diff", ["python3", str(TOOLS / "diff.py"),
                                    "--core", args.core])
    report["steps"]["diff"] = "OK" if rc == 0 else f"FAIL({rc})"

    # Step 4: translate
    rc, _, _ = run_step("translate", ["python3", str(TOOLS / "translate.py"),
                                       "--core", args.core])
    report["steps"]["translate"] = "OK" if rc == 0 else f"FAIL({rc})"

    # Step 5: summarize
    diff_path = ROOT / "diffs" / f"{args.core}.diff.json"
    if diff_path.exists():
        diff = json.loads(diff_path.read_text())
        report["summary"] = diff.get("summary", {})

    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    (REPORT_DIR / f"{args.core}.report.json").write_text(json.dumps(report, indent=2))

    print(f"\n{'='*50}")
    print(f"REPORT for {args.core}:")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 跑完整流水线验证**

```bash
cd /workspace/tools/core-fetcher
python3 verify.py --core paper 2>&1
```

Expected: 4 个步骤都 OK，最后有 summary 输出（new/existing/removed/drifted 计数）。

- [ ] **Step 3: Commit**

```bash
cd /workspace && git add tools/core-fetcher/verify.py
git commit -m "feat(core-fetcher): add verify.py — end-to-end pipeline wrapper for one core"
```

---

### Task 9: C# Registry.cs 加版本字段 + 拆 partial class（不立即执行，先 plan）

**说明：** 这个 Task 需要看 dotnet 能不能编译、需要人工审阅拆分后的文件，属于 Phase 4 的事情。这里只把具体步骤规划出来，**不在本次执行范围之内**，留给 Phase 4。

当 Phase 0–3 全部跑完、翻译结果 OK 之后：

1. 在 `ServerConfigDescriptor` 类上加 `IntroducedIn` / `RemovedIn` / `ValueHistory` 字段
2. 加 `ValueHistoryEntry` record
3. 把 Registry.cs 里 43 个 Register* 方法按以下分组拆到 partial class 文件：

| 文件 | 包含的 Register* 方法 |
|---|---|
| `ConfigDescriptorRegistry.cs` | 类定义 + 构造函数 + 查找/比对方法（保留） |
| `ConfigDescriptorRegistry.Vanilla.cs` | RegisterServerProperties, RegisterServerPropertiesExtras, RegisterBukkitYml, RegisterSpigotYml, RegisterCommandsYml, RegisterPermissionsYml, RegisterHelpYml |
| `ConfigDescriptorRegistry.Paper.cs` | RegisterPaperGlobalYml, RegisterPaperWorldDefaultsYml, RegisterPurpurYml, RegisterPufferfishYml, RegisterLeavesYml, RegisterLeafYml, RegisterFoliaGlobalYml, RegisterKaiijuYml, RegisterNachoYml, RegisterUSpigotYml, RegisterAirplaneYml, RegisterTuinityYml, RegisterYatopiaYml, RegisterAkarinYml |
| `ConfigDescriptorRegistry.Proxy.cs` | RegisterVelocityToml, RegisterBungeeCordConfigYml, RegisterWaterfallYml, RegisterFlameCordYml, RegisterHexaCordYml |
| `ConfigDescriptorRegistry.ModLoader.cs` | RegisterForgeServerToml, RegisterNeoForgeYml, RegisterFabricServerProperties, RegisterQuiltServerProperties |
| `ConfigDescriptorRegistry.Hybrid.cs` | RegisterArclightYml, RegisterBannerYml, RegisterCatServerYml, RegisterMagmaConf, RegisterMohistConfigYml |
| `ConfigDescriptorRegistry.Other.cs` | RegisterGlowstoneConfig, RegisterSpongeGlobalConf, RegisterSpongeForgeConf, RegisterNukkitYml, RegisterNukkitServerProperties, RegisterPowerNukkitYml, RegisterPowerNukkitServerProperties |

---

### Task 10: rag.py — RAG 知识库构建（Phase 0）

**Files:**
- Create: `tools/core-fetcher/rag.py`
- Create: `knowledge-base/` (git-tracked)

**说明：** 这个脚本扫项目内已有的 `docs/server-cores/*.md`，把其中表格结构化抽取成 `knowledge-base/<core>/<config_file>.json`。MineBBS 和 中文 Minecraft Wiki 的爬取作为后续迭代——先把最可靠的数据源（已有人工翻译）入库。

- [ ] **Step 1: 写 rag.py**

```python
#!/usr/bin/env python3
"""rag.py — Build RAG knowledge base from project-internal docs.

Primary source: docs/server-cores/*.md (already translated, highest quality).
Output: knowledge-base/<core>/<config_file>.json (git-tracked).

Usage:
    python rag.py                   # build/update all entries
    python rag.py --core paper
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
DOCS_DIR = ROOT / "docs" / "server-cores"
KB_DIR = ROOT / "knowledge-base"


# ──────────────────────────── Markdown table parser ────────────────────────────

def parse_markdown_tables(text: str) -> list[dict]:
    """Extract all markdown tables as list[list[str]]."""
    tables = []
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.startswith("|") and i + 1 < len(lines) and re.match(r"^\|[\s:\|]+\|", lines[i + 1]):
            header = [c.strip() for c in line.split("|")[1:-1]]
            rows = []
            i += 2  # skip header + separator
            while i < len(lines) and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].split("|")[1:-1]]
                # pad to header length
                while len(cells) < len(header):
                    cells.append("")
                rows.append(dict(zip(header, cells)))
                i += 1
            if rows:
                tables.append({"header": header, "rows": rows})
        else:
            i += 1
    return tables


# ──────────────────────────── Core manifest ────────────────────────────

def build_core_manifest() -> list[dict]:
    """Scan README.md for core list + docs/*.md for mapping."""
    manifest = []
    for md_path in sorted(DOCS_DIR.glob("*.md")):
        if md_path.name == "README.md" or md_path.name.startswith("_"):
            continue
        text = md_path.read_text(errors="replace")
        # Try to find display name from first heading
        m = re.search(r"^#\s+(.+?)\s*服务器", text, re.MULTILINE)
        display = m.group(1) if m else md_path.stem
        manifest.append({"file": md_path.name, "display": display, "path": str(md_path.relative_to(ROOT))})
    return manifest


# ──────────────────────────── Extract config entries from a single .md ────────────────────────────

def extract_from_md(md_path: Path) -> dict[str, list[dict]]:
    """
    Parse a server-core .md doc, pull out all config entries.
    Returns: {config_file: [{key_path, term, context}, ...]}
    """
    text = md_path.read_text(errors="replace")
    tables = parse_markdown_tables(text)

    entries_by_file: dict[str, list[dict]] = {}
    current_config_file = "unknown"

    # Detect config file sections (headings like "## config/paper-global.yml")
    for table in tables:
        # Find section context by searching backwards for last "##" heading
        # Simpler: look for "## " lines between tables
        pass  # we handle heading detection below

    # Process line by line for heading → tables
    lines = text.splitlines()
    for i, line in enumerate(lines):
        m = re.match(r"^##\s+(.+)", line)
        if m:
            heading = m.group(1)
            # Check if heading mentions a config file path
            cm = re.search(r"([\w\-./]+\.(yml|yaml|properties|toml|conf))", heading)
            if cm:
                current_config_file = cm.group(1)

    # Now extract from tables with known structure
    # Most tables have columns: 键名 | 中文含义 | 类型 | 默认值 ...
    for table in tables:
        header_lower = [h.lower() for h in table["header"]]
        key_col = None
        term_col = None
        desc_col = None

        for j, h in enumerate(header_lower):
            if h in ("键名", "键", "key", "路径", "path"):
                key_col = j
            if h in ("中文含义", "中文", "含义", "显示名"):
                term_col = j
            if h in ("说明", "描述", "description", "详情", "备注"):
                desc_col = j

        if key_col is None or term_col is None:
            continue

        config_file = current_config_file
        if config_file == "unknown":
            continue

        bucket = entries_by_file.setdefault(config_file, [])
        for row in table["rows"]:
            # row is dict keyed by header names in order
            key_path = list(row.values())[key_col]
            term = list(row.values())[term_col]
            desc = list(row.values())[desc_col] if desc_col is not None else ""

            if not key_path or not term or "│" in key_path:
                continue

            bucket.append({
                "key_path": key_path.replace("`", "").strip(),
                "chinese_entries": [{
                    "term": term.replace("`", "").strip(),
                    "context": desc.replace("`", "").strip(),
                    "source": str(md_path.relative_to(ROOT)),
                }],
            })

    return entries_by_file


# ──────────────────────────── Main ────────────────────────────

def extract_core_id_from_filename(filename: str) -> str:
    """Map doc filename like 04-paper.md → 'paper'."""
    stem = Path(filename).stem  # "04-paper"
    m = re.search(r"-(.+)$", stem)
    if m:
        return m.group(1)
    return stem


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core", help="filter by core id (part of filename)")
    args = parser.parse_args()

    manifest = build_core_manifest()
    print(f"[RAG] {len(manifest)} doc files found")

    count_files = 0
    count_entries = 0

    for item in manifest:
        core_id = extract_core_id_from_filename(item["file"])
        if args.core and args.core != core_id:
            continue

        entries_by_file = extract_from_md(Path(item["path"]))

        if not entries_by_file:
            print(f"[{core_id}] no extractable tables")
            continue

        kb_core_dir = KB_DIR / core_id
        kb_core_dir.mkdir(parents=True, exist_ok=True)

        for config_file, entries in entries_by_file.items():
            kb_entry = {
                "config_file": config_file,
                "cores": [core_id],
                "source_docs": [{"source": "project-md", "path": item["path"]}],
                "entries": entries,
                "schema_version": 1,
            }
            safe_name = config_file.replace("/", "__")
            out = kb_core_dir / f"{safe_name}.json"
            out.write_text(json.dumps(kb_entry, ensure_ascii=False, indent=2))
            count_files += 1
            count_entries += len(entries)
            print(f"  [{core_id}] {config_file}: {len(entries)} entries → {out.name}")

    print(f"\nDone: {count_files} KB files, {count_entries} entries")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 跑 rag.py 扫全部 doc**

```bash
cd /workspace/tools/core-fetcher
python3 rag.py 2>&1 | tail -30
```

Expected: 输出每个 core 抽取到多少 entries。

- [ ] **Step 3: 验证**

```bash
ls -la /workspace/knowledge-base/
```

Expected: 至少有 paper/paper-global.yml.json 等条目。

- [ ] **Step 4: Commit**

```bash
cd /workspace && git add tools/core-fetcher/rag.py knowledge-base/
git commit -m "feat(core-fetcher): rag.py — build initial knowledge-base from project server-cores .md docs"
```

---

## 自审

**1. Spec coverage:**

| Spec 章节 | 对应 Task |
|---|---|
| 3 (RAG 知识库) | Task 10 |
| 4 (核心清单 YAML) | Task 3 |
| 5 (fetch.py) | Task 4 |
| 6 (run.py) | Task 2 (JDK) + Task 5 |
| 7 (src.py 源码辅助) | **Phase 3 补** — 跳过（当前 repo 没 git clone 源码的必要，先靠运行时配置够了） |
| 8 (diff.py) | Task 6 |
| 9 (Registry 扩展 + 拆分) | Task 9 (规划) |
| 9 (translate.py) | Task 7 (脚手架) |
| 11 (Phase 顺序) | 按 Task 编号执行 |

**2. Placeholder scan:** 计划里没有 TODO/TBD/XXX。Task 7 明确说是脚手架版本。Task 9 明确标注为规划。

**3. Type consistency:** run.py 的 `run_one` 返回 `tuple[bool, str]`，main 里正确处理。diff.py 的 `diff_core` 返回 dict 或 None，main 里正确检查 `result is None`。命名一致。

**4. 缺失项:** spec 里提到的 src.py（源码辅助层）和 MineBBS/Wiki 爬取没有覆盖。这是有意的 YAGNI 简化——当前项目已有 .md 文档作为高质量知识库，MineBBS 爬取容易被反爬，源码 clone 也需要额外带宽。Phase 3 批量跑的时候再补。

---

## 执行顺序

按 Task 编号：**1 → 2 → 3 → 10 → 4 → 5 → 6 → 7 → 8**

- Task 1–2 是基础设施，必须最先
- Task 3 (YAML) 之后，Task 10 (RAG KB) 其实可以和 Task 4/5 并行（不互相依赖），但为了顺序干净放一起
- Task 4–8 是 fetch → run → diff → translate → verify 串行流水线
- Task 9（Registry C# 改动）推迟到 Phase 4，在有真实翻译结果后执行
