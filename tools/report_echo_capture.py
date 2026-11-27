"""Make a six-beat contact sheet from a verified Unity render."""
import csv
from pathlib import Path
import sys

from PIL import Image, ImageDraw


def main(take):
    with (take / "trace.csv").open(encoding="utf-8", newline="") as file:
        trace = list(csv.DictReader(file))
    frames = sorted(take.glob("frame-*.jpg"))
    assert frames and len(frames) == len(trace) == int((take / "complete.txt").read_text())
    assert frames == [take / f"frame-{i:05d}.jpg" for i in range(len(trace))]
    outcome = next(row for row in trace if row["receipt"])
    if take.name == "reduced":
        assert float(outcome["seconds"]) <= .05
        assert all(row["release"] == "False" and row["field"] == "False" for row in trace)
    else:
        assert 3.09 <= float(outcome["seconds"]) <= 3.25
        assert any(row["release"] == "True" and float(row["seatDistance"]) < .015 for row in trace)
    width, height = 174, 410
    sheet = Image.new("RGB", (6*width,height), "#121c27")
    draw = ImageDraw.Draw(sheet)
    for index, seconds in enumerate((.25,.84,1.77,2.78,3.29,4.92)):
        row = min(trace, key=lambda item: abs(float(item["seconds"])-seconds))
        with Image.open(take / f"frame-{int(row['frame']):05d}.jpg") as frame:
            frame.thumbnail((width-10,height-37),Image.Resampling.LANCZOS)
            sheet.paste(frame, (index*width+(width-frame.width)//2,6))
        draw.text((index*width+9,height-27), f"{row['beat']}  {row['seconds']}s", fill="white")
    sheet.save(take / "contact.png")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
