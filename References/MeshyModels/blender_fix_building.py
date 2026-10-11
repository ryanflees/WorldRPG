"""Clean a Meshy building and export an FBX for Unity.
usage: blender -b --factory-startup -P fix.py -- <src> <out.fbx> <target_height_m> <dry|apply> [decimate_tris] [preview.png]
"""
import bpy, bmesh, sys, json, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out, height, mode = a[0], a[1], float(a[2]), a[3]
dec_tris = int(a[4]) if len(a) > 4 else 0
preview = a[5] if len(a) > 5 else ""
name = out.replace("\\", "/").split("/")[-1].rsplit(".", 1)[0]
report = {"name": name}

bpy.ops.wm.read_factory_settings(use_empty=True)
if src.endswith(".fbx"):
    bpy.ops.import_scene.fbx(filepath=src)
else:
    bpy.ops.import_scene.gltf(filepath=src)

# 1. keep only the textured building mesh (Meshy sometimes adds junk icospheres / armatures)
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.data.materials]
keep = max(meshes, key=lambda o: len(o.data.polygons))
removed = [o.name for o in bpy.data.objects if o is not keep]
for o in list(bpy.data.objects):
    if o is not keep:
        bpy.data.objects.remove(o, do_unlink=True)
report["removed_objects"] = removed
obj = keep
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 2. floating debris: islands by face adjacency + coincident vertex positions (UV seams split verts)
me = obj.data
bm = bmesh.new(); bm.from_mesh(me)
# weld split verts so decimation does not tear cracks along them (UVs live on loops, so seams survive)
n0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
report["welded_verts"] = n0 - len(bm.verts)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
parent = list(range(len(bm.verts)))
def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]; i = parent[i]
    return i
def union(i, j):
    ri, rj = find(i), find(j)
    if ri != rj: parent[ri] = rj
pos = {}
for v in bm.verts:
    k = (round(v.co.x, 4), round(v.co.y, 4), round(v.co.z, 4))
    if k in pos: union(v.index, pos[k])
    else: pos[k] = v.index
for f in bm.faces:
    vs = f.verts
    for v in vs[1:]: union(vs[0].index, v.index)
islands = {}
for f in bm.faces:
    islands.setdefault(find(f.verts[0].index), []).append(f)
allmin = Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
allmax = Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
diag = (allmax - allmin).length
largest = max(len(fs) for fs in islands.values())
junk = []
for fs in islands.values():
    vs = {v for f in fs for v in f.verts}
    mn = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
    mx = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
    if len(fs) < 0.002 * largest and (mx - mn).length < 0.04 * diag:
        junk.append(fs)
report["islands"] = len(islands)
report["junk_islands"] = len(junk)
report["junk_faces"] = sum(len(fs) for fs in junk)
if mode == "apply" and junk:
    bmesh.ops.delete(bm, geom=[f for fs in junk for f in fs], context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(me); bm.free()

# 3. optional decimation (raw Meshy output without remesh is ~5M tris)
report["tris_before"] = sum(len(p.vertices) - 2 for p in me.polygons)
if mode == "apply" and dec_tris and report["tris_before"] > dec_tris:
    m = obj.modifiers.new("dec", 'DECIMATE')
    m.ratio = dec_tris / report["tris_before"]
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
report["tris_after"] = sum(len(p.vertices) - 2 for p in me.polygons)
# hard edges on rafters/walls, smooth on curved roof tiles
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))

# 4. real-world scale, origin at bottom centre
co = [v.co for v in me.vertices]
mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
s = height / (mx.z - mn.z)
off = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
for v in me.vertices:
    v.co = (v.co - off) * s
me.update()
report["size_m"] = [round((mx.x - mn.x) * s, 2), round((mx.y - mn.y) * s, 2), round(height, 2)]

obj.name = name; me.name = name
obj.data.materials[0].name = name + "_Mat"
for i in range(1, len(obj.data.materials)):
    obj.data.materials.pop(index=1)

if mode == "apply":
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF',
        use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False,
        add_leaf_bones=False, bake_anim=False)

report["verts_after"] = len(me.vertices)

if preview:
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = sc.render.resolution_y = 1024
    sc.render.film_transparent = True
    w, d, h = report["size_m"]
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = max(w, d, h) * 1.15
    # 3/4 view from the front (-Y) left
    cam.location = (-max(w, d) * 1.5, -max(w, d) * 2.0, h * 1.6)
    look = Vector((0, 0, h * 0.45)) - cam.location
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sc.render.filepath = preview
    bpy.ops.render.render(write_still=True)

print("REPORT " + json.dumps(report))
