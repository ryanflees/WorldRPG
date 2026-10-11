using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Replaces the model under Player/Model with the protagonist, keeping the Animator setup and
    /// re-pointing any scene references from the old model to the new one.
    /// Menu: WorldRPG/Character/Swap Player Model In Open Scene
    /// </summary>
    public static class PlayerModelSwapper
    {
        public const string ProtagonistModel = "Assets/Art/Characters/song_male_06_protagonist_thief/song_male_06.fbx";

        [MenuItem("WorldRPG/Character/Swap Player Model In Open Scene")]
        static void SwapFromMenu() => Debug.Log(Swap(ProtagonistModel));

        public static string Swap(string modelPath)
        {
            var player = GameObject.Find("Player");
            if (player == null) return "no Player in scene";
            Transform oldModel = player.transform.Find("Model");
            if (oldModel == null) return "Player has no Model child";

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null) return "model not found: " + modelPath;
            if (PrefabUtility.GetCorrespondingObjectFromSource(oldModel.gameObject) == modelAsset) return "already using " + modelPath;

            var newModel = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            newModel.name = "Model";
            newModel.transform.SetSiblingIndex(oldModel.GetSiblingIndex());
            newModel.transform.localPosition = oldModel.localPosition;
            newModel.transform.localRotation = oldModel.localRotation;
            newModel.transform.localScale = Vector3.one;

            var oldAnimator = oldModel.GetComponent<Animator>();
            var newAnimator = newModel.GetComponent<Animator>();
            if (newAnimator == null) newAnimator = newModel.AddComponent<Animator>();
            if (oldAnimator != null)
            {
                newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
                newAnimator.applyRootMotion = oldAnimator.applyRootMotion;
                newAnimator.updateMode = oldAnimator.updateMode;
                newAnimator.cullingMode = oldAnimator.cullingMode;
            }

            int repointed = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.transform.IsChildOf(oldModel)) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object v = it.objectReferenceValue;
                    Object replacement = null;
                    if (v == oldAnimator) replacement = newAnimator;
                    else if (v == oldModel) replacement = newModel.transform;
                    else if (v == oldModel.gameObject) replacement = newModel;
                    if (replacement == null) continue;
                    it.objectReferenceValue = replacement;
                    changed = true;
                    repointed++;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(oldModel.gameObject);
            EditorSceneManager.MarkSceneDirty(player.scene);
            return $"swapped Player model -> {modelAsset.name}, repointed {repointed} reference(s)";
        }
    }
}
