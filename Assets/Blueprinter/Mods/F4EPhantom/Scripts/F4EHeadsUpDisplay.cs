using UnityEngine;

namespace F4EPhantom
{
    public sealed class F4EHeadsUpDisplay : MonoBehaviour
    {
        public float airspeedKnots;
        public float altitudeFeet;
        public float headingDegrees;
        public float pitchDegrees;
        public float bankDegrees;
        public float gLoad = 1f;
        public bool masterArm;

        [SerializeField] private Color phosphorColor = new Color(0.30f, 1f, 0.48f, 0.92f);
        [SerializeField] private float displayScale = 1f;

        private GUIStyle style;

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            var size = Mathf.Max(13, Mathf.RoundToInt(18f * displayScale));
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = size
                };
            }

            style.normal.textColor = phosphorColor;
            style.fontSize = size;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var horizon = center.y + pitchDegrees * 4f;
            DrawLine(new Vector2(center.x - 150f, center.y), new Vector2(center.x + 150f, center.y), 2f);
            DrawLine(new Vector2(center.x - 22f, center.y - 20f), center, 2f);
            DrawLine(new Vector2(center.x + 22f, center.y - 20f), center, 2f);
            DrawLine(new Vector2(center.x - 95f, horizon), new Vector2(center.x + 95f, horizon), 1f);
            DrawText(new Rect(center.x - 255f, center.y - 110f, 140f, 28f), $"{Mathf.RoundToInt(airspeedKnots):000} KTS");
            DrawText(new Rect(center.x + 115f, center.y - 110f, 140f, 28f), $"{Mathf.RoundToInt(altitudeFeet):00000}");
            DrawText(new Rect(center.x - 65f, center.y - 185f, 130f, 28f), $"{Mathf.Repeat(headingDegrees, 360f):000}");
            DrawText(new Rect(center.x - 60f, center.y + 125f, 120f, 28f), $"{gLoad:0.0} G");
            DrawText(new Rect(center.x - 80f, center.y + 160f, 160f, 28f), masterArm ? "ARM" : "SAFE");
        }

        private void DrawText(Rect rect, string value)
        {
            GUI.Label(rect, value, style);
        }

        private void DrawLine(Vector2 from, Vector2 to, float width)
        {
            var direction = to - from;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, direction.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = previous;
        }
    }
}
