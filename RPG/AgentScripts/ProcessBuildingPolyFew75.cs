using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using BrainFailProductions.PolyFewRuntime;

public static class ProcessBuildingPolyFew75
{
    private const string SceneFolder = "Assets/Workshop/BuildingS";
    private const string SourceRoot = "Assets/Art/Buildings/SongDynasty/PolyFew90/";
    private const string OutputRoot = "Assets/Art/Buildings/SongDynasty/PolyFew75";
    private const string SourceSuffix = "_PolyFew90";
    private const string OutputSuffix = "_PolyFew75";
    private const float Gap = 5f;

    public static string RunOne(string scenePath) => Process(new[] { scenePath });

    public static string RunRange(int start, int count)
    {
        var paths = AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (start < 0 || start >= paths.Length) return $"No scenes at start={start}.";
        count = Mathf.Clamp(count, 1, paths.Length - start);
        return Process(paths.Skip(start).Take(count).ToArray());
    }

    public static string RunAll()
    {
        var paths = AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        return Process(paths);
    }

    private static string Process(string[] scenePaths)
    {
        EnsureFolder(OutputRoot);
        var sb = new StringBuilder();
        int processed = 0, failed = 0;
        foreach (var scenePath in scenePaths)
        {
            try { sb.AppendLine(ProcessScene(scenePath)); processed++; }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"ERROR|{scenePath}|{ex.GetType().Name}|{ex.Message}");
                Debug.LogError($"PolyFew 75 batch failed for {scenePath}: {ex}");
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine($"SUMMARY|requested={scenePaths.Length}|processed={processed}|failed={failed}");
        return sb.ToString();
    }

    private static string ProcessScene(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var allObjects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
        var allFilters = allObjects.SelectMany(go => go.GetComponents<MeshFilter>()).Where(f => f.sharedMesh != null).ToArray();
        var sourceFilter = allFilters.FirstOrDefault(filter =>
        {
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            return path.StartsWith(SourceRoot, StringComparison.OrdinalIgnoreCase);
        });
        if (sourceFilter == null) throw new InvalidOperationException("Could not find the existing PolyFew90 building MeshFilter.");

        var source = sourceFilter.gameObject;
        var old75 = allObjects.FirstOrDefault(go => go.name == source.name.Replace(SourceSuffix, OutputSuffix));
        if (old75 != null) UnityEngine.Object.DestroyImmediate(old75);

        int sourceTriangles = CountTriangles(source);
        Bounds bounds = GetBounds(source);
        var copy = UnityEngine.Object.Instantiate(source, source.transform.parent, true);
        copy.name = source.name.Replace(SourceSuffix, OutputSuffix);
        if (copy.name == source.name) copy.name = source.name + OutputSuffix;
        copy.transform.position = source.transform.position + Vector3.right * (Mathf.Max(bounds.size.x, 1f) + Gap);
        if (PrefabUtility.IsAnyPrefabInstanceRoot(copy))
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var options = new PolyfewRuntime.SimplificationOptions
        {
            simplificationStrength = 75f,
            simplifyMeshLossless = false,
            enableSmartlinking = true,
            recalculateNormals = false,
            preserveUVSeamEdges = false,
            preserveUVFoldoverEdges = true,
            preserveBorderEdges = true,
            regardPreservationSpheres = false,
            preservationSpheres = new List<PolyfewRuntime.PreservationSphere>(),
            regardCurvature = true,
            maxIterations = 100,
            aggressiveness = 7f,
            useEdgeSort = true
        };
        int reportedTriangles = PolyfewRuntime.SimplifyObjectDeep(copy, options, null);
        if (reportedTriangles < 0) throw new InvalidOperationException("PolyFew returned without simplifying the copy.");

        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        string outputFolder = OutputRoot + "/" + sceneName;
        if (AssetDatabase.IsValidFolder(outputFolder)) AssetDatabase.DeleteAsset(outputFolder);
        EnsureFolder(outputFolder);
        var generated = new Dictionary<int, string>();
        foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
            SaveMesh(filter.sharedMesh, filter.name, outputFolder, generated, mesh => filter.sharedMesh = mesh);
        foreach (var renderer in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            SaveMesh(renderer.sharedMesh, renderer.name, outputFolder, generated, mesh => renderer.sharedMesh = mesh);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity failed to save the scene.");

        int reducedTriangles = CountTriangles(copy);
        return $"OK|{scenePath}|source90={sourceTriangles}|reduced75={reducedTriangles}|reported={reportedTriangles}|meshAssets={generated.Count}|copy={copy.name}|offsetX={copy.transform.position.x - source.transform.position.x:0.###}";
    }

    private static void SaveMesh(Mesh mesh, string ownerName, string outputFolder, Dictionary<int, string> generated, Action<Mesh> assign)
    {
        if (mesh == null) return;
        string existing = AssetDatabase.GetAssetPath(mesh);
        if (!string.IsNullOrEmpty(existing)) { assign(mesh); return; }
        int id = mesh.GetInstanceID();
        if (!generated.TryGetValue(id, out string path))
        {
            string safeName = Sanitize(ownerName + "_PolyFew75.mesh");
            path = outputFolder + "/" + safeName;
            mesh.name = Path.GetFileNameWithoutExtension(safeName);
            AssetDatabase.CreateAsset(mesh, path);
            generated.Add(id, path);
        }
        assign(AssetDatabase.LoadAssetAtPath<Mesh>(path));
    }

    private static int CountTriangles(GameObject root)
    {
        int total = 0;
        foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) total += f.sharedMesh.triangles.Length / 3;
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh != null) total += r.sharedMesh.triangles.Length / 3;
        return total;
    }

    private static Bounds GetBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void EnsureFolder(string folder)
    {
        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c.ToString(), "_");
        return value.Replace("/", "_").Replace("\\", "_");
    }
}
