using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Builds one NPC prefab per villager model folder (Assets/Art/Characters/song_*), and swaps the
    /// placeholder capsules under Town/Townsfolk for random villager instances.
    /// Menu: WorldRPG/NPC/...
    /// </summary>
    public static class NPCPrefabBuilder
    {
        const string CharactersFolder = "Assets/Art/Characters";
        const string VillagerFolderPrefix = "song_";
        const string PrefabFolder = "Assets/Prefabs/NPC/Villagers";
        const string ControllerPath = "Assets/Art/Characters/AnimatorController/BasicAC.controller";
        const int Seed = 27;

        // Folder suffix -> role. Folders look like "song_male_02_carpenter".
        static readonly Dictionary<string, NPCRole> Roles = new Dictionary<string, NPCRole>
        {
            { "farm_worker", NPCRole.FarmWorker },
            { "carpenter", NPCRole.Carpenter },
            { "porter", NPCRole.Porter },
            { "shopkeeper", NPCRole.Shopkeeper },
            { "elder_villager", NPCRole.Elder },
            { "young_villager", NPCRole.Villager },
            { "market_vendor", NPCRole.MarketVendor },
            { "weaver", NPCRole.Weaver },
            { "kitchen_worker", NPCRole.KitchenWorker },
            { "tea_vendor", NPCRole.TeaVendor },
        };

        [MenuItem("WorldRPG/NPC/Build Villager Prefabs")]
        public static List<GameObject> BuildVillagerPrefabs()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"NPCPrefabBuilder: animator controller not found at {ControllerPath}");
                return new List<GameObject>();
            }
            EnsureFolder(PrefabFolder);

            var prefabs = new List<GameObject>();
            foreach (string folder in AssetDatabase.GetSubFolders(CharactersFolder))
            {
                string folderName = Path.GetFileName(folder);
                if (!folderName.StartsWith(VillagerFolderPrefix)) continue;

                string[] models = AssetDatabase.FindAssets("t:Model", new[] { folder });
                if (models.Length == 0) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(models[0]));

                prefabs.Add(BuildPrefab(folderName, model, controller));
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"NPCPrefabBuilder: built {prefabs.Count} villager prefabs in {PrefabFolder}");
            return prefabs;
        }

        static GameObject BuildPrefab(string folderName, GameObject model, RuntimeAnimatorController controller)
        {
            var root = new GameObject("NPC_" + folderName);
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
                capsule.center = new Vector3(0f, 0.9f, 0f);

                var npc = root.AddComponent<NPC>();
                npc.role = RoleFor(folderName, out string roleSuffix);
                npc.displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(roleSuffix.Replace('_', ' '));
                npc.gender = GenderFor(folderName);

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                modelInstance.name = "Model";
                var animator = modelInstance.GetComponent<Animator>();
                if (animator == null) animator = modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var locomotion = root.AddComponent<NPCLocomotionAnimator>();
                locomotion.animator = animator;
                root.AddComponent<NPCIdleAnimator>();

                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static NPCRole RoleFor(string folderName, out string roleSuffix)
        {
            foreach (var pair in Roles)
            {
                if (folderName.EndsWith(pair.Key))
                {
                    roleSuffix = pair.Key;
                    return pair.Value;
                }
            }
            roleSuffix = "villager";
            return NPCRole.Villager;
        }

        // Folders look like "song_female_02_weaver" / "song_male_02_carpenter".
        static NPCGender GenderFor(string folderName)
        {
            return folderName.StartsWith(VillagerFolderPrefix + "female") ? NPCGender.Female : NPCGender.Male;
        }

        /// <summary>Villager prefabs already on disk (empty if they haven't been built yet).</summary>
        public static List<GameObject> LoadVillagerPrefabs()
        {
            var prefabs = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) return prefabs;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponent<NPC>() != null) prefabs.Add(prefab);
            }
            return prefabs;
        }

        /// <summary>Instantiates a random villager prefab at the given pose, linked to its prefab asset.</summary>
        public static GameObject SpawnVillager(List<GameObject> prefabs, System.Random rng, Transform parent, Vector3 position, Quaternion rotation)
        {
            var prefab = prefabs[rng.Next(prefabs.Count)];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        [MenuItem("WorldRPG/NPC/Populate Townsfolk With Villagers")]
        public static int PopulateTownsfolk()
        {
            var townsfolk = GameObject.Find("Town/Townsfolk");
            if (townsfolk == null)
            {
                Debug.LogError("NPCPrefabBuilder: no Town/Townsfolk in the open scene.");
                return 0;
            }

            var prefabs = LoadVillagerPrefabs();
            if (prefabs.Count == 0) prefabs = BuildVillagerPrefabs();
            if (prefabs.Count == 0) return 0;

            var rng = new System.Random(Seed);
            var placeholders = new List<Transform>();
            foreach (Transform child in townsfolk.transform)
                if (child.GetComponent<NPC>() == null) placeholders.Add(child);

            Undo.SetCurrentGroupName("Populate Townsfolk");
            foreach (Transform placeholder in placeholders)
            {
                var villager = SpawnVillager(prefabs, rng, townsfolk.transform, placeholder.position, placeholder.rotation);
                villager.transform.SetSiblingIndex(placeholder.GetSiblingIndex());
                Undo.RegisterCreatedObjectUndo(villager, "Spawn villager");
                Undo.DestroyObjectImmediate(placeholder.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(townsfolk.scene);
            Debug.Log($"NPCPrefabBuilder: replaced {placeholders.Count} placeholders with villagers.");
            return placeholders.Count;
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
