"""Decode every actual Unity preview instead of only checking file presence."""
from pathlib import Path
from PIL import Image
import sys

directory = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/visuals")
files = sorted(directory.glob("[0-9][0-9]-*.png"))
# 90 used to be a single Pattern Broken still. It is intentionally replaced by
# the six timed 92-* captures below; 91 remains the Reality Convergence still.
expected_baseline_numbers = set(range(1, 90)) | {91}
baseline = [path for path in files if int(path.name[:2]) in expected_baseline_numbers]
cinematic_phases = [
    "92-pattern-fullscreen-01-charge.png",
    "92-pattern-fullscreen-02-breath-hold.png",
    "92-pattern-fullscreen-03-impact.png",
    "92-pattern-fullscreen-04-multiplier.png",
    "92-pattern-fullscreen-05-second-reveal.png",
    "92-pattern-fullscreen-06-settlement.png",
]
phase_files = [directory / name for name in cinematic_phases]

assert len(baseline) == len(expected_baseline_numbers), (
    f"Expected {len(expected_baseline_numbers)} baseline portrait previews, got {len(baseline)}"
)
assert {int(path.name[:2]) for path in baseline} == expected_baseline_numbers
assert [path.name for path in files if int(path.name[:2]) not in expected_baseline_numbers] == cinematic_phases, (
    "Full-screen cinematic phase evidence must contain the exact six-phase sequence"
)

for path in baseline + phase_files:
    with Image.open(path) as image:
        image.load()
        number = int(path.name[:2])
        expected_size = (1080, 2400) if number in (72, 73) else (540, 960) if number >= 90 else (1080, 1920)
        assert image.size == expected_size, (path, image.size)
        assert len(image.convert("RGB").getcolors(256) or []) != 1, f"Blank preview: {path}"

print(
    f"{len(baseline) + len(phase_files)} actual Unity previews decoded: "
    f"{len(baseline)} baseline portraits plus the complete six-phase full-screen cinematic sequence"
)
