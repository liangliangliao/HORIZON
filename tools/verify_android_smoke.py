"""Decode native phone captures without substituting editor renders."""
import json
import math
from pathlib import Path
import sys
from PIL import Image, ImageStat

directory = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/android-smoke/device")
report = json.loads((directory / "report.json").read_text())
assert report["status"] == "passed" and not report.get("errors"), report
assert report["graphics"] == "OpenGLES3", report["graphics"]
frames = report["frames"]
assert {"01-home", "03-returned-home", "04-resumed-home", "05-reward-visible", "06-reward-back-home", "07-battery-home"}.issubset(frames)
assert "01b-depth-focus" in frames, "GLES depth focus was not rendered"
for kind in ("Cascade", "CausalSingularity"):
    for phase in ("first", "impact"):
        assert f"08-causal-{kind}-{phase}" in frames, f"Native causal relay was not rendered: {kind}/{phase}"
for phase in ("charge", "hold", "flash", "wave", "receipt"):
    assert f"09-case-{phase}" in frames, f"Native case reveal was not rendered: {phase}"
for phase in ("origin", "charge", "wave", "settle"):
    assert f"10-echo-{phase}" in frames, f"Native recorded echo was not rendered: {phase}"
assert any(name.endswith("-Complete") for name in frames), "The actual imagination flow did not finish"
assert any(name.endswith("-Recover") for name in frames), "Recovery was not exercised"
assert len(frames) >= 9, frames
for name in frames:
    with Image.open(directory / (name + ".png")) as frame:
        frame.load()
        assert frame.size == (report["width"], report["height"])
        assert frame.height > frame.width > 100
        assert max(ImageStat.Stat(frame.convert("RGB")).stddev) > 8, f"Blank native frame: {name}"


def warm_coverage(name):
    with Image.open(directory / (name + ".png")) as frame:
        width, height = frame.size
        central = frame.crop((int(width*.12), int(height*.23), int(width*.88), int(height*.77)))
        pixels = central.convert("RGB").resize((96, 128)).getdata()
        return sum(r >= 142 and r - b >= 38 and g >= 65 for r, g, b in pixels) / len(pixels)


release_coverage = {}
for take in ("09-case", "10-echo"):
    charge = warm_coverage(take + "-charge")
    wave = warm_coverage(take + "-wave")
    assert wave - charge >= .02, (take, "The warm shock front did not spread across the scene", charge, wave)
    release_coverage[take] = {"charge": round(charge, 4), "wave": round(wave, 4)}
performance = json.loads((directory / "performance.json").read_text())
assert performance["graphicsApi"] == report["graphics"]
assert (performance["width"], performance["height"]) == (report["width"], report["height"])
assert performance["device"] and performance["operatingSystem"] and performance["capturedUtc"]
scenes = performance["scenes"]
assert scenes, "The native performance report contains no actual frame samples"
for scene in scenes:
    assert scene["scene"] and scene["quality"] and scene["frames"] > 0, scene
    assert scene["targetFps"] in (30, 60), scene
    assert 0 < scene["percentileSamples"] <= min(1800, scene["frames"]), scene
    for key in ("seconds", "averageFps", "p95Milliseconds", "worstMilliseconds"):
        assert math.isfinite(scene[key]) and scene[key] > 0, (key, scene)
    assert math.isclose(scene["averageFps"], scene["frames"] / scene["seconds"], rel_tol=1e-4), scene
    assert scene["p95Milliseconds"] <= scene["worstMilliseconds"], scene
    assert 0 <= scene["missedBudgetPercent"] <= 100, scene
for target in (30, 60):
    assert sum(scene["frames"] for scene in scenes if scene["targetFps"] == target) >= 60, target
print(json.dumps({"native_android": "passed", "graphics": report["graphics"],
                  "frames": len(frames), "resolution": [report["width"], report["height"]],
                  "recorded_frames": sum(scene["frames"] for scene in scenes),
                  "performance_profiles": sorted({scene["targetFps"] for scene in scenes}),
                  "reveal_warm_coverage": release_coverage,
                  "probable_emulator": performance["probableEmulator"]}))
