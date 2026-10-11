using UnityEngine;

namespace WorldRPG.VFX
{
    /// <summary>
    /// Runtime controls for the reusable chimney/food-stall steam prefab.
    /// The prefab contains two local particle systems: a soft main plume and small wisps.
    /// </summary>
    [AddComponentMenu("WorldRPG/VFX/Smoke VFX")]
    public sealed class SmokeVfx : MonoBehaviour
    {
        [Header("Particle systems")]
        [Tooltip("Main smoke plume. If left empty, the first child ParticleSystem is used.")]
        public ParticleSystem primarySystem;

        [Tooltip("Small secondary wisps. Optional.")]
        public ParticleSystem secondarySystem;

        [Header("Instance tuning")]
        [Range(0f, 3f)]
        [Tooltip("Multiplies the authored emission rate. Use 0 to temporarily hide the plume.")]
        public float emissionMultiplier = 1f;

        [Tooltip("Local drift applied to both systems. Y is the upward lift.")]
        public Vector3 drift = new Vector3(0.04f, 0.28f, 0.02f);

        public bool playOnEnable = true;

        void Reset()
        {
            if (primarySystem == null)
                primarySystem = GetComponentInChildren<ParticleSystem>(true);
        }

        void Awake() => ApplySettings();

        void OnEnable()
        {
            ApplySettings();
            if (playOnEnable) Play();
        }

        void OnValidate()
        {
            if (!Application.isPlaying) ApplySettings();
        }

        public void Play()
        {
            if (primarySystem != null) primarySystem.Play(true);
            if (secondarySystem != null) secondarySystem.Play(true);
        }

        public void Stop()
        {
            if (primarySystem != null) primarySystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (secondarySystem != null) secondarySystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Clear()
        {
            if (primarySystem != null) primarySystem.Clear(true);
            if (secondarySystem != null) secondarySystem.Clear(true);
        }

        void ApplySettings()
        {
            ApplyTo(primarySystem);
            ApplyTo(secondarySystem);
        }

        void ApplyTo(ParticleSystem system)
        {
            if (system == null) return;

            var emission = system.emission;
            emission.rateOverTimeMultiplier = Mathf.Max(0f, emissionMultiplier);

            var velocity = system.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(drift.x);
            velocity.y = new ParticleSystem.MinMaxCurve(drift.y);
            velocity.z = new ParticleSystem.MinMaxCurve(drift.z);
        }
    }
}
