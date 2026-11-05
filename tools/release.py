#!/usr/bin/env python3
"""Check and build local Thunderstore candidates. This tool never uploads."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import stat
import subprocess
import sys
import tomllib
from urllib.parse import urlparse
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = ("PistolMod", "StartupHelper", "ServerMetrics", "MobThreat")
VERSION = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\Z")
IDENTIFIER = re.compile(r"[A-Za-z0-9_]{1,128}\Z")


class ReleaseError(Exception):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ReleaseError(message)


def read_text(path: Path) -> str:
    data = path.read_bytes()
    require(not data.startswith(b"\xef\xbb\xbf"), f"{path}: save as UTF-8 without BOM")
    return data.decode("utf-8")


def plugin_version(path: Path) -> str:
    """Accept the repository's literal or const-string BepInPlugin version forms."""
    source = read_text(path)
    attribute = re.search(r"\[BepInPlugin\(([^)]*)\)\]", source)
    require(attribute is not None, f"{path}: BepInPlugin attribute not found")
    expression = attribute.group(1).rsplit(",", 1)[-1].strip()
    if expression.startswith('"') and expression.endswith('"'):
        return json.loads(expression)
    constant = re.search(r"\bconst\s+string\s+" + re.escape(expression) + r'\s*=\s*"([^"]+)"', source)
    require(constant is not None, f"{path}: unsupported plugin version expression {expression!r}")
    return constant.group(1)


def copy_map(project: str) -> dict[str, str]:
    mapping = {
        f"bin/Release/netstandard2.1/{project}.dll": f"plugins/{project}/{project}.dll",
        "package/CHANGELOG.md": "CHANGELOG.md",
        "../../LICENSE": "LICENSE",
    }
    if project == "PistolMod":
        mapping["Assets/Audio/ATTRIBUTION.md"] = "ATTRIBUTION.md"
    return mapping


def load_package(project: str) -> dict:
    folder = ROOT / "mods" / project
    config = tomllib.loads(read_text(folder / "thunderstore.toml"))
    require(config.get("config", {}).get("schemaVersion") == "0.0.1", f"{project}: unsupported tcli schema")
    package = config["package"]
    for key in ("namespace", "name"):
        require(isinstance(package.get(key), str) and IDENTIFIER.fullmatch(package[key]), f"{project}: invalid {key}")
    version = package.get("versionNumber", "")
    require(isinstance(version, str) and VERSION.fullmatch(version), f"{project}: version must be major.minor.patch")
    description = package.get("description", "")
    require(isinstance(description, str) and 0 < len(description) <= 250, f"{project}: description must be 1–250 characters")
    website = urlparse(package.get("websiteUrl", ""))
    require(website.scheme in ("http", "https") and bool(website.netloc), f"{project}: websiteUrl must be an HTTP(S) URL")
    require(package.get("containsNsfwContent") is False, f"{project}: review the declared NSFW flag")
    dependencies = package.get("dependencies", {})
    require(isinstance(dependencies, dict) and dependencies, f"{project}: loader dependency is missing")
    require("denikson-BepInExPack_Valheim" in dependencies, f"{project}: Valheim loader dependency is required")
    if project == "PistolMod":
        require("ValheimModding-Jotunn" in dependencies, "PistolMod: Jotunn dependency is required")
    for name, value in dependencies.items():
        parts = name.split("-")
        require(len(parts) == 2 and all(IDENTIFIER.fullmatch(p) for p in parts), f"{project}: invalid dependency {name}")
        require(isinstance(value, str) and VERSION.fullmatch(value), f"{project}: invalid dependency version {name}")
    project_version = ET.parse(folder / f"{project}.csproj").getroot().findtext(".//Version")
    require(project_version == version == plugin_version(folder / "Plugin.cs"),
            f"{project}: csproj, BepInPlugin and thunderstore.toml versions must match")
    build = config["build"]
    for key, expected in {"icon": "package/icon.png", "readme": "package/README.md", "outdir": "../../dist"}.items():
        require(build.get(key) == expected, f"{project}: build.{key} must be {expected!r}")
    copies = build.get("copy", [])
    actual = {entry["source"]: entry["target"] for entry in copies}
    require(len(actual) == len(copies) and actual == copy_map(project),
            f"{project}: build.copy must match the explicit plugin/docs/license allowlist")
    for source in ("package/README.md", "package/CHANGELOG.md", "../../LICENSE"):
        require(bool(read_text(folder / source).strip()), f"{project}: empty {source}")
    if project == "PistolMod":
        read_text(folder / "Assets/Audio/ATTRIBUTION.md")
    with Image.open(folder / build["icon"]) as icon:
        require(icon.format == "PNG" and icon.size == (256, 256), f"{project}: icon must be a 256×256 PNG")
        icon.verify()
    publish = config["publish"]
    require(publish.get("repository") == "https://thunderstore.io", f"{project}: unexpected publish repository")
    require(publish.get("communities") == ["valheim"], f"{project}: expected only the Valheim community")
    categories = publish.get("categories", {}).get("valheim", [])
    require("mods" in categories and "ai-generated" in categories, f"{project}: mods and ai-generated categories required")
    return config


def manifest_for(config: dict) -> dict:
    package = config["package"]
    return {
        "namespace": package["namespace"],
        "FullName": f"{package['namespace']}-{package['name']}",
        "name": package["name"],
        "version_number": package["versionNumber"],
        "website_url": package["websiteUrl"],
        "description": package["description"],
        "dependencies": [f"{name}-{version}" for name, version in package["dependencies"].items()],
    }


def archive_path(config: dict) -> Path:
    package = config["package"]
    return ROOT / "dist" / f"{package['namespace']}-{package['name']}-{package['versionNumber']}.zip"


def verify_archive(path: Path, project: str, config: dict, *, compare_dll: bool = False) -> None:
    folder = ROOT / "mods" / project
    sources = {target: folder / source for source, target in copy_map(project).items()}
    sources.update({"README.md": folder / "package/README.md", "icon.png": folder / "package/icon.png"})
    expected = set(sources) | {"manifest.json"}
    with zipfile.ZipFile(path) as archive:
        members = archive.namelist()
        require(len(members) == len(set(members)), f"{path}: duplicate ZIP members")
        require(set(members) == expected, f"{path}: unexpected/missing files; extra={sorted(set(members)-expected)}, missing={sorted(expected-set(members))}")
        for entry in archive.infolist():
            require(not stat.S_ISLNK(entry.external_attr >> 16), f"{path}: symlink payload {entry.filename}")
        info = archive.getinfo("manifest.json")
        require(info.file_size < 65536, f"{path}: oversized manifest")
        manifest = json.loads(archive.read("manifest.json").decode("utf-8-sig"))
        wanted = manifest_for(config)
        require(set(manifest) == set(wanted), f"{path}: unexpected manifest fields")
        for key, value in wanted.items():
            actual = manifest[key]
            if key == "dependencies":
                require(isinstance(actual, list) and len(actual) == len(value) and sorted(actual) == sorted(value), f"{path}: dependency mismatch")
            else:
                require(actual == value, f"{path}: manifest {key} differs from current package config")
        for target, source in sources.items():
            if target.endswith(".dll") and not compare_dll:
                require(archive.getinfo(target).file_size > 0, f"{path}: empty plugin DLL")
                continue
            with archive.open(target) as packed, source.open("rb") as original:
                require(hashlib.file_digest(packed, "sha256").digest() == hashlib.file_digest(original, "sha256").digest(),
                        f"{path}: {target} differs from its current release input")
    print(f"VERIFIED {path.relative_to(ROOT) if path.is_relative_to(ROOT) else path}")


def normalize_archive(path: Path) -> None:
    """Stable ZIP metadata/order; identical build inputs produce identical archives."""
    temporary = path.with_suffix(".zip.tmp")
    try:
        with zipfile.ZipFile(path) as source, zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as target:
            for name in sorted(source.namelist()):
                entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                entry.create_system = 3
                entry.external_attr = (stat.S_IFREG | 0o644) << 16
                entry.compress_type = zipfile.ZIP_DEFLATED
                target.writestr(entry, source.read(name), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def run(command: list[str]) -> None:
    print("+ " + shlex.join(command), flush=True)
    subprocess.run(command, cwd=ROOT, check=True)


def build(project: str, config: dict, args: argparse.Namespace) -> None:
    command = ["dotnet", "build", str(ROOT / "mods" / project / f"{project}.csproj"), "-c", "Release", "--verbosity", "minimal"]
    for option, prop in ((args.game_dir, "GameDir"), (args.reference_profile, "ReferenceProfile")):
        if option:
            command.append(f"-p:{prop}={Path(option).expanduser().resolve()}")
    if args.managed_dir and project == "ServerMetrics":
        command.append(f"-p:ManagedDir={Path(args.managed_dir).expanduser().resolve()}")
    run(command)
    run(["dotnet", "tcli", "build", "--config-path", str(ROOT / "mods" / project / "thunderstore.toml")])
    path = archive_path(config)
    verify_archive(path, project, config, compare_dll=True)
    normalize_archive(path)
    verify_archive(path, project, config, compare_dll=True)
    print(f"SHA256 {hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("check", "build"):
        command = commands.add_parser(name)
        command.add_argument("project", choices=(*PROJECTS, "all"), nargs="?", default="all")
        if name == "build":
            command.add_argument("--game-dir", default=os.environ.get("VALHEIM_GAME_DIR"))
            command.add_argument("--reference-profile", default=os.environ.get("VALHEIM_REFERENCE_PROFILE"))
            command.add_argument("--managed-dir", help="Optional server Managed directory; applies only to ServerMetrics")
    command = commands.add_parser("verify")
    command.add_argument("archive", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "verify":
            path = args.archive.expanduser().resolve()
            candidates = [(project, load_package(project)) for project in PROJECTS]
            matches = [(project, config) for project, config in candidates if archive_path(config).name == path.name]
            require(len(matches) == 1, "Archive filename must match a current namespace/package/version in this repository")
            verify_archive(path, *matches[0])
        else:
            projects = PROJECTS if args.project == "all" else (args.project,)
            if args.command == "build" and args.managed_dir:
                require("ServerMetrics" in projects, "--managed-dir is only supported for ServerMetrics")
            packages = [(project, load_package(project)) for project in projects]
            identities = [(c["package"]["namespace"], c["package"]["name"]) for _, c in packages]
            require(len(identities) == len(set(identities)), "Duplicate Thunderstore package identities")
            for project, config in packages:
                if args.command == "build":
                    build(project, config, args)
                else:
                    print(f"CHECKED {project}: {archive_path(config).name}")
        return 0
    except (ReleaseError, OSError, ValueError, KeyError, TypeError, ET.ParseError, zipfile.BadZipFile, subprocess.CalledProcessError) as exc:
        print(f"release: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
