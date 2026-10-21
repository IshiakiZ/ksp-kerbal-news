"""make_mic.py: Kerbal News' desk microphone (2026-10-19; the owner: "improve the mic model").

  blender --background --python tools/news/blender/make_mic.py -- OUT.mesh PREVIEW.png

A broadcast desk microphone, in units of the anchor's height (the newsroom scales it by him): a heavy round base with a bevelled
rim, a short upright stand, a gooseneck curving up and towards him, a slim capsule on its end with a grille and a foam windscreen
over its tip, and on the stand a cube-shaped flag for the channel's name (its letters laid on by the game). Its foot at the origin,
the gooseneck reaching towards -y (Blender; the game's -z: towards him when the newsroom turns it so). Written in the game's form
(anchor_io.write, no bones), one submesh a material: metal, dark, foam, grille, flag.
"""
import sys, os, math
import bpy, bmesh
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import anchor_io

argv = sys.argv[sys.argv.index("--") + 1:]
out_mesh, preview = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

colours = {"metal": (0.55, 0.56, 0.58, 1), "dark": (0.05, 0.05, 0.055, 1), "foam": (0.09, 0.09, 0.1, 1), "grille": (0.25, 0.26, 0.28, 1),
           "flag": (0.06, 0.09, 0.22, 1)}
mats = {}
for n, c in colours.items():
    m = bpy.data.materials.new(n); m.diffuse_color = c; mats[n] = m

parts = []

def finish(o, material, smooth=True):
    o.data.materials.clear(); o.data.materials.append(mats[material])
    if smooth:
        for p in o.data.polygons: p.use_smooth = True
    parts.append(o)
    return o

# ---- the base: a heavy disc with a bevelled rim
bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=0.045, depth=0.012, location=(0, 0, 0.006))
base = bpy.context.active_object
bev = base.modifiers.new("bev", "BEVEL"); bev.width = 0.004; bev.segments = 3
bpy.ops.object.modifier_apply(modifier="bev")
finish(base, "dark")
# (a ring of metal round its top edge)
bpy.ops.mesh.primitive_torus_add(major_radius=0.041, minor_radius=0.0018, major_segments=48, minor_segments=8, location=(0, 0, 0.0122))
finish(bpy.context.active_object, "metal")

# ---- the stand: upright from the base's middle, with a collar where the gooseneck begins
bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.005, depth=0.09, location=(0, 0, 0.012 + 0.045))
finish(bpy.context.active_object, "metal")
bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.0075, depth=0.012, location=(0, 0, 0.102))
finish(bpy.context.active_object, "dark")

# ---- the gooseneck: a curve from the stand's top up and over towards him, a tube with rings along it (the coils)
curve = bpy.data.curves.new("neck", "CURVE"); curve.dimensions = "3D"
sp = curve.splines.new("BEZIER"); sp.bezier_points.add(2)
pts = [(0, 0, 0.108), (0, -0.025, 0.25), (0, -0.075, 0.3)]
handles = [((0, 0.0, 0.07), (0, 0.0, 0.16)), ((0, 0.0, 0.215), (0, -0.05, 0.285)), ((0, -0.06, 0.292), (0, -0.09, 0.308))]
for bp, p, (hl, hr) in zip(sp.bezier_points, pts, handles):
    bp.co = p; bp.handle_left = hl; bp.handle_right = hr
curve.bevel_depth = 0.0042; curve.bevel_resolution = 4; curve.resolution_u = 24
neck = bpy.data.objects.new("neck", curve); scene.collection.objects.link(neck)
bpy.context.view_layer.objects.active = neck; neck.select_set(True)
bpy.ops.object.convert(target="MESH")
neck = bpy.context.active_object
finish(neck, "dark")
# (the coils: thin rings along it)
samples = 14
for k in range(1, samples):
    t = k / samples
    # (de Casteljau along the two pieces, enough for placing rings)
    def bez(p0, p1, p2, p3, u):
        a = [Vector(p0).lerp(Vector(p1), u), Vector(p1).lerp(Vector(p2), u), Vector(p2).lerp(Vector(p3), u)]
        b = [a[0].lerp(a[1], u), a[1].lerp(a[2], u)]
        return b[0].lerp(b[1], u)
    seg, u = (0, t * 2) if t < 0.5 else (1, t * 2 - 1)
    P0, P3 = pts[seg], pts[seg + 1]
    P1, P2 = handles[seg][1], handles[seg + 1][0]
    c = bez(P0, P1, P2, P3, u); c2 = bez(P0, P1, P2, P3, min(u + 0.02, 1.0))
    d = (c2 - c).normalized()
    bpy.ops.mesh.primitive_torus_add(major_radius=0.0044, minor_radius=0.0009, major_segments=16, minor_segments=6, location=c)
    ring = bpy.context.active_object
    ring.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    finish(ring, "metal")

# ---- the capsule at its end, pointing towards him (-y), its grille, and the foam over its tip
end = Vector(pts[-1]); way = Vector((0, -0.9, 0.44)).normalized()
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.0095, depth=0.05, location=end + way * 0.025)
cap = bpy.context.active_object; cap.rotation_euler = way.to_track_quat("Z", "Y").to_euler()
finish(cap, "metal")
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.0101, depth=0.016, location=end + way * 0.046)
gr = bpy.context.active_object; gr.rotation_euler = way.to_track_quat("Z", "Y").to_euler()
finish(gr, "grille")
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=0.016, location=end + way * 0.062)
foam = bpy.context.active_object; foam.scale = (1.0, 1.0, 1.25); foam.rotation_euler = way.to_track_quat("Z", "Y").to_euler()
finish(foam, "foam")

# ---- the flag: a cube on the stand, its faces for the channel's name
bpy.ops.mesh.primitive_cube_add(size=0.04, location=(0, 0, 0.074))
flag = bpy.context.active_object
bev = flag.modifiers.new("bev", "BEVEL"); bev.width = 0.002; bev.segments = 2
bpy.ops.object.modifier_apply(modifier="bev")
finish(flag, "flag", smooth=False)

# ---- one object, written
bpy.ops.object.select_all(action="DESELECT")
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
mic = bpy.context.active_object
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
nv, nt = anchor_io.write(out_mesh, mic, [], "microphone")
print("microphone: %d vertices, %d triangles -> %s" % (nv, nt, out_mesh))

scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.render.resolution_x, scene.render.resolution_y = 600, 600
cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = 0.4
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
for label, d in (("side", Vector((1, 0, 0))), ("front", Vector((0, -1, 0))), ("three", Vector((0.7, -0.7, 0.35)).normalized())):
    target = Vector((0, -0.04, 0.15))
    cam.location = target + d * 2
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = preview.replace(".png", "_%s.png" % label)
    bpy.ops.render.render(write_still=True)
print("pictured")
