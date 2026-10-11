using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG
{
    /// <summary>
    /// Trailer / opening: tick <see cref="trailerMode"/> and play opens with a cinematic flythrough of the level.
    /// The thief and gameplay HUD are hidden and villagers live their lives as usual, while the RTS camera rig glides
    /// through shots, cut through a paper-white fade, with a title card over the first one.
    /// Random (default): endless cut-scene-like shots. Each places the camera at a street spot A (often near
    ///   villagers) and glides in a straight line in a random direction to B for a few seconds, then cuts to C -> D.
    ///   The line must run along walkable street (navmesh linecast), and shots whose view a roof would block are rejected.
    ///   Framing is random too: following, tracking alongside, or facing the way it's moving.
    /// Scripted: plays the authored <see cref="shots"/> list in order.
    /// With <see cref="holdToStart"/> it is the game's opening: holding the start key fades out, brings the hidden
    /// objects (the thief, the HUD) back, hands the camera back to the rig in Follow mode, and the game begins.
    /// </summary>
    public class TownTrailer : MonoBehaviour
    {
        public enum Mode { Random, Scripted }

        [System.Serializable]
        public class Shot
        {
            public string name = "Shot";
            public float duration = 10f;
            public Vector3 fromPivot, toPivot;
            public float fromYaw, toYaw;
            public float fromDistance = 40f, toDistance = 40f;
            public float fromPitch = 45f, toPitch = 45f;
        }

        [Tooltip("Play opens with the trailer flythrough instead of going straight into the game.")]
        public bool trailerMode;
        public Mode mode = Mode.Random;
        public RTSCameraRig rig;

        [Header("Opening")]
        [Tooltip("Holding the start key ends the trailer and starts the game. Off = an endless attract loop.")]
        public bool holdToStart = true;
        public Key startKey = Key.Space;
        public float holdSeconds = 2f;

        [Header("Hidden while the trailer runs")]
        public GameObject[] hideObjects;
        public Behaviour[] disableComponents;

        [Header("Look")]
        public string title = "Linshui";
        public string subtitle = "A river town of the Song";
        public string endTitle = "Linshui";
        public float fadeTime = 0.7f;
        [Tooltip("0 = constant speed through a shot, 1 = full ease in and out.")]
        [Range(0f, 1f)] public float easing = 0.35f;

        [Header("Random shots")]
        [Tooltip("Seconds per shot (min / max).")]
        public Vector2 shotDuration = new Vector2(10f, 15f);
        [Tooltip("How fast the camera's ground point glides (m/s).")]
        public float glideSpeed = 1.8f;
        public Vector2 distanceRange = new Vector2(23f, 32f);
        public Vector2 pitchRange = new Vector2(40f, 54f);
        [Tooltip("Shots stay inside this area (x/z, world space): the town inside its walls.")]
        public Rect area = new Rect(-50f, -40f, 100f, 75f);
        [Tooltip("Chance a shot starts next to a villager rather than at a random street spot.")]
        [Range(0f, 1f)] public float villagerBias = 0.65f;

        [Header("Scripted shots")]
        public bool loop = true;
        public List<Shot> shots = new List<Shot>();

        static readonly Color Paper = new Color(0.95f, 0.92f, 0.85f, 1f);
        static readonly Color Ink = new Color(0.14f, 0.11f, 0.09f, 1f);
        static readonly Color Vermilion = new Color(0.74f, 0.19f, 0.13f, 1f);
        const float ExitFade = 0.6f, EnterFade = 1f;

        enum Phase { Trailer, FadingOut, FadingIn }

        // The shot being played: a straight glide of the ground point while yaw / distance / pitch ease.
        Shot _take = new Shot();
        readonly List<NPC> _villagers = new List<NPC>();
        int _shotIndex;     // shots played so far (scripted: index into the list)
        float _time;
        bool _running;
        Phase _phase;
        float _hold, _phaseTime;
        int _losMask;
        GUIStyle _titleStyle, _subStyle;

        public bool Running => _running;

        void Start()
        {
            bool ready = rig != null && rig.cam != null && (mode == Mode.Random || shots.Count > 0);
            if (!trailerMode || !ready)
            {
                enabled = false;
                return;
            }
            foreach (var go in hideObjects) if (go != null) go.SetActive(false);
            foreach (var b in disableComponents) if (b != null) b.enabled = false;
            rig.enabled = false;   // the trailer owns the rig's transforms until the game starts

            _villagers.AddRange(FindObjectsByType<NPC>(FindObjectsSortMode.None));
            int character = LayerMask.NameToLayer("Character");
            _losMask = Physics.DefaultRaycastLayers & ~(character >= 0 ? 1 << character : 0);

            _running = true;
            _phase = Phase.Trailer;
            _shotIndex = 0;
            _time = 0f;
            BeginShot();
        }

        void LateUpdate()
        {
            if (!_running) return;
            float dt = Time.unscaledDeltaTime;
            if (UpdateStart(dt)) return;

            _time += dt;
            if (_time >= _take.duration)
            {
                _time -= _take.duration;
                _shotIndex++;
                if (mode == Mode.Scripted && _shotIndex >= shots.Count)
                {
                    if (!loop)
                    {
                        _running = false;
                        return;
                    }
                    _shotIndex = 0;
                }
                BeginShot();
            }
            Pose(_take, Mathf.Clamp01(_time / _take.duration));
        }

        // ------------------------------------------------------------------ opening hand-over

        /// <summary>Hold-to-start and the hand-over to the game. Returns true once the game has taken the camera.</summary>
        bool UpdateStart(float dt)
        {
            switch (_phase)
            {
                case Phase.Trailer:
                    if (!holdToStart) return false;
                    var kb = Keyboard.current;
                    bool held = kb != null && kb[startKey].isPressed;
                    _hold = held ? _hold + dt : Mathf.Max(0f, _hold - dt * 2f);
                    if (_hold >= holdSeconds)
                    {
                        _phase = Phase.FadingOut;
                        _phaseTime = 0f;
                    }
                    return false;
                case Phase.FadingOut:
                    _phaseTime += dt;
                    if (_phaseTime >= ExitFade) StartGame();
                    return false;   // keep gliding under the fade
                default:
                    _phaseTime += dt;
                    if (_phaseTime >= EnterFade)
                    {
                        _running = false;
                        enabled = false;
                    }
                    return true;
            }
        }

        void StartGame()
        {
            foreach (var go in hideObjects) if (go != null) go.SetActive(true);
            foreach (var b in disableComponents) if (b != null) b.enabled = true;
            rig.enabled = true;
            rig.SetMode(RTSCameraRig.Mode.Follow);
            _phase = Phase.FadingIn;
            _phaseTime = 0f;
        }

        // ------------------------------------------------------------------ shots

        void BeginShot()
        {
            if (mode == Mode.Scripted) _take = shots[_shotIndex];
            // Long clear street lines are rarer: if none turns up, settle for a shorter glide before the fallback.
            else if (!TryRandomShot(1f) && !TryRandomShot(0.6f)) LoadFallback();
            Pose(_take, 0f);
        }

        /// <summary>Puts the rig at the given point of a shot (t in 0..1). Also handy for previewing framing.</summary>
        public void Pose(Shot shot, float t)
        {
            float eased = Mathf.Lerp(t, t * t * (3f - 2f * t), easing);
            Vector3 pivot = Vector3.Lerp(shot.fromPivot, shot.toPivot, eased);
            float yaw = Mathf.LerpAngle(shot.fromYaw, shot.toYaw, eased);
            float distance = Mathf.Lerp(shot.fromDistance, shot.toDistance, eased);
            float pitch = Mathf.Lerp(shot.fromPitch, shot.toPitch, eased);

            rig.transform.SetPositionAndRotation(pivot, Quaternion.Euler(0f, yaw, 0f));
            Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
            rig.cam.transform.localRotation = tilt;
            rig.cam.transform.localPosition = tilt * new Vector3(0f, 0f, -distance);
        }

        static Vector3 CameraPosition(Shot shot, float t)
        {
            Vector3 pivot = Vector3.Lerp(shot.fromPivot, shot.toPivot, t);
            float yaw = Mathf.LerpAngle(shot.fromYaw, shot.toYaw, t);
            float pitch = Mathf.Lerp(shot.fromPitch, shot.toPitch, t);
            float distance = Mathf.Lerp(shot.fromDistance, shot.toDistance, t);
            return pivot + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
        }

        bool TryRandomShot(float durationScale)
        {
            if (AstarPath.active == null) return false;
            var streets = AstarPath.active.data.recastGraph;
            var shot = new Shot { name = "Random" };
            for (int attempt = 0; attempt < 48; attempt++)
            {
                if (!TrySnap(PickStart(), out Vector3 a)) continue;

                // A -> B: one straight line in a random direction, all of it on walkable street.
                shot.duration = Random.Range(shotDuration.x, shotDuration.y) * durationScale;
                float angle = Random.Range(0f, 360f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (!TrySnap(a + dir * glideSpeed * shot.duration, out Vector3 b)) continue;
                if (streets != null && streets.Linecast(a, b)) continue;   // would cut through a building

                Vector3 heading = Flat(b - a);
                if (heading.sqrMagnitude < 1f) continue;
                float headingYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                float roll = Random.value;
                float offset = roll < 0.4f ? Random.Range(-25f, 25f)                                   // following
                    : roll < 0.8f ? (Random.value < 0.5f ? 90f : -90f) + Random.Range(-20f, 20f)      // tracking alongside
                    : 180f + Random.Range(-25f, 25f);                                                  // facing it, backing up

                shot.fromPivot = a;
                shot.toPivot = b;
                shot.fromYaw = headingYaw + offset;
                shot.toYaw = shot.fromYaw + Random.Range(-8f, 8f);
                shot.fromDistance = Random.Range(distanceRange.x, distanceRange.y);
                shot.toDistance = Mathf.Clamp(shot.fromDistance + Random.Range(-3f, 3f), distanceRange.x, distanceRange.y);
                shot.fromPitch = Random.Range(pitchRange.x, pitchRange.y);
                shot.toPitch = Mathf.Clamp(shot.fromPitch + Random.Range(-4f, 4f), pitchRange.x, pitchRange.y);

                if (Blocked(shot)) continue;
                _take = shot;
                return true;
            }
            return false;
        }

        Vector3 PickStart()
        {
            if (_villagers.Count > 0 && Random.value < villagerBias)
            {
                var npc = _villagers[Random.Range(0, _villagers.Count)];
                if (npc != null)
                {
                    Vector2 jitter = Random.insideUnitCircle * 3f;
                    return npc.transform.position + new Vector3(jitter.x, 0f, jitter.y);
                }
            }
            return new Vector3(Random.Range(area.xMin, area.xMax), 0f, Random.Range(area.yMin, area.yMax));
        }

        bool TrySnap(Vector3 point, out Vector3 snapped)
        {
            snapped = point;
            if (!area.Contains(new Vector2(point.x, point.z))) return false;
            var nn = AstarPath.active.GetNearest(point, NNConstraint.Walkable);
            if (nn.node == null || (nn.position - point).sqrMagnitude > 9f) return false;   // was inside a building
            snapped = nn.position;
            return true;
        }

        /// <summary>Would a roof or wall hide the street at the start, middle or end of the shot?</summary>
        bool Blocked(Shot shot)
        {
            for (int i = 0; i <= 2; i++)
            {
                float t = i * 0.5f;
                Vector3 pivot = Vector3.Lerp(shot.fromPivot, shot.toPivot, t);
                if (Physics.Linecast(CameraPosition(shot, t), pivot + Vector3.up * 1.2f, _losMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        /// <summary>No good street shot found: a slow high drift over the middle of the area.</summary>
        void LoadFallback()
        {
            Vector3 c = new Vector3(area.center.x, 0f, area.center.y);
            float yaw = Random.Range(0f, 360f);
            _take = new Shot
            {
                name = "Fallback", duration = shotDuration.y, fromPivot = c, toPivot = c + new Vector3(4f, 0f, 4f),
                fromYaw = yaw, toYaw = yaw + 15f, fromDistance = 60f, toDistance = 60f, fromPitch = 55f, toPitch = 55f,
            };
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ------------------------------------------------------------------ overlay

        void OnGUI()
        {
            if (!_running) return;
            EnsureStyles();
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);

            if (_phase == Phase.FadingIn)
            {
                // The game has started: lift the paper off it.
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, Mathf.Clamp01(1f - _phaseTime / EnterFade));
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            // Fade through paper at every cut (and in from paper at the very start), and out when the game starts.
            float fade = Mathf.Max(1f - _time / fadeTime, 1f - (_take.duration - _time) / fadeTime);
            if (_phase == Phase.FadingOut) fade = Mathf.Max(fade, _phaseTime / ExitFade);
            if (fade > 0f)
            {
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, Mathf.Clamp01(fade));
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }

            bool first = _shotIndex == 0;
            bool last = mode == Mode.Scripted && _shotIndex == shots.Count - 1;
            if (first || last)
            {
                // Title holds in the middle of the shot, clear of the fades.
                float a = Mathf.Clamp01(Mathf.Min(_time - 1f, _take.duration - 1.5f - _time));
                if (a > 0f) DrawTitle(first ? title : endTitle, first ? subtitle : "", a);
            }
            if (holdToStart && _phase == Phase.Trailer) DrawHoldPrompt();
            GUI.color = Color.white;
        }

        void DrawTitle(string text, string sub, float alpha)
        {
            float s = Screen.height / 1080f;
            float bandH = (string.IsNullOrEmpty(sub) ? 150f : 200f) * s;
            var band = new Rect(0f, Screen.height * 0.5f - bandH * 0.5f, Screen.width, bandH);
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, 0.55f * alpha);
            GUI.DrawTexture(band, Texture2D.whiteTexture);

            _titleStyle.fontSize = Mathf.RoundToInt(84 * s);
            _subStyle.fontSize = Mathf.RoundToInt(26 * s);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            float titleH = 110f * s;
            GUI.Label(new Rect(0f, band.y + (string.IsNullOrEmpty(sub) ? (bandH - titleH) * 0.5f : 24f * s), Screen.width, titleH), text, _titleStyle);
            if (!string.IsNullOrEmpty(sub))
                GUI.Label(new Rect(0f, band.y + 24f * s + titleH, Screen.width, 40f * s), sub, _subStyle);
        }

        /// <summary>"Hold Space to begin" plate with a fill bar along its foot.</summary>
        void DrawHoldPrompt()
        {
            float s = Screen.height / 1080f;
            float w = 300f * s, h = 54f * s;
            var plate = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - h - 48f * s, w, h);
            float pulse = _hold > 0f ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f);
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, 0.85f);
            GUI.DrawTexture(plate, Texture2D.whiteTexture);

            var bar = new Rect(plate.x + 16f * s, plate.yMax - 12f * s, plate.width - 32f * s, 4f * s);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.15f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Vermilion;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_hold / holdSeconds), bar.height), Texture2D.whiteTexture);

            _subStyle.fontSize = Mathf.RoundToInt(20 * s);
            _subStyle.fontStyle = FontStyle.Normal;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, pulse);
            GUI.Label(new Rect(plate.x, plate.y, plate.width, plate.height - 10f * s), "Hold " + startKey + " to begin", _subStyle);
            _subStyle.fontStyle = FontStyle.Italic;
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = Color.white;
            _subStyle = new GUIStyle(_titleStyle) { fontStyle = FontStyle.Italic };
        }
    }
}
