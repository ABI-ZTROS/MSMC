#!/usr/bin/env python3
"""run.py — Launch Minecraft server cores in sandbox, capture default config files.

Usage:
    python run.py --core leaves               # all downloaded versions
    python run.py --core leaves --version ... # specific version
    python run.py --all                       # all cores × all downloaded versions
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
GENERATED_DIR = ROOT / "generated-configs"
FAILURES_PATH = ROOT / "diffs" / "failures.json"

# Real JDK paths discovered via `mise ls java`
JDK_PATHS = {
    "1.8":   "/root/.local/share/mise/installs/java/temurin-8.0.502+7/bin/java",
    "11":    "/root/.local/share/mise/installs/java/11.0.2/bin/java",
    "17":    "/root/.local/share/mise/installs/java/17.0.2/bin/java",
    "21":    "/root/.local/share/mise/installs/java/21.0.2/bin/java",
    "25":    "/root/.local/share/mise/installs/java/25.0.2/bin/java",
}


def _parse_ver(s: str) -> tuple[int, ...]:
    parts = re.split(r"[.\-_]", s)
    nums = []
    for p in parts:
        m = re.match(r"(\d+)", p)
        if m:
            nums.append(int(m.group(1)))
    if not nums:
        return (0,)
    # Normalize "1.X" (legacy Java 8-style) -> (X,) so that
    # (1, 8) == (8,) and (1, 11) == (11,) for JDK comparison.
    if len(nums) >= 2 and nums[0] == 1 and nums[1] >= 5:
        return tuple(nums[1:])
    return tuple(nums)


def pick_jdk(java_min: str) -> str | None:
    v_min = _parse_ver(java_min)
    candidates = []
    for ver_str, path in JDK_PATHS.items():
        if not Path(path).exists():
            continue
        candidates.append((_parse_ver(ver_str), path))
    candidates.sort(key=lambda x: x[0])
    for v_tuple, path in candidates:
        if v_tuple >= v_min:
            return path
    return None


def load_registry() -> dict:
    with open(Path(__file__).resolve().parent / "core-registry.yaml") as f:
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
        try:
            failures = json.loads(FAILURES_PATH.read_text())
        except Exception:
            failures = []
    failures.append({
        "core": core_id, "version": version, "category": category,
        "detail": detail[:1000],
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    })
    FAILURES_PATH.parent.mkdir(parents=True, exist_ok=True)
    FAILURES_PATH.write_text(json.dumps(failures, indent=2, ensure_ascii=False))


def extract_from_jar(jar_path: Path, rel_path: str) -> str | None:
    try:
        alt = rel_path
        if alt.startswith("config/"):
            alt = alt[len("config/"):]
        for candidate in (rel_path, alt, rel_path.replace("/", ".")):
            proc = subprocess.run(
                ["unzip", "-p", str(jar_path), candidate],
                capture_output=True, text=True, timeout=15,
            )
            if proc.returncode == 0 and proc.stdout.strip():
                return proc.stdout
    except Exception:
        pass
    return None


def _proxy_jvm_flags() -> list[str]:
    """Build JVM proxy flags from environment if present."""
    flags = []
    proxy_host = None
    proxy_port = None
    for key in ("HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy"):
        val = os.environ.get(key)
        if val:
            # val is like http://127.0.0.1:18080
            try:
                from urllib.parse import urlparse
                parsed = urlparse(val)
                proxy_host = parsed.hostname or "127.0.0.1"
                proxy_port = parsed.port or 80
                break
            except Exception:
                pass
    if proxy_host and proxy_port:
        flags.extend([
            f"-Dhttp.proxyHost={proxy_host}",
            f"-Dhttp.proxyPort={proxy_port}",
            f"-Dhttps.proxyHost={proxy_host}",
            f"-Dhttps.proxyPort={proxy_port}",
            "-Dhttp.nonProxyHosts=localhost|127.0.0.1",
        ])
        print(f"  [PROXY] JVM proxy -> {proxy_host}:{proxy_port}", file=sys.stderr)
    return flags


def run_one(core_id: str, version: str, jar_path: Path, core: dict) -> tuple[bool, str]:
    workdir = GENERATED_DIR / core_id / version
    if workdir.exists():
        shutil.rmtree(workdir)
    workdir.mkdir(parents=True)

    # EULA
    if core.get("launch", {}).get("eula", True):
        (workdir / "eula.txt").write_text("eula=true\n")

    jdk = pick_jdk(str(core.get("java-min", "17")))
    if jdk is None:
        return False, f"No JDK >= {core.get('java-min')}"

    jvm_args = core.get("defaults", {}).get("launch", {}).get("java-args",
               ["-Xms256M", "-Xmx1024M", "-XX:+UseSerialGC", "-jar"])
    timeout = core.get("defaults", {}).get("launch", {}).get("timeout_seconds", 90)
    ready_match = core["launch"]["ready-match"]

    # Build JVM command with proxy flags
    proxy_flags = _proxy_jvm_flags()
    cmd = [jdk] + proxy_flags + jvm_args + [str(jar_path), "nogui"]
    env = {**os.environ, "JAVA_TOOL_OPTIONS": "-Djava.awt.headless=true"}

    print(f"  [RUN] jdk={jdk[-35:]}", file=sys.stderr)
    print(f"  [CWD] {workdir}", file=sys.stderr)
    print(f"  [CMD] {' '.join(cmd[:6])} ... nogui", file=sys.stderr)

    proc = None
    proc_ok = False
    proc_msg = ""
    log_tail = deque(maxlen=400)
    collected = {}

    try:
        proc = subprocess.Popen(
            cmd, cwd=workdir, env=env,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            stdin=subprocess.PIPE, bufsize=1, text=True,
        )
    except FileNotFoundError as e:
        proc_msg = f"launch failed: {e}"

    if proc is not None:
        ready_re = re.compile(ready_match, re.IGNORECASE | re.MULTILINE)
        start = time.time()
        timed_out = False
        while time.time() - start < timeout:
            line = proc.stdout.readline()
            if not line and proc.poll() is not None:
                break
            if line:
                line = line.rstrip()
                log_tail.append(line)
                print(f"    {line[:150]}", file=sys.stderr)
                if ready_re.search(line):
                    elapsed = time.time() - start
                    print(f"  [READY] after {elapsed:.1f}s", file=sys.stderr)
                    proc_ok = True
                    break
        else:
            timed_out = True
            proc.kill()
            proc_msg = f"timeout ({timeout}s)\n" + "\n".join(list(log_tail)[-30:])

        if not proc_ok and proc.poll() is not None and proc.returncode != 0 and not timed_out:
            proc_msg = f"early-exit code={proc.returncode}\n" + "\n".join(list(log_tail)[-30:])

        # Graceful stop only if we reached READY
        if proc_ok:
            try:
                if proc.stdin:
                    proc.stdin.write("stop\n")
                    proc.stdin.flush()
                proc.wait(timeout=25)
            except Exception:
                proc.kill()
                try:
                    proc.wait(timeout=5)
                except Exception:
                    pass
            if proc.poll() is None:
                proc.kill()
    else:
        proc_msg = "proc not started"

    # ── FINALLY: always try to collect configs (from disk OR JAR) ──
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
                fallback_path = workdir / rel
                fallback_path.parent.mkdir(parents=True, exist_ok=True)
                fallback_path.write_text(jcf)
                print(f"  [JAR]  {rel} fallback ({len(jcf)} chars)", file=sys.stderr)
            else:
                print(f"  [MISS] {rel}", file=sys.stderr)

    manifest = {
        "core": core_id, "version": version, "jar": str(jar_path),
        "jdk": jdk, "configs": collected,
        "launched_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "launched_ok": proc_ok,
        "error": proc_msg if not proc_ok else None,
    }
    (workdir / "_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False))

    return proc_ok, f"collected {len(collected)} config files" + (f"  | {proc_msg[:100]}" if not proc_ok else "")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core")
    parser.add_argument("--version")
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
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

    GENERATED_DIR.mkdir(parents=True, exist_ok=True)
    FAILURES_PATH.parent.mkdir(parents=True, exist_ok=True)

    ok, fail, skip = 0, 0, 0
    for core_id in target_ids:
        if core_id not in all_cores:
            print(f"[SKIP] unknown core: {core_id}")
            skip += 1
            continue
        core = all_cores[core_id]

        if args.version:
            versions = [args.version]
        else:
            versions = find_downloaded_versions(core_id)
        if not versions:
            print(f"[{core_id}] no downloaded JARs — run fetch.py first")
            skip += 1
            continue

        print(f"\n{'='*60}")
        print(f"[{core_id}] {len(versions)} version(s): {versions}")
        for v in versions:
            jar = CACHE_DIR / core_id / f"{v}.jar"
            if not jar.exists():
                continue
            if args.dry_run:
                print(f"  [DRY] would run {jar.name}")
                ok += 1
                continue
            ok2, fail2 = run_one(core_id, v, jar, core)
            if ok2:
                ok += 1
            else:
                fail += 1
                _record_failure(core_id, v, "run-fail", fail2)
                print(f"  [FAIL] {fail2[:200]}")

    print(f"\n{'='*60}")
    print(f"Summary: {ok} OK, {fail} failed, {skip} skipped")


if __name__ == "__main__":
    main()
