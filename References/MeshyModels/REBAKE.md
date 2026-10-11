# Rebaking Meshy models for Unity (GPU mesh reduction)

How to turn a raw Meshy model (3–8M triangles) into a game-ready mesh (300k–500k triangles)
with fresh UVs, a baked base colour and a baked normal map, then get it into the Unity project.
One building takes about 1–2 minutes. Written so an agent can run it unattended.

## Why rebake instead of decimate

Meshy's texture layout (UV atlas) is shattered into millions of tiny fragments: on
19_Xieshan_Public_Hall, 6.5M vertex copies sit on only 3.8M positions. Every tool that tries to
keep those UVs fails:

| Tried | Result |
|---|---|
| Blender Decimate | Tears the UV seams, so the texture smears across triangles |
| PolyFew in Unity | Ran all night on 19 without finishing |
| meshoptimizer, seam-aware (incl. gltfpack `-sp`) | Stalls at about 900k triangles |
| Blender Smart UV Project on the reduced mesh | Confetti atlas (tens of thousands of tiny islands) |
| xatlas on the whole mesh in one piece | Correct but O(n^2.4): about 6 hours for 500k triangles |

So the pipeline throws the Meshy UVs away, reduces the shape only, makes new UVs, and bakes the
original colours and surface detail onto them.

## Pipeline

`rebake_building.sh` runs these steps per model (working files go to `$WORK/<id>/`):

| # | Script | Runs on | What it does | Time |
|---|---|---|---|---|
| 1 | `simplify_geo.py` | CPU | Welds the UV-split vertices, then meshoptimizer position-only simplification to `TRIS` | ~10 s |
| 2 | `smooth_building.py` | CPU (Blender) | Laplacian smooth, lambda 0.5 × 1 iteration, volume-preserving, then 35° smooth-by-angle shading | ~10 s |
| 3 | `unwrap_building.py` | CPU, 24 threads | xatlas unwrap on ~10k-triangle spatial chunks in parallel, packed into one atlas | ~10 s |
| 4 | `bake_building.py` | **GPU** (Cycles OptiX) | Bakes base colour and tangent-space normals from the raw model onto the new UVs | ~30–60 s |
| 5 | `blender_fix_building.py` | CPU (Blender) | Removes floating debris, scales to real height, origin at bottom centre, exports FBX | ~5 s |

The output is written straight into the Unity project:

```
RPG/Assets/Art/Buildings/SongDynasty/<id>/<id>.fbx
RPG/Assets/Art/Buildings/SongDynasty/<id>/Textures/<id>_BaseColor.png   (4096, sRGB)
RPG/Assets/Art/Buildings/SongDynasty/<id>/Textures/<id>_Normal.png      (4096, tangent space)
```

An existing `<id>` folder is overwritten in place, so its `.meta` GUIDs, materials, prefabs and
scenes stay linked.

## One-time setup

1. **Blender 5.2+** at `C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe`
   (path is set at the top of `rebake_building.sh`). An NVIDIA GPU is used automatically through
   OptiX, falling back to CUDA. The `HIPEW initialization failed` warning is harmless.
2. **Python environment** with numpy, meshoptimizer and xatlas:

   ```bash
   python -m venv References/MeshyModels/.venv-rebake
   References/MeshyModels/.venv-rebake/Scripts/pip install -r References/MeshyModels/requirements-rebake.txt
   ```

   meshoptimizer compiles from source on install, so the MSVC Build Tools must be present.
   Its Python binding (0.2.30a0) is missing the ctypes declaration for `simplifyWithAttributes`;
   only `reduce_building.py` uses that call and it patches it itself.
3. Run everything from **Git Bash** (the scripts are POSIX `sh`).

## Input layout

```
References/MeshyModels/<SET>/<id>/<id>.glb        raw Meshy download (texture embedded in the GLB)
References/MeshyModels/<SET>/building_heights.txt  optional, one "<id> <height_m>" per line
```

Each model needs a real-world height in metres, taken from `building_heights.txt` or given with
`HEIGHT=`. Without one the script stops with an error. The GLB must be complete. Check it with:

```bash
python -c "import os,struct,sys;f=open(sys.argv[1],'rb');h=f.read(12);print(struct.unpack('<I',h[8:])[0]==os.path.getsize(sys.argv[1]))" <file.glb>
```

## Running it

One or more models from the default set (`Song_Dynasty_20_Types_Alt`):

```bash
cd References/MeshyModels
PY=.venv-rebake/Scripts/python TRIS=300000 sh rebake_building.sh 08_Cloth_Dye_Shop 09_Apothecary
```

A different set, a single height, and another Unity folder:

```bash
PY=.venv-rebake/Scripts/python SET=Song_Dynasty_10_Street_Stalls HEIGHT=2.5 TRIS=150000 \
  OUT=../../RPG/Assets/Art/Stalls sh rebake_building.sh 05_Steamed_Bun_Cart
```

The street stalls (`Song_Dynasty_10_Street_Stalls`, heights in its `building_heights.txt`) were
done at 150k triangles into `RPG/Assets/Art/Buildings/Stalls`, which keeps them under the
`Assets/Art/Buildings/` import rules:

```bash
ls -d Song_Dynasty_10_Street_Stalls/*/ | xargs -n1 basename | xargs -P 3 -I{}   sh -c 'PY=.venv-rebake/Scripts/python SET=Song_Dynasty_10_Street_Stalls TRIS=150000 OUT=../../RPG/Assets/Art/Buildings/Stalls sh rebake_building.sh {} > logs/{}.log 2>&1 && echo "DONE {}" || echo "FAIL {}"'
```

A whole batch, three at a time (64 GB RAM / 16 GB VRAM handles 3; each job loads the full raw
model into Blender, so use fewer on smaller machines). Logs go to one file per model:

```bash
mkdir -p logs
ls -d Song_Dynasty_20_Types_Alt/[01]*/ | xargs -n1 basename | xargs -P 3 -I{} \
  sh -c 'PY=.venv-rebake/Scripts/python TRIS=300000 sh rebake_building.sh {} > logs/{}.log 2>&1 && echo "DONE {}" || echo "FAIL {}"'
```

| Variable | Default | Meaning |
|---|---|---|
| `TRIS` | 500000 | Target triangles. Used so far: 300k for houses/shops (01–15), 500k for large halls and multi-roof buildings (16–20) |
| `TEX` | 4096 | Bake resolution. Unity imports building textures capped at 2048 (`BuildingImportPostprocessor`) |
| `SET` | `Song_Dynasty_20_Types_Alt` | Model set folder under `References/MeshyModels` |
| `HEIGHT` | from `building_heights.txt` | Real-world height in metres |
| `OUT` | `RPG/Assets/Art/Buildings/SongDynasty` | Unity folder that receives `<id>/` |
| `PY` | `python` | Python with numpy, meshoptimizer and xatlas |
| `WORK` | `References/MeshyModels/_rebake` | Intermediate GLBs and the raw bake PNGs |

Smoothing strength is an argument of `smooth_building.py` (default `0.5 1`). The user compared
options on 19: stronger (1.0 × 2) melts eave brackets, and Corrective Smooth smears detail.

## Checking a result

Read each log before calling a model done:

- `GEO ... error 0.0x% of bbox`: shape error. Under about 0.1% is normal.
- `UNWRAP ... NN% utilisation`: should be **about 80%**. `UNWRAP FAILED` means a lopsided atlas;
  `unwrap_building.py` caps chart size to prevent it (09_Apothecary once came out at 13%).
- `BAKE BaseColor done` and `BAKE Normal done` both present.
- `REPORT {... "tris_after": ..., "size_m": [w, d, h]}`: triangle count near `TRIS`, and the size
  looks right for the building.

Then look at it. The user reviews every building visually, so send images rather than only
numbers: a front three-quarter view and a close-up of a roof eave, which is where texture or
shape problems show first. Compare against the raw model when in doubt.

## Unity steps

1. Let Unity import the new files (focus the editor, or `AssetDatabase.Refresh()`).
2. Menu **WorldRPG/Buildings/Build Per-Building Scenes**. This rebuilds the materials (wires
   `_BaseColor` and `_Normal`), the prefabs in `Assets/Prefabs/Buildings/SongDynasty`, and one
   review scene per building in `Assets/Workshop/BuildingS/`.
3. Optional: **WorldRPG/Buildings/Build Building Showcase Scene** lays out every building in
   `Assets/Workshop/BuildingShowcase.unity`.

4. Stalls: **WorldRPG/Buildings/Build Stall Showcase Scene** builds the prefabs in
   `Assets/Prefabs/Stalls` and lays them out in `Assets/Workshop/StallShowcase.unity`. The
   per-building scenes (step 2) include the stalls too.

## LODs and collision meshes

Every building and stall also has two lower-detail rebakes made by the same pipeline (GPU bake, not
PolyFew/decimation): `LOD=<n>` writes `<id>_LOD<n>.fbx` plus `Textures/<id>_LOD<n>_BaseColor.png` /
`_Normal.png` next to the full model and leaves the full model untouched. Inside the FBX the mesh is
named `<id>_L<n>`, because Unity's importer treats meshes named `*_LOD<n>` as its own LOD-group
convention and warns when LOD0 isn't in the same file.

| Set | LOD1 | LOD2 (also the collision mesh) |
|---|---|---|
| Buildings | `TRIS=60000 TEX=2048` | `TRIS=12000 TEX=1024` |
| Stalls | `TRIS=25000 TEX=1024` | `TRIS=5000 TEX=512` |

```bash
PY=.venv-rebake/Scripts/python LOD=1 TRIS=60000 TEX=2048 sh rebake_building.sh 19_Xieshan_Public_Hall
PY=.venv-rebake/Scripts/python LOD=2 TRIS=12000 TEX=1024 sh rebake_building.sh 19_Xieshan_Public_Hall
PY=.venv-rebake/Scripts/python SET=Song_Dynasty_10_Street_Stalls OUT=../../RPG/Assets/Art/Buildings/Stalls \
  LOD=1 TRIS=25000 TEX=1024 sh rebake_building.sh 08_Cloth_Garment_Stall
```

About 1.5 minutes each; all 60 ran in ~30 minutes three at a time. Don't edit `rebake_building.sh`
while a batch is running: `sh` reads the script as it goes, and the running jobs fail at their last step.

The prefab builder (`BuildingShowcaseBuilder.BuildPrefabs`) picks the LODs up automatically: an
`LODGroup` with LOD0/LOD1/LOD2, each LOD aligned to LOD0 by surface centroid (at 12k triangles the
debris pass can drop a protruding piece, which shifts the bounding box Blender centres on), and a
`Collision` child with a MeshCollider on the LOD2 mesh. Without LOD files a prefab falls back to a
MeshCollider on the model itself, only at 300k triangles or fewer (`MaxColliderTris`).

## Rules and pitfalls

- Don't commit raw Meshy FBX/GLB files: they are 100–345 MB each and GitHub rejects files over
  100 MB. Only the rebaked results under `RPG/Assets` go into git.
- Don't push more than about 1.5 GB at once; commit and push in batches.
- Don't switch the unwrap to Blender Smart UV or a single-piece xatlas run (see the table at the top).
- Don't decimate with UVs preserved (Blender Decimate, PolyFew). It only works up to about 900k
  triangles and smears textures on Meshy models.
- If a step fails, rerun just that model; nothing is shared between models.
- Old PolyFew results live in `RPG/Assets/Art/Buildings/SongDynasty/PolyFewBackup/` (git-ignored,
  local only). The builder skips any folder whose name starts with `PolyFew`.
