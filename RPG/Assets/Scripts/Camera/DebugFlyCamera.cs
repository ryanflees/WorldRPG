using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG
{
    /// <summary>
    /// Debug free-fly camera. Toggle with F1: takes over the rig's camera (so all camera and
    /// post-processing settings are shared), suspends the rig and with it character input.
    /// Toggling back hands the camera to the rig, which restores its previous mode.
    /// Controls: WASD move, Q/E down/up, hold right mouse to look, Shift = fast, scroll = speed.
    /// </summary>
    public class DebugFlyCamera : MonoBehaviour
    {
        public RTSCameraRig rig;
        public Key toggleKey = Key.F1;

        [Header("Movement")]
        public float moveSpeed = 10f;
        public float fastMultiplier = 4f;
        public float minSpeed = 1f;
        public float maxSpeed = 100f;
        [Tooltip("Speed multiplier per scroll notch.")]
        public float scrollSpeedFactor = 1.2f;
        public float moveSmooth = 10f;

        [Header("Look")]
        public float lookSensitivity = 0.15f;   // degrees per pixel

        public bool IsActive { get; private set; }
        public event Action<bool> ActiveChanged;

        Camera _cam;
        float _yaw, _pitch;
        Vector3 _velocity;
        bool _looking;
        Vector2 _lookCursorPos;

        void Awake()
        {
            if (rig == null) rig = FindAnyObjectByType<RTSCameraRig>();
        }

        public void SetActive(bool active)
        {
            if (active == IsActive || rig == null) return;
            _cam = rig.cam != null ? rig.cam : rig.GetComponentInChildren<Camera>();
            if (_cam == null) return;

            IsActive = active;
            if (active)
            {
                Vector3 euler = _cam.transform.eulerAngles;
                _yaw = euler.y;
                _pitch = NormalizeAngle(euler.x);
                _velocity = Vector3.zero;
                rig.enabled = false;
            }
            else
            {
                EndLook();
                rig.enabled = true;   // rig re-applies its own camera pose next LateUpdate
            }
            ActiveChanged?.Invoke(active);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) SetActive(!IsActive);
        }

        void LateUpdate()
        {
            if (!IsActive || _cam == null) return;
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (mouse != null)
            {
                bool look = mouse.rightButton.isPressed;
                if (look && !_looking) BeginLook(mouse);
                else if (!look && _looking) EndLook();
                // Skip the press frame: locking the cursor can produce a one-off delta spike.
                else if (look)
                {
                    Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                    _yaw += delta.x;
                    _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    moveSpeed = Mathf.Clamp(moveSpeed * (scroll > 0f ? scrollSpeedFactor : 1f / scrollSpeedFactor), minSpeed, maxSpeed);
            }

            Vector3 input = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.z += 1f;
                if (kb.sKey.isPressed) input.z -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                if (kb.eKey.isPressed) input.y += 1f;
                if (kb.qKey.isPressed) input.y -= 1f;
            }

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            float speed = moveSpeed * (kb != null && kb.leftShiftKey.isPressed ? fastMultiplier : 1f);
            // Planar moves follow the view direction; Q/E are always world up/down.
            Vector3 target = (rot * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y);
            target = Vector3.ClampMagnitude(target, 1f) * speed;
            _velocity = Vector3.Lerp(_velocity, target, 1f - Mathf.Exp(-moveSmooth * dt));

            _cam.transform.SetPositionAndRotation(_cam.transform.position + _velocity * dt, rot);
        }

        void BeginLook(Mouse mouse)
        {
            _looking = true;
            _lookCursorPos = mouse.position.ReadValue();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void EndLook()
        {
            if (!_looking) return;
            _looking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Mouse.current?.WarpCursorPosition(_lookCursorPos);
        }

        void OnDisable() => SetActive(false);

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) EndLook();
        }

        static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;
    }
}
