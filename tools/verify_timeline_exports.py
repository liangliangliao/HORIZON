"""Decode Unity exports with Pillow and verify pixels, timing and animation."""
from pathlib import Path
import hashlib
import sys

from PIL import Image, ImageSequence


def verify(directory: Path) -> None:
    fixture = Image.open(directory / "gif-codec-fixture.gif")
    assert fixture.size == (96, 96) and fixture.n_frames == 3
    shades, blues = [0, 8, 20, 38, 68, 110, 175, 255], [0, 20, 80, 200]
    for frame_number, frame in enumerate(ImageSequence.Iterator(fixture)):
        actual = frame.convert("RGB")
        state = frame_number + 1
        expected = []
        for _ in range(96 * 96):
            state = (state * 1664525 + 1013904223) & 0xFFFFFFFF
            index = state >> 24
            expected.append((shades[index >> 5], shades[(index >> 2) & 7], blues[index & 3]))
        expected = [pixel for y in range(95, -1, -1) for pixel in expected[y * 96:(y + 1) * 96]]
        assert list(actual.getdata()) == expected, f"GIF LZW/palette/orientation mismatch in frame {frame_number}"

    story = Image.open(directory / "HORIZON-run-001.gif")
    assert story.size == (540, 960) and story.n_frames == 60
    assert story.info.get("loop") == 0
    duration, hashes = 0, set()
    for frame in ImageSequence.Iterator(story):
        duration += frame.info["duration"]
        hashes.add(hashlib.sha256(frame.convert("RGB").tobytes()).hexdigest())
    assert duration == 10_000, f"Story duration is {duration} ms"
    assert len(hashes) >= 40, "Animation is unexpectedly static"
    print(f"Unity GIF verified: {story.n_frames} frames, {story.width}x{story.height}, {duration} ms; codec pixels exact")


if __name__ == "__main__":
    verify(Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/visuals"))
