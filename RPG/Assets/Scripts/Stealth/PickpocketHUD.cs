using UnityEngine;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// IMGUI for <see cref="Pickpocketer"/>: the hold prompt and lift ring over the mark, "seen" marks over
    /// onlookers, the suspicion meter, the purse, and the pocket board during bullet time
    /// (left click takes a pouch, right click peeks through it). Laid out for 1080p and scaled to the screen.
    /// </summary>
    public class PickpocketHUD : MonoBehaviour
    {
        public Pickpocketer thief;
        public Camera cam;
        [Tooltip("Off when something else shows the purse (the guild quest's timeline).")]
        public bool showPurse = true;

        static readonly Color Paper = new Color(0.95f, 0.93f, 0.88f, 0.96f);
        static readonly Color PaperDark = new Color(0.84f, 0.81f, 0.74f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.13f, 0.11f, 1f);
        static readonly Color InkSoft = new Color(0.16f, 0.13f, 0.11f, 0.55f);
        static readonly Color Vermilion = new Color(0.74f, 0.19f, 0.13f, 1f);
        static readonly Color Jade = new Color(0.2f, 0.55f, 0.48f, 1f);
        static readonly Color Gold = new Color(0.82f, 0.63f, 0.24f, 1f);
        static readonly Color Cloth = new Color(0.5f, 0.38f, 0.27f, 1f);
        static readonly Color Junk = new Color(0.62f, 0.6f, 0.55f, 1f);

        static readonly string[] Coords = { "A1", "A2", "A3", "B1", "B2", "B3", "C1", "C2", "C3" };

        const int RingDots = 24;

        Texture2D _pixel, _dot;
        GUIStyle _label, _center, _title, _small, _key, _pouch;
        float _scale;

        void Awake()
        {
            if (thief == null) thief = FindFirstObjectByType<Pickpocketer>();
            _pixel = Texture2D.whiteTexture;
            _dot = MakeDot(48);
        }

        void OnDestroy()
        {
            if (_dot != null) Destroy(_dot);
        }

        void OnGUI()
        {
            if (thief == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();

            _scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale, h = 1080f;

            DrawPurse(w);
            if (thief.IsActive || thief.CurrentPhase == Pickpocketer.Phase.Stealing)
            {
                DrawWatchers();
                DrawSuspicion(w);
                if (thief.CurrentPhase == Pickpocketer.Phase.Stealing) DrawBoard(w, h);
                else DrawPrompt();
            }
            DrawNotice(w, h);
            GUI.matrix = Matrix4x4.identity;
        }

        // ------------------------------------------------------------------ world-space prompts

        bool ToGui(Vector3 world, out Vector2 gui)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            gui = new Vector2(sp.x, Screen.height - sp.y) / _scale;
            return sp.z > 0f;
        }

        void DrawPrompt()
        {
            if (thief.Candidate == null)
            {
                DrawBlocked();
                return;
            }
            var target = thief.Candidate;
            if (target == null || !ToGui(target.HeadPosition, out Vector2 c)) return;

            bool lifting = thief.CurrentPhase == Pickpocketer.Phase.Lifting;
            float pulse = lifting ? 1f : 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            float radius = thief.ringRadius;
            bool hot = lifting || thief.CursorOverRing;

            // Soft paper disc behind the ring so it reads on dark roofs and bright paving alike.
            DrawDot(c, (radius + 9f) * 2f, new Color(Paper.r, Paper.g, Paper.b, hot ? 0.55f : 0.4f));

            // Ring of dots: grey when waiting, fills with jade as the lift lands.
            Color fill = lifting && !thief.MarkInRearZone ? Vermilion : Jade;
            int lit = Mathf.FloorToInt(thief.LiftProgress * RingDots + 0.001f);
            for (int i = 0; i < RingDots; i++)
            {
                float a = (i / (float)RingDots) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                float size = i < lit ? 8f : 6f;
                Color idle = hot ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.5f * pulse);
                DrawDot(p, size, i < lit ? fill : idle);
            }

            // Hand badge: the press target.
            DrawDot(c, (hot ? 38f : 34f) * pulse, new Color(Paper.r, Paper.g, Paper.b, 0.95f));
            GUI.color = hot ? Vermilion : Ink;
            GUI.Label(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), "手", _key);

            string text;
            Color color = Ink;
            if (!lifting) text = "Click & hold  Pick pocket";
            else if (thief.MarkInRearZone) text = "Stay behind " + target.DisplayName;
            else { text = "Get behind them!"; color = Vermilion; }
            Plate(new Rect(c.x - 130f, c.y + radius + 10f, 260f, 30f), text, color, _center);
        }

        /// <summary>Greyed ring over someone the thief is behind but can't rob, with the reason.</summary>
        void DrawBlocked()
        {
            var target = thief.Blocked;
            if (target == null || !ToGui(target.HeadPosition, out Vector2 c)) return;
            float radius = thief.ringRadius;
            Color dim = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
            DrawDot(c, (radius + 9f) * 2f, new Color(Paper.r, Paper.g, Paper.b, 0.3f));
            for (int i = 0; i < RingDots; i += 2)
            {
                float a = (i / (float)RingDots) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                DrawDot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 5f, dim);
            }
            DrawDot(c, 30f, new Color(Paper.r, Paper.g, Paper.b, 0.7f));
            GUI.color = dim;
            GUI.Label(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), "手", _key);
            GUI.color = Color.white;
            Plate(new Rect(c.x - 110f, c.y + radius + 10f, 220f, 28f), target.BlockedReason, new Color(Ink.r, Ink.g, Ink.b, 0.85f), _center);
        }

        void DrawWatchers()
        {
            float s = Mathf.Clamp01(thief.Suspicion / 100f);
            foreach (var w in thief.Watchers)
            {
                if (!ToGui(w.HeadPosition + Vector3.up * 0.25f, out Vector2 c)) continue;
                Color col = Color.Lerp(Gold, Vermilion, s);
                DrawDot(c, 30f, new Color(Paper.r, Paper.g, Paper.b, 0.9f));
                GUI.color = col;
                GUI.Label(new Rect(c.x - 15f, c.y - 16f, 30f, 30f), s > 0.6f ? "!" : "?", _key);
            }
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------ screen-space HUD

        void DrawSuspicion(float w)
        {
            if (thief.Suspicion <= 0.5f && thief.CurrentPhase == Pickpocketer.Phase.Free) return;
            float s = Mathf.Clamp01(thief.Suspicion / 100f);
            // Sits below the guild quest's timeline.
            var bar = new Rect(w * 0.5f - 160f, 94f, 320f, 10f);
            Plate(new Rect(bar.x - 12f, 78f, bar.width + 24f, 54f), "", Ink, _center);
            Fill(bar, PaperDark);
            Fill(new Rect(bar.x, bar.y, bar.width * s, bar.height), Color.Lerp(Gold, Vermilion, s));
            GUI.color = Ink;
            string label = thief.Watchers.Count > 0 ? "Someone's watching..." : "Suspicion";
            GUI.Label(new Rect(bar.x, bar.yMax + 2f, bar.width, 22f), label, _small);
            GUI.color = Color.white;
        }

        void DrawPurse(float w)
        {
            if (!showPurse || thief.Purse <= 0) return;
            Plate(new Rect(w - 220f, 12f, 208f, 34f), "Purse  " + thief.Purse + " wen", Ink, _center);
        }

        void DrawNotice(float w, float h)
        {
            if (string.IsNullOrEmpty(thief.Notice) || thief.NoticeAge > 2.4f) return;
            float alpha = Mathf.Clamp01((2.4f - thief.NoticeAge) / 0.5f);
            var rect = new Rect(w * 0.5f - 220f, h - 150f, 440f, 40f);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Fill(rect, Paper);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            GUI.Label(rect, thief.Notice, _title);
            GUI.color = prev;
        }

        // ------------------------------------------------------------------ pocket board

        void DrawBoard(float w, float h)
        {
            var board = thief.Board;
            const float cell = 112f, gap = 10f;
            float grid = cell * 3f + gap * 2f;
            float panelW = grid + 48f, panelH = grid + 168f;
            var panel = new Rect(w - panelW - 48f, (h - panelH) * 0.5f, panelW, panelH);

            Fill(new Rect(panel.x + 6f, panel.y + 6f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.25f));
            Fill(panel, Paper);
            Fill(new Rect(panel.x, panel.y, panel.width, 4f), Vermilion);

            GUI.color = Ink;
            string mark = thief.Mark != null ? thief.Mark.DisplayName : "";
            GUI.Label(new Rect(panel.x, panel.y + 12f, panelW, 32f), "Pockets of " + mark, _title);

            // Incense stick: the real-time window left before the world speeds back up.
            var stick = new Rect(panel.x + 24f, panel.y + 50f, panelW - 48f, 6f);
            Fill(stick, PaperDark);
            float t = Mathf.Clamp01(thief.StealTimeLeft / thief.stealWindow);
            Fill(new Rect(stick.x, stick.y, stick.width * t, stick.height), t < 0.3f ? Vermilion : Ink);
            if (!board.Finished) DrawDot(new Vector2(stick.x + stick.width * t, stick.center.y), 10f, Gold);

            GUI.color = Ink;
            string stats = $"Moves {board.MovesLeft}/{PocketBoard.MovesMax}     Valuables {board.Valuables}/{PocketBoard.Quota}     " +
                           $"Bells {board.Alarm}%     {board.Value} wen";
            GUI.Label(new Rect(panel.x, panel.y + 62f, panelW, 24f), stats, _small);

            float x0 = panel.x + (panelW - grid) * 0.5f, y0 = panel.y + 96f;
            for (int i = 0; i < PocketBoard.Count; i++)
            {
                var rect = new Rect(x0 + (i % 3) * (cell + gap), y0 + (i / 3) * (cell + gap), cell, cell);
                DrawPouch(board, i, rect);
                HandleClick(board, i, rect);
            }

            GUI.color = board.Finished && board.State != PocketBoard.Result.Success ? Vermilion : Ink;
            GUI.Label(new Rect(panel.x + 12f, panel.yMax - 58f, panelW - 24f, 24f), Footer(board), _center);
            GUI.color = InkSoft;
            GUI.Label(new Rect(panel.x + 12f, panel.yMax - 32f, panelW - 24f, 22f),
                "Left click: take     Right click: peek through     Esc: back off", _small);
            GUI.color = Color.white;
        }

        void DrawPouch(PocketBoard board, int i, Rect rect)
        {
            bool shown = board.IsShown(i);
            bool probed = board.Probed == i;
            bool taken = board.IsTaken(i);
            bool hover = !board.Finished && !taken && rect.Contains(Event.current.mousePosition);
            var item = board.ItemAt(i);

            // Taken pouch: a bare [     ] slot with no text, readable without looking.
            // What was in it is told by the footer line.
            if (taken)
            {
                Brackets(rect, InkSoft, 3f, 18f);
                return;
            }

            Color bg;
            string body;
            if (probed && !board.Finished)
            {
                bg = Gold;
                body = "peeking\n" + LinkText(board, i);
            }
            else if (shown)
            {
                bg = item.Kind == PocketBoard.Kind.Valuable ? Jade : item.Kind == PocketBoard.Kind.Trap ? Vermilion : Junk;
                body = item.Name + (item.Value > 0 ? "\n" + item.Value + " wen" : "");
            }
            else
            {
                bg = hover ? Color.Lerp(Cloth, Gold, 0.35f) : Cloth;
                body = "?";
            }

            Fill(rect, bg);
            if (hover) Outline(rect, Gold, 3f);

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 40f, 18f), Coords[i], _small);
            GUI.color = Color.white;
            GUI.Label(rect, body, body == "?" ? _key : _pouch);
            GUI.color = Color.white;
        }

        void HandleClick(PocketBoard board, int i, Rect rect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !rect.Contains(e.mousePosition)) return;
            if (e.button == 0) board.Take(i);
            else if (e.button == 1) board.Probe(i);
            e.Use();
        }

        static string LinkText(PocketBoard board, int i)
        {
            var links = board.LinksOf(i);
            var text = "";
            for (int n = 0; n < links.Length; n++) text += (n > 0 ? " " : "") + Coords[links[n]];
            return "→ " + text;
        }

        static string Footer(PocketBoard board)
        {
            switch (board.State)
            {
                case PocketBoard.Result.Success: return "Three valuables. Walk away.";
                case PocketBoard.Result.Alarmed: return "The bells rang!";
                case PocketBoard.Result.OutOfMoves: return "No more chances. Ease off.";
            }
            if (board.LastTaken >= 0)
            {
                var item = board.ItemAt(board.LastTaken);
                if (item.Kind == PocketBoard.Kind.Trap) return "A bell jingles...";
                if (item.Kind == PocketBoard.Kind.Junk) return "Just a " + item.Name.ToLower() + ".";
                return "Got the " + item.Name.ToLower() + ".";
            }
            return "Find three valuables, avoid the bells.";
        }

        // ------------------------------------------------------------------ drawing helpers

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }

        void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>Square brackets on the left and right edges of a rect: [     ].</summary>
        void Brackets(Rect r, Color c, float t, float tick)
        {
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.x, r.y, tick, t), c);
            Fill(new Rect(r.x, r.yMax - t, tick, t), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - tick, r.y, tick, t), c);
            Fill(new Rect(r.xMax - tick, r.yMax - t, tick, t), c);
        }

        void Plate(Rect r, string text, Color textColor, GUIStyle style)
        {
            Fill(r, Paper);
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = textColor;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        void DrawDot(Vector2 center, float size, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _dot);
            GUI.color = Color.white;
        }

        static Texture2D MakeDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HUDDot" };
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _label.normal.textColor = Color.white;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _title = new GUIStyle(_center) { fontSize = 22, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_center) { fontSize = 14 };
            _key = new GUIStyle(_center) { fontSize = 22, fontStyle = FontStyle.Bold };
            _pouch = new GUIStyle(_center) { fontSize = 16, fontStyle = FontStyle.Bold };
        }
    }
}
