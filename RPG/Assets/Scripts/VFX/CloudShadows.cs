using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WorldRPG.VFX
{
    /// <summary>
    /// Drifts the main light's cookie (a tiling cloud / canopy dapple texture) across the world, so soft patches of
    /// shade slide over streets and roofs like passing clouds. Put on the directional light; the cookie texture and
    /// its world size are set on the light (URP: Light.cookie + UniversalAdditionalLightData.lightCookieSize).
    /// </summary>
    [AddComponentMenu("WorldRPG/VFX/Cloud Shadows")]
    [RequireComponent(typeof(Light))]
    public class CloudShadows : MonoBehaviour
    {
        [Tooltip("Drift in metres per second (world X/Z).")]
        public Vector2 windVelocity = new Vector2(0.6f, 0.35f);

        UniversalAdditionalLightData _data;

        void Awake() => _data = GetComponent<UniversalAdditionalLightData>();

        void Update()
        {
            if (_data == null) return;
            Vector2 size = _data.lightCookieSize;
            Vector2 offset = _data.lightCookieOffset + windVelocity * Time.deltaTime;
            // Wrap so the offset never grows large enough to lose float precision.
            if (size.x > 0f) offset.x = Mathf.Repeat(offset.x, size.x);
            if (size.y > 0f) offset.y = Mathf.Repeat(offset.y, size.y);
            _data.lightCookieOffset = offset;
        }
    }
}
