using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Drives the BasicAC locomotion blend tree from the CharacterController's actual
    /// planar speed. The blend tree thresholds (Idle 0, Walk 0.333, Run 1, Sprint 1.3) are
    /// mapped piecewise to the clips' real root speeds, so any movement speed plays the
    /// matching gait without foot sliding. Movement is script-driven: root motion stays off.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionAnimator : MonoBehaviour
    {
        static readonly int SpeedNormalizedId = Animator.StringToHash("SpeedNormalized");

        public Animator animator;

        [Header("Clip root speeds (m/s, MovementAnimsetPro)")]
        public float walkClipSpeed = 1.57f;
        public float runClipSpeed = 3.41f;
        public float sprintClipSpeed = 5.82f;

        [Header("Blend tree thresholds (BasicAC)")]
        public float walkThreshold = 0.333f;
        public float runThreshold = 1f;
        public float sprintThreshold = 1.3f;

        [Tooltip("Damping applied to the blend parameter, in seconds.")]
        public float dampTime = 0.1f;

        CharacterController _controller;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        void Update()
        {
            if (animator == null) return;
            Vector3 v = _controller.velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            animator.SetFloat(SpeedNormalizedId, SpeedToBlend(speed), dampTime, Time.deltaTime);
        }

        float SpeedToBlend(float speed)
        {
            // Clip speeds are authored for a human scale of 1; scale them to this avatar.
            float scale = animator.isHuman ? animator.humanScale : 1f;
            float walk = walkClipSpeed * scale, run = runClipSpeed * scale, sprint = sprintClipSpeed * scale;

            if (speed <= walk) return Mathf.Lerp(0f, walkThreshold, speed / walk);
            if (speed <= run) return Mathf.Lerp(walkThreshold, runThreshold, (speed - walk) / (run - walk));
            return Mathf.LerpUnclamped(runThreshold, sprintThreshold, Mathf.Min((speed - run) / (sprint - run), 1f));
        }
    }
}
