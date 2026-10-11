using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Builds materials + prefabs for the Meshy Song dynasty building set and lays them out in a
    /// showcase scene (copy of TownDemo with the town removed, so player/camera/HUD stay identical).
    /// Menu: WorldRPG/Buildings/...
    /// </summary>
    public static class BuildingShowcaseBuilder
    {
        // (model folder, prefab folder) per Meshy set; each model lives in <model folder>/<id>/<id>.fbx
        const string BuildingModels = "Assets/Art/Buildings/SongDynasty";
        const string BuildingPrefabs = "Assets/Prefabs/Buildings/SongDynasty";
        const string StallModels = "Assets/Art/Buildings/Stalls";
        const string StallPrefabs = "Assets/Prefabs/Stalls";
        const string SourceScene = "Assets/Workshop/TownDemo.unity";
        const string ScenePath = "Assets/Workshop/BuildingShowcase.unity";
        const string StallScenePath = "Assets/Workshop/StallShowcase.unity";
        // One scene per building, for reviewing each one up close.
        const string SingleSceneFolder = "Assets/Workshop/BuildingS";
        const string MatFolder = "Assets/Workshop/Materials";

        // Blender exports the Meshy front (-Y) as Unity +Z, so no correction is needed:
        // every prefab faces +Z like the characters.
        const float ModelYaw = 0f;

        const int MaxColliderTris = 300000;

        const int Columns = 5;
        const float Gap = 3f;          // between neighbours in a row, and between a street and the next row
        const float StreetWidth = 4f;

        [MenuItem("WorldRPG/Buildings/Build Building Prefabs")]
        static void BuildPrefabsFromMenu() => BuildPrefabs();

        [MenuItem("WorldRPG/Buildings/Build Building Showcase Scene")]
        static void BuildSceneFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("WorldRPG/Buildings/Build Stall Showcase Scene")]
        static void BuildStallSceneFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildStalls();
        }

        [MenuItem("WorldRPG/Buildings/Build Per-Building Scenes")]
        static void BuildSingleScenesFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildSingleScenes();
        }

        public static string Build()
        {
            var prefabs = new List<GameObject>();
            BuildPrefabs(BuildingModels, BuildingPrefabs, prefabs);
            AssetDatabase.SaveAssets();
            return BuildShowcase(prefabs, ScenePath, 40f, Gap, 0.1f);
        }

        public static string BuildStalls()
        {
            var prefabs = new List<GameObject>();
            BuildPrefabs(StallModels, StallPrefabs, prefabs);
            AssetDatabase.SaveAssets();
            // Stalls are only 2-3.5 m wide: spread them out so the signs don't overlap.
            return BuildShowcase(prefabs, StallScenePath, 20f, 6f, 0.05f);
        }

        static string BuildShowcase(List<GameObject> prefabs, string scenePath, float cameraDistance, float gap, float labelSize)
        {
            if (prefabs.Count == 0) return "No models to show.";

            Scene scene = OpenTownCopy(scenePath);

            var root = new GameObject("BuildingShowcase").transform;

            // Pack each row by the buildings' real footprints (sizes vary 5 m .. 15 m wide).
            int rows = Mathf.CeilToInt(prefabs.Count / (float)Columns);
            var rowWidth = new float[rows];
            var rowDepth = new float[rows];
            var sizes = new Vector3[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                sizes[i] = PrefabBounds(prefabs[i]).size;
                int row = i / Columns;
                rowWidth[row] += sizes[i].x + (i % Columns > 0 ? gap : 0f);
                rowDepth[row] = Mathf.Max(rowDepth[row], sizes[i].z);
            }
            float width = Mathf.Max(rowWidth);
            var rowZ = new float[rows];
            for (int r = 1; r < rows; r++)
                rowZ[r] = rowZ[r - 1] + rowDepth[r - 1] / 2f + StreetWidth + gap + rowDepth[r] / 2f;
            float depth = rowZ[rows - 1];
            BuildGround(root, width, depth, rowZ, rowDepth);

            float x = 0f;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int col = i % Columns, row = i / Columns;
                if (col == 0) x = -rowWidth[row] / 2f;
                // Prefab pivot is bottom centre; rotated 180 so the front faces the street to the south (-Z).
                var pos = new Vector3(x + sizes[i].x / 2f, 0f, rowZ[row]);
                x += sizes[i].x + gap;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], root);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));

                Label(root, prefabs[i].name, new Vector3(pos.x, 0f, rowZ[row] - rowDepth[row] / 2f - 1f), labelSize);
            }

            var player = GameObject.Find("Player");
            if (player != null) player.transform.position = new Vector3(0f, 0.05f, -rowDepth[0] / 2f - StreetWidth - 2f);

            FrameRig(new Vector2(-width / 2f - 30f, -40f), new Vector2(width / 2f + 30f, depth + 30f), cameraDistance);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return scenePath;
        }

        public static string BuildSingleScenes()
        {
            List<GameObject> prefabs = BuildPrefabs();
            if (prefabs.Count == 0) return "No building models to show.";
            EnsureFolder(SingleSceneFolder);

            foreach (GameObject prefab in prefabs)
            {
                Scene scene = OpenTownCopy($"{SingleSceneFolder}/{prefab.name}.unity");
                var root = new GameObject("BuildingShowcase").transform;

                Vector3 size = PrefabBounds(prefab).size;
                BuildGround(root, size.x, 0f, new[] { 0f }, new[] { size.z });
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                Label(root, prefab.name, new Vector3(-size.x / 2f - 1.5f, 0f, -size.z / 2f - 1f), 0.1f);

                var player = GameObject.Find("Player");
                if (player != null) player.transform.position = new Vector3(0f, 0.05f, -size.z / 2f - StreetWidth - 2f);

                float r = Mathf.Max(size.x, size.z);
                FrameRig(new Vector2(-r - 10f, -r - 10f), new Vector2(r + 10f, r + 10f), Mathf.Max(15f, r * 2f));

                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            return $"{prefabs.Count} scenes in {SingleSceneFolder}";
        }

        // Copy of TownDemo with the town removed, so player/camera/HUD stay identical.
        static Scene OpenTownCopy(string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(SourceScene, path);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var town = GameObject.Find("Town");
            if (town != null) Object.DestroyImmediate(town);
            return scene;
        }

        static void FrameRig(Vector2 boundsMin, Vector2 boundsMax, float distance)
        {
            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig == null) return;
            rig.boundsMin = boundsMin;
            rig.boundsMax = boundsMax;
            rig.distance = distance;
            EditorUtility.SetDirty(rig);
        }

        // ------------------------------------------------------------------ prefabs

        public static List<GameObject> BuildPrefabs()
        {
            var result = new List<GameObject>();
            BuildPrefabs(BuildingModels, BuildingPrefabs, result);
            BuildPrefabs(StallModels, StallPrefabs, result);
            AssetDatabase.SaveAssets();
            return result;
        }

        static void BuildPrefabs(string modelRoot, string prefabRoot, List<GameObject> result)
        {
            if (!Directory.Exists(modelRoot))
            {
                Debug.LogWarning($"[BuildingShowcase] no models in {modelRoot} (run References/MeshyModels/rebake_building.sh)");
                return;
            }
            EnsureFolder(prefabRoot);

            foreach (string dir in Directory.GetDirectories(modelRoot))
            {
                string id = Path.GetFileName(dir);
                if (id.StartsWith("PolyFew")) continue; // local PolyFewBackup (git-ignored), not a building
                string folder = $"{modelRoot}/{id}";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{id}.fbx");
                if (model == null)
                {
                    Debug.LogWarning($"[BuildingShowcase] missing model for {id}");
                    continue;
                }

                Material mat = BuildMaterial(folder, id);

                var root = new GameObject(id);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "Model";
                inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
                foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>())
                {
                    mr.sharedMaterial = mat;
                    var mf = mr.GetComponent<MeshFilter>();
                    // Raw Meshy meshes (millions of tris) take minutes to cook; collide only once reduced.
                    if (mf.sharedMesh.GetIndexCount(0) / 3 > MaxColliderTris)
                    {
                        Debug.LogWarning($"[BuildingShowcase] {id}: {mf.sharedMesh.GetIndexCount(0) / 3} tris, skipping MeshCollider until reduced");
                        continue;
                    }
                    var col = mr.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                }
                root.isStatic = true;
                foreach (Transform t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabRoot}/{id}.prefab");
                Object.DestroyImmediate(root);
                result.Add(prefab);
            }
        }

        static Material BuildMaterial(string folder, string id)
        {
            string path = $"{folder}/{id}_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{id}_BaseColor.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{id}_Normal.png");
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{id}_MetallicSmoothness.png");

            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.SetTexture("_MetallicGlossMap", metal);
            // With a map this scales its alpha; base-colour-only sets get a matte timber/plaster look.
            mat.SetFloat("_Smoothness", metal != null ? 1f : 0.15f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            // Meshy awnings/banners are single sheets; render both faces.
            mat.SetFloat("_Cull", 0f);
            mat.doubleSidedGI = true;
            if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            if (metal != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP"); else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ scene parts

        static void BuildGround(Transform root, float width, float depth, float[] rowZ, float[] rowDepth)
        {
            var ground = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Ground.mat");
            var street = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Street.mat");

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(root, false);
            g.transform.localPosition = new Vector3(0f, 0f, depth / 2f);
            g.transform.localScale = new Vector3((width + 60f) / 10f, 1f, (depth + 60f) / 10f);
            if (ground != null) g.GetComponent<Renderer>().sharedMaterial = ground;
            g.isStatic = true;

            // One street in front of each row of buildings.
            for (int r = 0; r < rowZ.Length; r++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = $"Street_{r}";
                s.transform.SetParent(root, false);
                s.transform.localPosition = new Vector3(0f, 0.01f, rowZ[r] - rowDepth[r] / 2f - StreetWidth / 2f);
                s.transform.localScale = new Vector3(width + 20f, 0.02f, StreetWidth);
                if (street != null) s.GetComponent<Renderer>().sharedMaterial = street;
                Object.DestroyImmediate(s.GetComponent<Collider>());
                s.isStatic = true;
            }
        }

        static void Label(Transform root, string id, Vector3 pos, float size)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"Sign_{id}";
            post.transform.SetParent(root, false);
            post.transform.localPosition = pos + new Vector3(0f, 0.9f, 0f);
            post.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
            var wood = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Wood.mat");
            if (wood != null) post.GetComponent<Renderer>().sharedMaterial = wood;

            var text = new GameObject("Label");
            text.transform.SetParent(post.transform, false);
            text.transform.localScale = new Vector3(1f / 0.12f, 1f / 1.8f, 1f / 0.12f);
            text.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            // Read from the south (player side).
            text.transform.localRotation = Quaternion.identity;
            var tm = text.AddComponent<TextMesh>();
            tm.text = id.Replace('_', ' ');
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.8f);
        }

        // Mesh bounds in prefab space (no instantiation, so it works for the raw multi-million-tri meshes too).
        static Bounds PrefabBounds(GameObject prefab)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            Bounds b = default;
            for (int i = 0; i < filters.Length; i++)
            {
                var m = filters[i].transform.localToWorldMatrix;
                var mb = filters[i].sharedMesh.bounds;
                var c = m.MultiplyPoint3x4(mb.center);
                var e = mb.extents;
                var ext = new Vector3(
                    Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                    Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                    Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
                var wb = new Bounds(c, ext * 2f);
                if (i == 0) b = wb; else b.Encapsulate(wb);
            }
            return b;
        }

        static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
