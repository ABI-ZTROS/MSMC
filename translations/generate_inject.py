#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
翻译合并 + inject.cs 生成脚本。
用法：
    python generate_inject.py        # 生成两个 inject.cs + 合并 lookup
    python generate_inject.py --dry # 只检查 JSON，不写文件
"""

import json, os, re, sys, argparse

WORKSPACE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ---------- ValueType 推断 ----------
BOOL_VALUES = {"true", "false", "True", "False", "TRUE", "FALSE"}
LIST_PREFIX = ("[",)

def infer_value_type(default: str, key_path: str) -> str:
    """根据 default 值推断 C# 侧的 ValueType"""
    if default is None:
        return "string"
    d = str(default).strip()
    # bool
    if d in BOOL_VALUES:
        return "bool"
    # list
    if d.startswith("["):
        return "list"
    # enum — 已知枚举值
    if re.fullmatch(r"[A-Z_][A-Z0-9_/\-]*", d):
        return "enum"
    # double
    if re.fullmatch(r"-?\d+\.\d+", d):
        return "double"
    # int
    if re.fullmatch(r"-?\d+", d):
        return "int"
    # duration 如 3600s, 15s, 30s — 视作 string（也可以单独类型，但 Leaves 用 int）
    if re.fullmatch(r"\d+[smhd]", d):
        return "string"
    # URL 或普通字符串
    return "string"


def classify_category(key_path: str, config_file: str) -> str:
    """根据 key 路径推断分类"""
    parts = key_path.split(".")
    if config_file == "glowstone.yml":
        # glowstone.yml 路径首段就是分类
        if parts[0] in {"advanced", "console", "creatures", "extras", "files", "folders", "game", "libraries", "server", "world"}:
            return parts[0]
        return "其他"
    if config_file == "worlds.yml":
        if parts[0] in {"overworld", "nether", "end", "general"}:
            return f"地形-{parts[0]}"
        return "地形"
    if config_file == "commands.yml":
        return "命令别名"
    if config_file == "akarin.yml":
        if parts[0] in {"alternative", "core", "messages", "bootstrap"}:
            return parts[0]
        return "其他"
    if config_file == "spigot.yml":
        if parts[0] in {"advancements", "commands", "messages", "settings", "stats", "world-settings"}:
            return parts[0]
        return "其他"
    return "其他"


# ---------- 读取数据 ----------
def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


# ---------- 生成 inject.cs ----------
def escape_cs_string(s: str) -> str:
    """把字符串转成 C# verbatim string 需要的形式"""
    # 简单转义：双引号变成 ""（C# verbatim string）
    return s.replace("\\", "\\").replace('"', '""')


def generate_inject(core_name: str, diff_path: str, lookup_path: str, output_path: str):
    diff = load_json(diff_path)
    lookup = load_json(lookup_path)

    files = diff["configs"]
    total_new = sum(
        1 for cfg in files.values()
        for k in cfg["keys"]
        if k["state"] == "new_key"
    )

    # 分类收集
    sections = []  # [(section_title, [(cfg_name, key_entry), ...])]
    for cfg_name, cfg_data in files.items():
        group = [(cfg_name, k) for k in cfg_data["keys"] if k["state"] == "new_key"]
        if group:
            sections.append((cfg_name, group))

    # 推断 ConfigFileName
    def config_file_path(cfg_name: str, core: str) -> str:
        # glowstone 的文件有特殊路径
        if core == "glowstone":
            base = cfg_name.replace(".yml", "")
            return f"config/glowstone/{cfg_name}"
        if core == "akarin":
            # akarin.yml 直接放根
            return cfg_name
        return cfg_name

    lines = []
    lines.append("// " + "=" * 60)
    lines.append(f"// INJECTABLE TRANSLATIONS — {core_name}")
    lines.append(f"// Coverage: {total_new}/{total_new}")
    lines.append("// " + "=" * 60)
    lines.append("")

    for cfg_name, entries in sections:
        section_title = f"// ── {cfg_name} ({len(entries)} new keys) ──"
        lines.append(section_title)

        for _, e in entries:
            key_path = e["path"]
            default = e["default_latest"]
            cfg_file = config_file_path(cfg_name, core_name)
            entry = lookup.get(key_path)

            if entry is None:
                # 缺失翻译 — TODO 格式
                dn = f"TODO: {key_path.split('.')[-1]}"
                desc = "需人工确认"
                tag = "  // [MISSING]"
            else:
                dn = entry["DisplayName"]
                desc = entry["Description"]
                tag = "  // [TRANSLATED]"

            vt = infer_value_type(default, key_path)
            cat = classify_category(key_path, cfg_name)

            # 处理描述中的 \n — C# 里用 verbatim string
            desc_cs = escape_cs_string(desc)
            dn_cs = escape_cs_string(dn)

            # 默认值处理
            default_clean = default if default != "" else '""'
            # 若是 list 或 string 含特殊字符，保持原样
            if default_clean.startswith("["):
                default_cs = default_clean
            elif default_clean in BOOL_VALUES:
                default_cs = default_clean.lower()
            elif re.fullmatch(r"-?\d+\.\d+", default_clean):
                default_cs = default_clean
            elif re.fullmatch(r"-?\d+", default_clean):
                default_cs = default_clean
            else:
                default_cs = f'"{escape_cs_string(default_clean)}"'

            line = (
                f'Register(new ServerConfigDescriptor {{ '
                f'ConfigFileName = "{cfg_file}", '
                f'Key = "{key_path}", '
                f'DisplayName = "{dn_cs}", '
                f'Description = "{desc_cs}", '
                f'Category = "{cat}", '
                f'ValueType = "{vt}", '
                f'DefaultValue = {default_cs}, '
                f'RequiresRestart = false }});{tag}'
            )
            lines.append(line)

        lines.append("")

    content = "\n".join(lines)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(content)
    return total_new


# ---------- 合并 lookup ----------
def merge_lookups(out_path: str):
    base_dir = WORKSPACE
    merged = {}

    for fname in ["leaves.lookup.json", "akarin.lookup.json", "glowstone.lookup.json"]:
        p = os.path.join(base_dir, "translations", fname)
        if os.path.exists(p):
            data = load_json(p)
            merged.update(data)
            print(f"  ✓ {fname}: {len(data)} entries")
        else:
            print(f"  ✗ {fname}: NOT FOUND")

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)
    print(f"\n  Merged total: {len(merged)} → {out_path}")
    return merged


# ---------- 验证 ----------
def verify(diff_path: str, lookup_path: str):
    diff = load_json(diff_path)
    lookup = load_json(lookup_path)

    missing = []
    found = []
    for cfg_name, cfg_data in diff["configs"].items():
        for k in cfg_data["keys"]:
            if k["state"] != "new_key":
                continue
            key_path = k["path"]
            if key_path in lookup:
                found.append(key_path)
            else:
                missing.append(key_path)

    total = len(found) + len(missing)
    print(f"  Coverage: {len(found)}/{total}")
    if missing:
        print(f"  ⚠ MISSING ({len(missing)}):")
        for m in missing[:10]:
            print(f"    - {m}")
        if len(missing) > 10:
            print(f"    ... and {len(missing) - 10} more")
    else:
        print(f"  ✓ All keys covered")
    return len(missing) == 0


# ---------- main ----------
def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry", action="store_true", help="只检查不写文件")
    args = parser.parse_args()

    diff_dir = os.path.join(WORKSPACE, "diffs")
    trans_dir = os.path.join(WORKSPACE, "translations")

    print("=" * 50)
    print("Akarin — 验证 & 生成")
    print("=" * 50)
    akarin_ok = verify(
        os.path.join(diff_dir, "akarin.diff.json"),
        os.path.join(trans_dir, "akarin.lookup.json"),
    )
    if not args.dry:
        n = generate_inject(
            "akarin",
            os.path.join(diff_dir, "akarin.diff.json"),
            os.path.join(trans_dir, "akarin.lookup.json"),
            os.path.join(diff_dir, "akarin.inject.cs"),
        )
        print(f"  → diffs/akarin.inject.cs written ({n} entries)")

    print()
    print("=" * 50)
    print("Glowstone — 验证 & 生成")
    print("=" * 50)
    glow_ok = verify(
        os.path.join(diff_dir, "glowstone.diff.json"),
        os.path.join(trans_dir, "glowstone.lookup.json"),
    )
    if not args.dry:
        n = generate_inject(
            "glowstone",
            os.path.join(diff_dir, "glowstone.diff.json"),
            os.path.join(trans_dir, "glowstone.lookup.json"),
            os.path.join(diff_dir, "glowstone.inject.cs"),
        )
        print(f"  → diffs/glowstone.inject.cs written ({n} entries)")

    print()
    print("=" * 50)
    print("合并所有 lookup → translations/all.lookup.json")
    print("=" * 50)
    if not args.dry:
        merge_lookups(os.path.join(trans_dir, "all.lookup.json"))

    # 验证 JSON 可解析
    for f in ["leaves.lookup.json", "akarin.lookup.json", "glowstone.lookup.json"]:
        p = os.path.join(trans_dir, f)
        try:
            with open(p, "r", encoding="utf-8") as fh:
                json.load(fh)
            print(f"  ✓ {f} — valid JSON")
        except Exception as e:
            print(f"  ✗ {f} — INVALID: {e}")

    if akarin_ok and glow_ok:
        print("\n🎉 All translations complete!")
        return 0
    else:
        print("\n⚠ Some keys need manual translation")
        return 1


if __name__ == "__main__":
    sys.exit(main())
