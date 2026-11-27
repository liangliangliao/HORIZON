"""Build HORIZON's original archive reader and source folio.
Run from the repository root with Blender 4.3:
    blender -b -t 2 --python tools/build_echo_archive.py
The reader is authored in metres. OBJ stores Unity-oriented geometry and
named parts so the scene can assign the authored bronze, enamel and paper.
"""
from pathlib import Path
from math import cos, pi, sin
from itertools import groupby
import struct
import zlib

import bpy
import numpy as np

OUT = Path("Assets/Resources/EchoArchive")


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def block(name, at, size, bevel=.022):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, bevel)


def finish(obj, bevel):
    if bevel:
        corners = obj.modifiers.new("Machined edge bevel", "BEVEL")
        corners.width = bevel
        corners.segments = 3
        normals = obj.modifiers.new("Weighted finishing normals", "WEIGHTED_NORMAL")
        normals.keep_sharp = True


def cylinder(name, at, radius, depth, bevel=.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48 if radius>.14 else 16, radius=radius, depth=depth, location=at)
    obj = bpy.context.object
    obj.name = name
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    finish(obj, bevel)


def arc(name, center, radius, width, start, end, elevation):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = width
    curve.bevel_resolution = 2
    curve.use_fill_caps = True
    points = curve.splines.new("POLY").points
    points.add(32)
    for i, point in enumerate(points):
        angle = start + (end - start) * i / 32
        point.co = (center[0] + radius*cos(angle),center[1] + radius*sin(angle),elevation,1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)


def obj_file(name):
    graph = bpy.context.evaluated_depsgraph_get()
    folder = OUT / name
    folder.mkdir(exist_ok=True)
    assembly = sorted(bpy.context.scene.objects,key=lambda item:item.name)
    for finish, pieces in groupby(assembly,key=lambda item:item.name.split("_",1)[0]):
        vertex_offset, corner_offset = 1, 1
        with (folder / (finish + ".obj")).open("w",encoding="utf-8") as file:
            file.write(f"# Original HORIZON archive art\no {finish}\n")
            for original in pieces:
                obj = original.evaluated_get(graph)
                mesh = obj.to_mesh()
                for vertex in mesh.vertices:
                    x,y,z = obj.matrix_world @ vertex.co
                    file.write(f"v {x:.6f} {z:.6f} {y:.6f}\n")
                uv = mesh.uv_layers.active
                for corner in mesh.loops:
                    if uv:
                        u,v = uv.data[corner.index].uv
                    else:
                        coordinate = mesh.vertices[corner.vertex_index].co
                        u,v = coordinate.x,coordinate.y
                    file.write(f"vt {u:.5f} {v:.5f}\n")
                    x,y,z = obj.matrix_world.to_3x3() @ mesh.corner_normals[corner.index].vector
                    file.write(f"vn {x:.6f} {z:.6f} {y:.6f}\n")
                for face in mesh.polygons:
                    corners = reversed(list(face.loop_indices))
                    body = " ".join(f"{vertex_offset+mesh.loops[i].vertex_index}/{corner_offset+i}/{corner_offset+i}" for i in corners)
                    file.write(f"f {body}\n")
                vertex_offset += len(mesh.vertices)
                corner_offset += len(mesh.loops)
                obj.to_mesh_clear()


def reader():
    reset()
    block("Enamel_Body", (0,0,.086), (1.68,1.02,.17), .053)
    block("Bronze_Seam", (0,0,.151), (1.69,1.03,.026), .012)
    block("Enamel_Bed", (0,0,.188), (1.57,.91,.065), .030)
    for side in (-1,1):
        block(f"Walnut_Handrest_{side}", (side*.845,0,.101), (.16,.945,.205), .036)
        block(f"Bronze_Rail_{side}", (0,side*.255,.244), (1.28,.047,.045), .012)
        for row in (-1,1):
            cylinder(f"Bronze_Rivet_{side}_{row}", (side*.73,row*.40,.225),.037,.018,.004)
    block("Enamel_CarrierSlot",(-.55,0,.223),(.31,.43,.046),.031)
    block("Bronze_SlotSpine",(-.703,0,.247),(.029,.42,.021),.009)
    cylinder("Bronze_ReaderCup",(.22,0,.238),.355,.105,.022)
    cylinder("Enamel_OpticalBed",(.22,0,.293),.306,.030,.014)
    for index in range(4):
        arc(f"Bronze_OpticalTrack_{index}",(.22,0),.317,.015,index*pi/2+.13,(index+1)*pi/2-.13,.314)
    for index in range(12):
        angle=index*pi/6
        cylinder(f"Bronze_OuterMarker_{index}",(.22+.40*cos(angle),.40*sin(angle),.235),.009,.009,.002)
    block("Bronze_NameRail",(0,-.515,.095),(1.09,.031,.074),.013)
    block("Enamel_NameRail", (0,-.534,.095), (.89,.012,.040), .006)
    obj_file("ArchiveReader")


def folio():
    reset()
    block("Bronze_Frame",(0,0,.004),(.345,.465,.055),.026)
    block("Enamel_LeafBack",(0,0,-.026),(.320,.432,.019),.018)
    block("Paper_RecordedLeaf",(0,0,.034),(.294,.411,.020),.012)
    for i in range(5):
        size=.145+.047*(i%2)
        block(f"Etch_Notes_{i}",(-.016,.008-i*.046,.046),(size,.006,.003),.001)
    arc("Etch_Imprint",(0,.092),.043,.004,0,pi*2,.046)
    block("Bronze_Ribbon",(.094,.159,.047),(.036,.116,.012),.003)
    for x in (-1,1):
        for y in (-1,1):
            cylinder(f"Bronze_LeafPin_{x}_{y}",(x*.138,y*.183,.047),.010,.009,.003)
    obj_file("ArchiveFolio")


def finish_atlas():
    # Packed, linear micro-surface map. R is stain/brush value, G is cavity,
    # BA are the two tangent-normal components derived from the groove height.
    size=128
    y,x=np.mgrid[0:size,0:size]*2*pi/size
    rng=np.random.default_rng(5784)
    grain=.36*np.sin(43*y+np.sin(2*x)+.27*np.cos(7*x))+.19*np.cos(19*y+1.11*np.sin(4*x))
    patina=.22*np.cos(3*x-4*y)+.15*np.sin(7*x+2*y)+rng.normal(0,.067,(size,size))
    dx=(np.roll(grain,-1,axis=1)-np.roll(grain,1,axis=1))*.22
    dy=(np.roll(grain,-1,axis=0)-np.roll(grain,1,axis=0))*.26
    values=np.stack((.82+.22*patina+.11*grain,.93+.115*patina,.5-dx,.5-dy),axis=-1)
    packed=(np.clip(values,0,1)*255).astype(np.uint8)
    rows=b"".join(b"\0"+row.tobytes() for row in packed)
    def chunk(key,data):
        return struct.pack(">I",len(data))+key+data+struct.pack(">I",zlib.crc32(key+data))
    png=b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">2I5B",size,size,8,6,0,0,0))
    (OUT/"ArchiveFinish.png").write_bytes(png+chunk(b"IDAT",zlib.compress(rows,9))+chunk(b"IEND",b""))


OUT.mkdir(parents=True,exist_ok=True)
reader()
folio()
finish_atlas()
