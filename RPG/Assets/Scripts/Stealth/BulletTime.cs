using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// Eases Time.timeScale down to <see cref="slowScale"/> and back, and fades in an ink-wash grade
    /// (desaturated, vignetted) while the world is slowed. Everything easing uses unscaled time.
    /// Physics steps scale with it so slowed motion stays smooth.
    /// </summary>
    public class BulletTime : MonoBehaviour
    {
        [Range(0.02f, 1f)] public float slowScale = 0.12f;
        [Tooltip("Real seconds to slow down / speed back up.")]
        public float easeIn = 0.15f;
        public float easeOut = 0.35f;

        [Header("Grade while slowed")]
        [Range(-100f, 0f)] public float saturation = -55f;
        [Range(0f, 1f)] public float vignette = 0.38f;

        bool _active;
        float _blend;
        float _baseFixedDelta;
        Volume _volume;
        VolumeProfile _profile;

        public bool Active => _active;
        /// <summary>0 = normal speed, 1 = fully slowed.</summary>
        public float Blend => _blend;

        void Awake()
        {
            _baseFixedDelta = Time.fixedDeltaTime;
            BuildVolume();
        }

        public void Enter() => _active = true;
        public void Exit() => _active = false;

        void Update()
        {
            float target = _active ? 1f : 0f;
            if (Mathf.Approximately(_blend, target) && !_active) return;
            float duration = _active ? easeIn : easeOut;
            _blend = Mathf.MoveTowards(_blend, target, duration > 0f ? Time.unscaledDeltaTime / duration : 1f);
            float eased = _blend * _blend * (3f - 2f * _blend);
            Time.timeScale = Mathf.Lerp(1f, slowScale, eased);
            Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;
            if (_volume != null) _volume.weight = eased;
        }

        void OnDisable()
        {
            _active = false;
            _blend = 0f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
            if (_volume != null) _volume.weight = 0f;
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        void BuildVolume()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "BulletTime (runtime)";
            var color = _profile.Add<ColorAdjustments>();
            color.saturation.Override(saturation);
            color.contrast.Override(8f);
            var vig = _profile.Add<Vignette>();
            vig.intensity.Override(vignette);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0.12f, 0.1f, 0.08f));

            var go = new GameObject("BulletTimeVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
        }
    }
}
