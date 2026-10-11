using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Maps a planar speed (m/s) onto the BasicAC "SpeedNormalized" blend parameter.
    /// The blend tree thresholds (Idle 0, Walk 0.333, Run 1, Sprint 1.3) are mapped piecewise
    /// to the clips' real root speeds, so any movement speed plays the matching gait without
    /// foot sliding. Shared by the player and NPC animator drivers.
    /// </summary>
    [System.Serializable]
    public class LocomotionBlend
    {
        public static readonly int SpeedNormalizedId = Animator.StringToHash("SpeedNormalized");

        [Header("Clip root speeds (m/s, MovementAnimsetPro)")]
        public float walkClipSpeed = 1.57f;
        public float runClipSpeed = 3.41f;
        public float sprintClipSpeed = 5.82f;

        [Header("Blend tree thresholds (BasicAC)")]
        public float walkThreshold = 0.333f;
        public float runThreshold = 1f;
        public float sprintThreshold = 1.3f;

        /// <summary>
        /// Clip speeds are authored for a human scale of 1 and playback speed 1; this scales them to the
        /// avatar and to the animator's playback speed (NPCs play at slightly different speeds).
        /// </summary>
        public static float ClipSpeedScale(Animator animator)
        {
            return (animator.isHuman ? animator.humanScale : 1f) * Mathf.Max(animator.speed, 0.01f);
        }

        /// <summary>Movement speed (m/s) at which the walk cycle plays without foot sliding.</summary>
        public float WalkSpeed(Animator animator) => walkClipSpeed * ClipSpeedScale(animator);

        public float SpeedToBlend(float speed, Animator animator)
        {
            float scale = ClipSpeedScale(animator);
            float walk = walkClipSpeed * scale, run = runClipSpeed * scale, sprint = sprintClipSpeed * scale;

            if (speed <= walk) return Mathf.Lerp(0f, walkThreshold, speed / walk);
            if (speed <= run) return Mathf.Lerp(walkThreshold, runThreshold, (speed - walk) / (run - walk));
            return Mathf.LerpUnclamped(runThreshold, sprintThreshold, Mathf.Min((speed - run) / (sprint - run), 1f));
        }
    }
}
