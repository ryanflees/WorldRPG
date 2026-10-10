using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG
{
    /// <summary>
    /// Camera-relative WASD walking. Only takes input while the camera rig is in
    /// Follow mode targeting this character.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TopDownCharacter : MonoBehaviour
    {
        public RTSCameraRig rig;
        public float walkSpeed = 4f;
        public float runSpeed = 8f;
        public float turnSmooth = 12f;
        public float gravity = -20f;

        CharacterController _controller;
        float _verticalVelocity;

        public bool IsControlled =>
            rig != null && rig.CurrentMode == RTSCameraRig.Mode.Follow && rig.followTarget == transform;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 move = Vector3.zero;
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
                move = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
                move *= kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSmooth * dt));
            }

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * dt;
            move.y = _verticalVelocity;

            _controller.Move(move * dt);
        }
    }
}
