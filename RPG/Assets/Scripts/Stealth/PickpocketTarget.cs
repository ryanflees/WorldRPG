using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// Makes a villager pickpocketable and lets them notice the thief:
    /// a forward vision cone (blocked by walls) and a band behind them where a lift can be started.
    /// Stall vendors can be picked too, and carry the day's takings, but most stand with their backs to the stall.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPC))]
    public class PickpocketTarget : MonoBehaviour
    {
        public static readonly List<PickpocketTarget> All = new List<PickpocketTarget>();

        [Header("Vision")]
        public float visionDistance = 6f;
        [Tooltip("Full cone angle, degrees.")]
        public float visionAngle = 110f;
        public float eyeHeight = 1.6f;
        [Tooltip("Half-width of a body that blocks sight (m): someone standing between this NPC and the thief hides him.")]
        public float bodyCoverRadius = 0.4f;
        [Tooltip("How fast suspicion builds while this NPC is chatting, relative to normal (absorbed in the conversation).")]
        [Range(0f, 1f)] public float chattingAttention = 0.3f;

        [Header("Rear zone (where a lift can start)")]
        [Tooltip("Full angle of the band behind the NPC, degrees.")]
        public float rearAngle = 150f;
        public float rearMin = 0.35f;
        public float rearMax = 2.2f;
        [Tooltip("The lift breaks if the thief falls further behind than this (m).")]
        public float followRange = 3.5f;

        [Header("Purse")]
        [Tooltip("Multiplier on purse values. Negative = derive from the NPC's role.")]
        public float wealth = -1f;
        [Tooltip("After being robbed, the purse stays empty for this long (s).")]
        public float refillTime = 180f;

        NPC _npc;
        NPCBrain _brain;
        IAstarAI _ai;
        float _unavailableUntil;
        float _waryUntil;
        static int _losMask = -1;
        const float VendorTakings = 1.5f;   // purse multiplier: vendors carry the day's takings

        public NPC Npc => _npc;
        public string DisplayName => _npc != null ? _npc.displayName : name;
        public float Wealth => wealth >= 0f ? wealth
            : RoleWealth(_npc != null ? _npc.role : NPCRole.Villager) * (IsVendor ? VendorTakings : 1f);
        public bool IsVendor => _brain != null && _brain.tendedStall != null;
        public bool CanBePicked => BlockedReason == null;

        /// <summary>Why this NPC can't be picked right now (shown on the greyed-out ring), or null if they can.</summary>
        public string BlockedReason
        {
            get
            {
                if (Time.time < _waryUntil) return "On guard";
                if (Time.time < _unavailableUntil) return "Already picked clean";
                return null;
            }
        }
        public bool RecentlyRobbed { get; private set; }
        public Vector3 Velocity => _ai != null ? _ai.velocity : Vector3.zero;
        public Vector3 HeadPosition => transform.position + Vector3.up * 2.05f;
        /// <summary>Multiplier on how fast this NPC grows suspicious (lower while chatting).</summary>
        public float Attention => _npc != null && _npc.State == NPCState.Chatting ? chattingAttention : 1f;

        void Awake()
        {
            _npc = GetComponent<NPC>();
            _brain = GetComponent<NPCBrain>();
            _ai = GetComponent<IAstarAI>();
            if (_losMask == -1)
            {
                int character = LayerMask.NameToLayer("Character");
                _losMask = Physics.DefaultRaycastLayers & ~(character >= 0 ? 1 << character : 0);
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (RecentlyRobbed && Time.time >= _unavailableUntil) RecentlyRobbed = false;
        }

        /// <summary>
        /// The thief is inside the vision cone with a clear line of sight: no wall in the way, and no other villager's
        /// body (so a chat partner looking at the mark can't see the thief tucked in behind them).
        /// </summary>
        public bool CanSee(Vector3 thiefPosition)
        {
            if (!InSector(thiefPosition, transform.forward, 0f, visionDistance, visionAngle)) return false;
            if (BodyInTheWay(thiefPosition)) return false;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 chest = thiefPosition + Vector3.up * 1.2f;
            return !Physics.Linecast(eye, chest, _losMask, QueryTriggerInteraction.Ignore);
        }

        bool BodyInTheWay(Vector3 thiefPosition)
        {
            Vector2 from = new Vector2(transform.position.x, transform.position.z);
            Vector2 ray = new Vector2(thiefPosition.x, thiefPosition.z) - from;
            float length2 = ray.sqrMagnitude;
            if (length2 < 0.0001f) return false;
            foreach (var other in All)
            {
                if (other == this) continue;
                Vector2 body = new Vector2(other.transform.position.x, other.transform.position.z) - from;
                float along = Vector2.Dot(body, ray) / length2;
                if (along <= 0f || along >= 1f) continue;
                if ((body - ray * along).sqrMagnitude < other.bodyCoverRadius * other.bodyCoverRadius) return true;
            }
            return false;
        }

        public bool IsInRearZone(Vector3 point) => InSector(point, -transform.forward, rearMin, rearMax, rearAngle);

        public float PlanarDistance(Vector3 point)
        {
            float dx = point.x - transform.position.x, dz = point.z - transform.position.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Robbed: nothing left to take for <see cref="refillTime"/>.</summary>
        public void MarkRobbed()
        {
            RecentlyRobbed = true;
            _unavailableUntil = Time.time + refillTime;
        }

        /// <summary>Caught the thief: stop, stare at them, and stay on guard for a while.</summary>
        public void Startle(Vector3 thiefPosition, float stareTime, float wariness)
        {
            _waryUntil = Mathf.Max(_waryUntil, Time.time + wariness);
            if (_brain != null) _brain.Startle(thiefPosition, stareTime);
        }

        bool InSector(Vector3 point, Vector3 facing, float min, float max, float angle)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            facing.y = 0f;
            float dist = dir.magnitude;
            if (dist < min || dist > max) return false;
            if (dist < 0.001f) return true;
            return Vector3.Angle(facing, dir) <= angle * 0.5f;
        }

        static float RoleWealth(NPCRole role)
        {
            switch (role)
            {
                case NPCRole.Shopkeeper:
                case NPCRole.Elder: return 1.6f;
                case NPCRole.MarketVendor:
                case NPCRole.TeaVendor: return 1.2f;
                case NPCRole.FarmWorker:
                case NPCRole.Porter: return 0.6f;
                default: return 1f;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            DrawFan(transform.forward, 0f, visionDistance, visionAngle);
            Gizmos.color = new Color(0.3f, 0.9f, 0.95f);
            DrawFan(-transform.forward, rearMin, rearMax, rearAngle);
        }

        void DrawFan(Vector3 facing, float min, float max, float angle)
        {
            Vector3 c = transform.position + Vector3.up * 0.05f;
            Vector3 l = Quaternion.Euler(0f, -angle * 0.5f, 0f) * facing;
            Vector3 r = Quaternion.Euler(0f, angle * 0.5f, 0f) * facing;
            Gizmos.DrawLine(c + l * min, c + l * max);
            Gizmos.DrawLine(c + r * min, c + r * max);
            const int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                Vector3 a = Quaternion.Euler(0f, Mathf.Lerp(-angle, angle, i / (float)steps) * 0.5f, 0f) * facing;
                Vector3 b = Quaternion.Euler(0f, Mathf.Lerp(-angle, angle, (i + 1) / (float)steps) * 0.5f, 0f) * facing;
                Gizmos.DrawLine(c + a * max, c + b * max);
                if (min > 0f) Gizmos.DrawLine(c + a * min, c + b * min);
            }
        }
    }
}
