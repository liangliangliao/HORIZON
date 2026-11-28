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
        assert not any(int(row["chargeBeams"]) or int(row["conduits"]) for row in trace)
        moments = [("Immediate receipt", trace[0]), ("Stable receipt", trace[-1])]
    else:
        with (take / "cues.csv").open(encoding="utf-8", newline="") as file:
            cues = {row["phase"]: float(row["time"]) for row in csv.DictReader(file)}
        assert 2.60 <= float(outcome["seconds"]) - cues["Impact"] <= 2.80
        assert any(row["release"] == "True" and float(row["seatDistance"]) < .015 for row in trace)
        assert guides == sorted(guides) and set(guides) == {0, 1, 2, 3}
        assert landed == sorted(landed) and set(landed) == set(range(landed[-1] + 1))
        assert visible == sorted(visible) and set(visible) == set(range(landed[-1] + 1))
        field_frames = [row for row in trace if row["field"] == "True"]
        first_visible = trace[visible.index(1)]
        assert field_frames and all(row["visible"] == "0" and not row["receipt"] for row in field_frames)
        assert float(field_frames[-1]["seconds"]) < float(first_visible["seconds"]) <= float(outcome["seconds"])
        assert float(trace[landed.index(landed[-1])]["seconds"]) < cues["SecondReveal"]
        contacts = [nearest(trace, float(trace[guides.index(i)]["seconds"]) + .20) for i in (1, 2, 3)]
        assert float(contacts[0]["seamWidth"]) < float(contacts[1]["seamWidth"]) < float(contacts[2]["seamWidth"])
        assert all(int(row["chargeBeams"]) > 0 for row in contacts)
        wave = nearest(trace, cues["Impact"] + .44)
        first = nearest(trace, float(outcome["seconds"]) + .22)
        assert float(first["lens"]) < 36 and int(first["visible"]) == len(first["receipt"].split("|")) == 1
        last_visible = float(trace[visible.index(visible[-1])]["seconds"])
        last = nearest(trace, last_visible + .55)
        assert last["opened"] == str(visible[-1])
        assert any(int(row["conduits"]) > 0 for row in trace if float(row["seconds"]) > last_visible)
        first_warm = warm_area(take, first)
        wave_warm = warm_area(take, wave)
        assert wave_warm - first_warm > .10, (take, first_warm, wave_warm)
        (take / "intensity.json").write_text(json.dumps({
            "first_prize_seconds": float(first_visible["seconds"]),
            "first_readout_seconds": float(outcome["seconds"]),
            "wide_wave_seconds": float(wave["seconds"]),
            "last_release_frame_seconds": float(field_frames[-1]["seconds"]),
            "prize_warm_coverage": round(first_warm, 4),
            "wide_warm_coverage": round(wave_warm, 4),
        }, indent=2)+"\n")
        moments = [
            ("Recorded action", nearest(trace, .30)),
            *[(f"Charge {i+1}", contact) for i, contact in enumerate(contacts)],
            ("Critical hold", nearest(trace, (cues["HitStop"] + cues["Impact"]) / 2)),
            ("Wide release", wave),
            ("First prize close-up", first),
            ("Last prize close-up", last),
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
