"""Bake HORIZON's original case flame atlas (64 frames, bottom row first)."""

from pathlib import Path

import numpy as np
from PIL import Image


def smoothstep(low, high, values):
    p = np.clip((values - low) / (high - low), 0, 1)
    return p * p * (3 - 2 * p)


def noise(grid, x, y):
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    u, v = smoothstep(0, 1, x - ix), smoothstep(0, 1, y - iy)
    a = grid[iy % 32, ix % 32] * (1 - u) + grid[iy % 32, (ix + 1) % 32] * u
    b = grid[(iy + 1) % 32, ix % 32] * (1 - u) + grid[(iy + 1) % 32, (ix + 1) % 32] * u
    return a * (1 - v) + b * v


def flame(time, grid, x, y):
    lift = .32 * time
    turbulence = noise(grid, 5 * x + 7, 5 * (y - lift) + 11)
    curl = .12 * np.sin(8 * y - 6 * time) * smoothstep(-.45, .7, y)
    x = x + curl + .32 * (turbulence - .5)
    wisps = (
        np.exp(-((x + .06 + .16 * time) / (.19 + time * .18)) ** 2 - ((y + .28 - lift) / .46) ** 2)
        + .72 * np.exp(-((x - .23 - time * .15) / .20) ** 2 - ((y + .19 - lift * 1.7) / .40) ** 2)
        + .55 * np.exp(-((x + .30 + time * .20) / .18) ** 2 - ((y - .12 - lift) / .36) ** 2)
    )
    eddies = noise(grid, 10 * x + 3.5, (y - time * .95) * 9 + 2)
    folds = .57 * eddies + .27 * turbulence + .16 * noise(grid, 21 * x, 20 * (y - time * .8))
    plume = wisps * (.45 + folds) - (.17 + time * .14)
    edge = 1 - smoothstep(.74, .94, np.maximum(np.abs(x - curl - .32 * (turbulence - .5)), np.abs(y)))
    alpha = smoothstep(.03, .46, plume) * edge * (1 - smoothstep(.64, 1.05, time))
    heat = smoothstep(.10, 1.03, plume + .22 * folds - time * .33 - .19 * y)
    r = np.clip(.29 + heat * 1.42, 0, 1)
    g = .06 + .89 * heat ** 1.32
    b = .025 + .67 * heat ** 3
    return (np.stack((r, g, b, alpha), axis=-1) * 255).round().astype(np.uint8)


def main():
    cell = 64
    coords = (np.arange(cell) + .5) / cell * 2 - 1
    x, y = np.meshgrid(coords, -coords)
    grid = np.random.default_rng(7384).random((32, 32))
    atlas = Image.new("RGBA", (8 * cell, 8 * cell))
    for frame in range(64):
        image = Image.fromarray(flame(frame / 63, grid, x, y))
        atlas.paste(image, (frame % 8 * cell, (7 - frame // 8) * cell))
    path = Path(__file__).resolve().parents[1] / "Assets/Resources/HorizonCaseFlame.png"
    atlas.save(path, optimize=True)


if __name__ == "__main__":
    main()
