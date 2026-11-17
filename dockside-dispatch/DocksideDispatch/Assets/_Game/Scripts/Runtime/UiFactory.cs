using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dockside
{
    /// <summary>Builds uGUI elements in code (no prefabs or imported fonts needed for the prototype).</summary>
    public static class UiFactory
    {
        private static Sprite _rounded;
        private static Font _font, _fontBold;

        /// <summary>Fredoka SemiBold (body text), as in the art direction. Falls back to Unity's built-in font.</summary>
        public static Font Font => _font ? _font : (_font = Resources.Load<Font>("Fonts/Fredoka-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Fredoka Bold (titles, numbers, labels).</summary>
        public static Font FontBold => _fontBold ? _fontBold : (_fontBold = Resources.Load<Font>("Fonts/Fredoka-Bold") ?? Font);

        /// <summary>White rounded-rectangle sprite, 9-sliced, generated at runtime.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded) return _rounded;
                const int size = 64, radius = 24;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                return _rounded;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                         Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, Color? shadow = null, float shadowOffset = 10f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            if (shadow.HasValue)
            {
                var s = go.AddComponent<Shadow>();
                s.effectColor = shadow.Value;
                s.effectDistance = new Vector2(0, -shadowOffset);
            }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter,
                                 FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            // Real bold/semibold font files instead of synthetic bold, so strokes stay clean.
            t.font = style == FontStyle.Bold ? FontBold : Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Normal;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Stretch((RectTransform)go.transform);
            return t;
        }

        public static Button Button(string name, Transform parent, Color bg, Color shadow, Action onClick, string accessibleName)
        {
            var img = Panel(name, parent, bg, shadow, 10f);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            b.colors = colors;
            b.onClick.AddListener(() => onClick?.Invoke());
            img.gameObject.name = $"Button_{accessibleName}";
            return b;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Simple icons drawn from rectangles (pause, plus, hook, shuffle) so no icon font is needed.</summary>
        public static void Icon(string kind, Transform parent, Color color, float size)
        {
            var root = Rect("Icon_" + kind, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            // Outline icons drawn to match the art direction (Resources/UI/icon_*.png, white, tinted here).
            var tex = Resources.Load<Texture2D>("UI/icon_" + kind);
            if (tex != null)
            {
                var img = root.gameObject.AddComponent<Image>();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                img.color = color;
                img.raycastTarget = false;
                return;
            }
            void Bar(float x, float y, float w, float h, float rot = 0)
            {
                var r = Rect("Bar", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y) * size, new Vector2(w, h) * size);
                r.localRotation = Quaternion.Euler(0, 0, rot);
                var img = r.gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
            }
            switch (kind)
            {
                case "pause": Bar(-0.17f, 0, 0.18f, 0.7f); Bar(0.17f, 0, 0.18f, 0.7f); break;
                case "berth": Bar(0, 0, 0.7f, 0.14f); Bar(0, 0, 0.14f, 0.7f); break;
                case "crane": Bar(0, 0.22f, 0.14f, 0.5f); Bar(0, 0.47f, 0.6f, 0.12f); Bar(0.12f, -0.12f, 0.36f, 0.12f); Bar(0.24f, -0.02f, 0.12f, 0.24f); break;
                case "shuffle": Bar(0, 0, 0.8f, 0.12f, 35); Bar(0, 0, 0.8f, 0.12f, -35); Bar(0.3f, 0.28f, 0.22f, 0.12f); Bar(0.3f, -0.28f, 0.22f, 0.12f); break;
                case "retry": Bar(0, 0.25f, 0.5f, 0.12f); Bar(-0.25f, 0, 0.12f, 0.5f); Bar(0, -0.25f, 0.5f, 0.12f); Bar(0.25f, 0.12f, 0.12f, 0.26f); break;
            }
        }
    }
}
