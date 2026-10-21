"""look.py: the anchor's exported parts built in Blender as he sits, and pictured from the front and the side (Workbench), to see the
export and the import are right before a suit is made on him.

  blender --background --python tools/news/blender/look.py -- EXPORT_FOLDER OUT.png [part ...]
"""
import sys, os, math
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import anchor_io

argv = sys.argv[sys.argv.index("--") + 1:]
folder, out = argv[0], argv[1]
parts = argv[2:] or ["body01", "headMesh01", "eyeballLeft", "eyeballRight", "pupilLeft", "pupilRight", "upTeeth01", "upTeeth02"]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
obs = []
for name in parts:
    path = os.path.join(folder, name + ".mesh")
    if not os.path.exists(path):
        continue
    part = anchor_io.read(path)
    ob = anchor_io.build(part, name, seated=True)
    # (each part in the anchor's own space: its "toanchor" matrix, Unity's, turned into Blender's axes)
    m = part["toanchor"]
    if m:
        # Unity's column-major 4x4 to rows; then the axis swap on both sides (y and z)
        rows = [[m[c * 4 + r] for c in range(4)] for r in range(4)]
        swap = [0, 2, 1, 3]
        rows = [[rows[swap[r]][swap[c]] for c in range(4)] for r in range(4)]
        from mathutils import Matrix
        ob.matrix_world = Matrix(rows)
    obs.append(ob)

# (how big he is, to place the camera)
lo = Vector((1e9, 1e9, 1e9)); hi = -lo
for ob in obs:
    for c in ob.bound_box:
        p = ob.matrix_world @ Vector(c)
        lo = Vector((min(lo[i], p[i]) for i in range(3))); hi = Vector((max(hi[i], p[i]) for i in range(3)))
mid, size = (lo + hi) / 2, max(hi - lo)
print("bounds", tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi))

scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "OBJECT"
scene.render.resolution_x, scene.render.resolution_y = 800, 800
for ob, colour in zip(obs, [(0.25, 0.3, 0.45, 1)] + [(0.85, 0.85, 0.6, 1)] * 20):
    ob.color = colour
cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = size * 1.15
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
views = []
for i, (label, direction) in enumerate((("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0))), ("back", Vector((0, 1, 0))))):
    cam.location = mid + direction * size * 3
    cam.rotation_euler = (math.pi / 2, 0, math.atan2(direction.x, -direction.y))
    path = out.replace(".png", "_%s.png" % label)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    views.append(path)
print("rendered", views)
