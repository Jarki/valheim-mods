"""Validate the generated mesh/recording payload before embedding it in the DLL."""
import math
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets"


def check_mesh(path):
    with path.open("rb") as f:
        def integer():
            return struct.unpack("<i", f.read(4))[0]
        assert f.read(4) == b"FHM1", path
        count = integer()
        assert 1 <= count <= 32, (path, count)
        groups = set()
        points = []
        triangles = 0
        for _ in range(count):
            group = f.read(integer()).decode()
            assert group in ("Body", "Slide", "Magazine"), group
            groups.add(group)
            assert 0 <= integer() < 7
            vertex_count = integer()
            assert 0 < vertex_count < 100000
            vertices = [struct.unpack("<6f", f.read(24)) for _ in range(vertex_count)]
            for v in vertices:
                assert all(math.isfinite(x) for x in v)
                assert .98 < sum(x*x for x in v[3:]) < 1.02, (path, "bad normal")
                points.append(v[:3])
            index_count = integer()
            assert index_count % 3 == 0
            indices = struct.unpack("<" + "i" * index_count, f.read(index_count * 4))
            assert all(0 <= i < vertex_count for i in indices)
            triangles += index_count // 3
        assert not f.read(1), (path, "trailing bytes")
    extent = [max(p[i] for p in points) - min(p[i] for p in points) for i in range(3)]
    expected = {"pistol": (.18, .30), "rifle": (.85, 1.1), "launcher": (1.1, 1.5),
                "ammo9mm": (.02, .05), "ammo762": (.03, .07), "rocket": (.4, .6)}
    low, high = expected[path.stem]
    assert low < extent[2] < high, (path, extent)
    if path.stem in ("pistol", "rifle"):
        assert {"Body", "Slide", "Magazine"} <= groups, (path, groups)
    print(path.name, triangles, "triangles; extents", [round(x, 3) for x in extent])


for name in ("pistol", "rifle", "launcher", "ammo9mm", "ammo762", "rocket"):
    check_mesh(ROOT / "Models" / (name + ".mesh"))
    with (ROOT / "Models" / (name + ".png")).open("rb") as f:
        assert f.read(8) == b"\x89PNG\r\n\x1a\n"

for name in ("pistol_shot", "pistol_reload", "ak_shot", "ak_reload", "rpg_launch", "explosion"):
    path = ROOT / "Audio" / (name + ".wav")
    with wave.open(str(path)) as wav:
        assert wav.getsampwidth() == 2 and wav.getnchannels() == 1, path
        assert wav.getframerate() >= 22050, path
        frames = wav.readframes(wav.getnframes())
        samples = struct.unpack("<" + "h" * (len(frames) // 2), frames)
        peak = max(abs(s) for s in samples)
        assert 1000 < peak < 32768, (path, peak)
        print(path.name, round(wav.getnframes()/wav.getframerate(), 3), "seconds; peak", peak)
