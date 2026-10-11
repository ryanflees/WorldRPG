#!/bin/sh
# Reduce a raw Meshy building by rebaking: simplify shape -> Laplacian smooth -> xatlas unwrap -> bake from raw -> Unity FBX.
# For buildings whose Meshy UVs are too shattered to decimate (PolyFew/Blender/meshopt seam-aware all fail).
# usage: [TRIS=500000] [TEX=4096] [PY=<python with numpy meshoptimizer xatlas>] [WORK=<dir>] sh rebake_building.sh <id> [...]
#   e.g. sh rebake_building.sh 19_Xieshan_Public_Hall 20_Market_Guild_Hall
# Afterwards in Unity: WorldRPG/Buildings/Build Building Showcase Scene (picks up _BaseColor + _Normal)
set -e
B="/c/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe"
HERE="$(cd "$(dirname "$0")" && pwd)"
M="$HERE/${SET:-Song_Dynasty_20_Types_Alt}"
U="${OUT:-$HERE/../../RPG/Assets/Art/Buildings/SongDynasty}"
TRIS="${TRIS:-500000}"
PY="${PY:-python}"
for n in "$@"; do
  W="${WORK:-$HERE/_rebake}/$n"; mkdir -p "$W" "$U/$n/Textures"
  h=$(grep "^$n " "$M/building_heights.txt" | cut -d' ' -f2)
  cd "$HERE"
  "$PY" simplify_geo.py "$M/$n/$n.glb" "$W/geo.glb" "$TRIS"
  "$B" -b --factory-startup -P smooth_building.py -- "$W/geo.glb" "$W/smooth.glb" 2>&1 | grep "SMOOTH\|Error"
  "$PY" unwrap_building.py "$W/smooth.glb" "$W/uv.glb" "${TEX:-4096}"
  KEEP_UV=1 "$B" -b --factory-startup -P bake_building.py -- "$M/$n/$n.glb" "$W/uv.glb" "$W/bake" "$n" "${TEX:-4096}" 2>&1 | grep "BAKE\|Error"
  "$B" -b --factory-startup -P blender_fix_building.py -- "$W/bake/$n.glb" "$U/$n/$n.fbx" "$h" apply 0 2>&1 | grep "REPORT\|Error"
  cp "$W/bake/${n}_BaseColor.png" "$W/bake/${n}_Normal.png" "$U/$n/Textures/"
done
