using UnityEngine;

namespace WorldRPG.Stealth
{
    /// <summary>
    /// The level's goal: the Thieves' Guild wants <see cref="targetWen"/> before sunset (<see cref="DayClock"/>).
    /// Draws the briefing card when play starts, the afternoon timeline across the top of the screen (hour markers,
    /// the sun moving along it, the sunset deadline, purse against target), and the verdict at sunset.
    /// Lives on the Player next to the clock, so it starts when the thief appears.
    /// </summary>
    public class GuildQuest : MonoBehaviour
    {
        public DayClock clock;
        public Pickpocketer thief;
        [Tooltip("Takes over the purse readout from the pickpocket HUD.")]
        public PickpocketHUD hud;

        public int targetWen = 4000;
        public string guildName = "The Thieves' Guild";
        [Tooltip("Seconds the briefing card stays up when play starts.")]
        public float briefingTime = 7f;

        static readonly Color Paper = new Color(0.95f, 0.93f, 0.88f, 0.94f);
        static readonly Color PaperDark = new Color(0.82f, 0.79f, 0.72f, 1f);
        static readonly Color Ink = new Color(0.14f, 0.12f, 0.1f, 1f);
        static readonly Color InkSoft = new Color(0.14f, 0.12f, 0.1f, 0.55f);
        static readonly Color Vermilion = new Color(0.74f, 0.19f, 0.13f, 1f);
        static readonly Color Jade = new Color(0.2f, 0.55f, 0.48f, 1f);
        static readonly Color Gold = new Color(0.86f, 0.62f, 0.2f, 1f);

        float _age;
        float _metAt = -1f;
        bool _over;
        float _overAge;
        Texture2D _dot;
        GUIStyle _title, _body, _small, _tick;

        public bool QuotaMet => thief != null && thief.Purse >= targetWen;

        void Start()
        {
            if (clock == null) clock = GetComponent<DayClock>();
            if (thief == null) thief = GetComponent<Pickpocketer>();
            if (hud == null) hud = FindFirstObjectByType<PickpocketHUD>();
            if (hud != null) hud.showPurse = false;
            if (clock != null) clock.Sunset += OnSunset;
            _dot = MakeDot(48);
        }

        void OnDestroy()
        {
            if (clock != null) clock.Sunset -= OnSunset;
            if (_dot != null) Destroy(_dot);
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            if (_over) _overAge += Time.unscaledDeltaTime;
            if (_metAt < 0f && QuotaMet) _metAt = _age;
        }

        void OnSunset() => _over = true;

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (clock == null || thief == null) return;
            EnsureStyles();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = 1080f;

            DrawTimeline(w);
            if (_age < briefingTime) DrawBriefing(w, h, Fade(_age, briefingTime));
            if (_metAt >= 0f && _age - _metAt < 4f && !_over) DrawBanner(w, h, "The guild's take is in your purse.", Jade, Fade(_age - _metAt, 4f));
            if (_over) DrawVerdict(w, h);

            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        /// <summary>Fades in over 0.5 s and out over the last 0.8 s of a card's life.</summary>
        static float Fade(float age, float life) => Mathf.Clamp01(Mathf.Min(age / 0.5f, (life - age) / 0.8f));

        void DrawTimeline(float w)
        {
            const float lineW = 640f, y = 34f;
            float x0 = w * 0.5f - lineW * 0.5f, x1 = x0 + lineW;
            GUI.color = Color.white;
            Fill(new Rect(x0 - 24f, 8f, lineW + 48f, 62f), Paper);

            // Elapsed afternoon in ink, what's left in a pale wash, the deadline in vermilion.
            float sunX = Mathf.Lerp(x0, x1, clock.Progress);
            Fill(new Rect(x0, y - 2f, lineW, 4f), PaperDark);
            Fill(new Rect(x0, y - 2f, sunX - x0, 4f), Ink);
            Fill(new Rect(x1 - 2f, y - 10f, 4f, 20f), Vermilion);

            // Hour markers.
            for (int hour = Mathf.CeilToInt(clock.startHour); hour <= Mathf.FloorToInt(clock.endHour); hour++)
            {
                float x = Mathf.Lerp(x0, x1, Mathf.InverseLerp(clock.startHour, clock.endHour, hour));
                GUI.color = Color.white;
                Fill(new Rect(x - 1f, y - 7f, 2f, 14f), InkSoft);
                GUI.color = InkSoft;
                GUI.Label(new Rect(x - 30f, y + 8f, 60f, 18f), (hour > 12 ? hour - 12 : hour) + " PM", _tick);
            }
            GUI.color = Vermilion;
            GUI.Label(new Rect(x1 - 40f, y + 8f, 80f, 18f), "Sunset", _tick);

            // The sun travelling along the afternoon: warm gold, reddening as the deadline nears.
            float urgency = Mathf.InverseLerp(0.75f, 1f, clock.Progress);
            Dot(new Vector2(sunX, y), 22f, Paper);
            Dot(new Vector2(sunX, y), 16f, Color.Lerp(Gold, Vermilion, urgency));

            // Purse against the guild's target, at the line's right end.
            var purse = new Rect(x1 + 24f, 8f, 210f, 62f);
            GUI.color = Color.white;
            Fill(purse, Paper);
            GUI.color = InkSoft;
            GUI.Label(new Rect(purse.x, purse.y + 6f, purse.width, 18f), "Guild's take", _tick);
            GUI.color = QuotaMet ? Jade : Ink;
            GUI.Label(new Rect(purse.x, purse.y + 22f, purse.width, 30f), $"{thief.Purse:N0} / {targetWen:N0} wen", _body);
            float fill = Mathf.Clamp01(thief.Purse / (float)targetWen);
            GUI.color = Color.white;
            Fill(new Rect(purse.x + 14f, purse.yMax - 9f, purse.width - 28f, 3f), PaperDark);
            Fill(new Rect(purse.x + 14f, purse.yMax - 9f, (purse.width - 28f) * fill, 3f), QuotaMet ? Jade : Gold);
        }

        void DrawBriefing(float w, float h, float alpha)
        {
            var card = new Rect(w * 0.5f - 400f, h * 0.24f, 800f, 190f);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Fill(card, Paper);
            Fill(new Rect(card.x, card.y, card.width, 4f), Vermilion);
            GUI.color = new Color(Vermilion.r, Vermilion.g, Vermilion.b, alpha);
            GUI.Label(new Rect(card.x, card.y + 18f, card.width, 26f), guildName, _small);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            GUI.Label(new Rect(card.x + 30f, card.y + 50f, card.width - 60f, 50f), $"Bring us {targetWen:N0} wen before sunset.", _title);
            GUI.color = new Color(InkSoft.r, InkSoft.g, InkSoft.b, alpha * InkSoft.a);
            GUI.Label(new Rect(card.x + 30f, card.y + 110f, card.width - 60f, 60f),
                "Work the crowds of Linshui. Slip behind a mark, lift their purse, stay unseen.\nWatch the sun.", _body);
            GUI.color = prev;
        }

        void DrawBanner(float w, float h, string text, Color color, float alpha)
        {
            var rect = new Rect(w * 0.5f - 260f, 150f, 520f, 40f);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Fill(rect, Paper);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, _body);
            GUI.color = prev;
        }

        void DrawVerdict(float w, float h)
        {
            float alpha = Mathf.Clamp01(_overAge / 0.8f);
            var card = new Rect(w * 0.5f - 330f, h * 0.3f, 660f, 200f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Fill(card, Paper);
            bool won = QuotaMet;
            Fill(new Rect(card.x, card.y, card.width, 4f), won ? Jade : Vermilion);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            GUI.Label(new Rect(card.x, card.y + 30f, card.width, 50f), won ? "The guild is pleased." : "The sun has set.", _title);
            GUI.color = new Color(InkSoft.r, InkSoft.g, InkSoft.b, alpha * InkSoft.a);
            string body = won
                ? $"You brought in {thief.Purse:N0} wen. Linshui's purses are lighter tonight."
                : $"Only {thief.Purse:N0} of {targetWen:N0} wen. The guild expected more of you.";
            GUI.Label(new Rect(card.x + 30f, card.y + 100f, card.width - 60f, 60f), body, _body);
        }

        // ------------------------------------------------------------------ helpers

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void Dot(Vector2 center, float size, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _dot);
            GUI.color = Color.white;
        }

        static Texture2D MakeDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "QuestDot" };
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _title.normal.textColor = Color.white;
            _body = new GUIStyle(_title) { fontSize = 19, fontStyle = FontStyle.Normal };
            _small = new GUIStyle(_body) { fontSize = 17, fontStyle = FontStyle.Bold };
            _tick = new GUIStyle(_body) { fontSize = 14 };
        }
    }
}
