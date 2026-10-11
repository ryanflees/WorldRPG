# WorldRPG smoke VFX

`PF_SmokeVFX.prefab` is a reusable stylised steam/smoke plume for chimney tops,
food pots, bamboo steamers, and small street stalls.

## Use

Drag the prefab into a scene and place its root at the smoke source. The prefab
is local-space, so it can be parented to a building or stall and moved with it.

`SmokeVfx` exposes the small set of per-instance controls that are useful when
the same effect is reused across the town:

- `emissionMultiplier`: overall density. Use a lower value for a small food pot.
- `drift`: local wind direction and upward lift. The default gives a gentle rise.
- `playOnEnable`: disable for scripted stalls, then call `Play()` when the stall
  is lit or starts cooking.

The effect uses two billboard particle systems and a generated soft-edge texture,
so it does not depend on a texture pack or a VFX Graph asset.
