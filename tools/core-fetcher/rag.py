#!/usr/bin/env python3
"""rag.py — Build RAG knowledge base from project-internal docs/server-cores/*.md.

Usage:
    python rag.py                      # build all
    python rag.py --core paper         # specific core
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent.parent
DOCS_DIR = ROOT / "docs" / "server-cores"
KB_DIR = ROOT / "knowledge-base"


# ── Markdown table parser ──

def parse_md_tables(text: str) -> list[dict]:
    """Extract all markdown tables as list[dict[row_number][col_name]]."""
    tables = []
    lines = text.splitlines()
    i = 0
    while i < len(lines) - 1:
        line = lines[i]
        # Detect table: row with |..| followed by separator row
        if (line.startswith("|") and
            re.match(r"^\|[\s:\-|]+\|", lines[i + 1]) if i + 1 < len(lines) else False):
            header = [c.strip() for c in line.split("|")[1:-1]]
            rows = []
            i += 2
            while i < len(lines) and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].split("|")[1:-1]]
                while len(cells) < len(header):
                    cells.append("")
                rows.append(dict(zip(header, cells)))
                i += 1
            if rows and any(any(v for v in r.values()) for r in rows):
                tables.append({"header": header, "rows": rows})
        else:
            i += 1
    return tables


# ── Config file heading detection ──

def detect_config_files(text: str) -> list[str]:
    """Find config file paths mentioned in ## headings."""
    files = []
    for line in text.splitlines():
        m = re.match(r"^###?\s+(.+)", line)
        if m:
            heading = m.group(1)
            cm = re.search(r"([\w\-./]+\.(yml|yaml|properties|toml|conf|hocon))", heading)
            if cm:
                files.append(cm.group(1))
    return files


# ── Extract config entries ──

def extract_from_md(md_path: Path) -> dict[str, list[dict]]:
    """Parse a server-core .md into {config_file: [RAG entries]}."""
    text = md_path.read_text(errors="replace")
    lines = text.splitlines()

    # Build list of heading lines with config file names
    heading_files: list[tuple[int, str]] = []
    heading_pattern = re.compile(r"^#{1,3}\s+(.+)")
    for i, line in enumerate(lines):
        m = heading_pattern.match(line)
        if m:
            cm = re.search(r"([\w\-./]+\.(yml|yaml|properties|toml|conf|hocon))", line)
            if cm:
                heading_files.append((i, cm.group(1)))

    def nearest_file_above(current_line: int) -> str:
        cand = None
        for line_no, fname in heading_files:
            if line_no <= current_line:
                cand = fname
            else:
                break
        return cand or "unknown"

    entries_by_file: dict[str, list[dict]] = {}

    # Walk line by line, find tables, determine their start line
    i = 0
    while i < len(lines) - 1:
        line = lines[i]
        if line.startswith("|") and re.match(r"^\|[\s:\-|]+\|", lines[i + 1]):
            header = [c.strip() for c in line.split("|")[1:-1]]
            rows = []
            i += 2
            while i < len(lines) and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].split("|")[1:-1]]
                while len(cells) < len(header):
                    cells.append("")
                rows.append(dict(zip(header, cells)))
                i += 1

            lower = [h.lower() for h in header]
            key_idx = term_idx = desc_idx = None
            for j, h in enumerate(lower):
                if h in ("键名", "键", "key", "路径", "path"):
                    key_idx = j
                if h in ("中文含义", "中文", "含义", "显示名"):
                    term_idx = j
                if h in ("说明", "描述", "description", "详情", "备注"):
                    desc_idx = j
            if key_idx is None or term_idx is None:
                continue

            config_file = nearest_file_above(i - len(rows) - 2)
            bucket = entries_by_file.setdefault(config_file, [])
            for row in rows:
                row_vals = list(row.values())
                if key_idx >= len(row_vals) or term_idx >= len(row_vals):
                    continue
                key_path = str(row_vals[key_idx]).replace("`", "").strip()
                term = str(row_vals[term_idx]).replace("`", "").strip()
                desc = ""
                if desc_idx is not None and desc_idx < len(row_vals):
                    desc = str(row_vals[desc_idx]).replace("`", "").strip()
                if not key_path or not term or "│" in key_path or len(key_path) < 3:
                    continue
                bucket.append({
                    "key_path": key_path,
                    "chinese_entries": [{
                        "term": term, "context": desc,
                        "source": str(md_path.relative_to(ROOT)),
                    }],
                })
        else:
            i += 1

    return entries_by_file


# ── Main ──

def core_id_from_filename(filename: str) -> str:
    stem = Path(filename).stem
    m = re.search(r"-(.+)$", stem)
    return m.group(1) if m else stem


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core")
    args = parser.parse_args()

    KB_DIR.mkdir(parents=True, exist_ok=True)

    docs = sorted(DOCS_DIR.glob("*.md"))
    docs = [d for d in docs if d.name != "README.md" and not d.name.startswith("_")]
    print(f"[RAG] scanning {len(docs)} doc files")

    files_written = 0
    entries_total = 0
    for md_path in docs:
        core_id = core_id_from_filename(md_path.name)
        if args.core and args.core != core_id:
            continue

        entries_by_file = extract_from_md(md_path)
        if not entries_by_file:
            print(f"  [{core_id}] no extractable tables (maybe just intro text)")
            continue

        kb_core_dir = KB_DIR / core_id
        kb_core_dir.mkdir(parents=True, exist_ok=True)

        for config_file, entries in entries_by_file.items():
            kb_entry = {
                "config_file": config_file,
                "cores": [core_id],
                "source_docs": [{"source": "project-md", "path": str(md_path.relative_to(ROOT))}],
                "entries": entries,
                "schema_version": 1,
                "indexed_at": __import__("time").strftime("%Y-%m-%dT%H:%M:%SZ", __import__("time").gmtime()),
            }
            safe_name = config_file.replace("/", "__").replace(".", "_").replace(" ", "_")
            out = kb_core_dir / f"{safe_name}.json"
            out.write_text(json.dumps(kb_entry, ensure_ascii=False, indent=2))
            files_written += 1
            entries_total += len(entries)
            print(f"  [{core_id}] {config_file}: {len(entries)} entries")

    print(f"\nDone: {files_written} KB files, {entries_total} entries")


if __name__ == "__main__":
    main()
