"""Decode every actual Unity preview instead of only checking file presence."""
from pathlib import Path
from PIL import Image
import sys

files = sorted(Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/visuals").glob("[0-9][0-9]-*.png"))
expected = 89
assert len(files) == expected, f"Expected {expected} portrait previews, got {len(files)}"
assert {int(p.name[:2]) for p in files} == set(range(1, expected + 1))
for path in files:
    with Image.open(path) as image:
        image.load()
        expected_size = (1080, 2400) if int(path.name[:2]) in (72, 73) else (1080, 1920)
        assert image.size == expected_size, (path, image.size)
        assert len(image.convert("RGB").getcolors(256) or []) != 1, f"Blank preview: {path}"
print(f"{expected} actual Unity portrait previews decoded, including two 1080 x 2400 phone previews")
