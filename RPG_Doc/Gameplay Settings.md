# Gameplay Settings — Linshui Town

How to tune the first level: the guild's target, the length of the afternoon, villager count, pickpocketing difficulty, the opening flythrough and the start camera.

Scene: `RPG/Assets/Levels/LinshuiTown/LinshuiTown.unity`

## Before you change anything: quick vs permanent

The level is **generated**. `WorldRPG ▸ Levels ▸ Build Linshui Town` throws the scene away and rebuilds it from `Assets/Workshop/TownDemo.unity`. Every new component then starts with the defaults written in its script. So there are two ways to change a value:

| | How | Survives a rebuild? | Use it for |
|---|---|---|---|
| **Quick** | Select the object in the Linshui scene, edit it in the Inspector, save the scene | **No** | Trying values out |
| **Permanent** | Change the default in the script (or the builder constant), then rebuild or re-run the setup menu | Yes | Values you're happy with |

There's one exception: **villager settings** (vision, rear zone, purse) live on the **villager prefabs**. Prefab edits survive a level rebuild. They only reset if you also rebuild the prefabs (`WorldRPG ▸ NPC ▸ Build Villager Prefabs`).

All values below are the current defaults.

---

## 1. The guild's target and the afternoon

Both components sit on **Player** (`Player ▸ GuildQuest`, `Player ▸ DayClock`). They start when the thief appears, after the opening flythrough.

### Wen required — `GuildQuest`
Script: `Assets/Scripts/Stealth/GuildQuest.cs`

| Field | Default | What it does |
|---|---|---|
| `targetWen` | 4000 | Wen the guild wants before sunset. One clean lift (3 valuables) earns about 700–1,200 wen at normal wealth |
| `guildName` | "The Thieves' Guild" | Heading on the briefing card |
| `briefingTime` | 7 | Seconds the briefing card stays up at the start |

The currency is **wen** (copper cash), the same unit the purse and pocket board use. To call it "gold" or "taels" instead, change the text in `GuildQuest.cs`, `PickpocketHUD.cs` and the item names in `PocketBoard.cs`.

### Length of the day — `DayClock`
Script: `Assets/Scripts/World/DayClock.cs`

| Field | Default | What it does |
|---|---|---|
| `dayLengthSeconds` | 600 | **Real seconds of play before sunset** (10 minutes). The main difficulty knob together with `targetWen` |
| `startHour` / `endHour` | 13.5 / 17.5 | In-game hours the timeline runs between (1:30 PM → 5:30 PM). Only affects the timeline's hour markers and the light, not the length |
| `startAngles` / `endAngles` | (42, 50) / (24, 82) | Sun (elevation, yaw) at start and at sunset. Lower elevation = longer shadows; yaw swings them round |
| `startColor` / `endColor` | near white / soft warm | Sunlight colour drift. Keep `endColor` mild: strong warmth makes the town yellow |
| `startIntensity` / `endIntensity` | 1.55 / 1.3 | Sun brightness drift |
| `endAmbient` | 0.9 | Ambient light at sunset relative to the start (1 = no dimming) |

The clock runs on game time, so **bullet time slows the clock too**.

---

## 2. Villagers

### How many — `LinshuiTownBuilder`
File: `Assets/Workshop/Editor/LinshuiTownBuilder.cs`

| Constant | Default | What it does |
|---|---|---|
| `VillagerCount` | 128 | Villagers placed in town. Change it, then run **Build Linshui Town** |

Where they appear is weighted in `BuildVillagers()`: the market square ×6, Main Street and Commerce Street ×2, back alleys ×0.4, everything else ×1. Nobody spawns within 6 m of the player spawn.

**Stall vendors:** `NPCNavigationSetup.SetupVendorStalls(2)` is called at the end of the build. The `2` is how many stalls are left **unattended**; the rest get a vendor.

Performance note: 128 villagers runs at about 14–15 ms per frame in the editor at 1080p.

### Eyes, back and purse — `PickpocketTarget` (on each villager prefab)
Prefabs: `Assets/Prefabs/NPC/Villagers/NPC_song_*.prefab` (10 prefabs). Edit them all together by multi-selecting.
Script: `Assets/Scripts/Stealth/PickpocketTarget.cs`

| Field | Default | What it does |
|---|---|---|
| `visionDistance` | 6 | How far they see (m) |
| `visionAngle` | 110 | Width of the forward vision cone (degrees) |
| `bodyCoverRadius` | 0.4 | Half-width of a body that blocks sight. Hiding behind the mark works because of this |
| `chattingAttention` | 0.3 | How fast suspicion builds while they're chatting (0.3 = 30% of normal) |
| `rearAngle` | 150 | Width of the band behind them where a lift can start (degrees) |
| `rearMin` / `rearMax` | 0.35 / 2.2 | Distance band behind them (m) |
| `followRange` | 3.5 | The lift breaks if you fall further behind than this (m) |
| `wealth` | -1 | Purse multiplier. **-1 = by role** (Shopkeeper / Elder 1.6, Market / Tea vendor 1.2, Farm worker / Porter 0.6, others 1.0). Stall vendors get ×1.5 on top (the day's takings) |
| `refillTime` | 180 | Seconds before a robbed villager has a purse again |

---

## 3. Pickpocketing

### The lift and getting caught — `Pickpocketer`
On **Player**. Script: `Assets/Scripts/Stealth/Pickpocketer.cs`

| Field | Default | What it does |
|---|---|---|
| `liftDuration` | 1.4 | Seconds of holding the mouse inside the ring (while behind the mark) to land the lift |
| `liftDecay` | 0.35 | How fast the ring drains while you're out of position |
| `ringRadius` / `ringHitRadius` | 26 / 46 | Size of the ring over the mark, and how close to it a click must land (pixels at 1080p) |
| `stealWindow` | 7 | **Real seconds of bullet time** to work the pocket board |
| `shadowDistance` | 0.75 | How close behind the mark the thief walks during the steal (m) |
| `suspicionPerSecond` | 70 | Suspicion gain while someone sees you (scaled up close, down at the edge of sight) |
| `suspicionDecay` | 25 | Suspicion lost per second when nobody is watching |
| `stareTime` | 5 | Seconds a villager stops and stares after catching you |
| `wariness` | 60 | Seconds that villager stays on guard (can't be picked) |

### Bullet time — `BulletTime`
On **Player**. Script: `Assets/Scripts/Stealth/BulletTime.cs`

| Field | Default | What it does |
|---|---|---|
| `slowScale` | 0.12 | World speed while stealing (0.12 = 12%) |
| `easeIn` / `easeOut` | 0.15 / 0.35 | Real seconds to slow down / speed back up |
| `saturation` / `vignette` | -55 / 0.38 | The ink-wash look while slowed |

### The pocket board — `PocketBoard` (code only)
Script: `Assets/Scripts/Stealth/PocketBoard.cs`. These are constants, so change them in code.

| Constant | Default | What it does |
|---|---|---|
| `MovesMax` | 8 | Takes + peeks per steal |
| `Quota` | 3 | Valuables needed for a clean lift |
| `TrapAlarm` | 50 | Alarm added by a bell-tied purse (100 = caught) |

Pouch contents are in `Deal()`: 4 valuables (String of cash 100, Silver shard 250, Jade pendant 400, Gold hairpin 600 wen, each × the mark's wealth × 0.8–1.2), 2 bell-tied purses and 3 junk items.

---

## 4. The opening flythrough and hold-to-start — `TownTrailer`
On **CameraRig**. Script: `Assets/Scripts/Camera/TownTrailer.cs`

| Field | Default | What it does |
|---|---|---|
| `trailerMode` | on | Play opens with the flythrough. **Untick to go straight into the game** |
| `holdToStart` | on | Hold the start key to begin. Off = endless attract loop (for recording) |
| `startKey` / `holdSeconds` | Space / 2 | Key and how long to hold it |
| `mode` | Random | `Random` = improvised street shots. `Scripted` = the fixed shot list written by the builder (`SetupTrailer()` in `LinshuiTownBuilder.cs`) |
| `shotDuration` | 10–15 | Seconds per shot |
| `glideSpeed` | 1.8 | Camera travel speed along each straight glide (m/s) |
| `distanceRange` | 56–78 | Camera distance (height) range. The gameplay camera is 38.9 |
| `pitchRange` | 40–54 | Camera tilt range (degrees) |
| `villagerBias` | 0.65 | Chance a shot starts next to a villager |
| `fadeTime` | 0.7 | Paper fade at each cut |
| `title` / `subtitle` | "Linshui" / "A river town of the Song" | Opening title card |
| `area` | town interior | Shots stay inside this rectangle (set by the builder) |

---

## 5. Start position and camera

| Setting | Where | Default |
|---|---|---|
| Player spawn | `PlayerSpawn` in `LinshuiTownBuilder.cs` | (0, 0, -14.5), Main Street at the square |
| Start yaw | `StartYaw` in `LinshuiTownBuilder.cs` | 45° |
| Camera FOV | `CameraFov` in `LinshuiTownBuilder.cs` | 10° (near-orthographic look) |
| Start camera distance | `StartDistance` in `LinshuiTownBuilder.cs` (used in `SetupPlayerAndCamera` **and** `ApplyLook`) | 38.9 |
| Zoom limits | `ApplyLook()` in `LinshuiTownBuilder.cs` | 34–535 |
| Walk / run speed | `TopDownCharacter` on Player | 1.6 / 3.4 m/s (matched to the walk/run animations; changing them makes feet slide) |

### Changing the FOV without changing the framing

The camera is a narrow perspective lens far away, for an almost orthographic look. To keep the same framing at a different FOV, multiply **every camera distance** by `tan(oldFov / 2) / tan(newFov / 2)`. Going from 24° to 10° that factor is 2.43. Camera distances live in:

- `StartDistance`, the zoom limits and the near/far clip in `LinshuiTownBuilder.cs`
- the scripted trailer shots, plus `distanceRange` and the fallback shot in `TownTrailer.cs`
- `_HazeStart/End`, `_LineFadeStart/End` and `_InnerFadeStart/End` on `Art/Shaders/M_InkEdges.mat` (set by `ApplyLook`)
- `_InkFadeStart/End` on the character `InkToon` materials (44 / 122). If this stays too short, villagers lose their ink lines
- the Shadow Distance on `Settings/PC_RPAsset` (389). Cascade splits are fractions of it, so scaling it keeps shadow sharpness

Pan speed already adapts on its own: it follows the visible width, not the raw distance.

---

## 6. Look (colour grade)

| What | Where |
|---|---|
| Colour grade (exposure, contrast, saturation, white balance, split toning, bloom, vignette) | `Assets/Levels/LinshuiTown/LinshuiTown_Volume.asset`. Permanent version: `CreateVolumeProfile()` in `LinshuiTownBuilder.cs` |
| Ground colour (sand yards, fields, stone, brick) | `Assets/Levels/LinshuiTown/Materials/LS_M_Ground_*.mat` (`_Saturation`, `_Brightness`, `_PaperTint`). Permanent version: `GroundMat()` in `LinshuiTownBuilder.cs` |

The current grade is tuned "fresh and clean": slightly cool white balance (-6), near-neutral split toning, sand and field ground at 0.55 saturation. Warm filters stacked on top of each other read as yellow. Add warmth in one place only.

---

## Useful menus

| Menu | Does |
|---|---|
| `WorldRPG ▸ Levels ▸ Build Linshui Town` | Full rebuild (layout, villagers, navmesh, stalls, stealth, quest, trailer) |
| `WorldRPG ▸ Levels ▸ Setup Linshui Trailer Shots` | Re-applies the trailer setup and scripted shots to the open scene |
| `WorldRPG ▸ Stealth ▸ Setup Pickpocketing In Open Scene` | Adds any missing stealth, quest and clock components to the open scene. Doesn't overwrite values that are already set |
| `WorldRPG ▸ Stealth ▸ Add Pickpocket Targets To Villager Prefabs` | Adds `PickpocketTarget` to villager prefabs that lack it |
