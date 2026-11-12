"""PID-scoped montage capture with optional application-only PipeWire audio.

Never captures a desktop sink, changes a default device or moves existing streams.
Video and audio finalize independently; no blocking audio pipe holds FFmpeg open.
"""
import json
import os
import re
import signal
import subprocess
import time

BIOMES = ("Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean")


class ShowcaseCapture:
    def __init__(self, evidence, env, start, stop, record_audio=False, biome=None):
        self.evidence, self.env = evidence, env
        self.start, self.stop = start, stop
        self.record_audio, self.biome = record_audio, biome
        self.recorder = self.log = None
        self.audio = self.audio_log = None
        self.current = None
        self.clips = []
        self.finished = False

    def prepare(self):
        if self.record_audio:
            self.env["PIPEWIRE_RUNTIME_DIR"] = f"/run/user/{os.getuid()}"
            self.env["PULSE_SERVER"] = os.environ.get("PULSE_SERVER", f"unix:/run/user/{os.getuid()}/pulse/native")

    def _start_audio(self, pid, marker):
        result = subprocess.run(["pw-dump"], env=self.env, text=True, capture_output=True, check=True, timeout=10)
        nodes = []
        for obj in json.loads(result.stdout):
            props = obj.get("info", {}).get("props", {})
            if (str(props.get("application.process.id")) == str(pid) and
                    props.get("media.class") == "Stream/Output/Audio"):
                nodes.append(props)
        if len(nodes) != 1:
            raise RuntimeError(f"Expected one audio output owned by game PID {pid}, found {len(nodes)}; no desktop audio was captured")
        serial = str(nodes[0]["object.serial"])
        self.current["audio_object_serial"] = serial
        self.current["audio_file"] = marker + ".wav"
        self.audio_log = (self.evidence / ("audio-" + marker + ".log")).open("w")
        self.audio_started = time.monotonic()
        self.audio = subprocess.Popen(["pw-record", "--target", serial, "--latency", "20ms",
                                       "--format", "s16", "--rate", "48000", "--channels", "2",
                                       str(self.evidence / self.current["audio_file"])],
                                      env=self.env, stdout=self.audio_log, stderr=self.audio_log)

    def consume(self, line, pid, game):
        match = re.search(r"\bSTAGE (showcase-[a-z0-9-]+)(?:\s|$)", line)
        if not match:
            return
        marker = match.group(1)
        if marker in {"showcase-gap", "showcase-finished"}:
            self.finish_capture()
            if marker == "showcase-finished":
                if self.finished:
                    raise RuntimeError("Duplicate showcase-finished marker")
                self.finished = True
                expected = {name.lower() for name in (self.biome or BIOMES)}
                observed = {clip["stage"][9:].rsplit("-", 1)[0] for clip in self.clips}
                if observed != expected or len(self.clips) != 2 * len(expected):
                    raise RuntimeError(f"Incomplete biome shot list: expected {sorted(expected)}, got {self.clips}")
                self._save()
            return
        if not (marker.endswith("-run") or marker.endswith("-treadmill")):
            return
        if self.finished or self.recorder is not None or any(clip["stage"] == marker for clip in self.clips):
            raise RuntimeError("Unexpected/duplicate showcase recording stage " + marker)
        self.log = (self.evidence / ("recorder-" + marker + ".log")).open("w")
        self.current = {"stage": marker, "file": marker + ".mkv", "audio": self.record_audio}
        if self.record_audio:
            self._start_audio(pid, marker)
        self.started = time.monotonic()
        self.current["video_file"] = "video-" + marker + ".mkv" if self.record_audio else self.current["file"]
        if self.record_audio:
            self.current["audio_lead_seconds"] = self.started - self.audio_started
        self.recorder = self.start(pid, game, self.evidence, self.env, self.log, filename=self.current["video_file"])

    def tick(self):
        if self.recorder is not None:
            if self.recorder.poll() is not None:
                raise RuntimeError(f"Showcase recorder exited during {self.current['stage']}")
            if self.audio is not None and self.audio.poll() is not None:
                raise RuntimeError("Game-only audio recorder exited during capture")
            if time.monotonic() - self.started >= 60:
                raise TimeoutError("Showcase shot exceeded 60 seconds without gap marker")

    def _stop_audio(self):
        if self.audio is not None:
            process, self.audio = self.audio, None
            stopped_here = process.poll() is None
            if stopped_here:
                process.send_signal(signal.SIGINT)
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
                    raise RuntimeError("Game audio recorder did not finalize its WAV after termination")
            # This installed pw-record returns 1 on clean SIGINT/SIGTERM (verified with an
            # unlinked recorder). Only accept that status when we initiated the stop.
            if not stopped_here or process.returncode != 1:
                raise RuntimeError(f"Game audio recorder exited unexpectedly with code {process.returncode}")
        if self.audio_log is not None:
            self.audio_log.close()
            self.audio_log = None

    def finish_capture(self):
        if self.recorder is None:
            return
        recorder, self.recorder = self.recorder, None
        try:
            self.stop(recorder)
            self._stop_audio()
            self.current["capture_wall_seconds"] = round(time.monotonic() - self.started, 3)
            if self.record_audio:
                result = subprocess.run(["ffmpeg", "-hide_banner", "-y", "-i", str(self.evidence / self.current["video_file"]),
                                         "-ss", str(self.current["audio_lead_seconds"]), "-i", str(self.evidence / self.current["audio_file"]),
                                         "-map", "0:v:0", "-map", "1:a:0", "-c:v", "copy", "-c:a", "aac", "-b:a", "160k",
                                         "-shortest", str(self.evidence / self.current["file"])], capture_output=True, text=True, timeout=30)
                self.log.write(result.stderr)
                if result.returncode:
                    raise RuntimeError("Game-only audio mux failed; inspect recorder log")
            path = self.evidence / self.current["file"]
            if not path.is_file() or path.stat().st_size == 0:
                raise RuntimeError("Missing captured showcase clip " + str(path))
            self.clips.append(self.current)
            self._save()
        finally:
            self.current = None
            self._stop_audio()
            if self.log is not None:
                self.log.close()
                self.log = None

    def _save(self):
        (self.evidence / "showcase-clips.json").write_text(json.dumps({
            "finished": self.finished, "audio_scope": "game PID only" if self.record_audio else "silent",
            "clips": self.clips,
        }, indent=2) + "\n")

    @property
    def complete(self):
        return self.finished and self.recorder is None and bool(self.clips)

    def close(self):
        try:
            self.finish_capture()
        finally:
            self._stop_audio()
            if self.log is not None:
                self.log.close()
                self.log = None
