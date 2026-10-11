using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// The thief's pickpocketing, in three phases:
    ///  Free     - walk up behind a villager; once inside their rear band an indicator appears over them.
    ///  Lifting  - click and hold the left mouse button inside the ring while tailing them. Progress only fills while you stay in the rear band,
    ///             and breaks if you fall behind or release the key.
    ///  Stealing - the lift landed: time slows to bullet time, the thief shadows the mark automatically and the
    ///             pocket matrix opens. Grab three valuables before the real-time window runs out.
    /// While lifting or stealing, anyone who sees the thief raises suspicion; at 100 they're caught.
    /// Drawn by <see cref="PickpocketHUD"/>.
    /// </summary>
    [RequireComponent(typeof(TopDownCharacter), typeof(BulletTime))]
    public class Pickpocketer : MonoBehaviour
    {
        public enum Phase { Free, Lifting, Stealing }

        [Header("Input")]
        [Tooltip("Radius of the ring drawn around the mark's head, in pixels at 1080p.")]
        public float ringRadius = 26f;
        [Tooltip("A press within this radius of the ring's centre (pixels at 1080p) starts the lift.")]
        public float ringHitRadius = 46f;
        [Tooltip("Seconds of holding (inside the rear band) to land the lift.")]
        public float liftDuration = 1.4f;
        [Tooltip("Progress lost per second while outside the rear band, as a fraction of the full lift.")]
        public float liftDecay = 0.35f;

        [Header("Steal")]
        [Tooltip("Real-time seconds of bullet time to work the pockets.")]
        public float stealWindow = 7f;
        [Tooltip("How far behind the mark the thief walks while stealing (m).")]
        public float shadowDistance = 0.75f;
        [Tooltip("Real seconds the finished board stays up before time speeds back up.")]
        public float outroTime = 0.9f;

        [Header("Suspicion")]
        public float suspicionPerSecond = 70f;
        public float suspicionDecay = 25f;
        [Tooltip("Seconds a villager stops and stares after catching the thief.")]
        public float stareTime = 5f;
        [Tooltip("Seconds a villager who caught the thief stays on guard (can't be picked).")]
        public float wariness = 60f;

        public Phase CurrentPhase { get; private set; }
        public PickpocketTarget Candidate { get; private set; }
        /// <summary>Nearest NPC the thief is behind who can't be picked (vendor, on guard, robbed); null if a Candidate exists.</summary>
        public PickpocketTarget Blocked { get; private set; }
        public PickpocketTarget Mark { get; private set; }
        public float LiftProgress { get; private set; }
        public bool MarkInRearZone { get; private set; }
        public float Suspicion { get; private set; }
        public readonly List<PickpocketTarget> Watchers = new List<PickpocketTarget>();
        public PocketBoard Board { get; } = new PocketBoard();
        public float StealTimeLeft { get; private set; }
        public int Purse { get; private set; }
        public string Notice { get; private set; } = "";
        public float NoticeAge { get; private set; } = 99f;
        /// <summary>The mouse cursor is over the candidate's ring (a left press there starts the lift).</summary>
        public bool CursorOverRing { get; private set; }
        public bool IsActive => _character != null && _character.IsControlled;

        TopDownCharacter _character;
        BulletTime _bulletTime;
        float _outro;
        bool _mouseWasDown;

        void Awake()
        {
            _character = GetComponent<TopDownCharacter>();
            _bulletTime = GetComponent<BulletTime>();
        }

        void OnDisable()
        {
            if (CurrentPhase == Phase.Stealing) EndSteal(false);
            CurrentPhase = Phase.Free;
        }

        void Update()
        {
            NoticeAge += Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool mouseDown = mouse != null && mouse.leftButton.isPressed;
            // Own edge detection: wasPressedThisFrame can miss presses when input is routed oddly in the editor.
            bool mousePressed = mouseDown && !_mouseWasDown;
            _mouseWasDown = mouseDown;

            if (!IsActive && CurrentPhase != Phase.Stealing)
            {
                CurrentPhase = Phase.Free;
                Candidate = null;
                Blocked = null;
                LiftProgress = 0f;
                return;
            }

            switch (CurrentPhase)
            {
                case Phase.Free: UpdateFree(mousePressed); break;
                case Phase.Lifting: UpdateLifting(mouseDown); break;
                case Phase.Stealing: UpdateStealing(kb); break;
            }
        }

        // ------------------------------------------------------------------ free

        void UpdateFree(bool mousePressed)
        {
            Suspicion = Mathf.Max(0f, Suspicion - suspicionDecay * Time.deltaTime);
            Watchers.Clear();
            FindCandidate();
            CursorOverRing = Candidate != null && IsCursorOverRing(Candidate);
            LiftProgress = 0f;
            if (Candidate == null) return;

            // Left hand walks (WASD), right hand presses and holds inside the ring.
            if (mousePressed && CursorOverRing)
            {
                Mark = Candidate;
                CurrentPhase = Phase.Lifting;
            }
        }

        bool IsCursorOverRing(PickpocketTarget target)
        {
            var mouse = Mouse.current;
            Camera cam = _character.rig != null && _character.rig.cam != null ? _character.rig.cam : Camera.main;
            if (mouse == null || cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(target.HeadPosition);
            if (sp.z <= 0f) return false;
            float scale = Screen.height / 1080f;
            return Vector2.Distance(mouse.position.ReadValue(), sp) <= ringHitRadius * scale;
        }

        void FindCandidate()
        {
            Vector3 p = transform.position;
            PickpocketTarget best = null, blocked = null;
            float bestDist = float.MaxValue, blockedDist = float.MaxValue;
            foreach (var t in PickpocketTarget.All)
            {
                if (!t.IsInRearZone(p)) continue;
                float d = t.PlanarDistance(p);
                if (t.CanBePicked)
                {
                    if (d < bestDist) { bestDist = d; best = t; }
                }
                else if (d < blockedDist) { blockedDist = d; blocked = t; }
            }
            Candidate = best;
            Blocked = best == null ? blocked : null;
        }

        // ------------------------------------------------------------------ lifting

        void UpdateLifting(bool holding)
        {
            float dt = Time.deltaTime;
            Candidate = Mark;
            Blocked = null;
            if (Mark == null || !Mark.isActiveAndEnabled)
            {
                Abort("");
                return;
            }
            if (!holding)
            {
                Abort("");
                return;
            }
            if (Mark.PlanarDistance(transform.position) > Mark.followRange)
            {
                Abort("Lost them");
                return;
            }

            MarkInRearZone = Mark.IsInRearZone(transform.position);
            LiftProgress += MarkInRearZone ? dt / liftDuration : -liftDecay * dt;
            LiftProgress = Mathf.Clamp01(LiftProgress);

            if (UpdateSuspicion(dt)) return;
            if (LiftProgress >= 1f) BeginSteal();
        }

        void Abort(string reason)
        {
            CurrentPhase = Phase.Free;
            Mark = null;
            LiftProgress = 0f;
            if (!string.IsNullOrEmpty(reason)) Flash(reason);
        }

        /// <summary>Raises suspicion for everyone who can see the thief. Returns true if that got them caught.</summary>
        bool UpdateSuspicion(float dt)
        {
            Watchers.Clear();
            Vector3 p = transform.position;
            float rate = 0f;
            foreach (var t in PickpocketTarget.All)
            {
                if (t.PlanarDistance(p) > t.visionDistance || !t.CanSee(p)) continue;
                Watchers.Add(t);
                // Up close it's obvious; at the edge of their sight it takes a while to register.
                // Someone deep in conversation barely registers it.
                rate += suspicionPerSecond * t.Attention * Mathf.Lerp(1.6f, 0.5f, t.PlanarDistance(p) / t.visionDistance);
            }

            Suspicion = Watchers.Count > 0
                ? Suspicion + rate * dt
                : Mathf.Max(0f, Suspicion - suspicionDecay * dt);
            if (Suspicion < 100f) return false;

            Caught("Spotted!");
            return true;
        }

        // ------------------------------------------------------------------ stealing

        void BeginSteal()
        {
            CurrentPhase = Phase.Stealing;
            LiftProgress = 1f;
            StealTimeLeft = stealWindow;
            _outro = 0f;
            Board.Deal(Mark.Wealth);
            _bulletTime.Enter();
            if (_character.rig != null) _character.rig.blockMouseRotate = true;
        }

        void UpdateStealing(Keyboard kb)
        {
            if (Mark == null)
            {
                EndSteal(false);
                return;
            }

            ShadowMark();

            if (Board.Finished)
            {
                _outro += Time.unscaledDeltaTime;
                if (_outro >= outroTime) EndSteal(Board.State == PocketBoard.Result.Alarmed);
                return;
            }

            StealTimeLeft -= Time.unscaledDeltaTime;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                EndSteal(false);
                return;
            }
            if (StealTimeLeft <= 0f)
            {
                StealTimeLeft = 0f;
                EndSteal(false);
                return;
            }

            // The world is slowed, so are the onlookers' wits.
            if (UpdateSuspicion(Time.deltaTime)) return;
        }

        /// <summary>Keep pace right behind the mark, facing their back.</summary>
        void ShadowMark()
        {
            Vector3 back = -Mark.transform.forward;
            back.y = 0f;
            Vector3 slot = Mark.transform.position + back.normalized * shadowDistance;
            Vector3 offset = slot - transform.position;
            offset.y = 0f;
            Vector3 velocity = Mark.Velocity + offset * 4f;
            velocity.y = 0f;
            velocity = Vector3.ClampMagnitude(velocity, _character.runSpeed);
            Vector3 facing = Mark.transform.position - transform.position;
            facing.y = 0f;
            _character.SetAutopilot(velocity, facing);
        }

        void EndSteal(bool caught)
        {
            _bulletTime.Exit();
            _character.ClearAutopilot();
            if (_character.rig != null) _character.rig.blockMouseRotate = false;

            int take = Board.Value;
            Purse += take;
            var mark = Mark;
            if (mark != null && take > 0) mark.MarkRobbed();

            CurrentPhase = Phase.Free;
            Mark = null;
            LiftProgress = 0f;

            if (caught)
            {
                Caught(take > 0 ? $"They felt it! Got away with {take} wen" : "They felt it!", mark);
                return;
            }
            if (Board.State == PocketBoard.Result.Success) Flash($"Clean lift: +{take} wen");
            else if (take > 0) Flash($"Slipped away with {take} wen");
            else Flash(StealTimeLeft <= 0f ? "Too slow, nothing taken" : "Backed off");
        }

        // ------------------------------------------------------------------ getting caught

        void Caught(string message, PickpocketTarget extra = null)
        {
            Vector3 p = transform.position;
            foreach (var w in Watchers) w.Startle(p, stareTime, wariness);
            if (Mark != null && !Watchers.Contains(Mark)) Mark.Startle(p, stareTime, wariness);
            if (extra != null) extra.Startle(p, stareTime, wariness);

            if (CurrentPhase == Phase.Stealing)
            {
                // Spotted mid-steal: keep whatever's already in hand, drop the rest.
                _bulletTime.Exit();
                _character.ClearAutopilot();
                if (_character.rig != null) _character.rig.blockMouseRotate = false;
                Purse += Board.Value;
                if (Mark != null && Board.Value > 0) Mark.MarkRobbed();
            }

            CurrentPhase = Phase.Free;
            Mark = null;
            LiftProgress = 0f;
            Suspicion = 0f;
            Watchers.Clear();
            Flash(message);
        }

        void Flash(string text)
        {
            Notice = text;
            NoticeAge = 0f;
        }
    }
}
