"""Decode native phone captures without substituting editor renders."""
import json
from pathlib import Path
from PIL import Image, ImageStat

directory = Path("artifacts/android-smoke/device")
report = json.loads((directory / "report.json").read_text())
assert report["status"] == "passed" and not report.get("errors"), report
assert report["graphics"] == "OpenGLES3", report["graphics"]
frames = report["frames"]
assert {"01-home", "03-returned-home", "04-resumed-home", "05-reward-visible", "06-reward-back-home"}.issubset(frames)
assert any(name.endswith("-Complete") for name in frames), "The actual imagination flow did not finish"
assert any(name.endswith("-Recover") for name in frames), "Recovery was not exercised"
assert len(frames) >= 9, frames
for name in frames:
    with Image.open(directory / (name + ".png")) as frame:
        frame.load()
        assert frame.size == (report["width"], report["height"])
        assert frame.height > frame.width > 100
        assert max(ImageStat.Stat(frame.convert("RGB")).stddev) > 8, f"Blank native frame: {name}"
print(json.dumps({"native_android": "passed", "graphics": report["graphics"],
                  "frames": len(frames), "resolution": [report["width"], report["height"]]}))
