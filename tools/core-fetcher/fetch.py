#!/usr/bin/env python3
"""fetch.py — Download Minecraft server core JARs from various sources.

Usage:
    python fetch.py --core paper                # all known versions of paper
    python fetch.py --core paper --latest-only  # just the latest
    python fetch.py --cores paper,purpur,leaf
    python fetch.py --all                       # all cores in registry
    python fetch.py --rebuild                   # ignore cache, re-download
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
from pathlib import Path
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


def _gh_get(url: str, params: dict | None = None) -> dict | list | None:
    r = requests.get(url, headers=_gh_headers(), params=params, timeout=30)
    if r.status_code == 403 and "rate" in r.text.lower():
        print(f"  [WARN] GitHub rate limit hit (403). Skipping this API call.")
        return None
    r.raise_for_status()
    return r.json()


# ──────────────────────────── Version discovery strategies ────────────────────────────

def discover_hangar(core: dict, latest_only: bool) -> list[str]:
    owner = core["download"]["owner"]
    project = core["download"]["project"]
    url = f"https://hangar.papermc.io/api/v2/projects/{owner}/{quote(project)}/versions"
    r = requests.get(url, params={"offset": 0, "limit": 500}, timeout=30)
    r.raise_for_status()
    versions = [v["version"] for v in r.json()]
    return versions[:1] if latest_only else versions


def discover_github_releases(core: dict, latest_only: bool) -> list[str]:
    gh_repo = core["github"]
    url = f"{GH_API}/repos/{gh_repo}/releases"
    data = _gh_get(url, params={"per_page": 100})
    if data is None:
        return []
    versions = []
    for r in data:
        if r.get("draft") or not r.get("tag_name"):
            continue
        versions.append(r["tag_name"])
    return versions[:1] if latest_only else versions


def discover_forge_installer(core: dict, latest_only: bool) -> list[str]:
    """Forge uses Maven — enumerate Minecraft versions, look up forge POMs."""
    out = []
    for mc_ver in core["download"].get("mc-versions", []):
        url = f"https://maven.minecraftforge.net/net/minecraftforge/forge/{mc_ver}/forge-{mc_ver}.pom"
        try:
            r = requests.get(url, timeout=15)
            if r.ok:
                out.append(mc_ver)
        except Exception:
            pass
    if latest_only and out:
        out = [out[-1]]
    return out


def discover_fabric_installer(core: dict, latest_only: bool) -> list[str]:
    try:
        r = requests.get("https://meta.fabricmc.net/v2/versions/installer", timeout=15)
        r.raise_for_status()
        data = r.json()
        if latest_only and data:
            return [data[0]["version"]]
        return [item["version"] for item in data]
    except Exception:
        return []


def discover_circleci(core: dict, latest_only: bool) -> list[str]:
    # CircleCI needs separate auth — skip, mark
    print(f"  [SKIP] circleci-workflow: CircleCI token required, implement later")
    return []


def discover_maven_central(core: dict, latest_only: bool) -> list[str]:
    group = core["download"]["group"].replace(".", "/")
    artifact = core["download"]["artifact"]
    url = f"https://repo1.maven.org/maven2/{group}/{artifact}/maven-metadata.xml"
    try:
        r = requests.get(url, timeout=15)
        r.raise_for_status()
        versions = re.findall(r"<version>([^<]+)</version>", r.text)
        if latest_only and versions:
            return [versions[-1]]
        return versions
    except Exception as e:
        print(f"  [FAIL] maven-central discovery: {e}")
        return []


def discover_hangar_direct(core: dict, latest_only: bool) -> list[str]:
    """Use GitHub tags for version enumeration + Hangar direct download URL."""
    gh_repo = core["github"]
    tags = _gh_get(f"{GH_API}/repos/{gh_repo}/tags", params={"per_page": 100})
    if tags is None:
        return []
    versions = [t["name"] for t in tags]
    # Filter out non-version tags like "downloads"
    versions = [v for v in versions if re.match(r"^[\d]", v)]
    # Hangar's direct download page lists all versions too — but GitHub tags are reliable
    return versions[:1] if latest_only else versions


def discover_versions(core: dict, latest_only: bool) -> list[str]:
    kind = core["download"]["kind"]
    dispatch = {
        "hangar": discover_hangar,
        "hangar-direct": discover_hangar_direct,
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
    try:
        return fn(core, latest_only)
    except Exception as e:
        print(f"  [FAIL] version discovery: {e}")
        return []


# ──────────────────────────── URL resolution ────────────────────────────

def resolve_download_url(core: dict, version: str) -> str | None:
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
            print(f"  [WARN] Hangar url fail for {version}: {e}")
            return None

    if kind == "github-releases":
        gh_repo = core["github"]
        data = _gh_get(f"{GH_API}/repos/{gh_repo}/releases", params={"per_page": 100})
        if data is None:
            return None
        for rel in data:
            if rel["tag_name"] == version:
                regex = core["download"].get("asset-regex", r"\.jar$")
                for asset in rel.get("assets", []):
                    if re.search(regex, asset["name"]):
                        return asset["browser_download_url"]
                if rel.get("assets"):
                    return rel["assets"][0]["browser_download_url"]
                return None
        return None

    if kind == "forge-installer":
        mc_ver = version
        return (f"https://maven.minecraftforge.net/net/minecraftforge/forge/"
                f"{mc_ver}/forge-{mc_ver}-installer.jar")

    if kind == "fabric-installer":
        try:
            r = requests.get("https://meta.fabricmc.net/v2/versions/installer", timeout=15)
            r.raise_for_status()
            for item in r.json():
                if item["version"] == version:
                    return item.get("url")
        except Exception:
            pass
        return None

    if kind == "hangar-direct":
        owner = core["download"]["owner"]
        slug = core["download"]["project"]
        artifact_slug = core["download"].get("artifact-slug", slug.lower())
        return f"https://hangar.papermc.io/{owner}/{slug}/versions/{quote(version)}/downloads/{artifact_slug}-{quote(version)}.jar"

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
            with requests.get(url, stream=True, timeout=120,
                              headers={"User-Agent": "MSMC-core-fetcher"}) as r:
                r.raise_for_status()
                tmp = dest.with_suffix(dest.suffix + ".part")
                with open(tmp, "wb") as f:
                    for chunk in r.iter_content(chunk_size=8192):
                        f.write(chunk)
                tmp.rename(dest)
            return True
        except Exception as e:
            if attempt == retries:
                print(f"  [FAIL] download: {e}")
                return False
            time.sleep(1)
    return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--core")
    parser.add_argument("--cores")
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--latest-only", action="store_true")
    parser.add_argument("--rebuild", action="store_true")
    args = parser.parse_args()

    reg = load_registry()
    all_cores = reg["cores"]

    if args.core:
        target_ids = [args.core]
    elif args.cores:
        target_ids = [c.strip() for c in args.cores.split(",")]
    elif args.all:
        target_ids = list(all_cores.keys())
    else:
        print("Need --core, --cores, or --all")
        sys.exit(1)

    CACHE_DIR.mkdir(parents=True, exist_ok=True)

    ok, fail, skip = 0, 0, 0
    for core_id in target_ids:
        if core_id not in all_cores:
            print(f"[SKIP] unknown core: {core_id}")
            skip += 1
            continue
        core = all_cores[core_id]
        print(f"\n{'─'*50}")
        print(f"[{core_id}] discovering versions ({core['download']['kind']})...")
        versions = discover_versions(core, latest_only=args.latest_only)
        if not versions:
            print(f"  [SKIP] no versions found")
            skip += 1
            continue
        print(f"  found {len(versions)} version(s): {versions[:5]}{'...' if len(versions) > 5 else ''}")

        for v in versions:
            jar = cache_path(core_id, v)
            meta = meta_path(core_id, v)
            if jar.exists() and not args.rebuild:
                print(f"  [CACHE] {jar.name}")
                ok += 1
                continue
            url = resolve_download_url(core, v)
            if url is None:
                print(f"  [SKIP] {v}: could not resolve URL")
                skip += 1
                continue
            print(f"  [GET]  {v}  ({url[:80]}...)")
            if download(url, jar):
                meta.write_text(json.dumps({
                    "core": core_id, "version": v, "url": url,
                    "downloaded_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                    "size_bytes": jar.stat().st_size,
                }, indent=2))
                print(f"  [OK]   {jar.name} ({jar.stat().st_size} bytes)")
                ok += 1
            else:
                fail += 1

    print(f"\n{'='*50}")
    print(f"Summary: {ok} OK, {fail} failed, {skip} skipped")


if __name__ == "__main__":
    main()
