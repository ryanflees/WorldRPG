using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG
{
    /// <summary>
    /// RTS-style top-down camera. The rig sits on the ground (pivot) and only yaws;
    /// the child camera looks down at a fixed pitch from a zoomable distance.
    /// Free mode: middle-mouse drag / WASD to pan. Follow mode: tracks followTarget.
    /// Both modes: Q/E or right-mouse drag to rotate, scroll to zoom, Tab to toggle mode.
    /// </summary>
    public class RTSCameraRig : MonoBehaviour
    {
        public enum Mode { Free, Follow }

        [Header("References")]
        public Camera cam;
        public Transform followTarget;
        [Tooltip("Mode on start. Follow falls back to Free when there is no followTarget.")]
        public Mode startMode = Mode.Follow;

        [Header("View")]
        [Range(10f, 89f)] public float pitch = 50f;
        public float distance = 45f;
        public float minDistance = 8f;
        public float maxDistance = 120f;
        [Tooltip("Multiplier applied to distance per scroll notch.")]
        public float zoomFactor = 0.88f;
        public float zoomSmooth = 10f;

        [Header("Rotation")]
        public float keyRotateSpeed = 90f;      // degrees per second
        public float mouseRotateSpeed = 0.3f;   // degrees per pixel
        [Tooltip("Mouse delta is averaged over this many frames (1 = raw input).")]
        [Range(1, 10)] public int mouseSmoothFrames = 4;
        public float keyRotateAccel = 600f;     // degrees per second^2 (reach full speed in ~0.15s)
        public float keyRotateDecel = 900f;     // stop a little quicker than starting

        [Header("Pan")]
        public float keyPanSpeed = 30f;         // units per second at distance 45
        public Vector2 boundsMin = new Vector2(-80f, -80f);
        public Vector2 boundsMax = new Vector2(80f, 80f);
        public float groundHeight = 0f;

        [Header("Follow")]
        public float followSmooth = 8f;

        public Mode CurrentMode { get; private set; } = Mode.Free;
        public event Action<Mode> ModeChanged;

        Vector3 _pivot;
        float _yaw, _keyYawVelocity;
        float _distance, _targetDistance;
        bool _dragging;
        Vector3 _dragAnchor;
        bool _mouseRotating;
        float[] _mouseDeltaHistory;
        int _mouseDeltaIndex;
        Vector2 _rotateCursorPos;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            _pivot = transform.position;
            _pivot.y = groundHeight;
            _yaw = transform.eulerAngles.y;
            _distance = _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            SetMode(startMode);
            if (CurrentMode == Mode.Follow)
            {
                _pivot = followTarget.position;
                _pivot.y = groundHeight;
            }
            Apply();
        }

        public void SetMode(Mode mode)
        {
            if (mode == Mode.Follow && followTarget == null) return;
            if (mode == CurrentMode) return;
            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.tabKey.wasPressedThisFrame)
                SetMode(CurrentMode == Mode.Free ? Mode.Follow : Mode.Free);

            // Rotation. Mouse is applied 1:1 (no smoothing = no felt lag);
            // keys ramp the angular velocity up/down for a soft start and stop.
            float keyInput = 0f;
            if (kb != null)
            {
                if (kb.qKey.isPressed) keyInput -= 1f;
                if (kb.eKey.isPressed) keyInput += 1f;
            }
            float accel = keyInput != 0f ? keyRotateAccel : keyRotateDecel;
            _keyYawVelocity = Mathf.MoveTowards(_keyYawVelocity, keyInput * keyRotateSpeed, accel * dt);
            _yaw += _keyYawVelocity * dt;

            float mouseDeltaX = 0f;
            if (mouse != null)
            {
                bool rotating = mouse.rightButton.isPressed;
                if (rotating && !_mouseRotating) BeginMouseRotate(mouse);
                else if (!rotating && _mouseRotating) EndMouseRotate();
                // Skip the press frame: locking the cursor can produce a one-off delta spike.
                else if (rotating) mouseDeltaX = mouse.delta.ReadValue().x;
            }
            // Always sample (zeros when idle) so the buffer drains after release and no input is lost.
            _yaw += SmoothMouseDelta(mouseDeltaX) * mouseRotateSpeed;

            // Zoom
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _targetDistance *= scroll > 0f ? zoomFactor : 1f / zoomFactor;
                    _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
                }
            }

            // Middle-mouse grab-pan (works in both modes; grabbing breaks follow)
            if (mouse != null)
            {
                if (!mouse.middleButton.isPressed)
                {
                    _dragging = false;
                }
                else if (!_dragging && RaycastGround(mouse.position.ReadValue(), out _dragAnchor))
                {
                    _dragging = true;
                    SetMode(Mode.Free);
                }
            }

            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-zoomSmooth * dt));

            if (CurrentMode == Mode.Follow && followTarget != null)
            {
                Vector3 target = followTarget.position;
                target.y = groundHeight;
                _pivot = Vector3.Lerp(_pivot, target, 1f - Mathf.Exp(-followSmooth * dt));
            }
            else if (kb != null && !_dragging)
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 0f)
                {
                    Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
                    Vector3 dir = yawRot * new Vector3(move.x, 0f, move.y).normalized;
                    float speed = keyPanSpeed * (_distance / 45f) * (kb.leftShiftKey.isPressed ? 2f : 1f);
                    _pivot += dir * speed * dt;
                }
            }

            ClampPivot();
            Apply();

            // Keep the grabbed ground point under the cursor.
            if (_dragging && RaycastGround(mouse.position.ReadValue(), out Vector3 hit))
            {
                _pivot += _dragAnchor - hit;
                ClampPivot();
                Apply();
            }
        }

        // Moving average of the last N mouse deltas. Each sample contributes 1/N over N frames,
        // so the total rotation is preserved while per-frame jitter is smoothed out.
        float SmoothMouseDelta(float delta)
        {
            int frames = Mathf.Max(1, mouseSmoothFrames);
            if (_mouseDeltaHistory == null || _mouseDeltaHistory.Length != frames)
            {
                _mouseDeltaHistory = new float[frames];
                _mouseDeltaIndex = 0;
            }

            _mouseDeltaHistory[_mouseDeltaIndex] = delta;
            _mouseDeltaIndex = (_mouseDeltaIndex + 1) % frames;

            float sum = 0f;
            for (int i = 0; i < frames; i++) sum += _mouseDeltaHistory[i];
            return sum / frames;
        }

        // Hide and lock the cursor while rotating, then put it back where it was.
        void BeginMouseRotate(Mouse mouse)
        {
            _mouseRotating = true;
            _rotateCursorPos = mouse.position.ReadValue();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void EndMouseRotate()
        {
            if (!_mouseRotating) return;
            _mouseRotating = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Mouse.current?.WarpCursorPosition(_rotateCursorPos);
        }

        void OnDisable() => EndMouseRotate();

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) EndMouseRotate();
        }

        void ClampPivot()
        {
            _pivot.x = Mathf.Clamp(_pivot.x, boundsMin.x, boundsMax.x);
            _pivot.z = Mathf.Clamp(_pivot.z, boundsMin.y, boundsMax.y);
            _pivot.y = groundHeight;
        }

        void Apply()
        {
            transform.SetPositionAndRotation(_pivot, Quaternion.Euler(0f, _yaw, 0f));
            if (cam == null) return;
            Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.localRotation = tilt;
            cam.transform.localPosition = tilt * new Vector3(0f, 0f, -_distance);
        }

        bool RaycastGround(Vector2 screenPos, out Vector3 point)
        {
            point = default;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            if (!plane.Raycast(ray, out float enter) || enter > cam.farClipPlane) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}
