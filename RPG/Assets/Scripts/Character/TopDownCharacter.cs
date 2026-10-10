using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG
{
    /// <summary>
    /// Camera-relative WASD walking. Only takes input while the camera rig is in
    /// Follow mode targeting this character.
    /// Speeds default to the root speeds of the Movement animset clips so feet don't slide.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TopDownCharacter : MonoBehaviour
    {
        public RTSCameraRig rig;
        public float walkSpeed = 1.6f;
        public float runSpeed = 3.4f;
        public float acceleration = 12f;        // m/s^2 when speeding up
        public float deceleration = 16f;        // m/s^2 when slowing down
        public float turnSmooth = 12f;
        public float gravity = -20f;

        CharacterController _controller;
        Vector3 _horizontalVelocity;
        float _verticalVelocity;

        public bool IsControlled =>
            rig != null && rig.isActiveAndEnabled && rig.CurrentMode == RTSCameraRig.Mode.Follow && rig.followTarget == transform;

        /// <summary>Planar velocity the character is trying to move at (before collisions).</summary>
        public Vector3 DesiredVelocity => _horizontalVelocity;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 targetVelocity = Vector3.zero;
            var kb = Keyboard.current;

            if (IsControlled && kb != null)
            {
                Vector2 input = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;

                Vector3 forward = rig.transform.forward;
                Vector3 right = rig.transform.right;
                targetVelocity = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
                targetVelocity *= kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            }

            float rate = targetVelocity.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * dt);

            // Face the input direction, not the eased velocity, so turning feels responsive.
            Vector3 facing = targetVelocity.sqrMagnitude > 0.001f ? targetVelocity : _horizontalVelocity;
            if (facing.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(facing, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSmooth * dt));
            }

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * dt;

            Vector3 move = _horizontalVelocity;
            move.y = _verticalVelocity;
            _controller.Move(move * dt);
        }
    }
}
