using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Installs the full-screen WorldRPG/InkEdges pass (ink contours, distance haze, paper grain) on the URP renderer
    /// of every quality level, and creates its material. Safe to run again: an existing feature is only updated.
    /// Menu: WorldRPG/Rendering/Setup Ink Edges
    /// </summary>
    public static class InkRenderingSetup
    {
        public const string MaterialPath = "Assets/Art/Shaders/M_InkEdges.mat";
        const string FeatureName = "InkEdges";

        [MenuItem("WorldRPG/Rendering/Setup Ink Edges")]
        static void SetupFromMenu() => Debug.Log(Setup());

        public static string Setup()
        {
            Material material = EnsureMaterial();
            int added = 0, renderers = 0;
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(q) as UniversalRenderPipelineAsset
                            ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (asset == null) continue;
                var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableRendererData data) continue;
                    renderers++;
                    if (AddOrUpdate(data, material)) added++;
                }
            }
            AssetDatabase.SaveAssets();
            return $"Ink edges: {added} feature(s) added, {renderers} renderer(s) checked, material {MaterialPath}.";
        }

        static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("WorldRPG/InkEdges"));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>True if the feature was newly added.</summary>
        static bool AddOrUpdate(ScriptableRendererData data, Material material)
        {
            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) feature = fs;

            bool added = feature == null;
            if (added)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                // The renderer keeps a parallel list of the features' local file ids.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Before post-processing, so the colour grade and bloom apply on top of the ink and haze.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
            feature.fetchColorBuffer = true;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            return added;
        }
    }
}
