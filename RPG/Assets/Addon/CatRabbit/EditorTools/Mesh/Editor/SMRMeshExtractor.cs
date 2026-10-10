using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace CR
{
    public class SMRMeshExtractor : EditorWindow
    {
        private SkinnedMeshRenderer targetSMR;
        private bool splitSubmeshes = false;
        private string saveFolder = "Assets";

        [MenuItem("CatRabbit/Mesh/SMR Mesh Extractor")]
        public static void ShowWindow()
        {
            GetWindow<SMRMeshExtractor>("SMR Extractor");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            targetSMR = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target SMR", targetSMR, typeof(SkinnedMeshRenderer), true);
            splitSubmeshes = EditorGUILayout.Toggle("Split Submeshes", splitSubmeshes);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Folder", saveFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Save Folder", saveFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        saveFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        ShowNotification(new GUIContent("Please select a folder inside the project Assets directory."));
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("Extract and Save Mesh", GUILayout.Height(40)))
            {
                if (targetSMR == null)
                {
                    ShowNotification(new GUIContent("Please select a Target SMR first."));
                    return;
                }
                Extract();
            }
        }

        private void Extract()
        {
            Mesh originalMesh = targetSMR.sharedMesh;
            if (originalMesh == null) return;

            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            if (splitSubmeshes)
            {
                for (int i = 0; i < originalMesh.subMeshCount; i++)
                {
                    ExtractSubmesh(originalMesh, i);
                }
            }
            else
            {
                ExtractFullMesh(originalMesh);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void ExtractFullMesh(Mesh originalMesh)
        {
            Mesh newMesh = Instantiate(originalMesh);
            string path = Path.Combine(saveFolder, targetSMR.gameObject.name + "_Mesh.asset");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(newMesh, path);

            GameObject newGO = new GameObject(targetSMR.gameObject.name + "_Extracted");
            newGO.transform.SetParent(targetSMR.transform.parent);
            newGO.transform.localPosition = targetSMR.transform.localPosition;
            newGO.transform.localRotation = targetSMR.transform.localRotation;
            newGO.transform.localScale = targetSMR.transform.localScale;

            SkinnedMeshRenderer newSMR = newGO.AddComponent<SkinnedMeshRenderer>();
            newSMR.sharedMesh = newMesh;
            newSMR.bones = targetSMR.bones;
            newSMR.rootBone = targetSMR.rootBone;
            newSMR.sharedMaterials = targetSMR.sharedMaterials;
            
            Selection.activeGameObject = newGO;
            Debug.Log($"Saved full mesh to: {path}");
        }

        private void ExtractSubmesh(Mesh originalMesh, int submeshIndex)
        {
            Mesh newMesh = CreateSubmesh(originalMesh, submeshIndex);
            string submeshName = $"{targetSMR.gameObject.name}_Submesh_{submeshIndex}";
            string path = Path.Combine(saveFolder, submeshName + ".asset");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(newMesh, path);

            GameObject newGO = new GameObject(submeshName);
            newGO.transform.SetParent(targetSMR.transform.parent);
            newGO.transform.localPosition = targetSMR.transform.localPosition;
            newGO.transform.localRotation = targetSMR.transform.localRotation;
            newGO.transform.localScale = targetSMR.transform.localScale;

            SkinnedMeshRenderer newSMR = newGO.AddComponent<SkinnedMeshRenderer>();
            newSMR.sharedMesh = newMesh;
            newSMR.bones = targetSMR.bones;
            newSMR.rootBone = targetSMR.rootBone;
            
            if (submeshIndex < targetSMR.sharedMaterials.Length)
            {
                newSMR.sharedMaterial = targetSMR.sharedMaterials[submeshIndex];
            }

            Debug.Log($"Saved submesh {submeshIndex} to: {path}");
        }

        private Mesh CreateSubmesh(Mesh sourceMesh, int submeshIndex)
        {
            Mesh newMesh = new Mesh();
            newMesh.name = sourceMesh.name + "_sub_" + submeshIndex;

            int[] triangles = sourceMesh.GetTriangles(submeshIndex);
            HashSet<int> vertexIndices = new HashSet<int>(triangles);
            List<int> sortedIndices = vertexIndices.ToList();
            sortedIndices.Sort();

            Dictionary<int, int> oldToNewMap = new Dictionary<int, int>();
            for (int i = 0; i < sortedIndices.Count; i++)
            {
                oldToNewMap[sortedIndices[i]] = i;
            }

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector4[] sourceTangents = sourceMesh.tangents;
            Color[] sourceColors = sourceMesh.colors;
            Vector2[] sourceUV1 = sourceMesh.uv;
            Vector2[] sourceUV2 = sourceMesh.uv2;
            Vector2[] sourceUV3 = sourceMesh.uv3;
            Vector2[] sourceUV4 = sourceMesh.uv4;
            BoneWeight[] sourceBoneWeights = sourceMesh.boneWeights;

            int vertexCount = sortedIndices.Count;
            Vector3[] newVertices = new Vector3[vertexCount];
            Vector3[] newNormals = sourceNormals.Length > 0 ? new Vector3[vertexCount] : null;
            Vector4[] newTangents = sourceTangents.Length > 0 ? new Vector4[vertexCount] : null;
            Color[] newColors = sourceColors.Length > 0 ? new Color[vertexCount] : null;
            Vector2[] newUV1 = sourceUV1.Length > 0 ? new Vector2[vertexCount] : null;
            Vector2[] newUV2 = sourceUV2.Length > 0 ? new Vector2[vertexCount] : null;
            Vector2[] newUV3 = sourceUV3.Length > 0 ? new Vector2[vertexCount] : null;
            Vector2[] newUV4 = sourceUV4.Length > 0 ? new Vector2[vertexCount] : null;
            BoneWeight[] newBoneWeights = sourceBoneWeights.Length > 0 ? new BoneWeight[vertexCount] : null;

            for (int i = 0; i < vertexCount; i++)
            {
                int oldIdx = sortedIndices[i];
                newVertices[i] = sourceVertices[oldIdx];
                if (newNormals != null) newNormals[i] = sourceNormals[oldIdx];
                if (newTangents != null) newTangents[i] = sourceTangents[oldIdx];
                if (newColors != null) newColors[i] = sourceColors[oldIdx];
                if (newUV1 != null) newUV1[i] = sourceUV1[oldIdx];
                if (newUV2 != null) newUV2[i] = sourceUV2[oldIdx];
                if (newUV3 != null) newUV3[i] = sourceUV3[oldIdx];
                if (newUV4 != null) newUV4[i] = sourceUV4[oldIdx];
                if (newBoneWeights != null) newBoneWeights[i] = sourceBoneWeights[oldIdx];
            }

            int[] newTriangles = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                newTriangles[i] = oldToNewMap[triangles[i]];
            }

            newMesh.vertices = newVertices;
            if (newNormals != null) newMesh.normals = newNormals;
            if (newTangents != null) newMesh.tangents = newTangents;
            if (newColors != null) newMesh.colors = newColors;
            if (newUV1 != null) newMesh.uv = newUV1;
            if (newUV2 != null) newMesh.uv2 = newUV2;
            if (newUV3 != null) newMesh.uv3 = newUV3;
            if (newUV4 != null) newMesh.uv4 = newUV4;
            if (newBoneWeights != null) newMesh.boneWeights = newBoneWeights;
            
            newMesh.triangles = newTriangles;
            newMesh.bindposes = sourceMesh.bindposes;

            newMesh.RecalculateBounds();
            return newMesh;
        }
    }
}
