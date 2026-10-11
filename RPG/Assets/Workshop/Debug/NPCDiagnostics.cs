using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WorldRPG.Debugging
{
    /// <summary>
    /// Temporary play-mode probe: records per-frame position jumps of NPCs and the lowest sole height
    /// (relative to the NPC root) per animator state, so foot sinking / snapping can be measured.
    /// Add one to any GameObject in play mode and read <see cref="Report"/>.
    /// </summary>
    public class NPCDiagnostics : MonoBehaviour
    {
        public float jumpSpeed = 6f;   // m/s in one frame counts as a jump

        public static NPCDiagnostics Instance;
        public readonly List<string> Jumps = new List<string>();
        readonly Dictionary<string, Vector2> _soleByState = new Dictionary<string, Vector2>(); // (min, sum) per state
        readonly Dictionary<string, int> _samples = new Dictionary<string, int>();
        readonly Dictionary<NPCBrain, Vector3> _last = new Dictionary<NPCBrain, Vector3>();
        readonly Dictionary<NPCBrain, string> _lastActivity = new Dictionary<NPCBrain, string>();
        readonly Dictionary<int, string> _stateNames = new Dictionary<int, string>();
        NPCBrain[] _brains;

        // Sliding monitor: planar speed averaged over a short window.
        public float window = 0.25f;
        public readonly List<string> Slides = new List<string>();
        class Track { public Queue<(float t, Vector3 p)> samples = new Queue<(float, Vector3)>(); public bool inEpisode; public float start; public Vector3 startPos; public float peak; public string why; public string detail; }
        readonly Dictionary<NPCBrain, Track> _tracks = new Dictionary<NPCBrain, Track>();

        void Awake()
        {
            Instance = this;
            _brains = FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);
            var ctrl = _brains.Length > 0 ? _brains[0].GetComponentInChildren<Animator>().runtimeAnimatorController : null;
            if (ctrl != null)
                foreach (var clip in ctrl.animationClips) _stateNames[Animator.StringToHash(clip.name)] = clip.name;
            _stateNames[Animator.StringToHash("Locomotion")] = "Locomotion";
            _stateNames[Animator.StringToHash("Empty")] = "Empty";
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var b in _brains)
            {
                if (b == null) continue;
                Vector3 p = b.transform.position;
                string act = b.CurrentActivity.ToString();
                if (_last.TryGetValue(b, out var prev) && dt > 0f)
                {
                    float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(prev.x, 0, prev.z));
                    if (d / dt > jumpSpeed && Jumps.Count < 200)
                        Jumps.Add($"t={Time.time:F1} {b.name} {d:F2}m {_lastActivity[b]}->{act} from {prev:F2} to {p:F2} dy={p.y - prev.y:F2}");
                }
                _last[b] = p;
                _lastActivity[b] = act;
                TrackSlide(b, p, act);

                var anim = b.GetComponentInChildren<Animator>();
                if (anim == null || !anim.isHuman) continue;
                string state = StateKey(anim);
                float sole = Mathf.Min(
                    anim.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - anim.leftFeetBottomHeight,
                    anim.GetBoneTransform(HumanBodyBones.RightFoot).position.y - anim.rightFeetBottomHeight) - p.y;
                _soleByState.TryGetValue(state, out var acc);
                _samples.TryGetValue(state, out int n);
                if (n == 0) acc.x = sole;
                acc.x = Mathf.Min(acc.x, sole);
                acc.y += sole;
                _soleByState[state] = acc;
                _samples[state] = n + 1;
            }
        }

        void TrackSlide(NPCBrain b, Vector3 p, string act)
        {
            if (!_tracks.TryGetValue(b, out var tr)) _tracks[b] = tr = new Track();
            float now = Time.time;
            tr.samples.Enqueue((now, p));
            while (tr.samples.Count > 2 && now - tr.samples.Peek().t > window) tr.samples.Dequeue();
            var first = tr.samples.Peek();
            float span = now - first.t;
            if (span < window * 0.5f) return;
            Vector3 d = p - first.p; d.y = 0f;
            float speed = d.magnitude / span;

            var ai = b.GetComponent<Pathfinding.RichAI>();
            bool shouldBeStill = act == "Standing" || act == "Chatting" || act == "WaitingForPartner" || act == "ChattingAtStall" || act == "TendingStall";
            string why = null;
            if (speed > ai.maxSpeed * 1.25f) why = $"fast {speed:F2}>{ai.maxSpeed:F2}";
            else if (shouldBeStill && speed > 0.25f) why = $"sliding while {act} {speed:F2}m/s";

            if (why != null)
            {
                if (!tr.inEpisode)
                {
                    tr.inEpisode = true; tr.start = first.t; tr.startPos = first.p; tr.peak = 0f; tr.why = why;
                    var rvo = b.GetComponent<Pathfinding.RVO.RVOController>();
                    NPCBrain nearest = null; float nd = 99f;
                    foreach (var o in _brains) { if (o == b || o == null) continue; float od = Vector3.Distance(o.transform.position, p); if (od < nd) { nd = od; nearest = o; } }
                    float pd = Vector3.Distance(GameObject.Find("Player").transform.position, p);
                    tr.detail = $"act={act} stopped={ai.isStopped} canMove={ai.canMove} locked={rvo.locked} desired={ai.desiredVelocity.magnitude:F2} desiredNoRVO={ai.desiredVelocityWithoutLocalAvoidance.magnitude:F2} vel={ai.velocity.magnitude:F2} nearest={(nearest ? nearest.name.Replace("NPC_song_", "") + " " + nearest.CurrentActivity : "-")} @{nd:F2}m player@{pd:F1}m offMesh={OffMesh(p):F2}m hist[{b.History}]";
                }
                tr.peak = Mathf.Max(tr.peak, speed);
            }
            else if (tr.inEpisode)
            {
                tr.inEpisode = false;
                Vector3 moved = p - tr.startPos; moved.y = 0f;
                if (Slides.Count < 300)
                    Slides.Add($"t={tr.start:F1}-{now:F1} {b.name.Replace("NPC_song_", "")} {tr.why} peak={tr.peak:F2} moved={moved.magnitude:F2}m now={act} | {tr.detail}");
            }
        }

        static float OffMesh(Vector3 p)
        {
            if (AstarPath.active == null) return -1f;
            var nn = AstarPath.active.GetNearest(p, Pathfinding.NNConstraint.Walkable);
            return nn.node == null ? 99f : Vector3.Distance(nn.position, p);
        }

        string StateKey(Animator anim)
        {
            float w = anim.layerCount > 1 ? anim.GetLayerWeight(1) : 0f;
            if (w > 0.99f)
            {
                var info = anim.GetCurrentAnimatorStateInfo(1);
                if (anim.IsInTransition(1)) return "IdleLayer(transition)";
                return _stateNames.TryGetValue(info.shortNameHash, out var n) ? n : info.shortNameHash.ToString();
            }
            if (w > 0.01f) return "IdleLayer(fading)";
            float blend = anim.GetFloat("SpeedNormalized");
            return blend < 0.05f ? "Loco:idle" : blend < 0.3f ? "Loco:idle-walk" : blend < 0.36f ? "Loco:walk" : "Loco:walk-run+";
        }

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"slides ({Slides.Count}):");
            foreach (var sl in Slides) sb.AppendLine("  " + sl);
            sb.AppendLine($"jumps ({Jumps.Count}):");
            foreach (var j in Jumps) sb.AppendLine("  " + j);
            sb.AppendLine("sole height vs root (cm): state: min / avg (samples)");
            var keys = new List<string>(_soleByState.Keys);
            keys.Sort();
            foreach (var k in keys)
            {
                var v = _soleByState[k];
                int n = _samples[k];
                sb.AppendLine($"  {k}: {v.x * 100f:F1} / {v.y / n * 100f:F1} ({n})");
            }
            return sb.ToString();
        }
    }
}
