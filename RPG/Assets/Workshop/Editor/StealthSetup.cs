using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldRPG.Stealth;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Wires pickpocketing into a scene: Pickpocketer + BulletTime on the Player, the HUD next to DemoHUD,
    /// and a PickpocketTarget on every villager prefab. Called by the level builders; also on the menu.
    /// </summary>
    public static class StealthSetup
    {
        const string VillagerPrefabFolder = "Assets/Prefabs/NPC/Villagers";

        [MenuItem("WorldRPG/Stealth/Setup Pickpocketing In Open Scene")]
        static void SetupOpenSceneMenu()
        {
            AddTargetsToVillagerPrefabs();
            SetupScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void SetupScene()
        {
            var player = GameObject.Find("Player");
            if (player == null || player.GetComponent<TopDownCharacter>() == null)
            {
                Debug.LogWarning("StealthSetup: no Player with a TopDownCharacter in the open scene.");
                return;
            }
            if (player.GetComponent<BulletTime>() == null) player.AddComponent<BulletTime>();
            var thief = player.GetComponent<Pickpocketer>();
            if (thief == null) thief = player.AddComponent<Pickpocketer>();

            var hudHost = Object.FindFirstObjectByType<DemoHUD>();
            var host = hudHost != null ? hudHost.gameObject : new GameObject("PickpocketHUD");
            var hud = host.GetComponent<PickpocketHUD>();
            if (hud == null) hud = host.AddComponent<PickpocketHUD>();
            hud.thief = thief;
            var rig = Object.FindFirstObjectByType<RTSCameraRig>();
            if (rig != null) hud.cam = rig.cam;

            // Scene NPCs that aren't villager prefab instances (or are, with the component removed).
            foreach (var npc in Object.FindObjectsByType<NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (npc.GetComponent<PickpocketTarget>() == null) npc.gameObject.AddComponent<PickpocketTarget>();

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(host);
        }

        [MenuItem("WorldRPG/Stealth/Add Pickpocket Targets To Villager Prefabs")]
        public static void AddTargetsToVillagerPrefabs()
        {
            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { VillagerPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<NPC>() == null || root.GetComponent<PickpocketTarget>() != null) continue;
                    root.AddComponent<PickpocketTarget>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"StealthSetup: added PickpocketTarget to {added} villager prefabs.");
        }
    }
}
