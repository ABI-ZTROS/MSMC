#!/usr/bin/env python3
"""diff.py — Compare runtime-generated configs against ConfigDescriptorRegistry.cs.

Usage:
    python diff.py --core leaves
    python diff.py --all
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from collections import defaultdict
from pathlib import Path

import yaml

try:
    import toml as _toml
except ImportError:
    _toml = None

ROOT = Path(__file__).resolve().parent.parent.parent
GENERATED_DIR = ROOT / "generated-configs"
DIFFS_DIR = ROOT / "diffs"
REGISTRY_PATH = ROOT / "src" / "MSMC" / "Features" / "ConfigEditor" / "Services" / "ConfigDescriptorRegistry.cs"


# ── Config flatteners ──

def flatten(data, prefix="") -> dict[str, str]:
    out = {}
    for k, v in data.items():
        key = f"{prefix}.{k}" if prefix else k
        if isinstance(v, dict):
            out.update(flatten(v, key))
        elif isinstance(v, list):
            out[key] = json.dumps(v, ensure_ascii=False)
        else:
            out[key] = str(v) if v is not None else ""
    return out


def flatten_properties(text: str) -> dict[str, str]:
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


def parse_config(text: str, fmt: str) -> dict[str, str]:
    fmt = fmt.lower()
    try:
        if fmt == "yaml":
            data = yaml.safe_load(text) or {}
            return flatten(data) if isinstance(data, dict) else {}
        if fmt == "properties":
            return flatten_properties(text)
        if fmt == "toml" and _toml:
            return flatten(_toml.loads(text))
        if fmt == "hocon":
            # HOCON isn't yaml but close enough for key extraction
            data = yaml.safe_load(text) or {}
            return flatten(data) if isinstance(data, dict) else {}
    except Exception as e:
        print(f"    [WARN] parse {fmt}: {e}", file=sys.stderr)
        return {}
    return {}


# ── Registry.cs parser ──

def parse_registry() -> set[tuple[str, str]]:
    """Parse ConfigDescriptorRegistry.cs for (ConfigFileName, Key) pairs.

    The C# source uses this pattern:
        const string file = "leaves.yml";
        Register(new ServerConfigDescriptor
        {
            Key = "settings.bstats-usage",
            ConfigFileName = file,  // or "inline-file.yml"
            ...
        });
    """
    if not REGISTRY_PATH.exists():
        print(f"[WARN] Registry.cs not found at {REGISTRY_PATH}", file=sys.stderr)
        return set()

    text = REGISTRY_PATH.read_text(encoding="utf-8", errors="replace")
    text = text.replace("\r\n", "\n").replace("\r", "\n")

    current_file = None
    pairs = set()
    total_descriptors = 0

    # Walk line by line
    lines = text.split("\n")
    i = 0
    while i < len(lines):
        line = lines[i]

        # Track `const string file = "..."`  (also handles `const string xxxFile = "..."`)
        m_file = re.search(r'const\s+string\s+\w*\s*=\s*"([^"]+)"', line)
        if m_file:
            current_file = m_file.group(1)
            i += 1
            continue

        # Detect start of Register(new ServerConfigDescriptor { ... })
        # (avoid matching the method signature `Register(ServerConfigDescriptor descriptor)`)
        if "Register(new ServerConfigDescriptor" in line:
            # Collect the full C# object initializer block.
            # NOTE: `{` may be on the SAME line (inline) or NEXT line.
            block_lines = [line]
            brace = line.count("{") - line.count("}")
            if brace <= 0:
                # Opening brace hasn't appeared yet — grab next line first
                i += 1
                if i < len(lines):
                    block_lines.append(lines[i])
                    brace += lines[i].count("{") - lines[i].count("}")
            i += 1
            while i < len(lines) and brace > 0:
                block_lines.append(lines[i])
                brace += lines[i].count("{") - lines[i].count("}")
                i += 1
            block = "\n".join(block_lines)

            m_key = re.search(r'Key\s*=\s*"([^"]+)"', block)
            if m_key:
                key = m_key.group(1)
                # ConfigFileName may be inline string or reference `file` constant
                m_cfn = re.search(r'ConfigFileName\s*=\s*"([^"]+)"', block)
                if m_cfn:
                    fname = m_cfn.group(1)
                elif current_file:
                    fname = current_file
                else:
                    fname = "unknown"

                # Normalize: strip "config/" directory prefix
                norm = fname
                if norm.startswith("config/"):
                    norm = norm[len("config/"):]
                pairs.add((norm, key))
                total_descriptors += 1
        else:
            i += 1

    print(f"[REGISTRY] parsed {total_descriptors} descriptors → {len(pairs)} unique (file, key) pairs",
          file=sys.stderr)
    return pairs


# ── Version evolution builder ──

def build_evolution(versions_configs: dict[str, dict[str, str]]) -> dict[str, dict]:
    """versions_configs: {version: flat_dict}"""
    sorted_versions = sorted(versions_configs.keys())
    all_keys = set()
    for cfg in versions_configs.values():
        all_keys.update(cfg.keys())

    out = {}
    for key in sorted(all_keys):
        present = [v for v in sorted_versions if key in versions_configs[v]]
        if not present:
            continue
        introduced = present[0]
        removed = None
        last_present = present[-1]
        for v in sorted_versions:
            if v > last_present:
                removed = v
                break
        # Track default changes
        default_changes = []
        prev = None
        for v in sorted(present):
            cur = versions_configs[v][key]
            if prev is not None and cur != prev:
                default_changes.append({
                    "from_version": None, "to_version": v,
                    "old_default": prev, "new_default": cur,
                })
            prev = cur
        out[key] = {
            "introduced_in": introduced,
            "removed_in": removed,
            "default_latest": versions_configs[last_present][key],
            "default_changes": default_changes,
            "versions_present": present,
        }
    return out


# ── Diff core ──

def diff_core(core_id: str, registry_pairs: set[tuple[str, str]]) -> dict | None:
    core_dir = GENERATED_DIR / core_id
    if not core_dir.exists():
        return None

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
            elif rel_path.endswith(".conf"):
                fmt = "hocon"
            flat = parse_config(content, fmt)
            normalized = rel_path.replace("config/", "")
            by_config[normalized][version] = flat

    if not by_config:
        return None

    result = {
        "core": core_id,
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "configs": {},
    }

    total_new = total_existing = total_removed = total_drifted = 0

    for config_file, versions_configs in by_config.items():
        key_evolution = build_evolution(versions_configs)

        keys_out = []
        for key, info in sorted(key_evolution.items()):
            reg_has = (config_file, key) in registry_pairs
            if not reg_has:
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

        result["configs"][config_file] = {
            "versions_scanned": sorted(versions_configs.keys()),
            "total_keys": len(key_evolution),
            "keys": keys_out,
        }

    result["summary"] = {
        "new_keys": total_new, "existing": total_existing,
        "removed": total_removed, "drifted": total_drifted,
    }
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core")
    parser.add_argument("--all", action="store_true")
    args = parser.parse_args()

    registry_pairs = parse_registry()
    print(f"[DIFF] Registry.cs has {len(registry_pairs)} (file, key) pairs")

    DIFFS_DIR.mkdir(parents=True, exist_ok=True)

    if args.all:
        targets = [d.name for d in GENERATED_DIR.iterdir() if d.is_dir()]
    elif args.core:
        targets = [args.core]
    else:
        print("Need --core or --all")
        sys.exit(1)

    summary = {
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "cores": {}, "totals": {"new": 0, "existing": 0, "removed": 0, "drifted": 0},
    }

    for core_id in sorted(targets):
        print(f"\n[{core_id}] diffing...")
        result = diff_core(core_id, registry_pairs)
        if result is None:
            print(f"  [SKIP] no generated configs")
            continue
        out = DIFFS_DIR / f"{core_id}.diff.json"
        out.write_text(json.dumps(result, ensure_ascii=False, indent=2))
        s = result["summary"]
        summary["cores"][core_id] = s
        summary["totals"]["new"] += s["new_keys"]
        summary["totals"]["existing"] += s["existing"]
        summary["totals"]["removed"] += s["removed"]
        summary["totals"]["drifted"] += s["drifted"]
        print(f"  new={s['new_keys']} existing={s['existing']} removed={s['removed']} drifted={s['drifted']}")

    DIFFS_DIR.mkdir(parents=True, exist_ok=True)
    (DIFFS_DIR / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"\n{'='*60}")
    print(f"TOTAL: new={summary['totals']['new']} existing={summary['totals']['existing']} "
          f"removed={summary['totals']['removed']} drifted={summary['totals']['drifted']}")


if __name__ == "__main__":
    main()
