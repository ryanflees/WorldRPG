using UnityEngine;

namespace WorldRPG
{
    /// <summary>Minimal on-screen help for the town demo.</summary>
    public class DemoHUD : MonoBehaviour
    {
        public RTSCameraRig rig;

        GUIStyle _style;

        void OnGUI()
        {
            if (rig == null) return;
            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8),
                richText = true
            };

            string text = rig.CurrentMode == RTSCameraRig.Mode.Free
                ? "<b>Free camera</b>\n" +
                  "Middle mouse drag / WASD: pan  (Shift = faster)\n" +
                  "Q / E or Right mouse drag: rotate\n" +
                  "Scroll: zoom\n" +
                  "Tab: control character"
                : "<b>Character control</b>\n" +
                  "WASD: walk  (Shift = run)\n" +
                  "Q / E or Right mouse drag: rotate\n" +
                  "Scroll: zoom\n" +
                  "Tab / Middle mouse drag: free camera";

            GUI.Box(new Rect(12, 12, 340, 104), text, _style);
        }
    }
}
