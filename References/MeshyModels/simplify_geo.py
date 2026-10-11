"""Position-only meshoptimizer simplification of a raw Meshy building GLB (UVs discarded; rebaked later).
usage: python simplify_geo.py <src.glb> <out.glb> <tris>
Welds the UV-split vertices so the shattered Meshy atlas no longer blocks collapses (seam-aware decimation stalls ~900k).
"""
import sys, time
import numpy as np
import meshoptimizer as mo
from reduce_building import read_glb, write_glb

src, dst, tris = sys.argv[1], sys.argv[2], int(sys.argv[3])
t = time.time()
p, n, _, i = read_glb(src)
up, first, pid = np.unique(p, axis=0, return_index=True, return_inverse=True)
idx = pid.ravel().astype(np.uint32)[i]
pos, nrm = np.ascontiguousarray(up, np.float32), n[first]
out = np.zeros(len(idx), np.uint32)
err = np.zeros(1, np.float32)
k = mo.simplify(out, idx, pos, target_index_count=tris * 3, target_error=1.0, result_error=err)
used, inv = np.unique(out[:k], return_inverse=True)
write_glb(dst, pos[used], nrm[used], np.zeros((len(used), 2), np.float32), inv.ravel().astype(np.uint32), "none.png")
print(f"GEO {len(i)//3:,} -> {k//3:,} tris, {len(used):,} verts, error {err[0]*100:.3f}% of bbox ({time.time()-t:.0f}s)")
