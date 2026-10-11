"""Re-unwrap a simplified Meshy building and bake base colour + tangent normals from the raw high-poly.
Meshy's UV atlas is shattered into millions of fragments, so seam-preserving decimation stalls (~900k tris on 19);
instead simplify positions only and bake onto fresh UVs (KEEP_UV=1 keeps the low's own UVs, e.g. from xatlas;
otherwise Blender Smart UV, which shatters into confetti on decimated meshes).
usage: [KEEP_UV=1] blender -b --factory-startup -P bake_building.py -- <high.glb> <low.glb> <out_dir> <name> [tex_size=4096]
writes <out_dir>/<name>.glb (feed to blender_fix_building.py with TRIS=0), <name>_BaseColor.png, <name>_Normal.png
"""
import bpy, sys, math, os, time

a = sys.argv[sys.argv.index("--") + 1:]
high_path, low_path, out, name = a[:4]
out = os.path.abspath(out)  # Image.save() resolves relative paths against the (unsaved) blend, not cwd
size = int(a[4]) if len(a) > 4 else 4096
os.makedirs(out, exist_ok=True)
t0 = time.time()

bpy.ops.wm.read_factory_settings(use_empty=True)


def import_mesh(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    mesh = max((o for o in new if o.type == 'MESH'), key=lambda o: len(o.data.polygons))
    for o in new:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    mesh.modifiers.clear()  # drop Meshy's armature deform
    mesh.parent = None
    return mesh


high = import_mesh(high_path)
low = import_mesh(low_path)
print(f"BAKE imported high {len(high.data.polygons):,} low {len(low.data.polygons):,} faces ({time.time()-t0:.0f}s)", flush=True)

# fresh UVs on the low-poly; same 35 deg smoothing blender_fix_building.py applies, so the tangent bake matches
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = low
low.select_set(True)
if os.environ.get("KEEP_UV") != "1":
    while low.data.uv_layers:
        low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=0.002)
    bpy.ops.object.mode_set(mode='OBJECT')
if low.data.has_custom_normals:  # imported GLB normals; recompute so the bake matches the exported shading
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
print(f"BAKE unwrapped ({time.time()-t0:.0f}s)", flush=True)

# low material with bake targets
mat = bpy.data.materials.new(name + "_Mat")
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
imgs = {}
for kind, cs in (("BaseColor", "sRGB"), ("Normal", "Non-Color")):
    img = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = cs
    node = nt.nodes.new("ShaderNodeTexImage"); node.image = img; node.name = kind
    imgs[kind] = (img, node)
nt.links.new(imgs["BaseColor"][1].outputs["Color"], bsdf.inputs["Base Color"])
low.data.materials.clear()
low.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'
prefs = bpy.context.preferences.addons['cycles'].preferences
for dev in ('OPTIX', 'CUDA'):
    try:
        prefs.compute_device_type = dev; prefs.get_devices()
        for d in prefs.devices: d.use = d.type != 'CPU'
        sc.cycles.device = 'GPU'
        break
    except Exception:
        pass
sc.cycles.samples = 8
bk = sc.render.bake
bk.use_selected_to_active = True
diag = max(low.dimensions)
bk.cage_extrusion = diag * 0.003   # simplification error is ~0.15% of the bbox diagonal
bk.max_ray_distance = diag * 0.01
bk.margin = 16
bk.margin_type = 'EXTEND'

bpy.ops.object.select_all(action='DESELECT')
high.select_set(True); low.select_set(True)
bpy.context.view_layer.objects.active = low

for kind, args in (("BaseColor", dict(type='DIFFUSE', pass_filter={'COLOR'})),
                   ("Normal", dict(type='NORMAL', normal_space='TANGENT'))):
    img, node = imgs[kind]
    nt.nodes.active = node
    bpy.ops.object.bake(**args)
    img.filepath_raw = os.path.join(out, f"{name}_{kind}.png")
    img.file_format = 'PNG'
    img.save()
    print(f"BAKE {kind} done ({time.time()-t0:.0f}s)", flush=True)

# normal map hookup so the GLB carries it (Unity material gets it via the importer/showcase)
nm = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(imgs["Normal"][1].outputs["Color"], nm.inputs["Color"])
nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])

bpy.data.objects.remove(high, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
low.name = name
bpy.ops.export_scene.gltf(filepath=os.path.join(out, name + ".glb"), export_format='GLB', use_selection=True,
                          export_image_format='AUTO')  # embedded so previews/Blender re-imports show the bake
print(f"BAKE exported {os.path.join(out, name + '.glb')} ({time.time()-t0:.0f}s)", flush=True)
