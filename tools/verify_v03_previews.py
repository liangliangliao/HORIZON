"""Decode every actual Unity preview instead of only checking file presence."""
from pathlib import Path
from PIL import Image

files = sorted(Path("artifacts/visuals").glob("[0-9][0-9]-*.png"))
assert len(files) == 22, f"Expected 22 portrait previews, got {len(files)}"
assert {int(p.name[:2]) for p in files} == set(range(1, 23))
for path in files:
    with Image.open(path) as image:
        image.load()
        assert image.size == (1080, 1920), (path, image.size)
        assert len(image.convert("RGB").getcolors(256) or []) != 1, f"Blank preview: {path}"
print("22 actual Unity portrait previews decoded at 1080 x 1920")
