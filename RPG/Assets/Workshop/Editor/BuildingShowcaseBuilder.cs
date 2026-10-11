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
        const string ModelRoot = "Assets/Art/Buildings/SongDynasty";
        const string PrefabRoot = "Assets/Prefabs/Buildings/SongDynasty";
        const string SourceScene = "Assets/Workshop/TownDemo.unity";
        const string ScenePath = "Assets/Workshop/BuildingShowcase.unity";
        const string MatFolder = "Assets/Workshop/Materials";

        // Blender exports the Meshy front (-Y) as Unity +Z, so no correction is needed:
        // every prefab faces +Z like the characters.
        const float ModelYaw = 0f;

        const int Columns = 5;
        const float CellX = 24f;
        const float CellZ = 28f;

        [MenuItem("WorldRPG/Buildings/Build Building Prefabs")]
        static void BuildPrefabsFromMenu() => BuildPrefabs();

        [MenuItem("WorldRPG/Buildings/Build Building Showcase Scene")]
        static void BuildSceneFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            List<GameObject> prefabs = BuildPrefabs();
            if (prefabs.Count == 0) return "No building models to show.";

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(SourceScene, ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var town = GameObject.Find("Town");
            if (town != null) Object.DestroyImmediate(town);

            var root = new GameObject("BuildingShowcase").transform;
            int rows = Mathf.CeilToInt(prefabs.Count / (float)Columns);
            float width = (Columns - 1) * CellX;
            float depth = (rows - 1) * CellZ;
            BuildGround(root, width, depth, rows);

            for (int i = 0; i < prefabs.Count; i++)
            {
                int col = i % Columns, row = i / Columns;
                var pos = new Vector3(-width / 2f + col * CellX, 0f, row * CellZ);
                // Face the street to the south (-Z), where the player starts.
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], root);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));

                Bounds b = WorldBounds(go);
                Label(root, prefabs[i].name, new Vector3(pos.x, 0f, b.min.z - 1.5f));
            }

            var player = GameObject.Find("Player");
            if (player != null) player.transform.position = new Vector3(0f, 0.05f, -14f);

            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig != null)
            {
                rig.boundsMin = new Vector2(-width / 2f - 30f, -40f);
                rig.boundsMax = new Vector2(width / 2f + 30f, depth + 30f);
                rig.distance = 40f;
                EditorUtility.SetDirty(rig);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        // ------------------------------------------------------------------ prefabs

        public static List<GameObject> BuildPrefabs()
        {
            var result = new List<GameObject>();
            if (!Directory.Exists(ModelRoot))
            {
                Debug.LogWarning($"[BuildingShowcase] no building models in {ModelRoot} (run References/MeshyModels/process_buildings.sh)");
                return result;
            }
            EnsureFolder(PrefabRoot);

            foreach (string dir in Directory.GetDirectories(ModelRoot))
            {
                string id = Path.GetFileName(dir);
                string folder = $"{ModelRoot}/{id}";
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
                    var col = mr.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                }
                root.isStatic = true;
                foreach (Transform t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{id}.prefab");
                Object.DestroyImmediate(root);
                result.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            return result;
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
            mat.SetFloat("_Smoothness", 1f); // scales the map's alpha
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

        static void BuildGround(Transform root, float width, float depth, int rows)
        {
            var ground = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Ground.mat");
            var street = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Street.mat");

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(root, false);
            g.transform.localPosition = new Vector3(0f, 0f, depth / 2f);
            g.transform.localScale = new Vector3((width + 80f) / 10f, 1f, (depth + 80f) / 10f);
            if (ground != null) g.GetComponent<Renderer>().sharedMaterial = ground;
            g.isStatic = true;

            // One street in front of each row of buildings.
            for (int r = 0; r < rows; r++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = $"Street_{r}";
                s.transform.SetParent(root, false);
                s.transform.localPosition = new Vector3(0f, 0.01f, r * CellZ - CellZ / 2f + 1f);
                s.transform.localScale = new Vector3(width + 30f, 0.02f, 6f);
                if (street != null) s.GetComponent<Renderer>().sharedMaterial = street;
                Object.DestroyImmediate(s.GetComponent<Collider>());
                s.isStatic = true;
            }
        }

        static void Label(Transform root, string id, Vector3 pos)
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
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.8f);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
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
