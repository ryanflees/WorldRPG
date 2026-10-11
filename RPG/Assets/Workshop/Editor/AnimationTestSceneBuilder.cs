using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldRPG.Debugging;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Builds a workshop scene where a male and a female villager cycle through every standing idle and
    /// chat take with the in-game cross-fade, on a 1 m grid with a marker at each character's true root
    /// (disc = position, bar = facing). Body drifting off the marker or turning away from the bar while
    /// switching takes means the clip's root import settings are wrong.
    /// Menu: WorldRPG/Workshop/Build Animation Switch Test Scene
    /// </summary>
    public static class AnimationTestSceneBuilder
    {
        const string ScenePath = "Assets/Workshop/AnimationSwitchTest.unity";
        const string ControllerPath = "Assets/Art/Characters/AnimatorController/BasicAC.controller";
        const string MaleModel = "Assets/Art/Characters/song_male_04_shopkeeper/song_male_04.fbx";
        const string FemaleModel = "Assets/Art/Characters/song_female_01_market_vendor/song_female_01.fbx";
        const string MatFolder = "Assets/Workshop/Materials";

        [MenuItem("WorldRPG/Workshop/Build Animation Switch Test Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var stone = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Stone.mat");
            var ink = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Ridge.mat");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            if (stone != null) ground.GetComponent<Renderer>().sharedMaterial = stone;

            // 1 m grid so drift is easy to read.
            var grid = new GameObject("Grid").transform;
            for (int i = -6; i <= 6; i++)
            {
                Line(grid, new Vector3(i, 0.002f, 0f), new Vector3(0.02f, 0.002f, 12f), ink);
                Line(grid, new Vector3(0f, 0.002f, i), new Vector3(12f, 0.002f, 0.02f), ink);
            }

            Character("Male", MaleModel, AnimationCycleTester.Pool.Male, new Vector3(-1.5f, 0f, 0f), controller);
            Character("Female", FemaleModel, AnimationCycleTester.Pool.Female, new Vector3(1.5f, 0f, 0f), controller);

            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 2.2f, -6.5f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            cam.fieldOfView = 40f;
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, 200f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        static void Character(string name, string modelPath, AnimationCycleTester.Pool pool, Vector3 pos, RuntimeAnimatorController controller)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            model.name = name;
            // Face the camera (characters face +Z, the camera looks along +Z from the south).
            model.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Root marker: disc at the root, bar along the root's forward.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "RootMarker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(model.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            marker.transform.localScale = new Vector3(0.5f, 0.004f, 0.5f);
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Facing";
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(model.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.006f, 0.45f);
            bar.transform.localScale = new Vector3(0.06f, 0.006f, 0.5f);
            var red = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/M_Lacquer.mat");
            if (red != null)
            {
                marker.GetComponent<Renderer>().sharedMaterial = red;
                bar.GetComponent<Renderer>().sharedMaterial = red;
            }

            var labelGo = new GameObject(name + "Label");
            labelGo.transform.position = pos + new Vector3(0f, 2.15f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 48;
            tm.characterSize = 0.03f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            var tester = model.AddComponent<AnimationCycleTester>();
            tester.animator = animator;
            tester.pool = pool;
            tester.label = tm;
        }

        static void Line(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
