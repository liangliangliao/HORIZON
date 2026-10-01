"""Decode every actual Unity preview instead of only checking file presence."""
from pathlib import Path
from PIL import Image
import sys

files = sorted(Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/visuals").glob("[0-9][0-9]-*.png"))
assert len(files) == 25, f"Expected 25 portrait previews, got {len(files)}"
assert {int(p.name[:2]) for p in files} == set(range(1, 26))
for path in files:
    with Image.open(path) as image:
        image.load()
        assert image.size == (1080, 1920), (path, image.size)
        assert len(image.convert("RGB").getcolors(256) or []) != 1, f"Blank preview: {path}"
print("25 actual Unity portrait previews decoded at 1080 x 1920")
