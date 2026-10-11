using UnityEngine;

namespace WorldRPG
{
    /// <summary>Minimal on-screen help for the town demo.</summary>
    public class DemoHUD : MonoBehaviour
    {
        public RTSCameraRig rig;
        public DebugFlyCamera flyCamera;

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

            string text;
            if (flyCamera != null && flyCamera.IsActive)
                text = "<b>Debug fly camera</b>\n" +
                       "WASD: move   Q / E: down / up  (Shift = fast)\n" +
                       "Right mouse drag: look\n" +
                       "Scroll: fly speed (" + flyCamera.moveSpeed.ToString("0.#") + ")\n" +
                       "F1: back to game camera";
            else if (rig.CurrentMode == RTSCameraRig.Mode.Free)
                text = "<b>Free camera</b>\n" +
                       "Middle mouse drag / WASD: pan  (Shift = faster)\n" +
                       "Q / E or Right mouse drag: rotate\n" +
                       "Scroll: zoom\n" +
                       "Tab: control character   F1: debug fly camera";
            else
                text = "<b>Character control</b>\n" +
                       "WASD: walk  (Shift = run)\n" +
                       "Q / E or Right mouse drag: orbit\n" +
                       "Scroll: zoom\n" +
                       "Behind a villager: click & hold the ring to pick pocket\n" +
                       "Tab: free camera   F1: debug fly camera";

            GUI.Box(new Rect(12, 12, 440, 124), text, _style);
        }
    }
}
