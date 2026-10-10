using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Drives the BasicAC locomotion blend tree from the CharacterController's actual
    /// planar speed (see <see cref="LocomotionBlend"/> for the speed mapping).
    /// Movement is script-driven: root motion stays off.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionAnimator : MonoBehaviour
    {
        public Animator animator;
        public LocomotionBlend blend = new LocomotionBlend();

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
            animator.SetFloat(LocomotionBlend.SpeedNormalizedId, blend.SpeedToBlend(speed, animator), dampTime, Time.deltaTime);
        }
    }
}
