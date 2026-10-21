"""make_suit.py: Kerbal News' anchor in a suit, made from the game's own kerbal body (2026-10-19; the owner: "can you actually
make a suit model for him").

  blender --background --python tools/news/blender/make_suit.py -- EXPORT_FOLDER OUT.mesh PREVIEW.png

The body the game exported (body01.mesh, KerbalNews.News.Export) is the spacesuit: a flat ring round the neck, ridges and pockets,
gloves and boots. The suit is cut from that same surface, so that every piece rides on the body's own bones with the body's own
weights: the ring taken off; the spacesuit's ridges smoothed away; the jacket and trousers a dark navy, the shoes black, the hands
bare (a kerbal's green); at the chest the jacket open in a V over a white shirt and a red tie, its lapels' edges dark; a shirt
collar round the neck where the ring was, and two buttons. Written in the game's form (anchor_io.write), one submesh a material:
jacket, shirt, tie, lapel, shoe, skin, button.
"""
import sys, os, math
import bpy, bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import anchor_io

argv = sys.argv[sys.argv.index("--") + 1:]
folder, out_mesh, preview = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
part = anchor_io.read(os.path.join(folder, "body01.mesh"))
bones = [b[0] for b in part["bones"]]
ob = anchor_io.build(part, "suit", seated=False)
me = ob.data

# ---- each vertex's strongest bone, and its family
def family(name):
    n = name.lower()
    if "helmet" in n: return "ring"
    if any(k in n for k in ("wrist", "mid_", "thumb")): return "hand"
    if any(k in n for k in ("foot", "ball")): return "foot"
    if any(k in n for k in ("hip", "knee")): return "leg"
    if any(k in n for k in ("shld", "arm", "elbow")): return "arm"
    return "body"

strongest = []
for v in part["verts"]:
    bi, w = max(v[3], key=lambda bw: bw[1]) if v[3] else (0, 1.0)
    strongest.append(family(bones[bi]) if 0 <= bi < len(bones) else "body")

# ---- the materials, one a submesh, in this order
names = ["jacket", "shirt", "tie", "lapel", "shoe", "skin", "button"]
colours = {"jacket": (0.075, 0.095, 0.19, 1), "shirt": (0.92, 0.93, 0.95, 1), "tie": (0.55, 0.05, 0.07, 1), "lapel": (0.05, 0.06, 0.12, 1),
           "shoe": (0.03, 0.03, 0.03, 1), "skin": (0.52, 0.72, 0.22, 1), "button": (0.02, 0.02, 0.03, 1)}
me.materials.clear()
for n in names:
    m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
    m.diffuse_color = colours[n]
    me.materials.append(m)
slot = {n: i for i, n in enumerate(names)}

# ---- where things are: the body's front (+y in Blender: the game's +z), its middle (x 0), its height (z)
co = [v.co.copy() for v in me.vertices]
torso = [co[i] for i in range(len(co)) if strongest[i] == "body"]
top = max(p.z for p in torso); bottom = min(p.z for p in torso)
front = max(p.y for p in torso)
height = top - bottom
print("torso from %.3f to %.3f, front at %.3f" % (bottom, top, front))

# ---- how much each vertex is smoothed, by its family (a vertex group, which survives the cutting below; the indices change)
smooth = ob.vertex_groups.new(name="smooth")
for i, fam in enumerate(strongest):
    smooth.add([i], {"body": 1.0, "arm": 0.9, "leg": 0.8, "hand": 0.35, "foot": 0.25, "ring": 0.0}.get(fam, 0.5), "REPLACE")

bm = bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()

# ---- materials by family and, on the chest, by place: the V of the jacket's opening (from the collar down to two fifths of the
# torso's height, wide at the top), the tie down its middle, the lapels its edges
def v_half_width(z):
    t = (top - z) / max(height, 1e-6)            # 0 at the top of the torso, 1 at its bottom
    return max(0.0, 0.045 * (1.0 - t / 0.42))
# (where the hands begin, each side: the shirt's cuffs show a little beyond the sleeves there)
hand_x = [abs(co[i].x) for i in range(len(co)) if strongest[i] == "hand"]
wrist = min(hand_x) if hand_x else 1e9
for f in bm.faces:
    fams = [strongest[v.index] for v in f.verts]
    fam = max(set(fams), key=fams.count)
    c = f.calc_center_median()
    m = "jacket"
    if fam == "arm" and abs(c.x) > wrist - 0.016: m = "shirt"
    elif fam == "hand": m = "skin"
    elif fam == "foot": m = "shoe"
    elif fam in ("body", "leg", "arm"):
        m = "jacket"
        if fam == "body" and f.normal.y > 0.25 and c.z > top - 0.42 * height:
            hw = v_half_width(c.z)
            if abs(c.x) < hw:
                m = "tie" if abs(c.x) < 0.007 + 0.005 * (top - c.z) / (0.42 * height) and c.z < top - 0.03 * height else "shirt"
            elif abs(c.x) < hw + 0.012:
                m = "lapel"
    f.material_index = slot[m]

# ---- the ring off (after the materials: deleting renumbers the vertices)
ring = [f for f in bm.faces if sum(1 for v in f.verts if strongest[v.index] == "ring") >= 2]
bmesh.ops.delete(bm, geom=ring, context="FACES")
# (and the seams' doubles welded: the game's mesh splits its vertices where its UVs or shading break, and smoothed apart they
# opened into slits; Blender keeps the UVs a corner, so welding loses nothing)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
for f in bm.faces: f.smooth = True
bm.to_mesh(me); bm.free()

# ---- the spacesuit's ridges smoothed away (not the hands' fingers or the shoes' soles: a little only there)
mod = ob.modifiers.new("smooth", "LAPLACIANSMOOTH")
mod.vertex_group = "smooth"; mod.lambda_factor = 0.9; mod.iterations = 8; mod.use_volume_preserve = True; mod.use_normalized = True
bpy.context.view_layer.objects.active = ob
bpy.ops.object.modifier_apply(modifier="smooth")
ob.vertex_groups.remove(ob.vertex_groups["smooth"])

# ---- the collar: a short white band round the neck where the ring was, riding on the top of the spine
neck = [co[i] for i in range(len(co)) if strongest[i] == "ring"]
cx = sum(p.x for p in neck) / max(len(neck), 1); cy = sum(p.y for p in neck) / max(len(neck), 1)
cz = top
r = 0.045
bm = bmesh.new()
segs = 20
ring_lo, ring_hi = [], []
for k in range(segs):
    a = 2 * math.pi * k / segs
    lean = 0.012 * (1 if math.sin(a) > 0 else 0) * math.sin(a)       # (higher at the back, as a collar sits)
    ring_lo.append(bm.verts.new((cx + r * math.cos(a), cy + r * math.sin(a), cz - 0.006)))
    ring_hi.append(bm.verts.new((cx + (r - 0.004) * math.cos(a), cy + (r - 0.004) * math.sin(a), cz + 0.014 - lean)))
for k in range(segs):
    k2 = (k + 1) % segs
    # (the collar's opening at the front, where the tie's knot is)
    a = 2 * math.pi * (k + 0.5) / segs
    if math.sin(a) > 0.93: continue
    bm.faces.new((ring_lo[k], ring_lo[k2], ring_hi[k2], ring_hi[k]))
collar_me = bpy.data.meshes.new("collar"); bm.to_mesh(collar_me); bm.free()
collar = bpy.data.objects.new("collar", collar_me); scene.collection.objects.link(collar)
collar_me.materials.append(bpy.data.materials["shirt"])

# ---- the tie's knot and two buttons, small, on the chest: where a ray from in front meets the smoothed surface
def on_front(x, z, proud):
    hit, loc, nor, idx = ob.ray_cast(Vector((x, 1.0, z)), Vector((0.0, -1.0, 0.0)))
    return (loc.x, loc.y + proud, loc.z) if hit else (x, front, z)

def blob(name, centre, size, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=size, location=centre)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(bpy.data.materials[material])
    return o
bpy.context.view_layer.update()
knot = blob("knot", on_front(cx, top - 0.05 * height, 0.002), 0.009, "tie")
knot.scale = (1.0, 0.6, 1.2)
buttons = [blob("button%d" % k, on_front(0.0, top - (0.52 + 0.11 * k) * height, 0.0015), 0.004, "button") for k in range(2)]
for o in [knot] + buttons:
    for f in o.data.polygons: f.use_smooth = True

# ---- the small pieces ride on the spine's top bone; joined to the suit
spine_top = "bn_spD01" if "bn_spD01" in bones else bones[0]
for o in [collar, knot] + buttons:
    g = o.vertex_groups.new(name=spine_top)
    g.add([v.index for v in o.data.vertices], 1.0, "REPLACE")
    for mi, mat in enumerate(o.data.materials):
        pass
bpy.ops.object.select_all(action="DESELECT")
for o in [collar, knot] + buttons: o.select_set(True)
ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.join()
ob = bpy.context.active_object

# (the material slots after the join are the suit's own and the pieces' (shirt, tie, button), merged by name)
nv, nt = anchor_io.write(out_mesh, ob, bones, "suit")
print("suit: %d vertices, %d triangles -> %s" % (nv, nt, out_mesh))

# ---- a picture of it, front and side, as the material colours
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.render.resolution_x, scene.render.resolution_y = 900, 600
cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = 0.95
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
for label, d in (("front", Vector((0, 1, 0))), ("side", Vector((1, 0, 0)))):
    cam.location = Vector((0, 0, 0.21)) + d * 2
    cam.rotation_euler = (math.pi / 2, 0, math.atan2(d.x, -d.y))
    scene.render.filepath = preview.replace(".png", "_%s.png" % label)
    bpy.ops.render.render(write_still=True)
print("pictured")
