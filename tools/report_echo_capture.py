"""Make a contact sheet from the actual Unity echo and verify its phase order."""
import csv
import json
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
    visible = [int(row["visible"]) for row in trace]
    if take.name == "reduced":
        assert float(outcome["seconds"]) <= .05
        assert all(row["release"] == "False" and row["field"] == "False" for row in trace)
        assert guides[0] == 3 and landed[0] == landed[-1] > 0
        assert not any(row["local"] == "True" or int(row["conduits"]) for row in trace)
        moments = [("Immediate receipt", trace[0]), ("Stable receipt", trace[-1])]
    else:
        with (take / "cues.csv").open(encoding="utf-8", newline="") as file:
            cues = {row["phase"]: float(row["time"]) for row in csv.DictReader(file)}
        assert .19 <= float(outcome["seconds"]) - cues["Impact"] <= .35
        assert any(row["release"] == "True" and float(row["seatDistance"]) < .015 for row in trace)
        assert guides == sorted(guides) and set(guides) == {0, 1, 2, 3}
        assert landed == sorted(landed) and set(landed) == set(range(landed[-1] + 1))
        assert visible == sorted(visible) and set(visible) == set(range(landed[-1] + 1))
        assert float(trace[landed.index(landed[-1])]["seconds"]) < cues["SecondReveal"]
        first = nearest(trace, cues["Impact"] + .27)
        last_visible = float(trace[visible.index(visible[-1])]["seconds"])
        last = nearest(trace, last_visible + .16)
        wave_start = next(row for row in trace if row["field"] == "True")
        wave = nearest(trace, float(wave_start["seconds"]) + .30)
        assert first["field"] == last["field"] == "False"
        assert first["local"] == last["local"] == "True"
        assert last["opened"] == str(visible[-1])
        assert int(wave_start["landed"]) == landed[-1] and int(wave_start["conduits"]) > 0
        assert int(first["visible"]) == len(first["receipt"].split("|")) == 1
        before_last = nearest(trace, last_visible - .27)
        assert int(before_last["visible"]) < visible[-1] and before_last["field"] == "False"
        first_warm = warm_area(take, first)
        wave_warm = warm_area(take, wave)
        assert wave_warm - first_warm > .10, (take, first_warm, wave_warm)
        (take / "intensity.json").write_text(json.dumps({
            "first_local_seconds": float(first["seconds"]),
            "last_local_seconds": float(last["seconds"]),
            "wide_peak_seconds": float(wave["seconds"]),
            "local_warm_coverage": round(first_warm, 4),
            "wide_warm_coverage": round(wave_warm, 4),
        }, indent=2)+"\n")
        moments = [
            ("Recorded action", nearest(trace, .30)),
            ("First rim light", nearest(trace, (cues["HitStop"] + cues["Impact"]) / 2)),
            ("First opening", first),
            ("Last seal intact", before_last),
            ("Last opening", last),
            ("Conduits join", wave_start),
            ("Wide wave", wave),
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


def warm_area(take, row):
    with Image.open(take / f"frame-{int(row['frame']):05d}.jpg") as source:
        width, height = source.size
        region = source.crop((int(width*.12), int(height*.23), int(width*.88), int(height*.77)))
        pixels = region.convert("RGB").resize((96,128), Image.Resampling.BILINEAR).get_flattened_data()
        return sum(r >= 142 and r-b >= 38 and g >= 65 for r,g,b in pixels)/len(pixels)


if __name__ == "__main__":
    main(Path(sys.argv[1]))
