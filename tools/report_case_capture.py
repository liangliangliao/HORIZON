"""Measure and create a contact sheet from the actual Unity portrait frames."""

import csv
from pathlib import Path
import sys

from PIL import Image, ImageChops, ImageDraw, ImageStat


def measure(rgb):
    red, green, blue = rgb.split()
    # All three channels must exceed 230. Use the minimum channel for the mask.
    near_white = ImageChops.darker(ImageChops.darker(red, green), blue)
    count = sum(near_white.histogram()[231:])
    return round(ImageStat.Stat(rgb.convert("L")).mean[0], 3), round(count / (rgb.width * rgb.height), 6)


def main(folder):
    with (folder / "trace.csv").open(encoding="utf-8", newline="") as trace:
        rows = list(csv.DictReader(trace))
    frames = sorted(folder.glob("frame-*.jpg"))
    assert rows and len(frames) == len(rows) == int((folder / "complete.txt").read_text())
    assert frames == [folder / f"frame-{i:05}.jpg" for i in range(len(rows))]

    first_release = next((int(row["frame"]) for row in rows if "开匣" in row["stage"]), 0)
    samples = sorted({min(len(rows) - 1, max(0, first_release + offset))
                      for offset in (-54, -40, -24, -12, -3, -1, 0, 1, 2, 3, 4, 5, 6, 8, 12, 18, 26, 44)})
    width, height = 156, 360
    columns = 6
    sheet = Image.new("RGB", (width * columns, height * ((len(samples) + columns - 1) // columns)), (13, 21, 33))
    draw = ImageDraw.Draw(sheet)
    with (folder / "pixel-metrics.csv").open("w", newline="", encoding="utf-8") as output:
        metrics = csv.writer(output)
        metrics.writerow(("frame", "videoSeconds", "meanLuma", "nearWhiteFraction", "subjectMeanLuma", "subjectNearWhiteFraction"))
        for frame, (source, row) in enumerate(zip(frames, rows)):
            with Image.open(source) as image:
                # The center of the desk and case stays above the receipt and below the title.
                subject = image.crop((int(image.width * .10), int(image.height * .27),
                                      int(image.width * .90), int(image.height * .61)))
                metrics.writerow((frame, row["videoSeconds"], *measure(image), *measure(subject)))
                if frame not in samples:
                    continue
                index = samples.index(frame)
                x, y = index % columns * width, index // columns * height
                image.thumbnail((width - 8, height - 24), Image.Resampling.LANCZOS)
                sheet.paste(image, (x + (width - image.width) // 2, y + 3))
                draw.text((x + 8, y + height - 17), f"{row['videoSeconds']}s / frame {frame}", fill="white")
    sheet.save(folder / "release-contact.png")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
