using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Plays random standing idles on BasicAC's IdleLayer whenever the NPC stands still. The layer
    /// fully overrides the base locomotion layer while standing: idles chain back to back, and the
    /// layer only fades out once the NPC starts moving. The idle pool follows the NPC's gender.
    /// While <see cref="Chatting"/> is set (by NPCBrain), standing chat takes are chained instead.
    /// </summary>
    [RequireComponent(typeof(NPCLocomotionAnimator))]
    public class NPCIdleAnimator : MonoBehaviour
    {
        [Tooltip("Planar speed (m/s) below which the NPC counts as standing.")]
        public float standingSpeed = 0.05f;

        [Tooltip("Cross-fade between consecutive idles, in seconds.")]
        public float crossFadeDuration = 0.5f;

        [Tooltip("Time to fade the IdleLayer in when the NPC stops / out when it starts moving, in seconds.")]
        public float layerFadeDuration = 0.25f;

        [Tooltip("Also draw idles performed by actors of the other gender.")]
        public bool mixGenders = false;

        NPCLocomotionAnimator _locomotion;
        BasicAC _ac;
        int[] _idlePool, _chatPool;
        bool _chatting;
        int _current;   // state ID playing on the idle layer, 0 when none
        float _weight;
        bool _parked;   // idle layer sent back to its Empty state after fading out

        void Awake()
        {
            _locomotion = GetComponent<NPCLocomotionAnimator>();
        }

        void Start()
        {
            Animator animator = _locomotion.animator;
            if (animator == null) { enabled = false; return; }
            _ac = new BasicAC(animator);

            var npc = GetComponent<NPC>();
            bool female = npc != null && npc.gender == NPCGender.Female;
            _idlePool = mixGenders ? Concat(_ac.FemaleIdleStates, _ac.MaleIdleStates) : female ? _ac.FemaleIdleStates : _ac.MaleIdleStates;
            _chatPool = mixGenders ? Concat(_ac.FemaleChatStates, _ac.MaleChatStates) : female ? _ac.FemaleChatStates : _ac.MaleChatStates;

            // NPCs spawn standing: start mid-idle at a random point so a crowd doesn't move in sync.
            _current = PickIdle();
            _ac.PlayIdleLayer(_current, Random.value);
            _weight = 1f;
            animator.SetLayerWeight(_ac.IdleLayer, _weight);
        }

        void Update()
        {
            bool standing = _locomotion.Speed < standingSpeed;
            if (standing)
            {
                if (_current == 0 || RemainingSeconds() <= crossFadeDuration)
                {
                    _current = PickIdle();
                    _ac.CrossFadeIdleLayer(_current, crossFadeDuration);
                    _parked = false;
                }
            }
            else
            {
                _current = 0;
            }

            float target = _current != 0 ? 1f : 0f;
            _weight = layerFadeDuration > 0f ? Mathf.MoveTowards(_weight, target, Time.deltaTime / layerFadeDuration) : target;
            _ac.Animator.SetLayerWeight(_ac.IdleLayer, _weight);
            if (_weight == 0f && _current == 0 && !_parked)
            {
                _ac.PlayEmpty_1(0f);   // park the layer so it stops evaluating an invisible clip
                _parked = true;
            }
        }

        /// <summary>
        /// Chat instead of idle while standing. Switching cuts the current take short with a cross-fade,
        /// so a conversation starts (and ends) as soon as the brain decides.
        /// </summary>
        public bool Chatting
        {
            get => _chatting;
            set
            {
                if (_chatting == value) return;
                _chatting = value;
                if (_ac != null && _current != 0)
                {
                    _current = PickIdle();
                    _ac.CrossFadeIdleLayer(_current, crossFadeDuration);
                }
            }
        }

        int PickIdle()
        {
            int[] pool = _chatting ? _chatPool : _idlePool;
            if (pool.Length == 1) return pool[0];
            int id;
            do id = pool[Random.Range(0, pool.Length)];
            while (id == _current);
            return id;
        }

        static int[] Concat(int[] a, int[] b)
        {
            var result = new int[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        /// <summary>Seconds left in the current idle; infinite until its cross-fade has started.</summary>
        float RemainingSeconds()
        {
            Animator animator = _ac.Animator;
            int layer = _ac.IdleLayer;
            AnimatorStateInfo info = animator.IsInTransition(layer)
                ? animator.GetNextAnimatorStateInfo(layer)
                : animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash != _current) return float.PositiveInfinity;
            return (1f - info.normalizedTime) * info.length / Mathf.Max(animator.speed, 0.01f);
        }
    }
}
