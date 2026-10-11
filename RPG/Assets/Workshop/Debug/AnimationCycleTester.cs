using System.Collections.Generic;
using UnityEngine;

namespace WorldRPG.Debugging
{
    /// <summary>
    /// Workshop test: cycles a character through every standing idle + chat take on BasicAC's
    /// IdleLayer with the same cross-fade the NPCs use, so switching problems (body jumping away from
    /// the root, facing the wrong way, feet sinking) are easy to spot. Measures, per clip, how far the
    /// hips drift from the root and how far the body faces away from the root's forward.
    /// </summary>
    public class AnimationCycleTester : MonoBehaviour
    {
        public enum Pool { Male, Female }

        public Animator animator;
        public Pool pool = Pool.Male;
        [Tooltip("Seconds each take plays before cross-fading to the next (0 = the whole clip).")]
        public float secondsPerClip = 5f;
        public float crossFade = 0.5f;
        public TextMesh label;

        public class ClipStats
        {
            public string name;
            public float maxOffset;      // m, hips vs root, planar
            public float avgOffset;
            public float avgYaw;         // degrees, body forward vs root forward
            public float minSole = float.MaxValue;   // m, sole height vs root
            public int samples;
        }
        public readonly List<ClipStats> Stats = new List<ClipStats>();

        BasicAC _ac;
        int[] _states;
        string[] _names;
        int _index = -1;
        float _clipTimer;
        ClipStats _current;
        static readonly Dictionary<int, string> StateNames = new Dictionary<int, string>();

        void Start()
        {
            _ac = new BasicAC(animator);
            if (StateNames.Count == 0)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    StateNames[Animator.StringToHash(clip.name)] = clip.name;

            int[] idles = pool == Pool.Male ? _ac.MaleIdleStates : _ac.FemaleIdleStates;
            int[] chats = pool == Pool.Male ? _ac.MaleChatStates : _ac.FemaleChatStates;
            _states = new int[idles.Length + chats.Length];
            idles.CopyTo(_states, 0);
            chats.CopyTo(_states, idles.Length);
            _names = new string[_states.Length];
            for (int i = 0; i < _states.Length; i++)
                _names[i] = StateNames.TryGetValue(_states[i], out var n) ? n : _states[i].ToString();

            animator.SetLayerWeight(_ac.IdleLayer, 1f);
            Next(0f);
        }

        void Next(float fade)
        {
            _index = (_index + 1) % _states.Length;
            if (fade > 0f) _ac.CrossFadeIdleLayer(_states[_index], fade);
            else _ac.PlayIdleLayer(_states[_index], 0f);
            _clipTimer = 0f;

            _current = Stats.Find(s => s.name == _names[_index]);
            if (_current == null)
            {
                _current = new ClipStats { name = _names[_index] };
                Stats.Add(_current);
            }
        }

        void Update()
        {
            _clipTimer += Time.deltaTime;
            float duration = secondsPerClip;
            if (duration <= 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(_ac.IdleLayer);
                duration = info.length - crossFade;
            }
            if (_clipTimer >= duration) Next(crossFade);
        }

        void LateUpdate()
        {
            Transform root = animator.transform;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 local = root.InverseTransformPoint(hips.position);
            float offset = new Vector2(local.x, local.z).magnitude;

            // Body forward from the hips' facing (perpendicular to the left-right hip axis).
            Vector3 left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            Vector3 right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
            Vector3 across = right - left; across.y = 0f;
            Vector3 bodyForward = Vector3.Cross(across, Vector3.up).normalized;
            float yaw = Vector3.SignedAngle(root.forward, bodyForward, Vector3.up);

            float sole = Mathf.Min(
                animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - animator.leftFeetBottomHeight,
                animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y - animator.rightFeetBottomHeight) - root.position.y;

            bool settled = !animator.IsInTransition(_ac.IdleLayer);
            if (settled && _current != null)
            {
                _current.maxOffset = Mathf.Max(_current.maxOffset, offset);
                _current.avgOffset += (offset - _current.avgOffset) / (_current.samples + 1);
                _current.avgYaw += (yaw - _current.avgYaw) / (_current.samples + 1);
                _current.minSole = Mathf.Min(_current.minSole, sole);
                _current.samples++;
            }

            if (label != null)
                label.text = $"{_names[_index]}\nhips off {offset * 100f:F0} cm   facing {yaw:F0}°   sole {sole * 100f:F1} cm";
        }

        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in Stats)
                sb.AppendLine($"{s.name}: hips offset avg {s.avgOffset * 100f:F0} / max {s.maxOffset * 100f:F0} cm, facing {s.avgYaw:F0}°, min sole {s.minSole * 100f:F1} cm ({s.samples})");
            return sb.ToString();
        }
    }
}
