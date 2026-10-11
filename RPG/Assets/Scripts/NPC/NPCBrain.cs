using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Ambient villager behaviour on top of the A* RichAI agent:
    /// wander to a random spot / visit a stall / walk up to someone standing, then
    ///  - empty spot: stand idle for a while,
    ///  - in front of a standing villager: both turn to face each other and chat,
    ///  - at a stall with a vendor behind it: customer and vendor chat (no vendor: just stand).
    /// NPCs with <see cref="tendedStall"/> set stay at that stall's vendor post.
    /// Animation follows automatically: NPCLocomotionAnimator reads the movement speed and
    /// NPCIdleAnimator switches to chat takes while <see cref="NPCIdleAnimator.Chatting"/> is set.
    /// </summary>
    [RequireComponent(typeof(NPC), typeof(RichAI))]
    public class NPCBrain : MonoBehaviour
    {
        public enum Activity
        {
            Deciding,
            WalkingToSpot,
            WalkingToStall,
            WalkingToPartner,
            Standing,
            WaitingForPartner,
            Chatting,
            ChattingAtStall,
            TendingStall,
        }

        static readonly List<NPCBrain> All = new List<NPCBrain>();

        [Header("Stall")]
        [Tooltip("If set, this NPC is the vendor of the stall and never leaves its post.")]
        public VendorStall tendedStall;

        [Header("Movement")]
        [Tooltip("How far a random wander target may be from the NPC (min/max, m).")]
        public Vector2 wanderDistance = new Vector2(6f, 30f);
        [Tooltip("Degrees per second when turning to face someone while standing.")]
        public float faceTurnSpeed = 220f;
        [Tooltip("Give up on a walk after barely moving for this long (s), e.g. boxed in by a crowd.")]
        public float stuckTime = 4f;

        [Header("Timing (seconds, min/max)")]
        public Vector2 standTime = new Vector2(4f, 12f);
        public Vector2 chatTime = new Vector2(10f, 24f);
        public Vector2 stallChatTime = new Vector2(8f, 18f);

        [Header("Choices when picking where to go next")]
        [Range(0f, 1f)] public float stallChance = 0.25f;
        [Range(0f, 1f)] public float socialChance = 0.3f;
        [Tooltip("Max distance to a stall / standing villager worth walking to (m).")]
        public float visitRadius = 30f;

        [Header("Chatting")]
        [Tooltip("Distance between two chatting villagers (m).")]
        public float chatDistance = 1.15f;
        [Tooltip("Two villagers standing this close and facing each other start chatting on their own (m).")]
        public float spontaneousChatDistance = 1.8f;

        [Header("Debug")]
        [SerializeField] Activity activity = Activity.Deciding;
        [SerializeField] NPCBrain partner;
        [SerializeField] VendorStall visitedStall;

        NPC _npc;
        RichAI _ai;
        NPCIdleAnimator _anim;
        float _timer;            // time left in the current activity
        float _walkTimeout;
        float _stuckTimer;
        NPCLocomotionAnimator _locomotion;
        Vector3 _facePoint;
        bool _hasFacePoint;

        public Activity CurrentActivity => activity;

#if UNITY_EDITOR
        // Last few activity changes ("12.3 Standing"), for debugging odd behaviour (editor only: allocates).
        readonly Queue<string> _history = new Queue<string>();
        Activity _loggedActivity = (Activity)(-1);
        public string History => string.Join(" > ", _history);
#else
        public string History => string.Empty;
#endif
        public NPCBrain Partner => partner;

        void Awake()
        {
            _npc = GetComponent<NPC>();
            _ai = GetComponent<RichAI>();
            _anim = GetComponent<NPCIdleAnimator>();
            _locomotion = GetComponent<NPCLocomotionAnimator>();
        }

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            if (partner != null) partner.PartnerLeft(this);
            partner = null;
            if (visitedStall != null) visitedStall.ReleaseSpot(this);
        }

        void Start()
        {
            if (tendedStall != null)
            {
                EnterTending();
                return;
            }
            // Villagers start out standing where they were placed, for a random while.
            EnterStanding(Random.Range(0.5f, standTime.y));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _timer -= dt;

            switch (activity)
            {
                case Activity.Deciding: Decide(); break;
                case Activity.WalkingToSpot: UpdateWalkToSpot(dt); break;
                case Activity.WalkingToStall: UpdateWalkToStall(dt); break;
                case Activity.WalkingToPartner: UpdateWalkToPartner(dt); break;
                case Activity.Standing: UpdateStanding(); break;
                case Activity.WaitingForPartner: UpdateWaiting(); break;
                case Activity.Chatting: UpdateChatting(); break;
                case Activity.ChattingAtStall: UpdateChattingAtStall(); break;
                case Activity.TendingStall: UpdateTending(); break;
            }

            if (_hasFacePoint && _ai.isStopped) FaceTowards(_facePoint, dt);

#if UNITY_EDITOR
            if (activity != _loggedActivity)
            {
                _loggedActivity = activity;
                _history.Enqueue($"{Time.time:F1} {activity}");
                if (_history.Count > 5) _history.Dequeue();
            }
#endif
        }

        // ------------------------------------------------------------------ deciding

        void Decide()
        {
            float roll = Random.value;
            if (roll < stallChance && TryVisitStall()) return;
            if (roll < stallChance + socialChance && TryApproachSomeone()) return;
            if (TryWander()) return;
            EnterStanding(Random.Range(standTime.x, standTime.y));
        }

        bool TryWander()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float dist = Random.Range(wanderDistance.x, wanderDistance.y);
                Vector3 candidate = transform.position + new Vector3(dir.x, 0f, dir.y) * dist;
                if (!TryGetReachable(candidate, out Vector3 point)) continue;
                if ((point - transform.position).sqrMagnitude < wanderDistance.x * wanderDistance.x * 0.25f) continue;
                StartWalking(Activity.WalkingToSpot, point);
                return true;
            }
            return false;
        }

        bool TryVisitStall()
        {
            VendorStall best = null;
            float bestScore = float.MaxValue;
            foreach (var stall in VendorStall.All)
            {
                if (!stall.HasRoom) continue;
                float d = Vector3.Distance(stall.transform.position, transform.position);
                if (d > visitRadius) continue;
                float score = d * Random.Range(0.5f, 1.5f);   // prefer near stalls, with some variety
                if (score < bestScore) { bestScore = score; best = stall; }
            }
            if (best == null || !best.TryClaimSpot(this, out Vector3 spot)) return false;
            if (!TryGetReachable(spot, out Vector3 point))
            {
                best.ReleaseSpot(this);
                return false;
            }
            visitedStall = best;
            StartWalking(Activity.WalkingToStall, point);
            return true;
        }

        bool TryApproachSomeone()
        {
            NPCBrain best = null;
            float bestDist = visitRadius;
            foreach (var other in All)
            {
                if (other == this || !other.CanBeApproached) continue;
                float d = Vector3.Distance(other.transform.position, transform.position);
                if (d < bestDist) { bestDist = d; best = other; }
            }
            if (best == null) return false;

            // Stand in front of them, on our side.
            Vector3 toUs = transform.position - best.transform.position;
            toUs.y = 0f;
            if (toUs.sqrMagnitude < 0.01f) toUs = best.transform.forward;
            Vector3 target = best.transform.position + toUs.normalized * chatDistance;
            if (!TryGetReachable(target, out Vector3 point)) return false;

            best.ReserveFor(this);
            partner = best;
            StartWalking(Activity.WalkingToPartner, point);
            return true;
        }

        // ------------------------------------------------------------------ walking

        void StartWalking(Activity walkActivity, Vector3 destination)
        {
            activity = walkActivity;
            _npc.SetState(NPCState.Walking);
            SetChatting(false);
            _hasFacePoint = false;
            // Walk exactly as fast as the walk cycle moves the feet (per-NPC playback speed included).
            _ai.maxSpeed = _locomotion != null ? _locomotion.WalkSpeed : 1.4f;
            _ai.isStopped = false;
            _ai.destination = destination;
            _ai.SearchPath();
            // Real blockages are caught by the stuck check; this is only a safety net (paths through the
            // streets can be several times longer than the straight-line distance).
            _walkTimeout = 120f;
            _stuckTimer = 0f;
        }

        /// <summary>
        /// At the end of the path *and* braked to (nearly) a stop. Switching to an idle/chat pose while
        /// RichAI is still decelerating made villagers visibly skate the last half metre.
        /// </summary>
        bool Arrived => !_ai.pathPending && (_ai.reachedEndOfPath || _ai.reachedDestination) && _ai.velocity.sqrMagnitude < 0.2f * 0.2f;

        /// <summary>True once the walk took far too long, or the NPC has been boxed in for <see cref="stuckTime"/>.</summary>
        bool WalkTimedOut(float dt)
        {
            _walkTimeout -= dt;
            bool barelyMoving = !_ai.pathPending && _locomotion != null && _locomotion.Speed < 0.15f;
            _stuckTimer = barelyMoving ? _stuckTimer + dt : 0f;
            return _walkTimeout <= 0f || _stuckTimer >= stuckTime;
        }

        void UpdateWalkToSpot(float dt)
        {
            if (Arrived || WalkTimedOut(dt)) EnterStanding(Random.Range(standTime.x, standTime.y));
        }

        void UpdateWalkToStall(float dt)
        {
            if (WalkTimedOut(dt))
            {
                LeaveStall();
                EnterStanding(Random.Range(standTime.x, standTime.y));
                return;
            }
            if (!Arrived) return;

            StopMoving();
            FacePoint(visitedStall.vendorPost != null ? visitedStall.vendorPost.position : visitedStall.transform.position);
            if (visitedStall.VendorPresent)
            {
                activity = Activity.ChattingAtStall;
                _npc.SetState(NPCState.Chatting);
                _timer = Random.Range(stallChatTime.x, stallChatTime.y);
                visitedStall.SetCustomerChatting(this, true);
                SetChatting(true);
            }
            else
            {
                // Nobody behind the stall: look at the goods for a while, no chatting.
                activity = Activity.Standing;
                _npc.SetState(NPCState.Idle);
                _timer = Random.Range(standTime.x, standTime.y);
            }
        }

        void UpdateWalkToPartner(float dt)
        {
            // They gave up waiting / got pulled into something else.
            if (partner == null || partner.activity != Activity.WaitingForPartner || partner.partner != this)
            {
                partner = null;
                EnterDeciding();
                return;
            }
            float d = PlanarDistance(transform.position, partner.transform.position);
            if (WalkTimedOut(dt))
            {
                partner.PartnerLeft(this);
                partner = null;
                EnterStanding(Random.Range(standTime.x, standTime.y));
                return;
            }
            if (Arrived || (d <= chatDistance + 0.25f && _ai.velocity.sqrMagnitude < 0.2f * 0.2f))
                StartChat(this, partner, Random.Range(chatTime.x, chatTime.y));
        }

        // ------------------------------------------------------------------ standing / chatting

        void EnterDeciding()
        {
            activity = Activity.Deciding;
            SetChatting(false);
        }

        void EnterStanding(float duration)
        {
            StopMoving();
            activity = Activity.Standing;
            _npc.SetState(NPCState.Idle);
            _timer = duration;
            _hasFacePoint = false;
            SetChatting(false);
        }

        void UpdateStanding()
        {
            if (_timer <= 0f)
            {
                LeaveStall();
                EnterDeciding();
                return;
            }
            if (visitedStall == null) TrySpontaneousChat();
        }

        /// <summary>Two idle villagers that happen to stand face to face strike up a chat.</summary>
        void TrySpontaneousChat()
        {
            foreach (var other in All)
            {
                if (other == this || other.activity != Activity.Standing || other.visitedStall != null) continue;
                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                float d = toOther.magnitude;
                if (d > spontaneousChatDistance || d < 0.01f) continue;
                Vector3 dir = toOther / d;
                if (Vector3.Dot(transform.forward, dir) < 0.5f || Vector3.Dot(other.transform.forward, -dir) < 0.5f) continue;
                StartChat(this, other, Random.Range(chatTime.x, chatTime.y));
                return;
            }
        }

        bool CanBeApproached => activity == Activity.Standing && visitedStall == null && tendedStall == null && _timer > 3f;

        void ReserveFor(NPCBrain approacher)
        {
            activity = Activity.WaitingForPartner;
            partner = approacher;
            _timer = 25f;   // give up if they never arrive
        }

        void UpdateWaiting()
        {
            if (partner == null || _timer <= 0f)
            {
                partner = null;
                EnterStanding(Random.Range(1f, 3f));
                return;
            }
            // Turn to look at them once they're close.
            if (PlanarDistance(transform.position, partner.transform.position) < 5f)
                FacePoint(partner.transform.position);
        }

        static void StartChat(NPCBrain a, NPCBrain b, float duration)
        {
            foreach (var (self, other) in new[] { (a, b), (b, a) })
            {
                self.StopMoving();
                self.activity = Activity.Chatting;
                self._npc.SetState(NPCState.Chatting);
                self.partner = other;
                self._timer = duration;
                self.FacePoint(other.transform.position);
                self.SetChatting(true);
            }
        }

        void UpdateChatting()
        {
            if (partner == null)
            {
                EnterStanding(Random.Range(1f, 4f));
                return;
            }
            FacePoint(partner.transform.position);
            if (_timer > 0f) return;

            // End the conversation for both; each wanders off on its own.
            var other = partner;
            partner = null;
            other.PartnerLeft(this);
            EnterDeciding();
        }

        /// <summary>Called by the partner when it walks away, gives up, or is disabled.</summary>
        void PartnerLeft(NPCBrain who)
        {
            if (partner != who) return;
            partner = null;
            if (activity == Activity.Chatting) EnterDeciding();
            else if (activity == Activity.WaitingForPartner) EnterStanding(Random.Range(1f, 3f));
            else if (activity == Activity.WalkingToPartner) EnterDeciding();
        }

        void UpdateChattingAtStall()
        {
            bool vendorGone = !visitedStall.VendorPresent;
            if (_timer > 0f && !vendorGone) return;
            LeaveStall();
            EnterDeciding();
        }

        void LeaveStall()
        {
            if (visitedStall == null) return;
            visitedStall.ReleaseSpot(this);
            visitedStall = null;
        }

        // ------------------------------------------------------------------ vendor

        void EnterTending()
        {
            activity = Activity.TendingStall;
            _npc.SetState(NPCState.Idle);
            // Vendors stand in a corner cut out of the navmesh (so nobody squeezes behind the stall),
            // so they don't path at all; they stay an RVO obstacle that passers-by steer around.
            StopMoving();
            _ai.canMove = false;
            _ai.canSearch = false;
            if (tendedStall.vendorPost != null)
                transform.SetPositionAndRotation(tendedStall.vendorPost.position, tendedStall.vendorPost.rotation);
        }

        void UpdateTending()
        {
            bool chatting = tendedStall.HasChattingCustomer;
            SetChatting(chatting);
            _npc.SetState(chatting ? NPCState.Chatting : NPCState.Idle);

            Component customer = tendedStall.AnyChattingCustomer;
            if (customer != null) FacePoint(customer.transform.position);
            else if (tendedStall.vendorPost != null) FacePoint(tendedStall.vendorPost.position + tendedStall.vendorPost.forward);
        }

        // ------------------------------------------------------------------ helpers

        void StopMoving()
        {
            _ai.isStopped = true;
            _ai.destination = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }

        void SetChatting(bool chatting)
        {
            if (_anim != null) _anim.Chatting = chatting;
        }

        void FacePoint(Vector3 point)
        {
            _facePoint = point;
            _hasFacePoint = true;
        }

        void FaceTowards(Vector3 point, float dt)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            _ai.rotation = Quaternion.RotateTowards(_ai.rotation, target, faceTurnSpeed * dt);
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        bool TryGetReachable(Vector3 candidate, out Vector3 point)
        {
            point = candidate;
            if (AstarPath.active == null) return false;
            NNInfo from = AstarPath.active.GetNearest(transform.position, NNConstraint.Walkable);
            NNInfo to = AstarPath.active.GetNearest(candidate, NNConstraint.Walkable);
            if (from.node == null || to.node == null) return false;
            if ((to.position - candidate).sqrMagnitude > 4f) return false;   // candidate was inside a building
            if (!PathUtilities.IsPathPossible(from.node, to.node)) return false;
            point = to.position;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = Color.cyan;
            if (partner != null) Gizmos.DrawLine(transform.position + Vector3.up, partner.transform.position + Vector3.up);
            if (!_ai.isStopped) Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, _ai.destination + Vector3.up * 0.1f);
        }
    }
}
