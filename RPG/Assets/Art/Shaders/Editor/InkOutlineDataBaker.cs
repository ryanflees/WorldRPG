using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Bakes per-vertex data for the WorldRPG/InkToon outline into UV channel 7 (TEXCOORD7)
    /// of every model under Assets/Art/Characters:
    ///   xyz = smoothed normal in the vertex's tangent space. The meshes have hard edges, so
    ///         extruding along the real normals would crack the outline hull at every seam.
    ///         Tangent space keeps it correct under skinning (UVs aren't skinned, the TBN is).
    ///         Its length is an outline mask (0..1): vertices whose extrusion would run into
    ///         another layer of the model (an inner garment just under the outer one) get 0,
    ///         otherwise their hull pokes through the outer layer as a black blot.
    ///   w   = brush stroke noise (0..1) from the bind-pose position, identical on split
    ///         vertices so stroke width and dry-brush gaps stay continuous across seams.
    /// </summary>
    public class InkOutlineDataBaker : AssetPostprocessor
    {
        const string Folder = "Assets/Art/Characters/";
        public const int UVChannel = 7;

        // Noise features per character height (~1.8 m -> strokes vary every ~15 cm).
        const float NoiseFrequency = 12f;

        // Layer test distance, relative to character height (~2.7 cm at 1.8 m).
        const float BuriedTestDistance = 0.015f;

        public override uint GetVersion() => 2;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Folder)) return;

            var meshes = new HashSet<Mesh>();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);

            foreach (var mesh in meshes) Bake(mesh);
        }

        static void Bake(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals.Length != vertices.Length) return;
            if (mesh.tangents.Length != vertices.Length) mesh.RecalculateTangents();
            Vector4[] tangents = mesh.tangents;

            // Area-weighted face normals accumulated per unique position.
            var triangles = new List<int>();
            var smooth = new Dictionary<Vector3, Vector3>(vertices.Length / 2);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                int[] tris = mesh.GetTriangles(s);
                triangles.AddRange(tris);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = vertices[tris[t]], b = vertices[tris[t + 1]], c = vertices[tris[t + 2]];
                    Vector3 face = Vector3.Cross(b - a, c - a);
                    Accumulate(smooth, a, face);
                    Accumulate(smooth, b, face);
                    Accumulate(smooth, c, face);
                }
            }

            float height = Mathf.Max(mesh.bounds.size.y, 1e-4f);
            float scale = NoiseFrequency / height;
            var raycaster = new TriangleGrid(vertices, triangles, BuriedTestDistance * height);
            var buried = new Dictionary<Vector3, bool>(smooth.Count);

            var data = new List<Vector4>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                Vector3 n = normals[i];
                Vector3 sn = smooth.TryGetValue(p, out Vector3 sum) && sum.sqrMagnitude > 1e-12f ? sum.normalized : n;

                // Same answer for every split copy of a position, so the hull stays watertight.
                if (!buried.TryGetValue(p, out bool isBuried))
                {
                    isBuried = raycaster.Hits(p + sn * (1e-4f * height), sn, BuriedTestDistance * height);
                    buried[p] = isBuried;
                }
                Vector3 masked = isBuried ? Vector3.zero : sn;

                Vector3 tangent = tangents[i];
                Vector3 bitangent = Vector3.Cross(n, tangent) * tangents[i].w;
                // Keep w off exactly 0 so all-zero data still means "not baked" in the shader.
                float noise = Mathf.Max(FractalNoise(p * scale), 0.002f);
                data.Add(new Vector4(Vector3.Dot(masked, tangent), Vector3.Dot(masked, bitangent), Vector3.Dot(masked, n), noise));
            }
            mesh.SetUVs(UVChannel, data);
        }

        /// <summary>Uniform grid of triangles for short-range ray casts against the mesh itself.</summary>
        class TriangleGrid
        {
            readonly Vector3[] _vertices;
            readonly List<int> _triangles;
            readonly Dictionary<Vector3Int, List<int>> _cells = new Dictionary<Vector3Int, List<int>>();
            readonly float _cellSize;
            readonly int[] _stamp;
            int _query;

            public TriangleGrid(Vector3[] vertices, List<int> triangles, float cellSize)
            {
                _vertices = vertices;
                _triangles = triangles;
                _cellSize = cellSize;
                _stamp = new int[triangles.Count / 3];

                for (int t = 0; t < triangles.Count; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3Int min = Cell(Vector3.Min(a, Vector3.Min(b, c)));
                    Vector3Int max = Cell(Vector3.Max(a, Vector3.Max(b, c)));
                    for (int x = min.x; x <= max.x; x++)
                    for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var key = new Vector3Int(x, y, z);
                        if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<int>();
                        list.Add(t / 3);
                    }
                }
            }

            Vector3Int Cell(Vector3 p) => Vector3Int.FloorToInt(p / _cellSize);

            public bool Hits(Vector3 origin, Vector3 dir, float maxDistance)
            {
                _query++;
                int steps = Mathf.CeilToInt(maxDistance / (_cellSize * 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector3Int center = Cell(origin + dir * Mathf.Min(s * _cellSize * 0.5f, maxDistance));
                    // Include neighbours: the ray can clip a cell corner between samples.
                    for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        if (!_cells.TryGetValue(center + new Vector3Int(x, y, z), out var list)) continue;
                        foreach (int tri in list)
                        {
                            if (_stamp[tri] == _query) continue;
                            _stamp[tri] = _query;
                            int i = tri * 3;
                            if (RayTriangle(origin, dir, _vertices[_triangles[i]], _vertices[_triangles[i + 1]], _vertices[_triangles[i + 2]], maxDistance))
                                return true;
                        }
                    }
                }
                return false;
            }

            // Möller–Trumbore, both faces.
            static bool RayTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, float maxDistance)
            {
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-12f) return false;
                float inv = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) return false;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0f || u + v > 1f) return false;
                float t = Vector3.Dot(e2, q) * inv;
                return t > 0f && t <= maxDistance;
            }
        }

        static void Accumulate(Dictionary<Vector3, Vector3> map, Vector3 key, Vector3 value)
        {
            map.TryGetValue(key, out Vector3 sum);
            map[key] = sum + value;
        }

        static float FractalNoise(Vector3 p)
        {
            float n = ValueNoise(p) * 0.65f + ValueNoise(p * 2.7f + new Vector3(17.1f, 3.3f, 9.7f)) * 0.35f;
            // Stretch the contrast a little so strokes reach both thin and full width.
            return Mathf.Clamp01((n - 0.5f) * 1.6f + 0.5f);
        }

        static float ValueNoise(Vector3 p)
        {
            Vector3Int i = Vector3Int.FloorToInt(p);
            Vector3 f = p - i;
            f = new Vector3(Smooth(f.x), Smooth(f.y), Smooth(f.z));

            float x00 = Mathf.Lerp(Hash(i.x, i.y, i.z), Hash(i.x + 1, i.y, i.z), f.x);
            float x10 = Mathf.Lerp(Hash(i.x, i.y + 1, i.z), Hash(i.x + 1, i.y + 1, i.z), f.x);
            float x01 = Mathf.Lerp(Hash(i.x, i.y, i.z + 1), Hash(i.x + 1, i.y, i.z + 1), f.x);
            float x11 = Mathf.Lerp(Hash(i.x, i.y + 1, i.z + 1), Hash(i.x + 1, i.y + 1, i.z + 1), f.x);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, f.y), Mathf.Lerp(x01, x11, f.y), f.z);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }
    }
}
