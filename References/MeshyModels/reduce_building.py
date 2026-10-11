"""Staged UV-seam-aware reduction of a raw Meshy building GLB with meshoptimizer.
usage: python reduce_building.py <src.glb> <out_dir> <tris> [<tris> ...]
  e.g. python reduce_building.py Song_Dynasty_20_Types_Alt/19_Xieshan_Public_Hall/19_Xieshan_Public_Hall.glb out 1000000 300000 100000
Each stage simplifies the previous one and writes <out_dir>/<name>_<tris>.glb (base colour copied alongside).
Then feed the chosen stage to blender_fix_building.py with TRIS=0 for cleanup/scale/FBX export.
needs: pip install numpy meshoptimizer  (0.2.30a0 wheel builds from source; C lib 0.22)
"""
import json, os, shutil, struct, sys, time
import ctypes as C
import numpy as np
from meshoptimizer._loader import lib

# the PyPI binding (0.2.30a0) never declares argtypes for this one, so floats fail to marshal; call the C API directly
_P = lambda t: C.POINTER(t)
lib.meshopt_simplifyWithAttributes.argtypes = [
    _P(C.c_uint), _P(C.c_uint), C.c_size_t, _P(C.c_float), C.c_size_t, C.c_size_t,
    _P(C.c_float), C.c_size_t, _P(C.c_float), C.c_size_t, _P(C.c_ubyte),
    C.c_size_t, C.c_float, C.c_uint, _P(C.c_float)]
lib.meshopt_simplifyWithAttributes.restype = C.c_size_t


def simplify_with_attributes(idx, pos, attrs, weights, target_tris):
    dst = np.zeros(len(idx), np.uint32)
    err = C.c_float(0)
    ptr = lambda a, t: a.ctypes.data_as(_P(t))
    n = lib.meshopt_simplifyWithAttributes(
        ptr(dst, C.c_uint), ptr(idx, C.c_uint), len(idx), ptr(pos, C.c_float), len(pos), 12,
        ptr(attrs, C.c_float), attrs.shape[1] * 4, ptr(weights, C.c_float), len(weights), None,
        target_tris * 3, 1.0, 0, C.byref(err))
    return dst[:n].copy(), err.value

UV_WEIGHT = float(os.environ.get("UV_WEIGHT", "1.0"))
NORMAL_WEIGHT = float(os.environ.get("NORMAL_WEIGHT", "0.25"))
SNAP = float(os.environ.get("SNAP", "1e-5"))  # fraction of bbox diagonal; merges Meshy's near-coincident seam verts

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def read_glb(path):
    with open(path, "rb") as f:
        f.read(12)
        ln, _ = struct.unpack("<II", f.read(8)); j = json.loads(f.read(ln))
        ln, _ = struct.unpack("<II", f.read(8)); bin_ = f.read(ln)

    def acc(i):
        a = j["accessors"][i]; bv = j["bufferViews"][a["bufferView"]]
        dt, n = CT[a["componentType"]], NC[a["type"]]
        off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = bv.get("byteStride", 0) or np.dtype(dt).itemsize * n
        raw = np.frombuffer(bin_, np.uint8, count=stride * (a["count"] - 1) + np.dtype(dt).itemsize * n, offset=off)
        if stride == np.dtype(dt).itemsize * n:
            return raw.view(dt).reshape(a["count"], n).copy()
        return np.lib.stride_tricks.as_strided(raw.view(dt), (a["count"], n), (stride, np.dtype(dt).itemsize)).copy()

    p = max((p for m in j["meshes"] for p in m["primitives"]), key=lambda p: j["accessors"][p["indices"]]["count"])
    at = p["attributes"]
    pos = acc(at["POSITION"])
    return (pos, acc(at["NORMAL"]), acc(at["TEXCOORD_0"]) if "TEXCOORD_0" in at else np.zeros((len(pos), 2), np.float32),
            acc(p["indices"]).reshape(-1).astype(np.uint32))


def write_glb(path, pos, nrm, uv, idx, tex_uri):
    pos, nrm, uv = (np.ascontiguousarray(a, np.float32) for a in (pos, nrm, uv))
    idx = np.ascontiguousarray(idx, np.uint32)
    blobs, views, accs = [], [], []
    off = 0
    for arr, typ, target, ct in ((pos, "VEC3", 34962, 5126), (nrm, "VEC3", 34962, 5126),
                                 (uv, "VEC2", 34962, 5126), (idx, "SCALAR", 34963, 5125)):
        b = arr.tobytes(); blobs.append(b)
        views.append({"buffer": 0, "byteOffset": off, "byteLength": len(b), "target": target})
        a = {"bufferView": len(views) - 1, "componentType": ct, "count": len(arr), "type": typ}
        if arr is pos:
            a["min"] = pos.min(0).tolist(); a["max"] = pos.max(0).tolist()
        accs.append(a)
        off += len(b)
    j = {"asset": {"version": "2.0", "generator": "reduce_building.py"},
         "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0, "name": "Mesh_0"}],
         "meshes": [{"name": "Mesh_0", "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2},
                                                       "indices": 3, "material": 0}]}],
         "materials": [{"name": "Material_0", "pbrMetallicRoughness": {"baseColorTexture": {"index": 0},
                                                                       "metallicFactor": 0, "roughnessFactor": 1}}],
         "textures": [{"source": 0}], "images": [{"uri": tex_uri}],
         "buffers": [{"byteLength": off}], "bufferViews": views, "accessors": accs}
    js = json.dumps(j).encode(); js += b" " * (-len(js) % 4)
    bn = b"".join(blobs); bn += b"\0" * (-len(bn) % 4)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 28 + len(js) + len(bn)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack("<II", len(bn), 0x004E4942)); f.write(bn)


def compact(pos, nrm, uv, idx):
    used, inv = np.unique(idx, return_inverse=True)
    return pos[used], nrm[used], uv[used], inv.astype(np.uint32)


def main():
    src, out = sys.argv[1], sys.argv[2]
    stages = [int(t) for t in sys.argv[3:]]
    name = os.path.splitext(os.path.basename(src))[0]
    os.makedirs(out, exist_ok=True)
    tex = f"{name}_BaseColor.png"
    shutil.copy(os.path.join(os.path.dirname(src), "textures", "texture_0_base_color.png"), os.path.join(out, tex))

    t = time.time()
    pos, nrm, uv, idx = read_glb(src)
    print(f"read {len(idx)//3:,} tris {len(pos):,} verts in {time.time()-t:.1f}s", flush=True)

    # snap positions so near-coincident split verts become exactly coincident: meshopt then sees them
    # as one position with an attribute seam (collapses along it only) instead of an open border crack
    diag = float(np.linalg.norm(pos.max(0) - pos.min(0)))
    q = SNAP * diag
    pos = (np.round(pos / q) * q).astype(np.float32)
    # drop exact duplicates (same pos+normal+uv) and triangles degenerated by the snap
    key = np.ascontiguousarray(np.hstack([pos, nrm, uv]).astype(np.float32)).view(np.dtype((np.void, 32))).ravel()
    _, first, inv = np.unique(key, return_index=True, return_inverse=True)
    pos, nrm, uv, idx = pos[first], nrm[first], uv[first], inv.astype(np.uint32)[idx]
    pk = np.ascontiguousarray(pos).view(np.dtype((np.void, 12))).ravel()
    _, pid = np.unique(pk, return_inverse=True)
    tri = pid[idx].reshape(-1, 3)
    keep = (tri[:, 0] != tri[:, 1]) & (tri[:, 1] != tri[:, 2]) & (tri[:, 0] != tri[:, 2])
    idx = idx.reshape(-1, 3)[keep].reshape(-1)
    pos, nrm, uv, idx = compact(pos, nrm, uv, idx)
    print(f"dedup: {len(idx)//3:,} tris {len(pos):,} verts ({len(np.unique(pid)):,} unique positions)", flush=True)

    weights = np.array([UV_WEIGHT, UV_WEIGHT, NORMAL_WEIGHT, NORMAL_WEIGHT, NORMAL_WEIGHT], np.float32)
    for tris in stages:
        t = time.time()
        attrs = np.ascontiguousarray(np.hstack([uv, nrm]), np.float32)
        idx, err = simplify_with_attributes(np.ascontiguousarray(idx), np.ascontiguousarray(pos), attrs, weights, tris)
        pos, nrm, uv, idx = compact(pos, nrm, uv, idx)
        p = os.path.join(out, f"{name}_{tris}.glb")
        write_glb(p, pos, nrm, uv, idx, tex)
        print(f"stage {tris:,}: {len(idx)//3:,} tris {len(pos):,} verts err={err:.5f} (relative to bbox) "
              f"{time.time()-t:.1f}s -> {p}", flush=True)


if __name__ == "__main__":
    main()
