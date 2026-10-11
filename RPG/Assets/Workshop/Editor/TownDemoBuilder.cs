using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Generates a greybox town (cubes only) with the RTS camera rig and a controllable character.
    /// Menu: WorldRPG/Build Town Demo Scene
    /// </summary>
    public static class TownDemoBuilder
    {
        const string ScenePath = "Assets/Workshop/TownDemo.unity";
        const string MatFolder = "Assets/Workshop/Materials";
        const int Seed = 27;

        // Street layout (shared by X and Z axes). Center street is the main avenue.
        static readonly float[] StreetCenters = { -56f, -28f, 0f, 28f, 56f };
        static readonly float[] StreetWidths = { 5f, 5f, 8f, 5f, 5f };
        const float WallHalfExtent = 63f;
        const float PlazaHalfSize = 15f;

        static Material _ground, _street, _stone, _plaster, _wood, _roof, _ridge, _red, _door,
            _leaves, _autumn, _trunk, _cityWall, _canvas, _canvasRed, _canvasBlue, _player, _playerHat;
        static Material[] _cloth;
        static System.Random _rng;

        [MenuItem("WorldRPG/Build Town Demo Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            _rng = new System.Random(Seed);
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var town = new GameObject("Town").transform;
            BuildGround(town);
            BuildBlocks(town);
            BuildPlaza(town);
            BuildCityWall(town);
            BuildOutskirts(town);
            BuildTownsfolk(town);

            Transform player = BuildPlayer();
            SetupCameraAndLight(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        // ------------------------------------------------------------------ materials

        static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/Workshop", "Materials");

            _ground = Mat("Ground", new Color(0.58f, 0.55f, 0.40f));
            _street = Mat("Street", new Color(0.70f, 0.64f, 0.50f));
            _stone = Mat("Stone", new Color(0.60f, 0.60f, 0.58f));
            _plaster = Mat("Plaster", new Color(0.86f, 0.80f, 0.68f));
            _wood = Mat("Wood", new Color(0.50f, 0.34f, 0.22f));
            _roof = Mat("Roof", new Color(0.26f, 0.28f, 0.31f), 0.3f);
            _ridge = Mat("Ridge", new Color(0.18f, 0.19f, 0.21f));
            _red = Mat("Lacquer", new Color(0.62f, 0.17f, 0.14f), 0.35f);
            _door = Mat("Door", new Color(0.28f, 0.18f, 0.12f));
            _leaves = Mat("Leaves", new Color(0.42f, 0.52f, 0.30f));
            _autumn = Mat("LeavesAutumn", new Color(0.86f, 0.46f, 0.20f));
            _trunk = Mat("Trunk", new Color(0.34f, 0.24f, 0.17f));
            _cityWall = Mat("CityWall", new Color(0.55f, 0.52f, 0.48f));
            _canvas = Mat("Canvas", new Color(0.90f, 0.82f, 0.62f));
            _canvasRed = Mat("CanvasRed", new Color(0.75f, 0.30f, 0.25f));
            _canvasBlue = Mat("CanvasBlue", new Color(0.35f, 0.50f, 0.62f));
            _player = Mat("Player", new Color(0.15f, 0.45f, 0.95f), 0.4f);
            _playerHat = Mat("PlayerHat", new Color(1.00f, 0.80f, 0.15f), 0.4f);
            _cloth = new[]
            {
                Mat("Cloth0", new Color(0.85f, 0.45f, 0.20f)),
                Mat("Cloth1", new Color(0.30f, 0.45f, 0.55f)),
                Mat("Cloth2", new Color(0.88f, 0.84f, 0.76f)),
                Mat("Cloth3", new Color(0.70f, 0.35f, 0.40f)),
                Mat("Cloth4", new Color(0.35f, 0.40f, 0.30f)),
                Mat("Cloth5", new Color(0.55f, 0.65f, 0.75f)),
            };
        }

        static VolumeProfile CreateVolumeProfile()
        {
            const string path = "Assets/Workshop/TownDemoVolumeProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);

            // Slightly desaturated, warm grade toward the painted reference look.
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(-12f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.9f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static Material Mat(string name, Color color, float smoothness = 0.15f)
        {
            string path = $"{MatFolder}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, bool collider = true, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat,
            bool collider = true, Quaternion? rot = null)
            => Prim(PrimitiveType.Cube, name, parent, localPos, size, mat, collider, rot);

        static Transform Group(string name, Transform parent, Vector3 localPos = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        static bool Chance(float p) => _rng.NextDouble() < p;
        static T Pick<T>(T[] items) => items[_rng.Next(items.Length)];

        // ------------------------------------------------------------------ town parts

        static void BuildGround(Transform town)
        {
            Box("Ground", town, new Vector3(0f, -0.5f, 0f), new Vector3(180f, 1f, 180f), _ground);

            var streets = Group("Streets", town);
            for (int i = 0; i < StreetCenters.Length; i++)
            {
                float c = StreetCenters[i], w = StreetWidths[i];
                // The main avenue runs out through the city gates.
                float length = c == 0f ? 178f : WallHalfExtent * 2f - 4f;
                // Colliders on the visible street/paving slabs so characters stand on them, not sunk 2-4 cm into them.
                Box($"Street_NS_{i}", streets, new Vector3(c, 0.01f, 0f), new Vector3(w, 0.02f, length), _street);
                Box($"Street_EW_{i}", streets, new Vector3(0f, 0.011f, c), new Vector3(length, 0.02f, w), _street);
            }
        }

        static void BuildBlocks(Transform town)
        {
            var blocks = Group("Blocks", town);
            for (int i = 0; i < StreetCenters.Length - 1; i++)
            for (int j = 0; j < StreetCenters.Length - 1; j++)
            {
                float x0 = StreetCenters[i] + StreetWidths[i] * 0.5f;
                float x1 = StreetCenters[i + 1] - StreetWidths[i + 1] * 0.5f;
                float z0 = StreetCenters[j] + StreetWidths[j] * 0.5f;
                float z1 = StreetCenters[j + 1] - StreetWidths[j + 1] * 0.5f;
                FillBlock(Group($"Block_{i}_{j}", blocks), x0, x1, z0, z1);
            }
        }

        static void FillBlock(Transform block, float x0, float x1, float z0, float z1)
        {
            const float alley = 1.4f;
            const float sidewalk = 0.8f;
            int nx = _rng.Next(2, 4);
            int nz = _rng.Next(2, 4);
            float lotW = (x1 - x0 - sidewalk * 2f - alley * (nx - 1)) / nx;
            float lotD = (z1 - z0 - sidewalk * 2f - alley * (nz - 1)) / nz;

            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float cx = x0 + sidewalk + lotW * 0.5f + ix * (lotW + alley);
                float cz = z0 + sidewalk + lotD * 0.5f + iz * (lotD + alley);

                if (Mathf.Abs(cx) < PlazaHalfSize + 2f && Mathf.Abs(cz) < PlazaHalfSize + 2f) continue;

                if (Chance(0.12f))
                {
                    Tree(block, new Vector3(cx, 0f, cz));
                    continue;
                }

                float w = lotW * Range(0.75f, 0.95f);
                float d = lotD * Range(0.75f, 0.95f);
                float h = Range(3f, 5.5f);

                // Door faces the nearest block edge (a street).
                float dl = cx - x0, dr = x1 - cx, db = cz - z0, df = z1 - cz;
                float min = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, df));
                Vector3 facing = min == dl ? Vector3.left : min == dr ? Vector3.right : min == db ? Vector3.back : Vector3.forward;

                House(block, new Vector3(cx, 0f, cz), w, d, h, facing);
            }
        }

        static void House(Transform parent, Vector3 pos, float w, float d, float h, Vector3 facing)
        {
            var root = Group("House", parent, pos);
            Box("Base", root, new Vector3(0f, 0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), _stone, false);
            Box("Body", root, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Chance(0.6f) ? _plaster : _wood);

            // Gable roof: a cube rotated 45° about the long axis, flattened by a scaled parent.
            bool alongX = w >= d;
            float shortSide = alongX ? d : w;
            float longSide = alongX ? w : d;
            float s = shortSide * 1.2f / Mathf.Sqrt(2f);
            const float flatten = 0.55f;
            var roof = Group("Roof", root, new Vector3(0f, h, 0f));
            roof.localScale = new Vector3(1f, flatten, 1f);
            Box("RoofMesh", roof, Vector3.zero,
                alongX ? new Vector3(longSide * 1.1f, s, s) : new Vector3(s, s, longSide * 1.1f),
                _roof, false, alongX ? Quaternion.Euler(45f, 0f, 0f) : Quaternion.Euler(0f, 0f, 45f));

            float peak = h + s / Mathf.Sqrt(2f) * flatten; // half-diagonal of the rotated cube, flattened
            Box("Ridge", root, new Vector3(0f, peak, 0f),
                alongX ? new Vector3(longSide * 1.15f, 0.3f, 0.35f) : new Vector3(0.35f, 0.3f, longSide * 1.15f),
                _ridge, false);

            // Door on the street side.
            float half = Mathf.Abs(facing.x) > 0f ? w * 0.5f : d * 0.5f;
            Vector3 doorPos = facing * (half + 0.03f) + Vector3.up * 1.1f;
            Vector3 doorSize = Mathf.Abs(facing.x) > 0f ? new Vector3(0.08f, 2.2f, 1.4f) : new Vector3(1.4f, 2.2f, 0.08f);
            Box("Door", root, doorPos, doorSize, _door, false);
        }

        static void Tree(Transform parent, Vector3 pos)
        {
            var root = Group("Tree", parent, pos);
            float height = Range(0.8f, 1.3f);
            Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0f, 1.5f * height, 0f),
                new Vector3(0.45f, 1.5f * height, 0.45f), _trunk);
            float size = Range(3f, 4.5f);
            Prim(PrimitiveType.Sphere, "Canopy", root, new Vector3(0f, 3f * height + size * 0.35f, 0f),
                new Vector3(size, size * 0.85f, size), Chance(0.3f) ? _autumn : _leaves, false);
        }

        static void BuildPlaza(Transform town)
        {
            var plaza = Group("Plaza", town);
            Box("Paving", plaza, new Vector3(0f, 0.02f, 0f), new Vector3(PlazaHalfSize * 2f, 0.04f, PlazaHalfSize * 2f), _stone);

            // Central pavilion on a raised platform with steps.
            var pavilion = Group("Pavilion", plaza, new Vector3(0f, 0f, 6f));
            Box("Platform", pavilion, new Vector3(0f, 0.4f, 0f), new Vector3(9f, 0.8f, 9f), _stone);
            Box("Step", pavilion, new Vector3(0f, 0.2f, -5f), new Vector3(4f, 0.4f, 1.2f), _stone);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box("Pillar", pavilion, new Vector3(x * 3.2f, 2.8f, z * 3.2f), new Vector3(0.5f, 4f, 0.5f), _red);
            Box("Roof1", pavilion, new Vector3(0f, 5f, 0f), new Vector3(10f, 0.6f, 10f), _roof, false);
            Box("Roof1Top", pavilion, new Vector3(0f, 5.6f, 0f), new Vector3(7.5f, 0.6f, 7.5f), _roof, false);
            Box("Roof2Body", pavilion, new Vector3(0f, 6.6f, 0f), new Vector3(4.5f, 1.4f, 4.5f), _red, false);
            Box("Roof2", pavilion, new Vector3(0f, 7.5f, 0f), new Vector3(6.5f, 0.5f, 6.5f), _roof, false);
            Box("Roof2Top", pavilion, new Vector3(0f, 8.1f, 0f), new Vector3(3.5f, 0.8f, 3.5f), _roof, false);
            Box("Finial", pavilion, new Vector3(0f, 8.9f, 0f), new Vector3(0.4f, 0.8f, 0.4f), _red, false);

            // Market stalls around the south half of the plaza.
            var stalls = Group("Stalls", plaza);
            Material[] canopies = { _canvas, _canvasRed, _canvasBlue };
            Vector3[] spots =
            {
                new(-11f, 0f, -11f), new(-6f, 0f, -11.5f), new(6f, 0f, -11.5f), new(11f, 0f, -11f),
                new(-11.5f, 0f, -4f), new(11.5f, 0f, -4f), new(-11.5f, 0f, 3f), new(11.5f, 0f, 3f),
            };
            foreach (var spot in spots)
                Stall(stalls, spot, Pick(canopies));

            // Banners (tall red poles) at the plaza corners.
            foreach (var corner in new[] { new Vector3(-14f, 0f, 14f), new Vector3(14f, 0f, 14f) })
            {
                Box("BannerPole", plaza, corner + Vector3.up * 4f, new Vector3(0.3f, 8f, 0.3f), _red);
                Box("Banner", plaza, corner + new Vector3(0.5f, 5.5f, 0f), new Vector3(0.8f, 4f, 0.05f), _canvas, false);
            }
        }

        static void Stall(Transform parent, Vector3 pos, Material canopy)
        {
            var root = Group("Stall", parent, pos);
            Box("Table", root, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.9f, 1.1f), _wood);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box("Post", root, new Vector3(x * 1.05f, 1.1f, z * 0.75f), new Vector3(0.1f, 2.2f, 0.1f), _wood, false);
            Box("Canopy", root, new Vector3(0f, 2.25f, 0f), new Vector3(2.5f, 0.1f, 1.9f), canopy, false,
                Quaternion.Euler(8f, 0f, 0f));
            Box("Goods", root, new Vector3(0f, 1.0f, 0f), new Vector3(1.4f, 0.2f, 0.6f), Pick(_cloth), false);
        }

        static void BuildCityWall(Transform town)
        {
            var wall = Group("CityWall", town);
            const float height = 5f, thick = 2f, gateHalf = 5.5f;
            float e = WallHalfExtent;
            float segLen = e - gateHalf;
            float segCenter = gateHalf + segLen * 0.5f;

            for (int side = 0; side < 4; side++)
            {
                Quaternion rot = Quaternion.Euler(0f, side * 90f, 0f);
                var sideRoot = Group($"Side_{side}", wall);
                sideRoot.localRotation = rot;
                foreach (float sign in new[] { -1f, 1f })
                    Box("Wall", sideRoot, new Vector3(sign * segCenter, height * 0.5f, e), new Vector3(segLen, height, thick), _cityWall);

                // Gatehouse spanning the avenue.
                foreach (float sign in new[] { -1f, 1f })
                    Box("GateTower", sideRoot, new Vector3(sign * (gateHalf + 1.5f), 3.5f, e), new Vector3(3f, 7f, 3.5f), _cityWall);
                Box("GateBridge", sideRoot, new Vector3(0f, 6.25f, e), new Vector3(gateHalf * 2f + 6f, 1.5f, 3.5f), _cityWall, false);
                Box("GateHall", sideRoot, new Vector3(0f, 8f, e), new Vector3(12f, 2f, 3f), _red, false);
                Box("GateRoof", sideRoot, new Vector3(0f, 9.3f, e), new Vector3(15f, 0.6f, 5f), _roof, false);
                Box("GateRoofTop", sideRoot, new Vector3(0f, 9.8f, e), new Vector3(11f, 0.5f, 3f), _roof, false);

                Box("CornerTower", sideRoot, new Vector3(e, 3.5f, e), new Vector3(4.5f, 7f, 4.5f), _cityWall);
            }
        }

        static void BuildOutskirts(Transform town)
        {
            var trees = Group("Outskirts", town);
            for (int n = 0; n < 90; n++)
            {
                float x = Range(-88f, 88f), z = Range(-88f, 88f);
                bool insideWall = Mathf.Abs(x) < WallHalfExtent + 4f && Mathf.Abs(z) < WallHalfExtent + 4f;
                bool onAvenue = Mathf.Abs(x) < 6f || Mathf.Abs(z) < 6f;
                if (insideWall || onAvenue) continue;
                Tree(trees, new Vector3(x, 0f, z));
            }

            // Street-side trees inside the walls.
            var streetTrees = Group("StreetTrees", town);
            for (int n = 0; n < 24; n++)
            {
                int i = _rng.Next(StreetCenters.Length);
                float edge = StreetCenters[i] + (Chance(0.5f) ? 1f : -1f) * (StreetWidths[i] * 0.5f + 0.4f);
                float along = Range(-58f, 58f);
                if (Mathf.Abs(along) < PlazaHalfSize + 2f) continue;
                Tree(streetTrees, Chance(0.5f) ? new Vector3(edge, 0f, along) : new Vector3(along, 0f, edge));
            }

            // A few carts parked along streets.
            var carts = Group("Carts", town);
            for (int n = 0; n < 8; n++)
            {
                int i = _rng.Next(StreetCenters.Length);
                float along = Range(-55f, 55f);
                if (Mathf.Abs(along) < PlazaHalfSize + 2f) continue;
                bool ns = Chance(0.5f);
                Vector3 pos = ns ? new Vector3(StreetCenters[i] + 1f, 0f, along) : new Vector3(along, 0f, StreetCenters[i] + 1f);
                Cart(carts, pos, ns ? 0f : 90f);
            }
        }

        static void Cart(Transform parent, Vector3 pos, float yaw)
        {
            var root = Group("Cart", parent, pos);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Bed", root, new Vector3(0f, 1.0f, 0f), new Vector3(1.6f, 0.6f, 2.6f), _wood);
            Box("Cover", root, new Vector3(0f, 1.6f, 0.1f), new Vector3(1.5f, 0.6f, 1.8f), _canvas, false);
            foreach (float side in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Wheel", root, new Vector3(side * 0.9f, 0.6f, -0.2f),
                    new Vector3(1.2f, 0.06f, 1.2f), _door, false, Quaternion.Euler(0f, 0f, 90f));
            Box("Shaft", root, new Vector3(0f, 0.8f, 2.2f), new Vector3(1.2f, 0.1f, 2f), _wood, false);
        }

        static void BuildTownsfolk(Transform town)
        {
            var folk = Group("Townsfolk", town);
            var villagers = NPCPrefabBuilder.LoadVillagerPrefabs();
            int placed = 0;
            while (placed < 70)
            {
                Vector3 pos;
                if (Chance(0.3f))
                {
                    pos = new Vector3(Range(-PlazaHalfSize + 1f, PlazaHalfSize - 1f), 0f, Range(-PlazaHalfSize + 1f, 0f));
                }
                else
                {
                    int i = _rng.Next(StreetCenters.Length);
                    float across = StreetCenters[i] + Range(-StreetWidths[i] * 0.4f, StreetWidths[i] * 0.4f);
                    float along = Range(-60f, 60f);
                    pos = Chance(0.5f) ? new Vector3(across, 0f, along) : new Vector3(along, 0f, across);
                }
                if (pos.sqrMagnitude < 36f) continue; // keep the spawn point clear

                Quaternion yaw = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                if (villagers.Count > 0)
                {
                    NPCPrefabBuilder.SpawnVillager(villagers, _rng, folk, pos, yaw);
                    placed++;
                    continue;
                }

                // Placeholder capsule until villager prefabs are built (WorldRPG/NPC/Build Villager Prefabs).
                var root = Group("Villager", folk, pos);
                root.localRotation = yaw;
                float scale = Range(0.85f, 1.05f);
                Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.85f * scale, 0f),
                    new Vector3(0.5f, 0.85f, 0.5f) * scale, Pick(_cloth));
                Prim(PrimitiveType.Cylinder, "Hat", root, new Vector3(0f, 1.75f * scale, 0f),
                    new Vector3(0.35f, 0.08f, 0.35f) * scale, _ridge, false);
                placed++;
            }
        }

        static Transform BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, -20f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;

            Prim(PrimitiveType.Capsule, "Body", player.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), _player, false);
            Prim(PrimitiveType.Cylinder, "Hat", player.transform, new Vector3(0f, 1.85f, 0f), new Vector3(0.9f, 0.06f, 0.9f), _playerHat, false);
            Box("Nose", player.transform, new Vector3(0f, 1.4f, 0.35f), new Vector3(0.25f, 0.2f, 0.3f), _playerHat, false);

            player.AddComponent<TopDownCharacter>();
            return player.transform;
        }

        static void SetupCameraAndLight(Transform player)
        {
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.position = new Vector3(0f, 0f, -8f);

            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            cam.transform.SetParent(rigGo.transform, false);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;

            var rig = rigGo.AddComponent<RTSCameraRig>();
            rig.cam = cam;
            rig.followTarget = player;
            rig.pitch = 50f;
            rig.distance = 60f;
            cam.transform.localRotation = Quaternion.Euler(rig.pitch, 0f, 0f);
            cam.transform.localPosition = cam.transform.localRotation * new Vector3(0f, 0f, -rig.distance);

            player.GetComponent<TopDownCharacter>().rig = rig;

            var hud = new GameObject("DemoHUD").AddComponent<DemoHUD>();
            hud.rig = rig;

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(45f, 225f, 0f);
                light.color = new Color(1f, 0.94f, 0.84f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.75f;
            }

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.76f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.60f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.37f, 0.32f);
        }
    }
}
