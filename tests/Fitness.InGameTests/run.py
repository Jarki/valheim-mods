#!/usr/bin/env python3
"""Set up and run Fitness's disposable native Valheim verification profile.

The parent builds Fitness.dll and Fitness.InGameTests.dll first. Then:
  python tests/Fitness.InGameTests/run.py setup
  FITNESS_RUN=disposable-local-only python tests/Fitness.InGameTests/run.py run --run-dir <printed path>

No game installation, existing profile, character or world is modified or copied.
Setup reads only BepInEx core/Jotunn and native Doorstop files from local profiles.
Override their locations with VALHEIM_REFERENCE_PROFILE and VALHEIM_LOADER_PROFILE.
Run requires an existing normal Steam desktop client and existing X11 session; it
never starts Steam, a display, a server, or an authentication/API bypass.

Use run --record-video for the native-resource demo: runtime stage markers delimit
a silent, child-window-only training.mkv (at most 60 seconds) and recorder.log.
"""
import argparse
import datetime
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import socket
import sys
import time
import uuid
from showcase_capture import BIOMES, ShowcaseCapture

ROOT = Path(__file__).resolve().parents[2]
LAB = ROOT / ".dev/fitness-lab"
TOKEN = "disposable-local-only"
APP_ID = "892970"


def require_file(path):
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"Required nonempty local input missing (builds belong to parent): {path}")
    return path


def loader_profile(reference):
    explicit = os.environ.get("VALHEIM_LOADER_PROFILE")
    if explicit:
        candidates = [Path(explicit).expanduser().resolve()]
    else:
        prior = ROOT / ".dev/rotheart-lab/runs"
        candidates = [reference]
        if prior.is_dir():
            candidates.extend(path.parent for path in sorted(prior.glob("*/profile/start_game_bepinex.sh"), reverse=True))
        candidates.extend(path.parent for path in sorted((ROOT / ".dev").glob("*/profile/start_game_bepinex.sh")))
    for candidate in candidates:
        script = candidate / "start_game_bepinex.sh"
        library = candidate / "doorstop_libs/libdoorstop_x64.so"
        if script.is_file() and library.is_file():
            require_file(library)
            # The launcher must exec, not fork, so its child PID becomes the game PID.
            if 'exec "$executable_path" "$@"' not in script.read_text():
                raise RuntimeError(f"Doorstop script does not preserve exact game PID ownership: {script}")
            return candidate
    raise RuntimeError("No native Doorstop launcher/library found. Set VALHEIM_LOADER_PROFILE to an existing "
                       "local lab profile containing start_game_bepinex.sh and doorstop_libs/libdoorstop_x64.so. "
                       "The reference profile may contain managed references only; do not install into the game folder.")


def setup(args):
    game = Path(os.environ.get("VALHEIM_GAME_DIR", Path.home() / ".local/share/Steam/steamapps/common/Valheim")).expanduser().resolve()
    reference = Path(os.environ.get("VALHEIM_REFERENCE_PROFILE", ROOT / ".dev/release/reference-profile")).expanduser().resolve()
    loader = loader_profile(reference)
    sources = {"Fitness": args.plugin.expanduser().resolve(), "Fitness.InGameTests": args.harness.expanduser().resolve()}
    for path in [*sources.values(), game / "valheim.x86_64", reference / "BepInEx/core/BepInEx.dll",
                 reference / "BepInEx/core/BepInEx.Preloader.dll",
                 reference / "BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll"]:
        require_file(path)
    if not os.access(game / "valheim.x86_64", os.X_OK):
        raise RuntimeError(f"Game is not executable: {game / 'valheim.x86_64'}")
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d%H%M%S%f") + "-" + uuid.uuid4().hex[:8]
    run = LAB / "runs" / stamp
    run.mkdir(parents=True, exist_ok=False)
    for child in ("saves", "xdg/config", "xdg/data", "xdg/cache", "evidence", "profile/BepInEx/config"):
        (run / child).mkdir(parents=True)
    profile = run / "profile"
    shutil.copytree(reference / "BepInEx/core", profile / "BepInEx/core")
    shutil.copytree(reference / "BepInEx/plugins/ValheimModding-Jotunn", profile / "BepInEx/plugins/ValheimModding-Jotunn")
    (profile / "doorstop_libs").mkdir()
    shutil.copy2(loader / "doorstop_libs/libdoorstop_x64.so", profile / "doorstop_libs/libdoorstop_x64.so")
    shutil.copy2(loader / "start_game_bepinex.sh", profile / "start_game_bepinex.sh")
    for name, source in sources.items():
        destination = profile / "BepInEx/plugins" / name
        destination.mkdir()
        shutil.copy2(source, destination / f"{name}.dll")
    (profile / "BepInEx/config/BepInEx.cfg").write_text(
        "[Chainloader]\nHideManagerGameObject = true\n[Logging.Console]\nEnabled = false\n"
        "[Logging.Disk]\nEnabled = true\nWriteUnityLog = true\nLogLevels = Fatal, Error, Warning, Message, Info\n")
    (run / "manifest.json").write_text(json.dumps({
        "run_id": stamp, "game": str(game), "profile": str(profile), "loader_source": str(loader),
        "reference_source": str(reference), "sources": {name: str(path) for name, path in sources.items()},
        "scope": "fresh-local-character-and-world-only", "network_mode": "singleplayer",
    }, indent=2) + "\n")
    print(f"Native loader: {loader}", file=sys.stderr)
    print(f"Managed references: {reference}", file=sys.stderr)
    print(run)
    return 0


def processes(all_users=False):
    """Read process identities only; never expose command lines or desktop secrets."""
    for directory in Path("/proc").iterdir():
        if not directory.name.isdigit():
            continue
        try:
            if not all_users and directory.stat().st_uid != os.getuid():
                continue
            yield int(directory.name), (directory / "comm").read_text().strip()
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            continue


def desktop_environment(session=None):
    env = os.environ.copy()
    allowed = {"DISPLAY", "XAUTHORITY", "DBUS_SESSION_BUS_ADDRESS", "XDG_RUNTIME_DIR"}
    if session:
        result = subprocess.run(["agent-desktop", "env", session], text=True, capture_output=True,
                                check=True, timeout=10)
        private = json.loads(result.stdout)
        if int(private["DISPLAY"].lstrip(":").split(".")[0]) < 90:
            raise RuntimeError("Concurrent verification requires an agent-desktop private display")
        env.update({key: value for key, value in private.items() if key in allowed})
    desktop_names = {"steam", "gnome-shell", "plasmashell", "xfce4-session", "cinnamon", "mate-session", "Xorg"}
    candidates = []
    for pid, name in processes():
        if name not in desktop_names:
            continue
        try:
            values = {}
            for entry in (Path("/proc") / str(pid) / "environ").read_bytes().split(b"\0"):
                key, separator, value = entry.partition(b"=")
                if separator and key.decode(errors="replace") in allowed:
                    values[key.decode()] = value.decode(errors="replace")
            if values.get("DISPLAY"):
                candidates.append(values)
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            continue
    display = env.get("DISPLAY")
    if not display:
        # Prefer the existing local desktop, never invent or start another display.
        desktop = next((values for values in candidates if values["DISPLAY"] == ":0"), None)
        desktop = desktop or next(iter(candidates), {})
        env.update(desktop)
    else:
        desktop = next((values for values in candidates if values["DISPLAY"] == display), {})
        for key, value in desktop.items():
            if not env.get(key):
                env[key] = value
    if not env.get("DISPLAY") or not re.fullmatch(r":\d+(?:\.\d+)?", env["DISPLAY"]):
        raise RuntimeError("No existing local X11 desktop found; use the owner's actual DISPLAY and XAUTHORITY.")
    authority = Path(env.get("XAUTHORITY", Path.home() / ".Xauthority")).expanduser()
    if not authority.is_file() or not os.access(authority, os.R_OK):
        raise RuntimeError("The actual desktop XAUTHORITY is missing or unreadable; no new display will be started.")
    env["XAUTHORITY"] = str(authority.resolve())
    if shutil.which("xdpyinfo") is None:
        raise RuntimeError("xdpyinfo is required to confirm access to the existing desktop.")
    result = subprocess.run(["xdpyinfo"], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
    if result.returncode:
        raise RuntimeError("Cannot access the existing desktop with discovered DISPLAY/XAUTHORITY; no display changes made.")
    return env


def game_window(pid, game, env):
    try:
        if (Path("/proc") / str(pid) / "exe").resolve() != game / "valheim.x86_64":
            return None
    except (FileNotFoundError, PermissionError, ProcessLookupError):
        return None
    result = subprocess.run(["xdotool", "search", "--onlyvisible", "--pid", str(pid)], env=env,
                            text=True, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=5)
    for window in reversed(result.stdout.split()):
        owner = subprocess.run(["xdotool", "getwindowpid", window], env=env, text=True,
                               stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=5)
        if owner.returncode == 0 and owner.stdout.strip() == str(pid):
            return window
    return None


def start_recording(pid, game, evidence, env, log, filename="training.mkv"):
    window = game_window(pid, game, env)
    if window is None:
        raise RuntimeError("Cannot record: no visible native game window owned by the launched child")
    geometry = subprocess.run(["xdotool", "getwindowgeometry", "--shell", window], env=env,
                              text=True, capture_output=True, check=True, timeout=5)
    values = dict(line.split("=", 1) for line in geometry.stdout.splitlines() if "=" in line)
    width, height = int(values["WIDTH"]), int(values["HEIGHT"])
    if width <= 0 or height <= 0:
        raise RuntimeError("Cannot record: invalid native game window geometry")
    command = ["ffmpeg", "-hide_banner", "-y", "-stdin", "-f", "x11grab", "-draw_mouse", "0",
               "-framerate", "24", "-video_size", f"{width}x{height}", "-window_id", window,
               "-i", env["DISPLAY"], "-t", "60", "-an", "-c:v", "libx264",
               "-preset", "veryfast", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
               "-pix_fmt", "yuv420p", "-threads", "2", str(evidence / filename)]
    log.write(json.dumps({"pid": pid, "window": window, "geometry": values, "command": command}) + "\n")
    log.flush()
    return subprocess.Popen(command, env=env, stdin=subprocess.PIPE, stdout=log,
                            stderr=subprocess.STDOUT, text=True)


def stop_recording(recorder):
    """Finalize this owned child only, including on launcher or encoder failure."""
    try:
        if recorder.poll() is None:
            try:
                recorder.stdin.write("q\n")
                recorder.stdin.flush()
            except (BrokenPipeError, OSError):
                pass
        try:
            code = recorder.wait(timeout=10)
        except subprocess.TimeoutExpired:
            recorder.terminate()
            try:
                recorder.wait(timeout=5)
            except subprocess.TimeoutExpired:
                recorder.kill()
                recorder.wait(timeout=5)
            raise RuntimeError("Recorder did not finalize after stdin q; inspect recorder.log")
        if code != 0:
            raise RuntimeError(f"Recorder exited with code {code}; inspect recorder.log")
    finally:
        if recorder.stdin is not None:
            try:
                recorder.stdin.close()
            except (BrokenPipeError, OSError):
                pass


def input_stage(pid, game, evidence, env, handled):
    request = evidence / "input.json"
    if not request.is_file():
        return
    text = request.read_text()
    if text in handled:
        return
    stage = json.loads(text)
    if stage.get("kind") not in {"hover", "screenshot"}:
        raise RuntimeError("Unknown harness input stage; only PID-scoped hover/screenshot are authorized")
    if shutil.which("xdotool") is None:
        raise RuntimeError("Harness requested native window interaction but xdotool is unavailable")
    window = game_window(pid, game, env)
    if window is None:
        return
    if stage["kind"] == "hover":
        x, y = int(stage["x"]), int(stage["y"])
        if not (0 <= x < 1280 and 0 <= y < 720):
            raise RuntimeError("Harness hover coordinates are outside the fixed game viewport")
        subprocess.run(["xdotool", "windowactivate", "--sync", window], env=env, check=True, timeout=5)
        active = subprocess.run(["xdotool", "getactivewindow"], env=env, text=True,
                                stdout=subprocess.PIPE, check=True, timeout=5).stdout.strip()
        if active != window:
            raise RuntimeError("Game window is not active; refusing to move pointer into another application")
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)],
                       env=env, check=True, timeout=5)
    else:
        if shutil.which("import") is None:
            raise RuntimeError("Harness requested a screenshot but ImageMagick import is unavailable")
        name = stage.get("name", "requested-screenshot.png")
        if not isinstance(name, str) or Path(name).name != name or not name.endswith(".png"):
            raise RuntimeError("Screenshot name must be a plain .png filename within evidence")
        subprocess.run(["import", "-window", window, str(evidence / name)], env=env, check=True, timeout=10)
    handled.add(text)
    ack = evidence / "input.done"
    temporary = evidence / "input.done.tmp"
    temporary.write_text(json.dumps({"id": stage.get("id"), "kind": stage["kind"], "pid": pid}) + "\n")
    temporary.replace(ack)


def collect_logs(run):
    evidence = run / "evidence"
    loader_log = run / "profile/BepInEx/LogOutput.log"
    if loader_log.is_file():
        shutil.copy2(loader_log, evidence / "BepInEx.log")
    errors = []
    for name in ("BepInEx.log", "runtime.log", "Player.log", "launcher.log"):
        path = evidence / name
        if path.is_file():
            for line in path.read_text(errors="replace").splitlines():
                if re.search(r"\b(error|fatal|exception|fail(?:ed|ure)?)\b|\w+Exception\b", line, re.IGNORECASE):
                    errors.append(f"{name}: {line}")
    (evidence / "production-errors.log").write_text("\n".join(errors) + ("\n" if errors else ""))


def launch(args):
    if os.environ.get("FITNESS_RUN") != TOKEN:
        raise RuntimeError(f"Explicit authorization required: FITNESS_RUN={TOKEN}")
    run = args.run_dir.expanduser().resolve()
    if run.parent != (LAB / "runs").resolve() or not (run / "manifest.json").is_file():
        raise RuntimeError("Refusing a run outside this lab's fresh setup directory")
    manifest = json.loads((run / "manifest.json").read_text())
    profile = run / "profile"
    if (manifest.get("run_id") != run.name or manifest.get("profile") != str(profile)
            or manifest.get("scope") != "fresh-local-character-and-world-only"
            or manifest.get("network_mode") != "singleplayer"):
        raise RuntimeError("Invalid isolated-run manifest")
    if args.record_audio and not args.showcase:
        raise RuntimeError("--record-audio requires --showcase and records only the game stream")
    if args.showcase_biome and not args.showcase:
        raise RuntimeError("--showcase-biome requires --showcase")
    if args.timeout is None:
        args.timeout = 5400 if args.showcase else 900
    if any(path.is_symlink() for path in run.rglob("*")):
        raise RuntimeError("Run profiles must not link to live saves, configuration, or profiles")
    for child in ("evidence", "saves", "xdg/config", "xdg/data", "xdg/cache"):
        directory = run / child
        if not directory.is_dir() or any(directory.iterdir()):
            raise RuntimeError("This run directory is not fresh; call setup for another disposable run")
    if (run / "started.json").exists() or (profile / "BepInEx/LogOutput.log").exists():
        raise RuntimeError("This run directory was already used; call setup again")
    running = [(pid, name) for pid, name in processes(all_users=True)
               if name in {"valheim.x86_64", "valheim", "valheim_server", "valheim_server."}]
    if running and not args.desktop_session:
        raise RuntimeError("Another Valheim instance is running; use an explicitly isolated --desktop-session")
    if not any(name == "steam" for _, name in processes()):
        raise RuntimeError("Normal Steam desktop client is not running; no loader/API bypass is permitted")
    env = desktop_environment(args.desktop_session)
    for pid, _ in running:
        entries = (Path("/proc") / str(pid) / "environ").read_bytes().split(b"\0")
        if ("DISPLAY=" + env["DISPLAY"]).encode() in entries:
            raise RuntimeError("A Valheim instance already uses this display; no existing PID was touched")
    if not 1024 <= args.port <= 65534:
        raise RuntimeError("Verification port must be between 1024 and 65534")
    for port in (args.port, args.port + 1):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.bind(("0.0.0.0", port))
    game = Path(manifest["game"]).resolve()
    for path in (game / "valheim.x86_64", profile / "start_game_bepinex.sh", profile / "doorstop_libs/libdoorstop_x64.so",
                 profile / "BepInEx/core/BepInEx.Preloader.dll", profile / "BepInEx/plugins/Fitness/Fitness.dll",
                 profile / "BepInEx/plugins/Fitness.InGameTests/Fitness.InGameTests.dll"):
        require_file(path)
    # Do not inherit a shell's preloader/debugger or another lab's harness configuration.
    for key in list(env):
        if key.startswith(("DOORSTOP_", "FITNESS_", "ROTHEART_", "BEPINEX_")) or key in {
                "LD_PRELOAD", "DYLD_INSERT_LIBRARIES", "MONO_ENV_OPTIONS", "MONO_PATH"}:
            env.pop(key)
    if args.record_video or args.showcase:
        for executable in ("ffmpeg", "xdotool", *(("pw-dump", "pw-record") if args.record_audio else ())):
            if shutil.which(executable) is None:
                raise RuntimeError(f"Video capture requires {executable}")
        if args.showcase:
            env["FITNESS_SHOWCASE"] = "1"
            if args.showcase_biome:
                env["FITNESS_SHOWCASE_BIOME"] = ",".join(args.showcase_biome)
        else:
            env["FITNESS_RECORD_VIDEO"] = "1"
    evidence = run / "evidence"
    env.update(FITNESS_RUN=TOKEN, FITNESS_RUN_ID=run.name, FITNESS_EVIDENCE=str(evidence),
               FITNESS_SAVES=str(run / "saves"), XDG_CONFIG_HOME=str(run / "xdg/config"),
               XDG_DATA_HOME=str(run / "xdg/data"), XDG_CACHE_HOME=str(run / "xdg/cache"),
               SteamAppId=APP_ID, SteamGameId=APP_ID)
    # No server, world, join, password or crossplay flags: the harness creates a local world.
    command = ["bash", str(profile / "start_game_bepinex.sh"), str(game / "valheim.x86_64"),
               "-savedir", str(run / "saves"), "-port", str(args.port), "-public", "0", "-screen-fullscreen", "0",
               "-screen-width", "1280", "-screen-height", "720", "-logFile", str(evidence / "Player.log")]
    with (run / "started.json").open("x") as started:
        json.dump({"run_id": run.name, "timeout_seconds": args.timeout}, started)
    (evidence / "launch.json").write_text(json.dumps({"command": command, "run_id": run.name,
        "loader_source": manifest["loader_source"], "display": env["DISPLAY"],
        "desktop_session": args.desktop_session, "other_game_pids": [pid for pid, _ in running],
        "port": args.port, "network_mode": "singleplayer", "record_video": args.record_video,
        "showcase": args.showcase, "showcase_biome": args.showcase_biome, "record_audio": args.record_audio}, indent=2) + "\n")
    process = None
    failure = None
    deadline = time.monotonic() + args.timeout
    handled = set()
    recorder = None
    recorder_log = None
    runtime_log = None
    runtime_pending = ""
    video_started = False
    video_finished = False
    recording_started = None
    showcase = (ShowcaseCapture(evidence, env, start_recording, stop_recording,
                                args.record_audio, args.showcase_biome) if args.showcase else None)
    try:
        with (evidence / "launcher.log").open("w") as log:
            if showcase is not None:
                showcase.prepare()
            process = subprocess.Popen(command, cwd=profile, env=env, stdout=log, stderr=subprocess.STDOUT)
            (evidence / "pid.txt").write_text(str(process.pid) + "\n")
            while True:
                exited = process.poll() is not None
                if args.record_video or args.showcase:
                    runtime_path = evidence / "runtime.log"
                    if runtime_log is None and runtime_path.is_file():
                        runtime_log = runtime_path.open(errors="replace")
                    if runtime_log is not None:
                        runtime_pending += runtime_log.read()
                        lines = runtime_pending.split("\n")
                        runtime_pending = lines.pop()
                        if exited and runtime_pending:
                            lines.append(runtime_pending)
                            runtime_pending = ""
                        for line in lines:
                            if showcase is not None:
                                showcase.consume(line, process.pid, game)
                            if "STAGE video-training" in line:
                                if video_started:
                                    raise RuntimeError("Duplicate video-training marker")
                                recorder_log = (evidence / "recorder.log").open("w")
                                recorder = start_recording(process.pid, game, evidence, env, recorder_log)
                                recording_started = time.monotonic()
                                video_started = True
                            if "STAGE video-finished" in line:
                                if not video_started or video_finished:
                                    raise RuntimeError("Unexpected video-finished marker")
                                stop_recording(recorder)
                                video_finished = True
                    if recorder is not None and not video_finished:
                        if recorder.poll() is not None:
                            raise RuntimeError(f"Recorder exited before video-finished (code {recorder.returncode}); "
                                               "inspect recorder.log")
                        if time.monotonic() - recording_started >= 60:
                            raise TimeoutError("Video exceeded 60 seconds without video-finished marker")
                    if showcase is not None:
                        showcase.tick()
                if exited:
                    break
                if time.monotonic() >= deadline:
                    raise TimeoutError("Native harness exceeded its deadline; inspect startup/runtime evidence")
                input_stage(process.pid, game, evidence, env, handled)
                time.sleep(.2)
    except (Exception, KeyboardInterrupt) as error:
        failure = f"{type(error).__name__}: {error}"
    finally:
        if showcase is not None:
            try:
                showcase.close()
            except (Exception, KeyboardInterrupt) as error:
                detail = f"{type(error).__name__}: {error}"
                failure = f"{failure}; {detail}" if failure else detail
        try:
            if recorder is not None and not video_finished:
                try:
                    stop_recording(recorder)
                except (Exception, KeyboardInterrupt) as error:
                    detail = f"{type(error).__name__}: {error}"
                    failure = f"{failure}; {detail}" if failure else detail
        finally:
            if runtime_log is not None:
                runtime_log.close()
            if recorder_log is not None:
                recorder_log.close()
            try:
                if process is not None and process.poll() is None:
                    # Popen owns this unreaped child. Never signal a name, group, or unrelated PID.
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait(timeout=5)
            finally:
                collect_logs(run)
    complete = evidence / "complete.txt"
    result = complete.read_text().strip() if complete.is_file() else "MISSING completion marker"
    screenshots = [path for path in evidence.glob("*.png") if path.is_file() and path.stat().st_size > 8]
    video = evidence / "training.mkv"
    video_ok = (video_started and video_finished and recorder is not None and recorder.returncode == 0
                and video.is_file() and video.stat().st_size > 0)
    if args.record_video and not video_ok and failure is None:
        failure = "Missing completed native training video; require both runtime stages and nonempty training.mkv"
    showcase_ok = showcase is not None and showcase.complete
    if args.showcase and not showcase_ok and failure is None:
        failure = "Missing complete native biome showcase shot list"
    success = (failure is None and process is not None and process.returncode == 0
               and re.fullmatch(r"PASS [1-9]\d*", result) is not None
               and (evidence / "runtime.log").is_file() and bool(screenshots)
               and (not args.record_video or video_ok) and (not args.showcase or showcase_ok))
    status = {"success": success, "exit_code": process.returncode if process is not None else None,
              "completion": result, "launcher_failure": failure, "screenshots": [path.name for path in screenshots],
              "record_video": args.record_video, "video": video.name if video_ok else None,
              "recorder_exit_code": recorder.returncode if recorder is not None else None,
              "showcase": args.showcase, "showcase_complete": showcase_ok,
              "showcase_clips": showcase.clips if showcase is not None else []}
    (evidence / "result.json").write_text(json.dumps(status, indent=2) + "\n")
    print(f"{'PASS' if success else 'FAIL'}: {result}; exit={status['exit_code']}; evidence: {evidence}")
    if failure:
        print(failure, file=sys.stderr)
    if not success and not screenshots:
        print("No native screenshot evidence was produced.", file=sys.stderr)
    return 0 if success else 1


def bounded_timeout(value):
    result = float(value)
    if not math.isfinite(result) or not 1 <= result <= 5400:
        raise argparse.ArgumentTypeError("timeout must be between 1 and 5400 seconds")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("setup", help="create a unique fresh save/XDG/native loader profile; never builds")
    create.add_argument("--plugin", type=Path, default=ROOT / "mods/Fitness/bin/Release/netstandard2.1/Fitness.dll")
    create.add_argument("--harness", type=Path, default=ROOT / "tests/Fitness.InGameTests/bin/Release/netstandard2.1/Fitness.InGameTests.dll")
    create.set_defaults(handler=setup)
    run = commands.add_parser("run", help="run an authorized fresh local-only native test using existing Steam and desktop")
    run.add_argument("--run-dir", type=Path, required=True)
    run.add_argument("--timeout", type=bounded_timeout, help="overall deadline, 1–5400 seconds (default: 900; showcase: 5400)")
    run.add_argument("--desktop-session", help="existing private agent-desktop session; permits another game on a different display")
    run.add_argument("--port", type=int, default=24876, help="distinct local UDP game/query port pair (default: 24876/24877)")
    capture = run.add_mutually_exclusive_group()
    capture.add_argument("--record-video", action="store_true",
                         help="native-resource demo; child-window-only silent H.264 MKV, up to 60 seconds")
    capture.add_argument("--showcase", action="store_true", help="record outdoor and treadmill shots across all nine real biomes")
    run.add_argument("--showcase-biome", choices=BIOMES, nargs="+", help="capture selected native biomes (requires --showcase)")
    run.add_argument("--record-audio", action="store_true", help="record only the showcase game's exact PipeWire audio stream")
    run.set_defaults(handler=launch)
    args = parser.parse_args()
    try:
        return args.handler(args)
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
