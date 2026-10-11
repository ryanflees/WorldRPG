"""Laplacian-smooth a simplified building (evens out lumpy Meshy roof tiles / beams without shrinking them).
usage: blender -b --factory-startup -P smooth_building.py -- <in.glb> <out.glb> [lambda=0.5] [iterations=1]
"Laplacian light" (0.5 x1) was picked on 19; stronger (1.0 x2) melts eave brackets, Corrective Smooth smears detail.
"""
import bpy, sys, os, math

a = sys.argv[sys.argv.index("--") + 1:]
src, dst = os.path.abspath(a[0]), os.path.abspath(a[1])
lam = float(a[2]) if len(a) > 2 else 0.5
iters = int(a[3]) if len(a) > 3 else 1

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src, merge_vertices=True)
o = next(x for x in bpy.data.objects if x.type == 'MESH')
bpy.context.view_layer.objects.active = o
o.select_set(True)
m = o.modifiers.new("smooth", 'LAPLACIANSMOOTH')
m.lambda_factor = lam
m.iterations = iters
m.use_volume_preserve = True
m.use_normalized = True
m.lambda_border = 0.0
bpy.ops.object.modifier_apply(modifier="smooth")
if o.data.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=True,
                          export_image_format='NONE', export_materials='NONE')
print(f"SMOOTH lambda {lam} x{iters}: {len(o.data.polygons):,} faces -> {dst}")
