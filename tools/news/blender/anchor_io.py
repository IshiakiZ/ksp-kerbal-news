"""anchor_io.py: reading and writing Kerbal News' anchor parts between the game and Blender (2026-10-19).

The game writes each of the anchor's skinned parts (KerbalNews.News.Export, development build) as text, one file a part
(name.mesh): its bones (index, name, parent, local place, bind pose), its vertices as stored (bind pose: position, normal,
UV, four bone weights), its triangles by submesh with each submesh's material, and the same vertices as he sits. This
reads such a file into Blender as a mesh with a vertex group a bone, and writes a mesh made in Blender back in the same
form (the suit, riding on the body's own bones: the game gives it the body's bones and bind poses).

Unity's space is left-handed with y up; Blender's right-handed with z up. A point (x, y, z) in Unity is (x, z, y) here, and
back the same way; the swap mirrors, so each triangle's corners are taken in the other order both ways.

Used inside Blender (blender --background --python make_suit.py -- ...); importing bpy only there.
"""


def read(path):
    """The parsed part: dict with bones [(name, parent, local(10), bind(16))], verts [(p(3), n(3), t(2), w((i, w) x4))],
    submeshes [(index, material, [(a, b, c), ...])], seated [p(3)], toanchor (16)."""
    part = {"bones": [], "verts": [], "submeshes": [], "seated": [], "toanchor": None, "name": "", "root": ""}
    sub = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            w = line.split()
            if not w:
                continue
            k = w[0]
            if k == "part":
                part["name"] = " ".join(w[1:])
            elif k == "root":
                part["root"] = w[1]
            elif k == "bone":
                i, name, parent = int(w[1]), w[2], w[3]
                local, bind = None, None
                if "local" in w:
                    j = w.index("local")
                    local = [float(x) for x in w[j + 1:j + 11]]
                if "bind" in w:
                    j = w.index("bind")
                    bind = [float(x) for x in w[j + 1:j + 17]]
                part["bones"].append((name, parent, local, bind))
            elif k == "v":
                p = [float(x) for x in w[1:4]]
                n = t = None
                wt = []
                if "n" in w:
                    j = w.index("n"); n = [float(x) for x in w[j + 1:j + 4]]
                if "t" in w:
                    j = w.index("t"); t = [float(x) for x in w[j + 1:j + 3]]
                if "w" in w:
                    j = w.index("w"); q = w[j + 1:j + 9]
                    wt = [(int(q[0]), float(q[1])), (int(q[2]), float(q[3])), (int(q[4]), float(q[5])), (int(q[6]), float(q[7]))]
                part["verts"].append((p, n, t, wt))
            elif k == "submesh":
                sub = (int(w[1]), w[3] if len(w) > 3 else "-", [])
                part["submeshes"].append(sub)
            elif k == "f" and sub is not None:
                sub[2].append((int(w[1]), int(w[2]), int(w[3])))
            elif k == "s":
                part["seated"].append([float(x) for x in w[1:4]])
            elif k == "toanchor":
                part["toanchor"] = [float(x) for x in w[1:17]]
    return part


def to_blender(p):
    return (p[0], p[2], p[1])


def to_unity(p):
    return (p[0], p[2], p[1])


def build(part, name=None, seated=False):
    """The part as a Blender mesh object (bind pose, or as he sits), its UVs, a vertex group a bone, a material slot a submesh."""
    import bpy
    source = part["seated"] if seated and part["seated"] else [v[0] for v in part["verts"]]
    verts = [to_blender(p) for p in source]
    faces, mats = [], []
    for index, material, tris in part["submeshes"]:
        for a, b, c in tris:
            faces.append((a, c, b))          # (the mirror: the corners the other way round)
            mats.append(index)
    me = bpy.data.meshes.new(name or part["name"])
    me.from_pydata(verts, [], faces)
    me.update()
    if part["verts"] and part["verts"][0][2] is not None:
        uv = me.uv_layers.new(name="UVMap")
        for poly in me.polygons:
            for li in poly.loop_indices:
                vi = me.loops[li].vertex_index
                t = part["verts"][vi][2]
                uv.data[li].uv = (t[0], t[1])
    for poly, m in zip(me.polygons, mats):
        poly.material_index = m
    ob = bpy.data.objects.new(name or part["name"], me)
    bpy.context.scene.collection.objects.link(ob)
    for index, material, tris in part["submeshes"]:
        mat = bpy.data.materials.get(material) or bpy.data.materials.new(material)
        ob.data.materials.append(mat)
    groups = [ob.vertex_groups.new(name=b[0]) for b in part["bones"]]
    for vi, v in enumerate(part["verts"]):
        for bi, wt in v[3]:
            if wt > 0.0 and 0 <= bi < len(groups):
                groups[bi].add([vi], wt, "REPLACE")
    return ob


def write(path, ob, bones, name):
    """A mesh object made in Blender written in the game's form: its vertices (positions, normals, UVs), its four strongest
    bone weights by the body's bone order (`bones`: the names, in the body's order), its triangles by material slot."""
    import bpy, bmesh
    me = ob.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    me.calc_normals_split() if hasattr(me, "calc_normals_split") else None
    index = {n: i for i, n in enumerate(bones)}
    uv = me.uv_layers.active.data if me.uv_layers.active else None
    # (one vertex of the game's a corner of Blender's, where position, normal or UV differ: split by all three)
    out_v, key_of, tris = [], {}, {}
    for poly in me.polygons:
        corners = []
        for li in poly.loop_indices:
            loop = me.loops[li]
            vi = loop.vertex_index
            p = me.vertices[vi].co
            n = loop.normal
            t = uv[li].uv if uv else (0.0, 0.0)
            key = (vi, round(n[0], 4), round(n[1], 4), round(n[2], 4), round(t[0], 5), round(t[1], 5))
            if key not in key_of:
                ws = []
                for g in me.vertices[vi].groups:
                    gname = ob.vertex_groups[g.group].name
                    if gname in index and g.weight > 0.0:
                        ws.append((index[gname], g.weight))
                ws.sort(key=lambda x: -x[1]); ws = ws[:4]
                total = sum(w for _, w in ws) or 1.0
                ws = [(i, w / total) for i, w in ws] + [(0, 0.0)] * (4 - len(ws))
                key_of[key] = len(out_v)
                out_v.append((to_unity(p), to_unity(n), (t[0], t[1]), ws))
            corners.append(key_of[key])
        tris.setdefault(poly.material_index, []).append((corners[0], corners[2], corners[1]))
    with open(path, "w", encoding="utf-8") as f:
        f.write("part %s\n" % name)
        f.write("vertices %d\n" % len(out_v))
        for p, n, t, ws in out_v:
            f.write("v %r %r %r n %r %r %r t %r %r w %d %r %d %r %d %r %d %r\n" % (p + n + t + (ws[0][0], ws[0][1], ws[1][0], ws[1][1], ws[2][0], ws[2][1], ws[3][0], ws[3][1])))
        for m in sorted(tris):
            mat = ob.material_slots[m].material.name if m < len(ob.material_slots) and ob.material_slots[m].material else "-"
            f.write("submesh %d %d %s\n" % (m, len(tris[m]), mat.replace(" ", "_")))
            for a, b, c in tris[m]:
                f.write("f %d %d %d\n" % (a, b, c))
    ob.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    return len(out_v), sum(len(v) for v in tris.values())
