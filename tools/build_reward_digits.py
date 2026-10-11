"""Bake extruded reward numerals from the project's SIL OFL font.

Build dependency: fonttools and shapely. Unity only reads the baked JSON; no
font outline, triangulation or asset download is needed on the phone.
"""
import json
from pathlib import Path

from fontTools.pens.basePen import BasePen
from fontTools.ttLib import TTFont
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient
from shapely import constrained_delaunay_triangles

ROOT = Path(__file__).resolve().parents[1]


class Outline(BasePen):
    def __init__(self, glyphs, units):
        super().__init__(glyphs)
        self.units, self.contours, self.points = units, [], []

    def _moveTo(self, point):
        self.points = [point]

    def _lineTo(self, point):
        self.points.append(point)

    def _qCurveToOne(self, control, end):
        start = self.points[-1]
        for index in range(1, 9):
            t = index / 8
            self.points.append(tuple((1-t)**2*start[k]+2*(1-t)*t*control[k]+t*t*end[k] for k in range(2)))

    def _curveToOne(self, first, second, end):
        start = self.points[-1]
        for index in range(1, 11):
            t = index / 10
            self.points.append(tuple((1-t)**3*start[k]+3*(1-t)**2*t*first[k]+3*(1-t)*t*t*second[k]+t**3*end[k] for k in range(2)))

    def _closePath(self):
        if len(self.points) > 2:
            self.contours.append([(x/self.units, y/self.units) for x, y in self.points])
        self.points = []

    _endPath = _closePath


def bake(character, glyphs, cmap, units):
    glyph = glyphs[cmap[ord(character)]]
    pen = Outline(glyphs, units)
    glyph.draw(pen)
    shape = Polygon()
    for contour in pen.contours:
        shape = shape.symmetric_difference(Polygon(contour).buffer(0))
    shape = shape.buffer(.008, join_style=1).simplify(.0009, preserve_topology=True)
    polygons = [shape] if shape.geom_type == "Polygon" else list(shape.geoms)
    vertices, indices = [], []

    def vertex(point, z):
        vertices.extend([round(point[0], 6), round(point[1], 6), z])
        return len(vertices)//3-1

    for polygon in polygons:
        polygon = orient(polygon, sign=1)
        loops = [list(polygon.exterior.coords)[:-1]] + [list(r.coords)[:-1] for r in polygon.interiors]
        front_polygon = polygon.buffer(-.009, join_style=2)
        if front_polygon.is_empty:
            raise ValueError(f"Empty bevel for {character}")
        front_lookup, back_lookup, bevel_lookup = {}, {}, {}
        for loop in loops:
            back, edge = [], []
            for point in loop:
                back.append(vertex(point, .12))
                edge.append(vertex(point, -.028))
                back_lookup[tuple(point)] = back[-1]
                bevel_lookup[tuple(point)] = edge[-1]
            for i in range(len(loop)):
                j = (i+1) % len(loop)
                indices.extend([back[i], edge[j], back[j], back[i], edge[i], edge[j]])
        front_parts = [front_polygon] if front_polygon.geom_type == "Polygon" else list(front_polygon.geoms)
        for part in front_parts:
            for boundary in [part.exterior, *part.interiors]:
                for point in list(boundary.coords)[:-1]:
                    index = vertex(point, -.055)
                    front_lookup[tuple(point)] = bevel_lookup[tuple(point)] = index
        bevel = polygon.difference(front_polygon)
        for surface, lookup, reverse in [(front_polygon, front_lookup, True), (polygon, back_lookup, False), (bevel, bevel_lookup, True)]:
            area = 0
            for triangle in constrained_delaunay_triangles(surface).geoms:
                if not surface.covers(triangle):
                    continue
                points = list(orient(triangle, sign=1).exterior.coords)[:3]
                face = [lookup[tuple(point)] for point in points]
                indices.extend(reversed(face) if reverse else face)
                area += triangle.area
            if abs(area-surface.area) > .00001:
                raise ValueError(f"Incomplete numeral triangulation for {character}: {area} != {surface.area}")
    return {"character": character, "advance": round(glyph.width/units+.025, 6), "vertices": vertices, "triangles": indices}


if __name__ == "__main__":
    font = TTFont(ROOT/"Assets/Resources/HorizonSansSC.ttf")
    glyphs, cmap, units = font.getGlyphSet(), font.getBestCmap(), font["head"].unitsPerEm
    atlas = {"glyphs": [bake(char, glyphs, cmap, units) for char in "×0123456789"]}
    output = ROOT/"Assets/Resources/HorizonRewardDigits.json"
    output.write_text(json.dumps(atlas, separators=(",", ":"), ensure_ascii=False)+"\n", encoding="utf-8")
    print(f"Baked {len(atlas['glyphs'])} glyphs, {sum(len(g['vertices'])//3 for g in atlas['glyphs'])} vertices: {output}")
