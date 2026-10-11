#!/bin/sh
# Clean Meshy buildings in Blender and export to Unity.
# usage: [SET=Song_Dynasty_20_Types_Alt] [TRIS=80000 | 0 = raw, reduce later with PolyFew] [OUT=<dir>] sh process_buildings.sh [id-prefix ...]
#   e.g. sh process_buildings.sh 07 16   (no args = all); PREVIEW=<dir> also renders a QA image per building
# Afterwards in Unity: WorldRPG/Buildings/Build Building Showcase Scene
B="/c/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe"
HERE="$(cd "$(dirname "$0")" && pwd)"
M="$HERE/${SET:-Song_Dynasty_20_Types_Alt}"
TRIS="${TRIS:-80000}"
U="${OUT:-$HERE/../../RPG/Assets/Art/Buildings/SongDynasty}"
while read n h; do
  if [ $# -gt 0 ]; then match=0; for p in "$@"; do case "$n" in "$p"*) match=1;; esac; done; [ $match = 1 ] || continue; fi
  mkdir -p "$U/$n/Textures"
  src="$M/$n/$n.glb"
  # fall back to FBX if the GLB is truncated/missing (14 had a broken GLB download)
  python -c "import os,struct,sys;f=open(sys.argv[1],'rb');h=f.read(12);sys.exit(0 if len(h)==12 and struct.unpack('<I',h[8:])[0]==os.path.getsize(sys.argv[1]) else 1)" "$src" 2>/dev/null || src="$M/$n/$n.fbx"
  "$B" -b --factory-startup -P "$HERE/blender_fix_building.py" -- "$src" "$U/$n/$n.fbx" "$h" apply "$TRIS" ${PREVIEW:+"$PREVIEW/$n.png"} 2>&1 | grep "REPORT\|Error"
  python - "$M/$n/textures" "$U/$n/Textures" "$n" <<'PY'
import os, sys, shutil, numpy as np
from PIL import Image
t, d, n = sys.argv[1:]
shutil.copy(f"{t}/texture_0_base_color.png", f"{d}/{n}_BaseColor.png")
# Alt set is base colour only (no PBR maps)
if not os.path.exists(f"{t}/texture_0_metallic.png"): sys.exit()
shutil.copy(f"{t}/texture_0_normal.png", f"{d}/{n}_Normal.png")
met = np.asarray(Image.open(f"{t}/texture_0_metallic.png").convert('L'))
rough = Image.open(f"{t}/texture_0_roughness.png").convert('L')
if rough.size != met.shape[::-1]: rough = rough.resize(met.shape[::-1])
rough = np.asarray(rough)
Image.fromarray(np.dstack([met, met, met, 255 - rough]).astype(np.uint8), 'RGBA').save(f"{d}/{n}_MetallicSmoothness.png")
PY
done < "$M/building_heights.txt"
