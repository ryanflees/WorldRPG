using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Drives the BasicAC locomotion blend tree for an NPC from how fast its transform actually
    /// moves, so it works with whatever moves the NPC later (pathfinding agent, scripted mover...).
    /// Standing still plays the idle. Each NPC starts the loop at a random phase and slightly
    /// different playback speed so a crowd doesn't breathe in lockstep.
    /// </summary>
    public class NPCLocomotionAnimator : MonoBehaviour
    {
        public Animator animator;
        public LocomotionBlend blend = new LocomotionBlend();

        [Tooltip("Damping applied to the blend parameter, in seconds.")]
        public float dampTime = 0.15f;

        [Tooltip("Random playback speed range, to desync idles across a crowd.")]
        public Vector2 speedVariation = new Vector2(0.9f, 1.1f);

        Vector3 _lastPosition;

        /// <summary>Measured planar speed in m/s.</summary>
        public float Speed { get; private set; }

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        void Start()
        {
            _lastPosition = transform.position;
            if (animator == null) return;
            animator.speed = Random.Range(speedVariation.x, speedVariation.y);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(info.fullPathHash, 0, Random.value);
        }

        void Update()
        {
            Vector3 position = transform.position;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 delta = position - _lastPosition;
                Speed = new Vector2(delta.x, delta.z).magnitude / dt;
            }
            _lastPosition = position;

            if (animator == null) return;
            animator.SetFloat(LocomotionBlend.SpeedNormalizedId, blend.SpeedToBlend(Speed, animator), dampTime, dt);
        }
    }
}
