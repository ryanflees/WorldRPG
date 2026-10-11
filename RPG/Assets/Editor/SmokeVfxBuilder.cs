using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WorldRPG.VFX;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Creates the reusable smoke prefab and its soft particle texture.
    /// The first import builds the assets automatically; the menu item can rebuild them later.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeVfxBuilder
    {
        const string Root = "Assets/Art/VFX/Smoke";
        const string TexturePath = Root + "/SmokeSoft.png";
        const string MaterialPath = Root + "/M_SmokeSoft.mat";
        const string PrefabPath = Root + "/PF_SmokeVFX.prefab";
        const string PreviewScenePath = "Assets/Workshop/SmokeVFX_Showcase.unity";

        static SmokeVfxBuilder()
        {
            // Delay until the asset database is ready after a script/domain reload.
            EditorApplication.delayCall += AutoBuild;
        }

        [MenuItem("WorldRPG/Build Smoke VFX Assets")]
        static void BuildFromMenu()
        {
            BuildAssets(true, true);
        }

        // Used by a one-shot Unity batch invocation when the editor is not focused
        // and has not yet picked up external file changes.
        public static void BuildFromCommandLine()
        {
            BuildAssets(true, false);
        }

        static void AutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsAssetImportWorkerProcess())
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                BuildAssets(false, false);
        }

        static void BuildAssets(bool rebuild, bool buildPreview)
        {
            try
            {
                EnsureFolders();

                if (rebuild)
                    AssetDatabase.DeleteAsset(TexturePath);

                var texture = CreateSmokeTexture();
                var material = CreateSmokeMaterial(texture);
                if (material == null)
                {
                    Debug.LogError("[WorldRPG] Could not find a URP particle shader for the smoke material.");
                    return;
                }

                if (rebuild)
                    AssetDatabase.DeleteAsset(PrefabPath);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                    prefab = CreateSmokePrefab(material);

                if (buildPreview)
                    CreatePreviewScene(prefab, rebuild);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[WorldRPG] Smoke VFX ready at " + PrefabPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/VFX"))
                AssetDatabase.CreateFolder("Assets/Art", "VFX");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/Art/VFX", "Smoke");

            // AssetDatabase.CreateFolder is asynchronous in a fresh batch project;
            // ensure the physical directory exists before writing the generated PNG.
            Directory.CreateDirectory(ToFullPath(Root));
        }

        static Texture2D CreateSmokeTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null) return texture;

            const int size = 128;
            var generated = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "SmokeSoft",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(u * u + v * v);
                float softCircle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 0.05f, radius));

                // A few deterministic lobes keep the puff from reading as a perfect circle.
                float lobeA = Mathf.Exp(-((u + 0.22f) * (u + 0.22f) + (v - 0.12f) * (v - 0.12f)) * 5.5f);
                float lobeB = Mathf.Exp(-((u - 0.25f) * (u - 0.25f) + (v + 0.06f) * (v + 0.06f)) * 6.5f);
                float alpha = Mathf.Clamp01(softCircle * 0.62f + lobeA * 0.26f + lobeB * 0.20f);
                alpha *= Mathf.SmoothStep(0f, 1f, (1f - radius) * 1.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }

            generated.SetPixels(pixels);
            // Keep the temporary texture readable until EncodeToPNG has finished.
            generated.Apply(false, false);

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                TexturePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(fullPath, generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        static Material CreateSmokeMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) return null;
                material = new Material(shader) { name = "M_SmokeSoft" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            var tint = new Color(0.83f, 0.86f, 0.89f, 0.58f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CreateSmokePrefab(Material material)
        {
            var root = new GameObject("PF_SmokeVFX");
            var controller = root.AddComponent<SmokeVfx>();
            controller.emissionMultiplier = 1f;
            controller.drift = new Vector3(0.04f, 0.28f, 0.02f);
            controller.playOnEnable = true;

            var main = CreateParticleSystem("Steam", root.transform, material,
                new Color(0.83f, 0.86f, 0.89f, 0.55f),
                new Vector2(0.50f, 0.82f), new Vector2(2.4f, 3.6f),
                new Vector2(5.2f, 7.5f), new Vector2(0.18f, 0.42f), 0.25f);
            var wisps = CreateParticleSystem("Wisps", root.transform, material,
                new Color(0.92f, 0.94f, 0.97f, 0.36f),
                new Vector2(0.25f, 0.48f), new Vector2(1.5f, 2.5f),
                new Vector2(2.5f, 4.0f), new Vector2(0.28f, 0.62f), 0.15f);

            controller.primarySystem = main;
            controller.secondarySystem = wisps;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material,
            Color color, Vector2 size, Vector2 lifetime, Vector2 emissionRate, Vector2 lift, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate.x, emissionRate.y);

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius;
            shape.length = 0.10f;
            shape.arc = 360f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.10f);
            velocity.y = new ParticleSystem.MinMaxCurve(lift.x, lift.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.10f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            noise.frequency = 0.32f;
            noise.scrollSpeed = 0.12f;
            noise.octaveCount = 2;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 0.78f),
                    new Keyframe(0.62f, 1.08f),
                    new Keyframe(1f, 0.72f)));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.90f, 0.93f, 0.97f), 0.65f),
                    new GradientColorKey(new Color(0.74f, 0.78f, 0.83f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.62f, 0.70f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Facing;
            renderer.material = material;
            renderer.minParticleSize = 0.02f;
            renderer.maxParticleSize = 3f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;

            return system;
        }

        static void CreatePreviewScene(GameObject prefab, bool rebuild)
        {
            if (!rebuild && File.Exists(ToFullPath(PreviewScenePath))) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.65f, 0.72f);
            RenderSettings.ambientIntensity = 0.85f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Preview Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.GetComponent<Renderer>().sharedMaterial = PreviewMaterial("M_SmokePreviewGround", new Color(0.38f, 0.32f, 0.25f));

            const string buildingPath = "Assets/Prefabs/Buildings/SongDynasty/07_Steamed_Food_Shop.prefab";
            var buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(buildingPath);
            if (buildingPrefab != null)
            {
                var building = PrefabUtility.InstantiatePrefab(buildingPrefab) as GameObject;
                building.name = "Steamed Food Shop (Preview)";
                building.transform.position = Vector3.zero;
            }
            else
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "Food Stall Placeholder";
                fallback.transform.position = new Vector3(0f, 1f, 0f);
                fallback.transform.localScale = new Vector3(3f, 2f, 2f);
                fallback.GetComponent<Renderer>().sharedMaterial = PreviewMaterial("M_SmokePreviewBuilding", new Color(0.64f, 0.32f, 0.18f));
            }

            var chimneySmoke = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            chimneySmoke.name = "Smoke - Chimney";
            chimneySmoke.transform.position = new Vector3(-0.4f, 3.7f, 0.1f);
            chimneySmoke.transform.localScale = Vector3.one * 0.85f;

            var stallSmoke = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            stallSmoke.name = "Steam - Food Stall";
            stallSmoke.transform.position = new Vector3(1.25f, 1.45f, -0.7f);
            stallSmoke.transform.localScale = Vector3.one * 0.55f;
            var stallController = stallSmoke.GetComponent<SmokeVfx>();
            if (stallController != null)
            {
                stallController.emissionMultiplier = 0.7f;
                stallController.drift = new Vector3(0.08f, 0.20f, 0.02f);
            }

            var cameraObject = new GameObject("Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(7.5f, 5.2f, -10.5f);
            camera.transform.LookAt(new Vector3(0f, 2f, 0f));
            camera.fieldOfView = 42f;

            var lightObject = new GameObject("Preview Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.91f, 0.78f);
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            Debug.Log("[WorldRPG] Smoke VFX showcase saved at " + PreviewScenePath);
        }

        static Material PreviewMaterial(string name, Color color)
        {
            string path = Root + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string ToFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
