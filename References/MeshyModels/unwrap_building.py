"""Fresh UV atlas for a simplified building with xatlas (Blender Smart UV shatters decimated meshes into confetti).
usage: python unwrap_building.py <in.glb> <out.glb> [tex_size=4096] [chunk_tris=10000]
xatlas charting is ~O(n^2.4) on one connected mesh (500k tris ~6 h), so the mesh is median-split into spatial
chunks that xatlas charts in parallel and packs into one atlas (100k tris: 487 s -> 4.5 s, same chart count).
"""
import sys, time
import numpy as np
import xatlas
from reduce_building import read_glb, write_glb

src, dst = sys.argv[1], sys.argv[2]
size = int(sys.argv[3]) if len(sys.argv) > 3 else 4096
chunk_tris = int(sys.argv[4]) if len(sys.argv) > 4 else 10000
p, n, _, i = read_glb(src)
t = time.time()
tris = i.reshape(-1, 3)
cen = p[tris].mean(1)
groups = [np.arange(len(tris))]
while max(len(g) for g in groups) > chunk_tris:
    g = groups.pop(max(range(len(groups)), key=lambda k: len(groups[k])))
    order = np.argsort(cen[g, np.argmax(np.ptp(cen[g], 0))])
    groups += [g[order[:len(g) // 2]], g[order[len(g) // 2:]]]

atlas = xatlas.Atlas()
parts = []
for g in groups:
    used, inv = np.unique(tris[g], return_inverse=True)
    parts.append(used)
    atlas.add_mesh(p[used], inv.reshape(-1, 3).astype(np.uint32), n[used])
co = xatlas.ChartOptions()
co.max_iterations = 2
co.normal_deviation_weight = 1.0
po = xatlas.PackOptions()
po.resolution = size
po.padding = 4
po.bilinear = True
po.rotate_charts = True
po.max_chart_size = size // 4  # a failed parametrisation once made a 370k-texel strip (09) and squashed the atlas
atlas.generate(co, po, verbose=False)
# UVs are normalised to the atlas, so only a lopsided atlas (or a second page) distorts the texture
if atlas.atlas_count != 1 or max(atlas.width, atlas.height) > 1.1 * min(atlas.width, atlas.height):
    sys.exit(f"UNWRAP FAILED: {atlas.atlas_count} atlases, {atlas.width}x{atlas.height}")

vs, idxs, uvs, base = [], [], [], 0
for k, used in enumerate(parts):
    vmap, idx, uv = atlas[k]
    vs.append(used[vmap]); uvs.append(uv); idxs.append(idx.reshape(-1) + base)
    base += len(vmap)
v = np.concatenate(vs)
write_glb(dst, p[v], n[v], np.concatenate(uvs).astype(np.float32), np.concatenate(idxs).astype(np.uint32), "none.png")
print(f"UNWRAP {len(groups)} chunks, {atlas.chart_count:,} charts, {len(v):,} verts, "
      f"{atlas.utilization*100:.0f}% utilisation ({time.time()-t:.0f}s)")
