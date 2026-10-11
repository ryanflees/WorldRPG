using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// The afternoon running down to sunset. Game time goes from <see cref="startHour"/> to <see cref="endHour"/> over
    /// <see cref="dayLengthSeconds"/>, and the sun follows: it sinks and swings west so shadows lengthen and turn,
    /// and the light warms and softens a little. It never gets dark: the deadline lands on a 5-6 PM feel.
    /// Runs on game time, so bullet time slows the clock too. Lives on the Player, so it starts when play does
    /// (after the opening trailer).
    /// </summary>
    public class DayClock : MonoBehaviour
    {
        public Light sun;

        [Header("Time")]
        public float startHour = 13.5f;
        public float endHour = 17.5f;
        [Tooltip("Real seconds from the start of play to the deadline.")]
        public float dayLengthSeconds = 600f;

        [Header("Sun (elevation, yaw in degrees)")]
        public Vector2 startAngles = new Vector2(42f, 50f);
        public Vector2 endAngles = new Vector2(24f, 82f);
        public Color startColor = new Color(1f, 0.98f, 0.95f);
        public Color endColor = new Color(1f, 0.88f, 0.76f);
        public float startIntensity = 1.55f;
        public float endIntensity = 1.3f;
        [Tooltip("Ambient light at the deadline, relative to the start.")]
        public float endAmbient = 0.9f;

        float _elapsed;
        Color _sky, _equator, _ground;
        bool _sunset;

        public float Progress => Mathf.Clamp01(_elapsed / dayLengthSeconds);
        public float Hour => Mathf.Lerp(startHour, endHour, Progress);
        public float SecondsLeft => Mathf.Max(0f, dayLengthSeconds - _elapsed);
        public bool IsOver => _elapsed >= dayLengthSeconds;
        public event System.Action Sunset;

        void Start()
        {
            if (sun == null) sun = RenderSettings.sun;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            Apply();
        }

        void Update()
        {
            if (_sunset) return;
            _elapsed += Time.deltaTime;
            Apply();
            if (!IsOver) return;
            _sunset = true;
            Sunset?.Invoke();
        }

        void Apply()
        {
            float t = Progress;
            // Ease in: the light changes faster towards the end of the afternoon, as it does.
            float light = t * t * 0.5f + t * 0.5f;
            if (sun != null)
            {
                Vector2 angles = Vector2.Lerp(startAngles, endAngles, t);
                sun.transform.rotation = Quaternion.Euler(angles.x, angles.y, 0f);
                sun.color = Color.Lerp(startColor, endColor, light);
                sun.intensity = Mathf.Lerp(startIntensity, endIntensity, light);
            }
            float ambient = Mathf.Lerp(1f, endAmbient, light);
            Color warm = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.92f), light);
            RenderSettings.ambientSkyColor = _sky * ambient * warm;
            RenderSettings.ambientEquatorColor = _equator * ambient * warm;
            RenderSettings.ambientGroundColor = _ground * ambient * warm;
        }

        void OnDestroy()
        {
            if (_sky == default) return;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
        }

        /// <summary>"3:45 PM" style clock text for an hour value.</summary>
        public static string FormatHour(float hour)
        {
            int minutes = Mathf.FloorToInt(hour * 60f);
            int h = minutes / 60 % 24, m = minutes % 60;
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{m:00} {(h < 12 ? "AM" : "PM")}";
        }
    }
}
