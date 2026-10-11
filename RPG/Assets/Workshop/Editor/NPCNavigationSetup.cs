using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using Pathfinding;
using Pathfinding.RVO;
using Pathfinding.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldRPG.EditorTools
{
    /// <summary>
    /// Sets up NPC navigation in the open scene: A* RecastGraph (baked and cached so nothing is
    /// scanned at startup), RVO local avoidance, the Character layer, and vendor stalls.
    /// Menu: WorldRPG/NPC/...
    /// </summary>
    public static class NPCNavigationSetup
    {
        public const string CharacterLayerName = "Character";
        public const string NavBlockerLayerName = "NavBlocker";   // baked into the navmesh, no physics collisions

        // Town walls are at ±63 m; a little margin lets villagers step out of the gates.
        static readonly Vector3 GraphCenter = new Vector3(0f, 4f, 0f);
        static readonly Vector3 GraphSize = new Vector3(136f, 16f, 136f);

        [MenuItem("WorldRPG/NPC/Setup Navigation (Recast) In Open Scene")]
        static void SetupFromMenu() => Debug.Log(SetupNavigation());

        [MenuItem("WorldRPG/NPC/Rebake Navmesh In Open Scene")]
        static void RebakeFromMenu() => Debug.Log(Bake(Object.FindFirstObjectByType<AstarPath>()));

        [MenuItem("WorldRPG/NPC/Setup Vendor Stalls In Open Scene")]
        static void StallsFromMenu() => Debug.Log(SetupVendorStalls());

        public static string SetupNavigation() => SetupNavigation(GraphCenter, GraphSize);

        /// <summary>Same, for a level whose navmesh covers a different box than the TownDemo's.</summary>
        public static string SetupNavigation(Vector3 graphCenter, Vector3 graphSize)
        {
            int characterLayer = LayerMask.NameToLayer(CharacterLayerName);
            if (characterLayer < 0) return $"Add a '{CharacterLayerName}' layer first (Project Settings > Tags and Layers).";

            // Characters must not be baked into the navmesh as obstacles.
            int tagged = 0;
            foreach (var npc in Object.FindObjectsByType<NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                tagged += SetLayerRecursive(npc.gameObject, characterLayer);
            var player = GameObject.Find("Player");
            if (player != null)
            {
                tagged += SetLayerRecursive(player, characterLayer);
                if (player.GetComponent<RVOController>() == null) player.AddComponent<RVOController>();
                if (player.GetComponent<PlayerAvoidanceAgent>() == null) player.AddComponent<PlayerAvoidanceAgent>();
            }

            var astar = Object.FindFirstObjectByType<AstarPath>();
            if (astar == null)
            {
                var go = new GameObject("Pathfinding");
                astar = go.AddComponent<AstarPath>();
            }
            if (astar.GetComponent<RVOSimulator>() == null) astar.gameObject.AddComponent<RVOSimulator>();
            astar.logPathResults = PathLog.None;
            astar.threadCount = ThreadCount.AutomaticHighLoad;
            AstarPath.active = astar;

            var graph = astar.data.recastGraph ?? astar.data.AddGraph<RecastGraph>();
            graph.name = "Town";
            graph.forcedBoundsCenter = graphCenter;
            graph.forcedBoundsSize = graphSize;
            graph.cellSize = 0.2f;
            graph.characterRadius = 0.3f;
            graph.walkableHeight = 1.8f;
            graph.walkableClimb = 0.45f;   // pavilion steps are 0.4 m
            graph.maxSlope = 35f;
            graph.useTiles = true;
            graph.editorTileSize = 128;
            graph.minRegionSize = 3f;
            graph.collectionSettings.collectionMode = RecastGraph.CollectionSettings.FilterMode.Layers;
            graph.collectionSettings.layerMask = ~((1 << characterLayer) | (1 << LayerMask.NameToLayer("Ignore Raycast")));
            graph.collectionSettings.rasterizeColliders = true;
            graph.collectionSettings.rasterizeMeshes = false;
            graph.collectionSettings.rasterizeTerrain = false;
            graph.collectionSettings.rasterizeTrees = false;

            string bake = Bake(astar);
            EditorSceneManager.MarkSceneDirty(astar.gameObject.scene);
            EditorSceneManager.SaveScene(astar.gameObject.scene);
            return $"Navigation set up: {tagged} objects moved to the {CharacterLayerName} layer. {bake}";
        }

        /// <summary>Scans the graphs and caches the result next to the scene (loaded at startup instead of scanning).</summary>
        public static string Bake(AstarPath astar)
        {
            if (astar == null) return "No AstarPath in the scene (run Setup Navigation first).";
            AstarPath.active = astar;
            // Recast finds colliders with a physics overlap query; colliders created/moved this frame
            // (e.g. stall blockers) aren't in the physics scene until transforms are synced.
            Physics.SyncTransforms();
            astar.Scan();

            // Graph settings live in the AstarPath component; nodes go to the startup cache file.
            astar.data.SetData(astar.data.SerializeGraphs());
            var bytes = astar.data.SerializeGraphs(new SerializeSettings { nodes = true });
            string scenePath = astar.gameObject.scene.path;
            string cachePath = Path.ChangeExtension(scenePath, null) + "_NavGraph.bytes";
            File.WriteAllBytes(cachePath, bytes);
            AssetDatabase.ImportAsset(cachePath);
            astar.data.file_cachedStartup = AssetDatabase.LoadAssetAtPath<TextAsset>(cachePath);
            astar.data.cacheStartup = true;
            EditorUtility.SetDirty(astar);

            int nodes = 0;
            astar.data.recastGraph?.GetNodes(_ => nodes++);
            return $"Baked RecastGraph: {nodes} navmesh triangles, cached to {cachePath} ({bytes.Length / 1024} KB).";
        }

        // ------------------------------------------------------------------ stalls

        static readonly NPCRole[] VendorRoles = { NPCRole.MarketVendor, NPCRole.TeaVendor, NPCRole.Shopkeeper };

        /// <summary>
        /// Adds a VendorStall (vendor post + customer area + navmesh blocker) to every greybox "Stall" object and
        /// every object that already has a VendorStall (the Meshy stall prefabs placed by a level builder), and
        /// posts a villager behind all but <paramref name="unattended"/> of them so both cases can be seen.
        /// </summary>
        public static string SetupVendorStalls(int unattended = 2)
        {
            var stalls = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
                if (t.name == "Stall" || t.GetComponent<VendorStall>() != null) stalls.Add(t);
            if (stalls.Count == 0) return "No stalls (objects named 'Stall' or with a VendorStall) in the scene.";
            Physics.SyncTransforms();   // spots are snapped to the ground with raycasts

            var free = new List<NPC>(Object.FindObjectsByType<NPC>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID));
            foreach (var t in stalls)
            {
                var old = t.GetComponent<VendorStall>();
                if (old != null && old.vendor != null) free.Remove(old.vendor);
            }

            int staffed = 0;
            for (int i = 0; i < stalls.Count; i++)
            {
                Transform t = stalls[i];
                var stall = t.GetComponent<VendorStall>();
                if (stall == null) stall = t.gameObject.AddComponent<VendorStall>();
                BuildSpots(stall);

                bool wantVendor = i >= unattended;
                if (!wantVendor)
                {
                    if (stall.vendor != null) UnassignVendor(stall);
                    continue;
                }
                NPC vendor = stall.vendor;
                if (vendor == null)
                {
                    vendor = PickVendor(free, stall.vendorPost.position);
                    if (vendor == null) continue;
                    free.Remove(vendor);
                }
                AssignVendor(stall, vendor);   // also re-places an existing vendor at a moved post
                staffed++;
                EditorUtility.SetDirty(stall);
            }

            // The blockers behind the stalls change the navmesh.
            string bake = Bake(Object.FindFirstObjectByType<AstarPath>());

            EditorSceneManager.MarkSceneDirty(stalls[0].gameObject.scene);
            EditorSceneManager.SaveScene(stalls[0].gameObject.scene);
            return $"Vendor stalls: {stalls.Count} set up, {staffed} with a vendor, {stalls.Count - staffed} unattended. {bake}";
        }

        static void BuildSpots(VendorStall stall)
        {
            Transform t = stall.transform;
            float halfDepth, halfWidth;
            Vector3 front;
            if (t.Find("Table") != null)
            {
                // Greybox stalls are axis-aligned tables (2 m along X, 1.1 m along Z). Customers stand at the
                // long side facing the plaza centre (in Z), the vendor across the table.
                halfDepth = 0.55f;
                halfWidth = 1.0f;
                front = new Vector3(0f, 0f, t.position.z > 0f ? -1f : 1f);
            }
            else
            {
                // Meshy stall prefabs: +Z is the customer side (goods on display), -Z the vendor's.
                Bounds local = LocalFootprint(t);
                halfDepth = local.extents.z;
                halfWidth = local.extents.x;
                front = t.forward;
            }

            stall.vendorPost = Spot(t, "VendorPost", -front * (halfDepth + 0.45f), front);
            // Customers browse anywhere in a strip along the counter, 0.4–1.2 m in front of it.
            stall.customerArea = Spot(t, "CustomerArea", front * (halfDepth + 0.8f), -front);
            stall.areaSize = new Vector2(halfWidth * 2f + 0.4f, 0.8f);
            foreach (string old in new[] { "CustomerSpot_0", "CustomerSpot_1" })
            {
                Transform legacy = t.Find(old);
                if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            }

            // Invisible block behind the table: the vendor's corner is cut out of the navmesh, so
            // passers-by walk round the front instead of squeezing past the vendor. The NavBlocker
            // layer collides with nothing, so the player can still sneak behind stalls.
            Transform blocker = t.Find("NavBlocker");
            if (blocker == null)
            {
                blocker = new GameObject("NavBlocker").transform;
                blocker.SetParent(t, false);
                blocker.gameObject.AddComponent<BoxCollider>();
            }
            int blockerLayer = LayerMask.NameToLayer(NavBlockerLayerName);
            if (blockerLayer >= 0) blocker.gameObject.layer = blockerLayer;
            const float depth = 1.3f;
            blocker.position = t.position - front * (halfDepth + depth * 0.5f) + Vector3.up * 0.5f;
            blocker.rotation = Quaternion.LookRotation(front);
            blocker.GetComponent<BoxCollider>().size = new Vector3(halfWidth * 2f + 0.6f, 1f, depth);
            // Solid + unwalkable: no navmesh inside the box *or* on its top face.
            var modifier = blocker.GetComponent<RecastNavmeshModifier>();
            if (modifier == null) modifier = blocker.gameObject.AddComponent<RecastNavmeshModifier>();
            modifier.mode = RecastNavmeshModifier.Mode.UnwalkableSurface;
            modifier.solid = true;
            modifier.dynamic = false;
            modifier.geometrySource = RecastNavmeshModifier.GeometrySource.Collider;
            modifier.includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysInclude;
        }

        /// <summary>Stall footprint in its own space, from the full-detail model (LOD0).</summary>
        static Bounds LocalFootprint(Transform stall)
        {
            Transform model = stall.Find("Model") ?? stall;
            var b = new Bounds();
            bool first = true;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 m = stall.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static Transform Spot(Transform parent, string name, Vector3 worldOffset, Vector3 facing)
        {
            Transform spot = parent.Find(name);
            if (spot == null)
            {
                spot = new GameObject(name).transform;
                spot.SetParent(parent, false);
            }
            Vector3 pos = parent.position + worldOffset;
            // Sit on the visible ground (plaza paving is a slab above y=0), not inside it.
            int ignore = (1 << LayerMask.NameToLayer(CharacterLayerName)) | (1 << LayerMask.NameToLayer(NavBlockerLayerName));
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, ~ignore, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            spot.position = pos;
            spot.rotation = Quaternion.LookRotation(facing);
            return spot;
        }

        static NPC PickVendor(List<NPC> candidates, Vector3 near)
        {
            NPC best = null;
            float bestScore = float.MaxValue;
            foreach (var npc in candidates)
            {
                if (npc == null || npc.GetComponent<NPCBrain>() == null) continue;
                float score = Vector3.Distance(npc.transform.position, near);
                if (System.Array.IndexOf(VendorRoles, npc.role) < 0) score += 1000f;   // prefer trade roles
                if (score < bestScore) { bestScore = score; best = npc; }
            }
            return best;
        }

        static void AssignVendor(VendorStall stall, NPC vendor)
        {
            stall.vendor = vendor;
            vendor.transform.SetPositionAndRotation(stall.vendorPost.position, stall.vendorPost.rotation);
            var brain = vendor.GetComponent<NPCBrain>();
            brain.tendedStall = stall;
            EditorUtility.SetDirty(brain);
            EditorUtility.SetDirty(vendor.transform);
        }

        static void UnassignVendor(VendorStall stall)
        {
            var brain = stall.vendor.GetComponent<NPCBrain>();
            if (brain != null) { brain.tendedStall = null; EditorUtility.SetDirty(brain); }
            stall.vendor = null;
        }

        static int SetLayerRecursive(GameObject go, int layer)
        {
            int count = 0;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == layer) continue;
                t.gameObject.layer = layer;
                count++;
            }
            return count;
        }
    }
}
