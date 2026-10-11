using UnityEditor;
using UnityEngine;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Import rules for the Meshy building set under Assets/Art/Buildings.
    /// FBX files are cleaned/scaled in Blender beforehand (1 unit = 1 m, origin at bottom centre),
    /// so the importer only has to skip materials/animation; materials are built by BuildingShowcaseBuilder.
    /// </summary>
    public class BuildingImportPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/Art/Buildings/";
        const int MaxTextureSize = 2048;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;

            if (assetPath.EndsWith("_Normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.EndsWith("_MetallicSmoothness.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
            }
        }
    }
}
