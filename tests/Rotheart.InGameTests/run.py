#!/usr/bin/env python3
"""Isolated Rotheart native-game harness setup/launcher; never builds or touches installed profiles.

After the parent builds both projects:
  python tests/Rotheart.InGameTests/run.py setup
  ROTHEART_RUN=disposable-local-only python tests/Rotheart.InGameTests/run.py run --run-dir <printed directory>

Each setup creates a new empty save/XDG tree and a loader containing only Jotunn,
Rotheart and its test harness. No existing character/world/config is copied.
The run command owns only its exact child PID and a single hover input stage.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
LAB = ROOT / ".dev/rotheart-lab"
GAME = Path(os.environ.get("VALHEIM_GAME_DIR", Path.home() / ".local/share/Steam/steamapps/common/Valheim"))
REFERENCE = Path(os.environ.get("VALHEIM_REFERENCE_PROFILE", ROOT / ".dev/release/reference-profile")).resolve()
LOADER = Path(os.environ.get("VALHEIM_LOADER_PROFILE", REFERENCE)).resolve()
TOKEN = "disposable-local-only"


def setup(args):
    sources = {
        "Rotheart": args.plugin.resolve(),
        "Rotheart.InGameTests": args.harness.resolve(),
    }
    for path in [*sources.values(), GAME / "valheim.x86_64", LOADER / "start_game_bepinex.sh",
                 LOADER / "doorstop_libs/libdoorstop_x64.so", REFERENCE / "BepInEx/core/BepInEx.dll",
                 REFERENCE / "BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll"]:
        if not path.is_file():
            raise SystemExit(f"Required local input missing (build is parent's responsibility): {path}")
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d%H%M%S%f")
    run = LAB / "runs" / stamp
    run.mkdir(parents=True, exist_ok=False)
    for child in ("saves", "xdg/config", "xdg/data", "xdg/cache", "evidence", "profile/BepInEx/config"):
        (run / child).mkdir(parents=True)
    profile = run / "profile"
    shutil.copytree(REFERENCE / "BepInEx/core", profile / "BepInEx/core")
    shutil.copytree(REFERENCE / "BepInEx/plugins/ValheimModding-Jotunn", profile / "BepInEx/plugins/ValheimModding-Jotunn")
    shutil.copytree(LOADER / "doorstop_libs", profile / "doorstop_libs")
    shutil.copy2(LOADER / "start_game_bepinex.sh", profile / "start_game_bepinex.sh")
    for name, path in sources.items():
        destination = profile / "BepInEx/plugins" / name
        destination.mkdir()
        shutil.copy2(path, destination / path.name)
    (profile / "BepInEx/config/BepInEx.cfg").write_text(
        "[Chainloader]\nHideManagerGameObject = true\n[Logging.Console]\nEnabled = false\n"
        "[Logging.Disk]\nEnabled = true\nWriteUnityLog = true\nLogLevels = Fatal, Error, Warning, Message, Info\n")
    (run / "manifest.json").write_text(json.dumps({"run_id": stamp, "game": str(GAME),
        "profile": str(profile), "sources": {name: str(path) for name, path in sources.items()},
        "scope": "fresh-local-character-and-world-only"}, indent=2) + "\n")
    print(run)
    return 0


def hover(pid, evidence):
    request = evidence / "input.json"
    ack = evidence / "input.done"
    if not request.exists() or ack.exists():
        return
    stage = json.loads(request.read_text())
    if stage.get("kind") != "hover":
        raise RuntimeError(f"Unknown harness input stage: {stage}")
    result = subprocess.run(["xdotool", "search", "--onlyvisible", "--pid", str(pid)], text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    windows = result.stdout.split()
    if not windows:
        return
    window = windows[-1]
    subprocess.run(["xdotool", "windowactivate", "--sync", window, "mousemove", "--window", window,
                    str(int(stage["x"])), str(int(stage["y"]))], check=True, timeout=10)
    ack.write_text("actual X11 pointer positioned over native inventory element\n")


def launch(args):
    if os.environ.get("ROTHEART_RUN") != TOKEN:
        raise SystemExit(f"Explicit authorization required: ROTHEART_RUN={TOKEN}")
    run = args.run_dir.resolve()
    if run.parent != LAB / "runs" or not (run / "manifest.json").is_file():
        raise SystemExit("Refusing a run outside this lab's fresh setup directory")
    if shutil.which("xdotool") is None or not os.environ.get("DISPLAY"):
        raise SystemExit("X11 DISPLAY and xdotool are required for the native inventory tooltip hover")
    if subprocess.run(["pgrep", "-x", "valheim.x86_64"], stdout=subprocess.DEVNULL).returncode == 0:
        raise SystemExit("Close the running Valheim instance before launching this isolated lab")
    evidence = run / "evidence"
    if any(evidence.iterdir()) or any((run / "saves").iterdir()):
        raise SystemExit("This run directory was used already; call setup for another disposable run")
    profile = run / "profile"
    env = os.environ.copy()
    env.update(ROTHEART_RUN=TOKEN, ROTHEART_RUN_ID=run.name, ROTHEART_EVIDENCE=str(evidence),
               ROTHEART_SAVES=str(run / "saves"), XDG_CONFIG_HOME=str(run / "xdg/config"),
               XDG_DATA_HOME=str(run / "xdg/data"), XDG_CACHE_HOME=str(run / "xdg/cache"),
               SteamAppId="892970", SteamGameId="892970")
    command = ["bash", str(profile / "start_game_bepinex.sh"), str(GAME / "valheim.x86_64"),
               "-savedir", str(run / "saves"), "-screen-fullscreen", "0", "-screen-width", "1280",
               "-screen-height", "720", "-console", "-logFile", str(evidence / "Player.log")]
    (evidence / "launch.json").write_text(json.dumps({"command": command, "run_id": run.name}, indent=2) + "\n")
    with (evidence / "launcher.log").open("w") as log:
        process = subprocess.Popen(command, cwd=profile, env=env, stdout=log, stderr=subprocess.STDOUT)
        (evidence / "pid.txt").write_text(str(process.pid) + "\n")
        deadline = time.monotonic() + args.timeout
        try:
            while process.poll() is None:
                hover(process.pid, evidence)
                if time.monotonic() >= deadline:
                    raise TimeoutError("Harness deadline exceeded; inspect runtime.log/Player.log and any startup dialog")
                time.sleep(.2)
        except BaseException:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
            raise
    loader_log = profile / "BepInEx/LogOutput.log"
    if loader_log.exists():
        shutil.copy2(loader_log, evidence / "BepInEx.log")
    complete = evidence / "complete.txt"
    if not complete.exists():
        print(f"FAIL: game exited {process.returncode} without completion marker; evidence: {evidence}", file=sys.stderr)
        return 1
    result = complete.read_text().strip()
    print(f"{result}; evidence: {evidence}")
    return 0 if result.startswith("PASS ") and process.returncode == 0 else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("setup", help="copy only local loader/dependencies/built Rotheart into a fresh lab")
    create.add_argument("--plugin", type=Path, default=ROOT / "mods/Rotheart/bin/Release/netstandard2.1/Rotheart.dll")
    create.add_argument("--harness", type=Path, default=ROOT / "tests/Rotheart.InGameTests/bin/Release/netstandard2.1/Rotheart.InGameTests.dll")
    create.set_defaults(handler=setup)
    run = commands.add_parser("run", help="explicitly gated launch plus native hover stage; no build")
    run.add_argument("--run-dir", type=Path, required=True)
    run.add_argument("--timeout", type=float, default=600)
    run.set_defaults(handler=launch)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    sys.exit(main())
