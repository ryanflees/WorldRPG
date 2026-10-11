using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Builds the first real level, Linshui Town (临水镇, "Riverside Town"): a small, tightly packed walled Song dynasty
    /// river-market town made of the Meshy building and stall prefabs on the tiled ground set. About 110 x 80 m inside
    /// the walls, with narrow streets so crowds gather and every block has a back alley.
    /// Layout (X east, Z north, metres):
    ///
    ///   canal (z 42..52) and quay (z 34..42) with warehouses, the riverside inn and a bridge north ..... escape route
    ///   Temple Street (z 22..25.5)
    ///   Market Guild Hall facing the Market Square (x ±13, z -12..8) with stalls and a well | Magistrate's Yamen (east)
    ///   West Gate == Commerce Street (z ±2.5) == East Gate
    ///   craft quarter (west) | Main Street (x ±3) | wealthy quarter (east)
    ///   mossy back alleys behind every row; the thief's hideout in the south-west alleys
    ///   South Gate (z -46)
    ///
    /// Copies TownDemo (player, camera rig, HUD) and replaces its greybox town, sets up the painted look (lighting,
    /// cloud shadows, post, ink pass), then bakes the A* navmesh, places villagers and staffs the stalls.
    /// Deterministic: the same seed builds the same town.
    /// Menu: WorldRPG/Levels/Build Linshui Town
    /// </summary>
    public static class LinshuiTownBuilder
    {
        const string LevelFolder = "Assets/Levels/LinshuiTown";
        const string ScenePath = LevelFolder + "/LinshuiTown.unity";
        const string GeneratedFolder = LevelFolder + "/Generated";
        const string MaterialFolder = LevelFolder + "/Materials";
        const string SourceScene = "Assets/Workshop/TownDemo.unity";
        const string BuildingPrefabs = "Assets/Prefabs/Buildings/SongDynasty";
        const string StallPrefabs = "Assets/Prefabs/Stalls";
        const string GroundFolder = "Assets/Art/Textures/Ground";
        const string WorkshopMats = "Assets/Workshop/Materials";
        const int Seed = 1127;
        const int VillagerCount = 128;

        // ------------------------------------------------------------------ layout

        // Inside face of the city wall; buildings stay within it (the quay is the northern edge).
        static readonly Rect Interior = Rect.MinMaxRect(-54.5f, -44.5f, 54.5f, 34f);
        const float WallX = 56f, WallZ = -46f, WallThick = 3f, WallHeight = 6f;
        const float SideGateZ = 0f;   // west and east gates, on Commerce Street
        const float CanalSouth = 42f, CanalNorth = 52f, WaterY = -1.1f, CanalBedY = -2.6f;
        // Walkable map (invisible walls) and the larger visible ground around it.
        static readonly Rect Map = Rect.MinMaxRect(-80f, -70f, 80f, 80f);
        static readonly Rect Visible = Rect.MinMaxRect(-150f, -140f, 150f, 150f);
        const float GroundTop = 0.03f;   // top of the ground collider; street decals sit 0..6 cm above y=0

        static readonly Vector3 PlayerSpawn = new Vector3(0f, 0.05f, -14.5f);   // Main Street, at the market square's south edge
        const float StartYaw = 45f;          // camera (and thief) yaw on play: the town seen corner-on
        // Near-orthographic look: a narrow FOV from far away. Every camera distance below was scaled by
        // tan(24/2) / tan(CameraFov/2) (= 2.43) from the old FOV 24 values, so the framing is unchanged.
        const float CameraFov = 10f;
        const float StartDistance = 38.9f;   // close street-level follow distance on play (was 16 at FOV 24)
        // Navmesh box: the town, a few metres past each gate, the bridge and a strip of the north bank.
        static readonly Vector3 NavCenter = new Vector3(0f, 4f, 0f);
        static readonly Vector3 NavSize = new Vector3(132f, 16f, 112f);

        enum Ground { PackedEarth = 1, GrayFlagstone, BlueGrayBrick, SandyGravel, RedClayBrick, DampOldStone, LightCourtyard, MossyCobble }

        // Texture repeat in metres per ground type (the tiles are square; bricks/stones need smaller repeats).
        static readonly Dictionary<Ground, float> TileSize = new Dictionary<Ground, float>
        {
            { Ground.PackedEarth, 6f }, { Ground.GrayFlagstone, 4f }, { Ground.BlueGrayBrick, 3f }, { Ground.SandyGravel, 6f },
            { Ground.RedClayBrick, 3f }, { Ground.DampOldStone, 3.5f }, { Ground.LightCourtyard, 6f }, { Ground.MossyCobble, 3.5f },
        };

        struct Street
        {
            public string name; public Rect rect; public Ground ground; public float y;
            public Street(string name, Rect rect, Ground ground, float y) { this.name = name; this.rect = rect; this.ground = ground; this.y = y; }
        }

        static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0, z0, x1, z1);

        static Rect StreetRect(string name) => System.Array.Find(Streets, s => s.name == name).rect;

        // Paved / packed surfaces. Higher y wins where two overlap (1 cm apart so they don't z-fight at RTS distance).
        static readonly Street[] Streets =
        {
            new Street("MainStreet", R(-3f, -70f, 3f, -12f), Ground.GrayFlagstone, 0.04f),
            new Street("MarketSquare", R(-13f, -12f, 13f, 8f), Ground.RedClayBrick, 0.05f),
            new Street("CommerceStreet_W", R(-80f, -2.5f, -13f, 2.5f), Ground.BlueGrayBrick, 0.04f),
            new Street("CommerceStreet_E", R(13f, -2.5f, 80f, 2.5f), Ground.BlueGrayBrick, 0.04f),
            new Street("Quay", R(-54.5f, 34f, 54.5f, CanalSouth), Ground.DampOldStone, 0.04f),
            new Street("TempleStreet", R(-31.5f, 22f, 31.5f, 25.5f), Ground.GrayFlagstone, 0.03f),
            new Street("BridgeStreet", R(-2.5f, 25.5f, 2.5f, 34f), Ground.GrayFlagstone, 0.03f),
            new Street("GuildLane_W", R(-16f, 2.5f, -13f, 34f), Ground.GrayFlagstone, 0.03f),
            new Street("GuildLane_E", R(13f, 2.5f, 16f, 34f), Ground.GrayFlagstone, 0.03f),
            new Street("WestLane", R(-35f, -44.5f, -31.5f, 34f), Ground.PackedEarth, 0.03f),
            new Street("EastLane", R(31.5f, -44.5f, 35f, 34f), Ground.BlueGrayBrick, 0.03f),
            new Street("CraftAlley", R(-54.5f, -27f, -3f, -24f), Ground.PackedEarth, 0.02f),
            new Street("GardenAlley", R(3f, -27f, 54.5f, -24f), Ground.GrayFlagstone, 0.02f),
            // Back alleys: 2 m, mossy, out of sight of the main streets.
            new Street("BackAlley_SW", R(-31.5f, -37f, -3f, -35f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_SE", R(3f, -37f, 31.5f, -35f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_WS", R(-54.5f, -37f, -35f, -35f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_ES", R(35f, -37f, 54.5f, -35f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_W", R(-31.5f, -14f, -16f, -12f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_E", R(16f, -14f, 31.5f, -12f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_WW", R(-54.5f, -14f, -35f, -12f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_EE", R(35f, -14f, 54.5f, -12f), Ground.MossyCobble, 0.02f),
            new Street("BackAlley_NW", R(-54.5f, 14f, -35f, 16f), Ground.MossyCobble, 0.02f),
            // Courtyards in front of the landmarks.
            new Street("YamenForecourt", R(35f, 12f, 40f, 24f), Ground.LightCourtyard, 0.03f),
            new Street("ShrineCourtyard", R(-39f, -22f, -35f, -16f), Ground.LightCourtyard, 0.03f),
        };

        // Building pools (repeat an id to make it more likely). Ids are prefab names without the number prefix.
        static readonly string[] Shops = { "04_Street_Grocery_Shop", "05_Tea_Stall_Shop", "06_Wine_Shop", "07_Steamed_Food_Shop",
            "08_Cloth_Dye_Shop", "09_Apothecary", "10_Book_Paper_Shop", "16_Two_Story_Merchant_Shop" };
        static readonly string[] SmallShops = { "04_Street_Grocery_Shop", "05_Tea_Stall_Shop", "06_Wine_Shop", "07_Steamed_Food_Shop",
            "10_Book_Paper_Shop", "02_Narrow_One_Bay_House" };
        static readonly string[] Craft = { "11_Carpenter_Workshop", "12_Blacksmith_Workshop", "01_Humble_Two_Bay_Dwelling",
            "02_Narrow_One_Bay_House", "01_Humble_Two_Bay_Dwelling", "02_Narrow_One_Bay_House", "06_Wine_Shop" };
        static readonly string[] Homes = { "01_Humble_Two_Bay_Dwelling", "02_Narrow_One_Bay_House", "03_Small_Courtyard_Residence",
            "01_Humble_Two_Bay_Dwelling", "02_Narrow_One_Bay_House" };
        static readonly string[] Wealthy = { "17_Wealthy_Residence_Gatehouse", "03_Small_Courtyard_Residence",
            "17_Wealthy_Residence_Gatehouse", "16_Two_Story_Merchant_Shop" };
        static readonly string[] Waterfront = { "13_Grain_Warehouse", "14_Riverside_Storehouse", "13_Grain_Warehouse",
            "14_Riverside_Storehouse", "06_Wine_Shop", "05_Tea_Stall_Shop" };

        struct Frontage
        {
            public Vector2 a, b, facing; public string[] pool; public float maxDepth;
            public Frontage(Vector2 a, Vector2 b, Vector2 facing, string[] pool, float maxDepth)
            { this.a = a; this.b = b; this.facing = facing; this.pool = pool; this.maxDepth = maxDepth; }
        }

        static Frontage F(float ax, float az, float bx, float bz, float fx, float fz, string[] pool, float maxDepth = 9f)
            => new Frontage(new Vector2(ax, az), new Vector2(bx, bz), new Vector2(fx, fz), pool, maxDepth);

        // Street edges lined with buildings, in priority order (earlier rows claim corners). `facing` points from
        // the buildings to the street, i.e. the way their fronts face.
        static readonly Frontage[] Frontages =
        {
            // Market square and main street: the busiest shopfronts.
            F(-13f, -12f, -3f, -12f, 0f, 1f, Shops), F(3f, -12f, 13f, -12f, 0f, 1f, Shops),
            F(13f, -12f, 13f, -2.5f, -1f, 0f, SmallShops, 8f),
            F(-3f, -41f, -3f, -12f, 1f, 0f, Shops), F(3f, -41f, 3f, -12f, -1f, 0f, Shops),
            // Commerce street.
            F(-54.5f, -2.5f, -13f, -2.5f, 0f, 1f, Shops), F(-54.5f, 2.5f, -16f, 2.5f, 0f, -1f, Shops),
            F(13f, -2.5f, 54.5f, -2.5f, 0f, 1f, Shops), F(16f, 2.5f, 54.5f, 2.5f, 0f, -1f, Shops),
            // Waterfront warehouses, and the houses on Temple Street behind the guild hall.
            F(-54.5f, 34f, 54.5f, 34f, 0f, 1f, Waterfront, 7.5f),
            F(-31.5f, 22f, 31.5f, 22f, 0f, 1f, Homes, 8f),
            F(-2.5f, 25.5f, -2.5f, 34f, 1f, 0f, SmallShops, 6f), F(2.5f, 25.5f, 2.5f, 34f, -1f, 0f, SmallShops, 6f),
            // Lanes.
            F(-16f, 2.5f, -16f, 34f, 1f, 0f, Homes, 8f), F(-13f, 8f, -13f, 34f, -1f, 0f, Homes, 5.5f),
            F(16f, 2.5f, 16f, 34f, -1f, 0f, Wealthy, 8f), F(13f, 8f, 13f, 34f, 1f, 0f, Wealthy, 5.5f),
            F(-35f, -44.5f, -35f, 34f, 1f, 0f, Craft), F(-31.5f, -44.5f, -31.5f, 34f, -1f, 0f, Craft),
            F(31.5f, -44.5f, 31.5f, 34f, 1f, 0f, Homes), F(35f, -44.5f, 35f, 34f, -1f, 0f, Wealthy),
            F(-54.5f, -27f, -3f, -27f, 0f, 1f, Craft), F(-54.5f, -24f, -3f, -24f, 0f, -1f, Craft),
            F(3f, -27f, 54.5f, -27f, 0f, 1f, Wealthy), F(3f, -24f, 54.5f, -24f, 0f, -1f, Wealthy),
            // Back alleys (both sides), for the houses deeper in the blocks.
            F(-31.5f, -37f, -3f, -37f, 0f, 1f, Homes, 7f), F(-31.5f, -35f, -3f, -35f, 0f, -1f, Homes, 7f),
            F(3f, -37f, 31.5f, -37f, 0f, 1f, Homes, 7f), F(3f, -35f, 31.5f, -35f, 0f, -1f, Homes, 7f),
            F(-54.5f, -37f, -35f, -37f, 0f, 1f, Homes, 7f), F(-54.5f, -35f, -35f, -35f, 0f, -1f, Homes, 7f),
            F(35f, -37f, 54.5f, -37f, 0f, 1f, Wealthy, 7f), F(35f, -35f, 54.5f, -35f, 0f, -1f, Wealthy, 7f),
            F(-31.5f, -14f, -16f, -14f, 0f, 1f, Homes, 7f), F(-31.5f, -12f, -16f, -12f, 0f, -1f, Homes, 7f),
            F(16f, -14f, 31.5f, -14f, 0f, 1f, Wealthy, 7f), F(16f, -12f, 31.5f, -12f, 0f, -1f, Wealthy, 7f),
            F(-54.5f, -14f, -35f, -14f, 0f, 1f, Craft, 7f), F(-54.5f, -12f, -35f, -12f, 0f, -1f, Craft, 7f),
            F(35f, -14f, 54.5f, -14f, 0f, 1f, Wealthy, 7f), F(35f, -12f, 54.5f, -12f, 0f, -1f, Wealthy, 7f),
            F(-54.5f, 14f, -35f, 14f, 0f, 1f, Homes, 7f), F(-54.5f, 16f, -35f, 16f, 0f, -1f, Homes, 7f),
        };

        // ------------------------------------------------------------------ build state

        static System.Random _rng;
        static readonly List<Rect> _occupied = new List<Rect>();   // buildings, stalls, gates, props
        static readonly List<Rect> _keepClear = new List<Rect>();  // streets and walls
        static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Vector3> _sizes = new Dictionary<string, Vector3>();
        static readonly Dictionary<Material, MeshBatch> _batches = new Dictionary<Material, MeshBatch>();
        static Material _water, _wood, _lacquer;
        static int _buildingCount;

        class MeshBatch
        {
            public readonly List<Vector3> verts = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> tris = new List<int>();
            public float uvScale = 1f;   // 1 / (metres per texture repeat), including the material's own tiling
        }

        [MenuItem("WorldRPG/Levels/Build Linshui Town")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Build());
        }

        public static string Build(bool withNavigation = true)
        {
            _rng = new System.Random(Seed);
            _occupied.Clear();
            _keepClear.Clear();
            _batches.Clear();
            _buildingCount = 0;

            EnsureFolder(GeneratedFolder);
            EnsureFolder(MaterialFolder);
            BuildingShowcaseBuilder.BuildPrefabs();   // picks up LODs / orientation fixes
            LoadPrefabs();
            CreateMaterials();

            Scene scene = OpenTownCopy();
            var level = new GameObject("LinshuiTown").transform;

            foreach (var s in Streets) _keepClear.Add(s.rect);
            BuildGround(level);
            BuildCityWalls(level);
            BuildCanal(level);
            BuildLandmarks(level);
            int stalls = BuildStalls(level);
            BuildWell(level, new Vector3(0f, 0f, -6f));
            var buildings = Group("Buildings", level);
            foreach (var f in Frontages) LineStreet(buildings, f);
            BuildQuayProps(level);
            FlushMeshes(level);
            BuildMarkers(level);
            SetupPlayerAndCamera();
            ApplyLook();
            int villagers = BuildVillagers(level);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            string nav = "Navigation skipped.";
            if (withNavigation)
            {
                nav = NPCNavigationSetup.SetupNavigation(NavCenter, NavSize);
                nav += " " + NPCNavigationSetup.SetupVendorStalls(2);
            }
            AssetDatabase.SaveAssets();
            return $"Linshui Town: {_buildingCount} buildings, {stalls} stalls, {villagers} villagers -> {ScenePath}. {nav}";
        }

        // ------------------------------------------------------------------ setup

        static Scene OpenTownCopy()
        {
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(SourceScene, ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var town = GameObject.Find("Town");
            if (town != null) Object.DestroyImmediate(town);
            return scene;
        }

        static void LoadPrefabs()
        {
            _prefabs.Clear();
            _sizes.Clear();
            foreach (string folder in new[] { BuildingPrefabs, StallPrefabs })
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                _prefabs[prefab.name] = prefab;
                _sizes[prefab.name] = BuildingShowcaseBuilder.PrefabBounds(prefab).size;
            }
        }

        static void CreateMaterials()
        {
            string path = $"{MaterialFolder}/M_CanalWater.mat";
            _water = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (_water == null)
            {
                _water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(_water, path);
            }
            // Pale blue-grey, as in the references: the water reads by reflecting the sky, not by being dark.
            _water.SetColor("_BaseColor", new Color(0.40f, 0.49f, 0.52f));
            _water.SetFloat("_Smoothness", 0.85f);
            _water.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(_water);

            _wood = AssetDatabase.LoadAssetAtPath<Material>($"{WorkshopMats}/M_Wood.mat");
            _lacquer = AssetDatabase.LoadAssetAtPath<Material>($"{WorkshopMats}/M_Lacquer.mat");
        }

        /// <summary>
        /// The level's painted (InkPaint) copy of a ground tile material: same textures and tiling as the shared
        /// URP/Lit one in Art/Textures/Ground, which stays untouched for other scenes.
        /// </summary>
        static Material GroundMat(Ground g)
        {
            string name = $"M_Ground_{(int)g:00}_{g}";
            string path = $"{MaterialFolder}/LS_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>($"{GroundFolder}/{name}.mat");
                if (source == null) throw new System.Exception($"Missing ground material {g} in {GroundFolder}");
                mat = new Material(Shader.Find("WorldRPG/InkPaint") ?? source.shader);
                mat.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                mat.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                mat.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                mat.SetFloat("_UseNormalMap", 1f);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 0.6f);
                mat.SetFloat("_Cull", 2f);
                AssetDatabase.CreateAsset(mat, path);
            }
            // Ground fills most of the frame: a little calmer and darker than the buildings, so they stand out.
            // Loud tiles (the red-brick square) are calmed further; dark stone is lifted so the quay doesn't go murky.
            (float saturation, float brightness) look = g switch
            {
                Ground.RedClayBrick => (0.6f, 0.9f),
                Ground.DampOldStone => (0.8f, 1.12f),
                Ground.MossyCobble => (0.85f, 1.06f),
                Ground.BlueGrayBrick => (0.8f, 1.02f),
                // Sandy yards and the fields outside fill most of the frame: kept pale and low in colour so the town
                // reads fresh and clean rather than yellow.
                _ => (0.55f, 0.97f),
            };
            mat.SetFloat("_Saturation", look.saturation);
            mat.SetFloat("_PaperTint", 0.03f);
            mat.SetFloat("_Brightness", look.brightness);
            EditorUtility.SetDirty(mat);
            Batch(mat, TileSize[g]);
            return mat;
        }

        // ------------------------------------------------------------------ ground

        static void BuildGround(Transform level)
        {
            Material earth = GroundMat(Ground.LightCourtyard);
            Material gravel = GroundMat(Ground.SandyGravel);
            Material gutter = GroundMat(Ground.DampOldStone);

            // Pale dry fields outside the walls, the canal cut out; sandy gravel yards inside the walls.
            TopQuad(earth, R(Visible.xMin, Visible.yMin, Visible.xMax, CanalSouth), 0f);
            TopQuad(earth, R(Visible.xMin, CanalNorth, Visible.xMax, Visible.yMax), 0f);
            TopQuad(gravel, R(-WallX, WallZ, WallX, CanalSouth), 0.01f);

            // Two different surfaces overlapping at the same height z-fight (the same surface is harmless: its UVs
            // are world-aligned, so both copies draw identical pixels).
            for (int i = 0; i < Streets.Length; i++)
            for (int j = i + 1; j < Streets.Length; j++)
                if (Streets[i].ground != Streets[j].ground && Mathf.Approximately(Streets[i].y, Streets[j].y)
                    && Streets[i].rect.Overlaps(Streets[j].rect))
                    Debug.LogWarning($"[LinshuiTown] {Streets[i].name} and {Streets[j].name} overlap at the same height and will z-fight");

            foreach (var s in Streets)
            {
                Rect r = s.rect;
                if (s.name == "MainStreet") r.yMin = Mathf.Max(r.yMin, Visible.yMin);
                TopQuad(GroundMat(s.ground), r, s.y);
            }
            // Stone gutters along Main Street (inside the walls, stopping at the square) and a flagstone border round
            // the square. Taken from the street rects so they follow the layout; each sits 1 cm above everything it
            // overlaps (coplanar strips z-fight).
            Rect main = StreetRect("MainStreet"), square = StreetRect("MarketSquare");
            const float gutterWidth = 0.5f, border = 0.8f;
            TopQuad(gutter, R(main.xMin, Interior.yMin, main.xMin + gutterWidth, square.yMin), 0.055f);
            TopQuad(gutter, R(main.xMax - gutterWidth, Interior.yMin, main.xMax, square.yMin), 0.055f);
            Material flag = GroundMat(Ground.GrayFlagstone);
            TopQuad(flag, R(square.xMin, square.yMin, square.xMax, square.yMin + border), 0.065f);
            TopQuad(flag, R(square.xMin, square.yMax - border, square.xMax, square.yMax), 0.065f);
            TopQuad(flag, R(square.xMin, square.yMin, square.xMin + border, square.yMax), 0.065f);
            TopQuad(flag, R(square.xMax - border, square.yMin, square.xMax, square.yMax), 0.065f);

            // Colliders: one slab south of the canal, one north of it, the canal bed, and the map edge.
            var colliders = Group("GroundColliders", level);
            BoxCollider(colliders, "Ground_South", R(Map.xMin, Map.yMin, Map.xMax, CanalSouth), GroundTop - 1f, GroundTop);
            BoxCollider(colliders, "Ground_North", R(Map.xMin, CanalNorth, Map.xMax, Map.yMax), GroundTop - 1f, GroundTop);
            const float edge = 1f, edgeHeight = 12f;
            BoxCollider(colliders, "MapEdge_W", R(Map.xMin - edge, Map.yMin, Map.xMin, Map.yMax), 0f, edgeHeight);
            BoxCollider(colliders, "MapEdge_E", R(Map.xMax, Map.yMin, Map.xMax + edge, Map.yMax), 0f, edgeHeight);
            BoxCollider(colliders, "MapEdge_S", R(Map.xMin, Map.yMin - edge, Map.xMax, Map.yMin), 0f, edgeHeight);
            BoxCollider(colliders, "MapEdge_N", R(Map.xMin, Map.yMax, Map.xMax, Map.yMax + edge), 0f, edgeHeight);
        }

        // ------------------------------------------------------------------ city walls and gates

        static void BuildCityWalls(Transform level)
        {
            var walls = Group("CityWalls", level);
            Material stone = GroundMat(Ground.BlueGrayBrick);
            Material cap = GroundMat(Ground.DampOldStone);
            const float half = WallThick / 2f, gateHalf = GateHalfWidth;
            float wallEndZ = CanalSouth;

            // South wall, broken by the south gatehouse; west/east walls broken by their gates, running to the canal.
            WallRun(walls, stone, cap, new Vector3(-WallX - half, 0f, WallZ), new Vector3(-gateHalf, 0f, WallZ), Vector3.back);
            WallRun(walls, stone, cap, new Vector3(gateHalf, 0f, WallZ), new Vector3(WallX + half, 0f, WallZ), Vector3.back);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 outward = new Vector3(side, 0f, 0f);
                WallRun(walls, stone, cap, new Vector3(side * WallX, 0f, WallZ - half), new Vector3(side * WallX, 0f, SideGateZ - gateHalf), outward);
                WallRun(walls, stone, cap, new Vector3(side * WallX, 0f, SideGateZ + gateHalf), new Vector3(side * WallX, 0f, wallEndZ), outward);
            }

            Gate(walls, "SouthGate", new Vector3(0f, 0f, WallZ), 0f, 6f, 180f);
            Gate(walls, "WestGate", new Vector3(-WallX, 0f, SideGateZ), 90f, 5f, 270f);
            Gate(walls, "EastGate", new Vector3(WallX, 0f, SideGateZ), 90f, 5f, 90f);

            // Corner towers, and water towers where the walls meet the canal.
            foreach (var corner in new[] { new Vector3(-WallX, 0f, WallZ), new Vector3(WallX, 0f, WallZ),
                         new Vector3(-WallX, 0f, wallEndZ - 2.5f), new Vector3(WallX, 0f, wallEndZ - 2.5f) })
            {
                SolidBox(walls, "CornerTower", stone, corner + Vector3.up * 4f, new Vector3(5f, 8f, 5f), Quaternion.identity);
                Box(cap, corner + Vector3.up * 8.15f, new Vector3(5.6f, 0.3f, 5.6f), Quaternion.identity);
            }
        }

        /// <summary>A straight wall from a to b (on the ground), crenellated on the outward side.</summary>
        static void WallRun(Transform parent, Material stone, Material cap, Vector3 a, Vector3 b, Vector3 outward)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            Quaternion rot = Quaternion.LookRotation(along / length);
            Vector3 mid = (a + b) / 2f;
            SolidBox(parent, "Wall", stone, mid + Vector3.up * WallHeight / 2f, new Vector3(WallThick, WallHeight, length), rot);
            Box(cap, mid + Vector3.up * (WallHeight + 0.1f), new Vector3(WallThick + 0.3f, 0.2f, length), rot);
            _keepClear.Add(FlatRect(mid, new Vector3(WallThick, 0f, length), rot));

            // Merlons every 3 m along the outer edge.
            int count = Mathf.FloorToInt(length / 3f);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = a + along * ((i + 0.5f) / count) + outward * (WallThick / 2f - 0.35f) + Vector3.up * (WallHeight + 0.7f);
                Box(stone, p, new Vector3(0.7f, 1.0f, 1.5f), rot);
            }
        }

        const float GateHalfWidth = 8f;

        /// <summary>
        /// A 16 x 9 m gatehouse with a passage along its local Z, topped by a (scaled-down) Xieshan hall as the gate tower.
        /// </summary>
        static void Gate(Transform parent, string name, Vector3 center, float yaw, float passage, float towerYaw)
        {
            var root = Group(name, parent, center);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Material stone = GroundMat(Ground.BlueGrayBrick);
            Material cap = GroundMat(Ground.DampOldStone);
            const float halfWidth = GateHalfWidth, depth = 9f, height = 6.5f, lintelBottom = 4.8f;
            float pierWidth = halfWidth - passage / 2f;
            foreach (float side in new[] { -1f, 1f })
                SolidBox(root, "Pier", stone, center + rot * new Vector3(side * (passage / 2f + pierWidth / 2f), height / 2f, 0f),
                    new Vector3(pierWidth, height, depth), rot);
            SolidBox(root, "Lintel", stone, center + rot * new Vector3(0f, (lintelBottom + height) / 2f, 0f),
                new Vector3(passage, height - lintelBottom, depth), rot);
            Box(cap, center + Vector3.up * (height + 0.1f), new Vector3(halfWidth * 2f + 0.4f, 0.2f, depth + 0.4f), rot);
            _occupied.Add(FlatRect(center, new Vector3(halfWidth * 2f, 0f, depth), rot));

            var tower = Place(root, "19_Xieshan_Public_Hall", new Vector3(center.x, height + 0.2f, center.z), towerYaw, 0.6f, register: false);
            if (tower != null) tower.name = "GateTower";
        }

        // ------------------------------------------------------------------ canal and bridge

        static void BuildCanal(Transform level)
        {
            var canal = Group("Canal", level);
            Material bank = GroundMat(Ground.DampOldStone);
            Material stone = GroundMat(Ground.GrayFlagstone);
            float x0 = Visible.xMin, x1 = Visible.xMax;

            // Water, bed, and stone embankments down to the bed.
            Batch(_water, 8f);
            TopQuad(_water, R(x0, CanalSouth, x1, CanalNorth), WaterY);
            TopQuad(bank, R(x0, CanalSouth, x1, CanalNorth), CanalBedY);
            BoxCollider(canal, "CanalBed", R(Map.xMin, CanalSouth, Map.xMax, CanalNorth), CanalBedY - 1f, CanalBedY);
            float bankHeight = -CanalBedY;
            Box(bank, new Vector3(0f, CanalBedY / 2f, CanalSouth + 0.2f), new Vector3(x1 - x0, bankHeight, 0.4f), Quaternion.identity);
            Box(bank, new Vector3(0f, CanalBedY / 2f, CanalNorth - 0.2f), new Vector3(x1 - x0, bankHeight, 0.4f), Quaternion.identity);

            // Parapets on both banks, open where the bridge lands.
            const float bridgeHalf = 3f, parapetH = 0.9f;
            foreach (float z in new[] { CanalSouth - 0.25f, CanalNorth + 0.25f })
            foreach (var (a, b) in new[] { (Map.xMin, -bridgeHalf), (bridgeHalf, Map.xMax) })
                SolidBox(canal, "Parapet", stone, new Vector3((a + b) / 2f, parapetH / 2f, z), new Vector3(b - a, parapetH, 0.5f), Quaternion.identity);

            // Hump bridge: ramp up 1 m over 4 m, flat span, ramp down, with parapets.
            var bridge = Group("Bridge", canal);
            const float rise = 1.1f, run = 4f, deck = 0.5f;
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float rampLen = Mathf.Sqrt(rise * rise + run * run);
            float zA = CanalSouth - 2f, zB = zA + run, zC = CanalNorth + 2f - run, zD = CanalNorth + 2f;
            void Span(Vector3 topMid, float length, Quaternion r)
            {
                Vector3 up = r * Vector3.up;
                SolidBox(bridge, "Deck", stone, topMid - up * deck / 2f, new Vector3(bridgeHalf * 2f, deck, length), r);
                foreach (float side in new[] { -1f, 1f })
                    SolidBox(bridge, "BridgeParapet", stone, topMid + r * new Vector3(side * (bridgeHalf - 0.25f), 0.45f, 0f),
                        new Vector3(0.5f, 0.9f, length), r);
            }
            Span(new Vector3(0f, rise / 2f, (zA + zB) / 2f), rampLen, Quaternion.Euler(-slope, 0f, 0f));
            Span(new Vector3(0f, rise, (zB + zC) / 2f), zC - zB, Quaternion.identity);
            Span(new Vector3(0f, rise / 2f, (zC + zD) / 2f), rampLen, Quaternion.Euler(slope, 0f, 0f));
            // Piers down to the bed.
            foreach (float z in new[] { CanalSouth + 3f, CanalNorth - 3f })
                SolidBox(bridge, "Pier", bank, new Vector3(0f, (CanalBedY + rise - deck) / 2f, z),
                    new Vector3(bridgeHalf * 2f - 1f, rise - deck - CanalBedY, 1.2f), Quaternion.identity);
        }

        // ------------------------------------------------------------------ landmarks

        static void BuildLandmarks(Transform level)
        {
            var root = Group("Landmarks", level);
            // The guild hall closes the north side of the market square; the merchant house looks onto it from the west.
            Place(root, "20_Market_Guild_Hall", OnEdge("20_Market_Guild_Hall", 0f, 8.2f, 180f), 180f).name = "MarketGuildHall";
            Place(root, "16_Two_Story_Merchant_Shop", OnEdge("16_Two_Story_Merchant_Shop", -13.2f, -7.3f, 90f), 90f).name = "MerchantHouse";
            // The riverside inn on the quay: boatmen, smugglers, rooms by the hour.
            Place(root, "15_Small_Inn", OnEdge("15_Small_Inn", -24f, 33.8f, 0f), 0f).name = "SmallInn";
            // Magistrate's yamen in the wealthy quarter, behind its forecourt, facing the east lane.
            Place(root, "19_Xieshan_Public_Hall", OnEdge("19_Xieshan_Public_Hall", 40.2f, 18f, 270f), 270f).name = "MagistrateYamen";
            // Neighbourhood shrine in the craft quarter, behind its courtyard off the west lane.
            Place(root, "18_Neighborhood_Shrine", OnEdge("18_Neighborhood_Shrine", -39.2f, -19f, 90f), 90f).name = "NeighborhoodShrine";
            // The thief's hideout: an anonymous narrow house on the south-west back alley.
            Place(root, "02_Narrow_One_Bay_House", OnEdge("02_Narrow_One_Bay_House", -20f, -37.2f, 0f), 0f).name = "ThiefHideout";
        }

        /// <summary>Centre of a building whose front edge is at (x, z), facing the yaw direction.</summary>
        static Vector3 OnEdge(string id, float x, float z, float yaw)
        {
            float depth = _sizes[id].z;
            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            return new Vector3(x, 0f, z) + back * (depth / 2f);
        }

        // ------------------------------------------------------------------ stalls

        struct StallSpot
        {
            public Vector3 pos; public float yaw;
            public StallSpot(float x, float z, float yaw) { pos = new Vector3(x, 0f, z); this.yaw = yaw; }
        }

        /// <summary>
        /// Stall prefabs face +Z with the goods (customer side); the vendor stands behind at -Z. Every spot's yaw points
        /// the front at the walkway and puts the vendor against the square's edge, a building, or the stall it backs onto.
        /// </summary>
        static int BuildStalls(Transform level)
        {
            var root = Group("Stalls", level);
            var square = new List<StallSpot>
            {
                // West and east rows with their backs to the square's edges, fronts to the middle
                // (Commerce Street's crossing, z ±2.5, left open).
                new StallSpot(-10.5f, -9.5f, 90f), new StallSpot(-10.5f, -5.5f, 90f), new StallSpot(-10.5f, 5.5f, 90f),
                new StallSpot(10.5f, -9.5f, 270f), new StallSpot(10.5f, -5.5f, 270f), new StallSpot(10.5f, 5.5f, 270f),
                // Against the south edge facing north, and against the guild hall steps facing south.
                new StallSpot(-5f, -9.8f, 0f), new StallSpot(5f, -9.8f, 0f),
                new StallSpot(-5f, 5.6f, 180f), new StallSpot(5f, 5.6f, 180f),
            };
            // Carts on the quay, backs to the canal, selling to the dockhands.
            var street = new List<StallSpot>
            {
                new StallSpot(-42f, 37.6f, 180f), new StallSpot(-10f, 37.6f, 180f),
                new StallSpot(10f, 37.6f, 180f), new StallSpot(42f, 37.6f, 180f),
            };

            string[] carts = { "01_Vegetable_Pushcart", "04_Tea_Water_Cart", "05_Steamed_Bun_Cart" };
            _cartSpots.Clear();
            foreach (var spot in street) _cartSpots.Add(spot.pos);
            var stallIds = new List<string>();
            foreach (var p in _prefabs.Keys)
                if (AssetDatabase.GetAssetPath(_prefabs[p]).StartsWith(StallPrefabs)) stallIds.Add(p);
            stallIds.Sort();
            var squareIds = new List<string>();
            foreach (string id in stallIds) if (System.Array.IndexOf(carts, id) < 0) { squareIds.Add(id); squareIds.Add(id); }
            Shuffle(squareIds);

            int n = 0;
            for (int i = 0; i < square.Count; i++)
                n += PlaceStall(root, squareIds[i % squareIds.Count], square[i]) ? 1 : 0;
            for (int i = 0; i < street.Count; i++)
                n += PlaceStall(root, carts[i % carts.Length], street[i]) ? 1 : 0;
            return n;
        }

        static readonly List<Vector3> _cartSpots = new List<Vector3>();

        static bool PlaceStall(Transform parent, string id, StallSpot spot)
        {
            var go = Place(parent, id, spot.pos, spot.yaw);
            if (go == null) return false;
            go.AddComponent<VendorStall>();   // spots, blocker and vendor are set up by NPCNavigationSetup.SetupVendorStalls
            return true;
        }

        static void BuildWell(Transform level, Vector3 pos)
        {
            var well = Group("Well", level, pos);
            Material stone = GroundMat(Ground.GrayFlagstone);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "WellRing";
            ring.transform.SetParent(well, false);
            ring.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            ring.transform.localScale = new Vector3(2.2f, 0.4f, 2.2f);
            ring.GetComponent<Renderer>().sharedMaterial = stone;
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "WellWater";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(well, false);
            water.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            water.transform.localScale = new Vector3(1.7f, 0.01f, 1.7f);
            water.GetComponent<Renderer>().sharedMaterial = _water;
            // Wooden frame with a little roof.
            foreach (float side in new[] { -1f, 1f })
                SolidBox(well, "Post", _wood, pos + new Vector3(side * 1.0f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 0.15f), Quaternion.identity);
            Box(_wood, pos + new Vector3(0f, 2.3f, 0f), new Vector3(2.2f, 0.12f, 0.12f), Quaternion.identity);
            Box(_lacquer, pos + new Vector3(0f, 2.9f, 0.45f), new Vector3(2.6f, 0.08f, 1.1f), Quaternion.Euler(-28f, 0f, 0f));
            Box(_lacquer, pos + new Vector3(0f, 2.9f, -0.45f), new Vector3(2.6f, 0.08f, 1.1f), Quaternion.Euler(28f, 0f, 0f));
            _occupied.Add(R(pos.x - 1.4f, pos.z - 1.4f, pos.x + 1.4f, pos.z + 1.4f));
        }

        // ------------------------------------------------------------------ street fill

        /// <summary>Lines a street edge with buildings from the pool, skipping anything that would overlap.</summary>
        static void LineStreet(Transform parent, Frontage f)
        {
            const float setback = 0.25f;
            Vector2 dir = (f.b - f.a).normalized;
            float length = (f.b - f.a).magnitude;
            float yaw = Mathf.Atan2(f.facing.x, f.facing.y) * Mathf.Rad2Deg;
            float t = 0.3f;
            string last = null, beforeLast = null;

            while (t < length - 2f)
            {
                bool placed = false;
                foreach (string id in Candidates(f.pool))
                {
                    if (id == last || id == beforeLast) continue;
                    Vector3 size = _sizes[id];
                    if (size.z > f.maxDepth || t + size.x > length) continue;

                    Vector2 c = f.a + dir * (t + size.x / 2f) - f.facing * (setback + size.z / 2f);
                    Rect rect = FootprintRect(c, size, yaw);
                    if (!IsFree(rect)) continue;

                    Place(parent, id, new Vector3(c.x, 0f, c.y), yaw);
                    beforeLast = last;
                    last = id;
                    // Usually eaves almost touching; now and then a gap just wide enough to slip through.
                    t += size.x + (Chance(0.12f) ? Range(1.8f, 2.4f) : Range(0.35f, 0.9f));
                    placed = true;
                    break;
                }
                if (!placed) t += 0.75f;
            }
        }

        static IEnumerable<string> Candidates(string[] pool)
        {
            var list = new List<string>(pool);
            Shuffle(list);
            var seen = new HashSet<string>();
            foreach (string id in list)
                if (seen.Add(id) && _sizes.ContainsKey(id)) yield return id;
        }

        static bool IsFree(Rect r)
        {
            if (r.xMin < Interior.xMin || r.xMax > Interior.xMax || r.yMin < Interior.yMin || r.yMax > Interior.yMax) return false;
            foreach (var o in _occupied) if (o.Overlaps(r)) return false;
            foreach (var k in _keepClear) if (k.Overlaps(r)) return false;
            return true;
        }

        static Rect FootprintRect(Vector2 center, Vector3 size, float yaw)
        {
            bool sideways = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
            float hx = (sideways ? size.z : size.x) / 2f, hz = (sideways ? size.x : size.z) / 2f;
            return R(center.x - hx, center.y - hz, center.x + hx, center.y + hz);
        }

        /// <summary>Instantiates a prefab (linked) and, unless told otherwise, reserves its footprint.</summary>
        static GameObject Place(Transform parent, string id, Vector3 pos, float yaw, float scale = 1f, bool register = true)
        {
            if (!_prefabs.TryGetValue(id, out var prefab))
            {
                Debug.LogWarning($"[LinshuiTown] missing prefab {id}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (register) _occupied.Add(FootprintRect(new Vector2(pos.x, pos.z), _sizes[id] * scale, yaw));
            if (AssetDatabase.GetAssetPath(prefab).StartsWith(BuildingPrefabs)) _buildingCount++;
            return go;
        }

        // ------------------------------------------------------------------ props

        static void BuildQuayProps(Transform level)
        {
            var props = Group("QuayProps", level);
            // Stacks of crates and sacks waiting to be loaded: cover to hide behind on the open quay.
            for (int i = 0; i < 12; i++)
            {
                float x = Range(-52f, 52f);
                if (Mathf.Abs(x) < 5f) continue;   // keep the bridge approach clear
                if (_cartSpots.Exists(c => Mathf.Abs(c.x - x) < 4.5f)) continue;   // and the carts' customers
                float z = Range(39f, 40.6f);
                var stack = Group("CrateStack", props, new Vector3(x, 0f, z));
                int crates = _rng.Next(2, 5);
                for (int c = 0; c < crates; c++)
                {
                    float s = Range(0.7f, 1.1f);
                    Vector3 p = new Vector3(x + Range(-1f, 1f), 0f, z + Range(-0.6f, 0.6f));
                    int level2 = c >= 2 ? 1 : 0;
                    p.y = level2 * 0.9f + s / 2f;
                    SolidBox(stack, "Crate", _wood, p, Vector3.one * s, Quaternion.Euler(0f, Range(-15f, 15f), 0f));
                }
            }
        }

        static void BuildMarkers(Transform level)
        {
            var markers = Group("LevelMarkers", level);
            void Marker(string name, Vector3 pos, int icon)
            {
                var go = new GameObject(name);
                go.transform.SetParent(markers, false);
                go.transform.position = pos;
                EditorGUIUtility.SetIconForObject(go, (Texture2D)EditorGUIUtility.IconContent($"sv_label_{icon}").image);
            }
            Marker("PlayerSpawn", PlayerSpawn, 3);
            Marker("ThiefHideout_Door", OnEdgeFront("ThiefHideout"), 3);
            Marker("Target_MagistrateYamen", OnEdgeFront("MagistrateYamen"), 6);
            Marker("Target_MarketGuildHall", OnEdgeFront("MarketGuildHall"), 6);
            Marker("Target_MerchantHouse", OnEdgeFront("MerchantHouse"), 6);
            Marker("Exit_SouthGate", new Vector3(0f, 0f, WallZ - 8f), 4);
            Marker("Exit_WestGate", new Vector3(-WallX - 8f, 0f, SideGateZ), 4);
            Marker("Exit_EastGate", new Vector3(WallX + 8f, 0f, SideGateZ), 4);
            Marker("Exit_Bridge", new Vector3(0f, 0f, CanalNorth + 4f), 4);
        }

        static Vector3 OnEdgeFront(string landmark)
        {
            var t = GameObject.Find("LinshuiTown/Landmarks/" + landmark).transform;
            string id = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject).name;
            return t.position + t.forward * (_sizes[id].z / 2f + 1f);
        }

        // ------------------------------------------------------------------ player, camera, villagers

        static void SetupPlayerAndCamera()
        {
            var player = GameObject.Find("Player");
            if (player != null) player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.Euler(0f, StartYaw, 0f));

            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig != null)
            {
                // Play starts on the thief by the market square, close and at a 45 degree yaw;
                // Tab still switches to the free RTS camera for an overview.
                rig.startMode = RTSCameraRig.Mode.Follow;
                rig.distance = StartDistance;
                rig.transform.SetPositionAndRotation(new Vector3(PlayerSpawn.x, 0f, PlayerSpawn.z), Quaternion.Euler(0f, StartYaw, 0f));
                rig.boundsMin = new Vector2(Map.xMin + 5f, Map.yMin + 5f);
                rig.boundsMax = new Vector2(Map.xMax - 5f, Map.yMax - 5f);
                EditorUtility.SetDirty(rig);
            }

            StealthSetup.SetupScene();
            SetupTrailer();
        }

        [MenuItem("WorldRPG/Levels/Setup Linshui Trailer Shots")]
        static void SetupTrailerMenu()
        {
            SetupTrailer();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Opening flythrough on the camera rig (untick Trailer Mode on TownTrailer to jump straight into play). Random mode needs
        /// only the area; the scripted shots are RTS camera moves: ground pivot, yaw (0 = looking north), distance
        /// and pitch, gliding from -> to.
        /// </summary>
        static void SetupTrailer()
        {
            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig == null) return;
            var trailer = rig.GetComponent<TownTrailer>();
            if (trailer == null)
            {
                // The flythrough is the game's opening (hold Space to start). An existing component keeps its settings.
                trailer = rig.gameObject.AddComponent<TownTrailer>();
                trailer.trailerMode = true;
            }
            trailer.rig = rig;
            var player = GameObject.Find("Player");
            trailer.hideObjects = player != null ? new[] { player } : new GameObject[0];
            var hud = Object.FindFirstObjectByType<DemoHUD>();
            trailer.disableComponents = hud != null ? new Behaviour[] { hud } : new Behaviour[0];
            trailer.title = "Linshui";
            trailer.subtitle = "A river town of the Song";
            trailer.endTitle = "Linshui";
            trailer.area = Interior;   // random shots stay inside the walls

            // Scripted alternative (Mode = Scripted); the default Random mode improvises street shots instead.

            Vector3 square = new Vector3(0f, 0f, -2f);
            trailer.shots = new List<TownTrailer.Shot>
            {
                // Close street-level glides, just above the gameplay camera (distance 38.9, pitch 45).
                Shot("Opening on the square", 12f, new Vector3(-6f, 0f, -5f), new Vector3(4f, 0f, -1f), 30f, 15f, 58.3f, 51.0f, 52f, 48f),
                Shot("Up Main Street", 10f, new Vector3(0f, 0f, -34f), new Vector3(0f, 0f, -16f), 0f, 6f, 58.3f, 53.4f, 44f, 42f),
                Shot("Market orbit", 13f, square, square, 200f, 290f, 58.3f, 53.4f, 42f, 40f),
                Shot("Stall row", 10f, new Vector3(-7f, 0f, -6f), new Vector3(7f, 0f, -2f), 20f, 0f, 48.6f, 46.2f, 46f, 44f),
                Shot("Guild hall", 9f, new Vector3(-5f, 0f, 9f), new Vector3(5f, 0f, 11f), 0f, 0f, 63.2f, 58.3f, 36f, 34f),
                Shot("Canal and quay", 12f, new Vector3(-30f, 0f, 38f), new Vector3(20f, 0f, 38f), 350f, 10f, 63.2f, 58.3f, 40f, 40f),
                Shot("Magistrate's yamen", 10f, new Vector3(40f, 0f, 18f), new Vector3(44f, 0f, 18f), 80f, 100f, 68.0f, 58.3f, 40f, 40f),
                Shot("Back alleys", 10f, new Vector3(-14f, 0f, -30f), new Vector3(-28f, 0f, -36f), 150f, 170f, 53.4f, 53.4f, 60f, 58f),
                Shot("Rise over the square", 12f, square, square, 225f, 195f, 53.4f, 97.2f, 42f, 50f),
            };
            EditorUtility.SetDirty(trailer);
        }

        static TownTrailer.Shot Shot(string name, float duration, Vector3 fromPivot, Vector3 toPivot, float fromYaw, float toYaw,
                                     float fromDistance, float toDistance, float fromPitch, float toPitch)
        {
            return new TownTrailer.Shot
            {
                name = name, duration = duration, fromPivot = fromPivot, toPivot = toPivot, fromYaw = fromYaw, toYaw = toYaw,
                fromDistance = fromDistance, toDistance = toDistance, fromPitch = fromPitch, toPitch = toPitch,
            };
        }

        // ------------------------------------------------------------------ look

        // Target: the gongbi-illustration isometric look in References/frames (warm low-contrast daylight, warm grey
        // shadows that never go black, cream ground, a soft haze, a high near-orthographic camera).
        static readonly Color Haze = new Color(0.84f, 0.84f, 0.82f);

        static void ApplyLook()
        {
            // Afternoon sun from the south-west, behind the default camera's left shoulder, so street-facing fronts
            // are lit and shadows fall up-right across the streets, long enough to read but not night-dark.
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, 50f, 0f);
                sun.useColorTemperature = false;
                sun.color = new Color(1f, 0.98f, 0.95f);   // near white: warmth comes from the materials, not a tint over everything
                sun.intensity = 1.55f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sun.shadowBias = 0.04f;
                sun.shadowNormalBias = 0.3f;
                var data = sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                if (data != null) data.softShadowQuality = UnityEngine.Rendering.Universal.SoftShadowQuality.High;
                ApplyCloudShadows(sun);
                EditorUtility.SetDirty(sun);
            }

            // Bright, near-neutral fill: shade reads as a slightly darker olive-brown of the lit colour (as in the
            // references), not blue and never black.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.70f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.61f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.38f, 0.35f);

            // Distance haze, ink contours and paper grain come from the full-screen InkEdges pass (scene fog's
            // distances don't come out as configured on this URP setup, so it stays off).
            RenderSettings.fog = false;
            InkRenderingSetup.Setup();
            var ink = AssetDatabase.LoadAssetAtPath<Material>(InkRenderingSetup.MaterialPath);
            if (ink != null)
            {
                ink.SetColor("_HazeColor", Haze);
                // Starts beyond the overview's own distance, so only the countryside past the walls fades.
                ink.SetFloat("_HazeStart", 413f);
                ink.SetFloat("_HazeEnd", 1264f);
                // Contour fades are camera distances too: scaled with the camera so lines keep their weight.
                ink.SetFloat("_LineFadeStart", 219f);
                ink.SetFloat("_LineFadeEnd", 632f);
                ink.SetFloat("_InnerFadeStart", 97f);
                ink.SetFloat("_InnerFadeEnd", 267f);
                EditorUtility.SetDirty(ink);
            }

            // High, narrow camera: close to the isometric framing of the references without losing perspective.
            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig != null)
            {
                rig.pitch = 45f;
                rig.distance = StartDistance;   // play starts close on the thief; scroll out to ~150 for the whole town
                rig.minDistance = 34f;
                rig.maxDistance = 535f;
                EditorUtility.SetDirty(rig);
                if (rig.cam != null)
                {
                    var cam = rig.cam;
                    cam.fieldOfView = CameraFov;
                    cam.nearClipPlane = 3f;
                    cam.farClipPlane = 2200f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Haze;
                    cam.transform.localRotation = Quaternion.Euler(rig.pitch, 0f, 0f);
                    cam.transform.localPosition = cam.transform.localRotation * new Vector3(0f, 0f, -rig.distance);
                    var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (camData != null)
                    {
                        camData.renderPostProcessing = true;
                        // TAA: the 300k-triangle roofs have tile detail finer than a pixel at RTS distance, which
                        // shimmers under any post-process AA (SMAA/FXAA) as the camera pans.
                        camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing;
                        camData.taaSettings.quality = UnityEngine.Rendering.Universal.TemporalAAQuality.High;
                        camData.taaSettings.contrastAdaptiveSharpening = 0.25f;
                    }
                    EditorUtility.SetDirty(cam);
                }
            }

            var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (volume == null) volume = new GameObject("Global Volume").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();
            EditorUtility.SetDirty(volume);
        }

        // ------------------------------------------------------------------ cloud / canopy dapple

        const string CookiePath = GeneratedFolder + "/CloudShadowCookie.png";
        const int CookieSize = 512;
        const float CookieWorldSize = 110f;   // metres covered by one repeat of the texture

        /// <summary>
        /// Puts a drifting dapple cookie on the sun: broad soft cloud shade plus finer leaf-like flecks. The painted
        /// references get much of their life from soft patches of shade breaking up streets and roofs.
        /// </summary>
        static void ApplyCloudShadows(Light sun)
        {
            var data = sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (data == null) return;
            sun.cookie = CreateCookie();
            data.lightCookieSize = new Vector2(CookieWorldSize, CookieWorldSize);
            data.lightCookieOffset = Vector2.zero;
            if (sun.GetComponent<VFX.CloudShadows>() == null) sun.gameObject.AddComponent<VFX.CloudShadows>();
            EditorUtility.SetDirty(data);
        }

        static Texture2D CreateCookie()
        {
            var tex = new Texture2D(CookieSize, CookieSize, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[CookieSize * CookieSize];
            for (int y = 0; y < CookieSize; y++)
            for (int x = 0; x < CookieSize; x++)
            {
                float u = x / (float)CookieSize, v = y / (float)CookieSize;
                // Clouds: low-frequency fBm, only the denser half casts shade.
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.46f, 0.68f, Fbm(u, v, 3, 4)));
                // Canopy flecks: higher frequency, thresholded into soft blobs, gathered where the clouds aren't.
                float leaves = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.66f, Fbm(u + 0.37f, v + 0.11f, 16, 3)));
                float gather = Mathf.InverseLerp(0.35f, 0.65f, Fbm(u + 0.7f, v + 0.5f, 4, 2));
                // Strong enough to survive the ambient fill (which lifts all shade): clouds take ~55% of the sun.
                float light = 1f - cloud * 0.55f - leaves * gather * 0.35f;
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(light) * 255f);
                pixels[y * CookieSize + x] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(CookiePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(CookiePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
        }

        /// <summary>Tileable fBm in [0,1]: value noise on a lattice that wraps every <paramref name="period"/> cells.</summary>
        static float Fbm(float u, float v, int period, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * period, v * period, period);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0, y0, period), b = Lattice(x0 + 1, y0, period);
            float c = Lattice(x0, y0 + 1, period), d = Lattice(x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Lattice(int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + period * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffu) / 65535f;
        }

        static UnityEngine.Rendering.VolumeProfile CreateVolumeProfile()
        {
            string path = $"{LevelFolder}/LinshuiTown_Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // Neutral keeps the painted palette (ACES would push contrast and saturation the wrong way).
            var tonemap = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tonemap.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);

            // Soft but not flat: a touch of contrast and saturation so robes, lacquer and roofs keep their colour.
            var color = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            color.postExposure.Override(0.05f);
            color.contrast.Override(12f);
            color.saturation.Override(8f);

            // Lift the blacks a little (shade never goes black) and keep highlights from clipping; colour-neutral,
            // so the scene isn't tinted yellow on top of its warm materials.
            var lgg = profile.Add<UnityEngine.Rendering.Universal.LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1f, 1f, 1f, 0.03f));
            lgg.gain.Override(new Vector4(1f, 1f, 1f, -0.04f));

            // Soft glow on bright plaster and water (and on lanterns, when they arrive).
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);

            // Depth without a colour cast: lit areas lean warm, shade leans cool (the painter's warm-light /
            // cool-shadow rule). No depth of field: a fixed focus range can't follow the RTS zoom.
            var split = profile.Add<UnityEngine.Rendering.Universal.SplitToning>(true);
            // Kept almost neutral: highlights barely warm, shade a little cool. Warm highlights read as yellow.
            split.shadows.Override(new Color(0.47f, 0.50f, 0.55f));
            split.highlights.Override(new Color(0.55f, 0.54f, 0.52f));
            split.balance.Override(0f);

            // Slightly cool overall for a fresh, clean daylight (the user found the warm grades too yellow).
            var white = profile.Add<UnityEngine.Rendering.Universal.WhiteBalance>(true);
            white.temperature.Override(-6f);

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.color.Override(new Color(0.2f, 0.18f, 0.16f));
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.6f);

            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static int BuildVillagers(Transform level)
        {
            var folk = Group("Townsfolk", level);
            var prefabs = NPCPrefabBuilder.LoadVillagerPrefabs();
            if (prefabs.Count == 0) return 0;

            Physics.SyncTransforms();
            int characterLayer = LayerMask.NameToLayer(NPCNavigationSetup.CharacterLayerName);
            int blockers = ~((1 << characterLayer) | (1 << LayerMask.NameToLayer(NPCNavigationSetup.NavBlockerLayerName)));

            // Weighted towards the market: square, main street and commerce street, then anywhere paved inside the walls.
            var areas = new List<(Rect rect, float weight)>();
            foreach (var s in Streets)
            {
                Rect r = s.rect;
                r.xMin = Mathf.Max(r.xMin, Interior.xMin + 1f); r.xMax = Mathf.Min(r.xMax, Interior.xMax - 1f);
                r.yMin = Mathf.Max(r.yMin, Interior.yMin + 1f); r.yMax = Mathf.Min(r.yMax, CanalSouth - 1.5f);
                if (r.width <= 1f || r.height <= 1f) continue;
                float w = s.name == "MarketSquare" ? 6f : s.name.StartsWith("MainStreet") || s.name.StartsWith("Commerce") ? 2f
                    : s.name.StartsWith("BackAlley") ? 0.4f : 1f;
                areas.Add((r, r.width * r.height * w));
            }
            float total = 0f;
            foreach (var a in areas) total += a.weight;

            var placedPositions = new List<Vector3>();
            int placed = 0, attempts = 0;
            while (placed < VillagerCount && attempts++ < VillagerCount * 50)
            {
                float pick = (float)_rng.NextDouble() * total;
                Rect r = areas[0].rect;
                foreach (var a in areas) { if ((pick -= a.weight) <= 0f) { r = a.rect; break; } }
                var pos = new Vector3(Range(r.xMin + 0.5f, r.xMax - 0.5f), 0f, Range(r.yMin + 0.5f, r.yMax - 0.5f));
                if ((pos - PlayerSpawn).sqrMagnitude < 36f) continue;
                if (Physics.CheckCapsule(pos + Vector3.up * 0.5f, pos + Vector3.up * 1.6f, 0.45f, blockers, QueryTriggerInteraction.Ignore)) continue;
                bool crowded = false;
                foreach (var p in placedPositions) if ((p - pos).sqrMagnitude < 1.5f * 1.5f) { crowded = true; break; }
                if (crowded) continue;

                pos.y = GroundTop;
                NPCPrefabBuilder.SpawnVillager(prefabs, _rng, folk, pos, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                placedPositions.Add(pos);
                placed++;
            }
            return placed;
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ geometry helpers

        static void Batch(Material mat, float metresPerRepeat)
        {
            if (_batches.ContainsKey(mat)) return;
            Vector2 tiling = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : Vector2.one;
            _batches[mat] = new MeshBatch { uvScale = 1f / (metresPerRepeat * Mathf.Max(0.01f, tiling.x)) };
        }

        /// <summary>
        /// One face: corners o, o+u, o+u+v, o+v, facing Cross(v, u). UVs run in metres from uvOrigin, so neighbouring
        /// pieces of the same material line up.
        /// </summary>
        static void Face(Material mat, Vector3 o, Vector3 u, Vector3 v, Vector2 uvOrigin)
        {
            if (!_batches.ContainsKey(mat)) Batch(mat, 4f);
            var b = _batches[mat];
            int i = b.verts.Count;
            Vector3 n = Vector3.Cross(v, u).normalized;
            float s = b.uvScale, lu = u.magnitude, lv = v.magnitude;
            b.verts.Add(o); b.verts.Add(o + u); b.verts.Add(o + u + v); b.verts.Add(o + v);
            for (int k = 0; k < 4; k++) b.normals.Add(n);
            b.uvs.Add(uvOrigin * s); b.uvs.Add((uvOrigin + new Vector2(lu, 0f)) * s);
            b.uvs.Add((uvOrigin + new Vector2(lu, lv)) * s); b.uvs.Add((uvOrigin + new Vector2(0f, lv)) * s);
            b.tris.Add(i); b.tris.Add(i + 2); b.tris.Add(i + 1);
            b.tris.Add(i); b.tris.Add(i + 3); b.tris.Add(i + 2);
        }

        /// <summary>A flat, upward-facing rectangle with world-space UVs.</summary>
        static void TopQuad(Material mat, Rect r, float y)
            => Face(mat, new Vector3(r.xMin, y, r.yMin), new Vector3(r.width, 0f, 0f), new Vector3(0f, 0f, r.height), new Vector2(r.xMin, r.yMin));

        /// <summary>A visual box (no bottom face) added to the material's batch.</summary>
        static void Box(Material mat, Vector3 c, Vector3 size, Quaternion q)
        {
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            Vector3 h = size / 2f;
            Vector3 P(float x, float y, float z) => c + X * (x * h.x) + Y * (y * h.y) + Z * (z * h.z);
            Vector2 off = new Vector2(c.x + c.z, c.y);
            Face(mat, P(-1, 1, -1), X * size.x, Z * size.z, new Vector2(c.x, c.z));    // top
            Face(mat, P(1, -1, -1), Z * size.z, Y * size.y, off);                       // +X
            Face(mat, P(-1, -1, -1), Y * size.y, Z * size.z, off);                      // -X  (u up, v along)
            Face(mat, P(-1, -1, 1), Y * size.y, X * size.x, off);                       // +Z
            Face(mat, P(-1, -1, -1), X * size.x, Y * size.y, off);                      // -Z
        }

        /// <summary>A visual box plus a matching BoxCollider (static, so it bakes into the navmesh).</summary>
        static void SolidBox(Transform parent, string name, Material mat, Vector3 c, Vector3 size, Quaternion q)
        {
            Box(mat, c, size, q);
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(c, q);
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        static void BoxCollider(Transform parent, string name, Rect r, float y0, float y1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(r.center.x, (y0 + y1) / 2f, r.center.y);
            go.AddComponent<BoxCollider>().size = new Vector3(r.width, y1 - y0, r.height);
            go.isStatic = true;
        }

        static Rect FlatRect(Vector3 c, Vector3 size, Quaternion q)
        {
            Vector3 ex = q * new Vector3(size.x / 2f, 0f, 0f), ez = q * new Vector3(0f, 0f, size.z / 2f);
            float hx = Mathf.Abs(ex.x) + Mathf.Abs(ez.x), hz = Mathf.Abs(ex.z) + Mathf.Abs(ez.z);
            return R(c.x - hx, c.z - hz, c.x + hx, c.z + hz);
        }

        /// <summary>Writes each material's batch to a mesh asset and adds one static renderer per material.</summary>
        static void FlushMeshes(Transform level)
        {
            var root = Group("Surfaces", level);
            foreach (var pair in _batches)
            {
                var b = pair.Value;
                if (b.verts.Count == 0) continue;
                string name = pair.Key.name.Replace("LS_", "").Replace("M_", "");
                string path = $"{GeneratedFolder}/LS_{name}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh();
                    AssetDatabase.CreateAsset(mesh, path);
                }
                mesh.Clear();
                mesh.name = $"LS_{name}";
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(b.verts);
                mesh.SetNormals(b.normals);
                mesh.SetUVs(0, b.uvs);
                mesh.SetTriangles(b.tris, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);

                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                go.isStatic = true;
            }
        }

        static Transform Group(string name, Transform parent, Vector3 pos = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
        }

        static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        static bool Chance(float p) => _rng.NextDouble() < p;

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
