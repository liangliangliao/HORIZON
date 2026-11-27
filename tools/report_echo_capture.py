"""Make a contact sheet from the actual Unity echo and verify its phase order."""
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
    guides = [int(row["guides"]) for row in trace]
    landed = [int(row["landed"]) for row in trace]
    if take.name == "reduced":
        assert float(outcome["seconds"]) <= .05
        assert all(row["release"] == "False" and row["field"] == "False" for row in trace)
        assert guides[0] == 3 and landed[0] == landed[-1] > 0
        moments = [("Immediate receipt", trace[0]), ("Stable receipt", trace[-1])]
    else:
        with (take / "cues.csv").open(encoding="utf-8", newline="") as file:
            cues = {row["phase"]: float(row["time"]) for row in csv.DictReader(file)}
        assert .19 <= float(outcome["seconds"]) - cues["Impact"] <= .35
        assert any(row["release"] == "True" and float(row["seatDistance"]) < .015 for row in trace)
        assert guides == sorted(guides) and set(guides) == {0, 1, 2, 3}
        assert landed == sorted(landed) and set(landed) == set(range(landed[-1] + 1))
        assert float(trace[landed.index(landed[-1])]["seconds"]) < cues["SecondReveal"]
        moments = [
            ("Recorded action", nearest(trace, .30)),
            ("Folio face", nearest(trace, (cues["Charge"] + cues["Escalation"]) / 2)),
        ]
        moments += [(f"Guide {i}", nearest(trace, float(trace[guides.index(i)]["seconds"]) + .14))
                    for i in range(1, 4)]
        moments += [
            ("Dock and hold", nearest(trace, (cues["HitStop"] + cues["Impact"]) / 2)),
            ("Peak and wave", nearest(trace, cues["Impact"] + .30)),
            ("First contact", trace[landed.index(1)]),
            ("All seated", nearest(trace, cues["SecondReveal"] + .65)),
        ]
    width, height = 174, 410
    sheet = Image.new("RGB", (len(moments)*width,height), "#121c27")
    draw = ImageDraw.Draw(sheet)
    for index, (label, row) in enumerate(moments):
        with Image.open(take / f"frame-{int(row['frame']):05d}.jpg") as frame:
            frame.thumbnail((width-10,height-37),Image.Resampling.LANCZOS)
            sheet.paste(frame, (index*width+(width-frame.width)//2,6))
        draw.text((index*width+9,height-27), f"{label}  {row['seconds']}s", fill="white")
    sheet.save(take / "contact.png")


def nearest(trace, seconds):
    return min(trace, key=lambda row: abs(float(row["seconds"]) - seconds))


if __name__ == "__main__":
    main(Path(sys.argv[1]))
