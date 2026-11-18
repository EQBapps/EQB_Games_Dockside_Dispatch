using System;
using System.Collections;
using Dockside.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dockside
{
    /// <summary>
    /// Game mode menu: the Dockside Dispatch logo over a live scene of the selected mode (the harbour for Load, the city
    /// road for Unload), a card per mode that can be swiped, stepped with the arrows or picked with the dots, and a big
    /// Play button. The logo ribbon, card title and Play button take the mode's colour. Unload stays locked (with the
    /// reason shown) until Load level 5 is complete.
    /// </summary>
    public class ModeMenuView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static readonly Color LoadAccent = Palette.Hex("2F7FE0"), UnloadAccent = Palette.Hex("EE8A2B");
        private const float CardPitch = 1000f, SwipeThreshold = 110f;

        public event Action<GameMode> ModeChanged;   // the scene behind should switch to this mode
        public event Action<GameMode> PlayClicked;

        private Palette _pal;
        private RectTransform _cards, _ribbon;
        private Image _ribbonImg, _playImg, _fade;
        private Text _playText;
        private readonly Image[] _dots = new Image[2];
        private Button _left, _right, _play;
        private readonly Text[] _levelTexts = new Text[2];
        private readonly Image[] _barFills = new Image[2];
        private int _index;
        private float _dragStartX, _cardsX;
        private Coroutine _slide;

        public GameMode Selected => (GameMode)_index;
        public bool IsOpen => gameObject.activeSelf;

        public static ModeMenuView Create(Palette pal)
        {
            var canvas = UiFactory.CreateCanvas("ModeMenu");
            canvas.sortingOrder = 20;   // above the HUD
            var view = canvas.gameObject.AddComponent<ModeMenuView>();
            view.Build(pal, canvas.transform);
            canvas.gameObject.SetActive(false);
            return view;
        }

        private static Color Accent(GameMode m) => m == GameMode.Unload ? UnloadAccent : LoadAccent;

        private void Build(Palette pal, Transform root)
        {
            _pal = pal;
            // Full-screen transparent catcher so a swipe anywhere changes the mode.
            var catcher = UiFactory.Rect("SwipeArea", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            catcher.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            // Scrims so the logo and the card read over any sky or scene.
            Scrim("TopScrim", root, true);
            Scrim("BottomScrim", root, false);

            var safe = UiFactory.Rect("SafeArea", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // ---- Logo: "DOCKSIDE" in cream with a deep shadow, "DISPATCH" on a ribbon in the mode's colour ----
            var logo = UiFactory.Rect("Logo", safe, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(900, 330));
            var top = UiFactory.Rect("Dockside", logo, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t1 = UiFactory.Label("Text", top, "DOCKSIDE", 150, pal.uiCream);
            Outline(t1, Palette.Hex("3A2A28"), 6f);
            var sh = t1.gameObject.AddComponent<Shadow>(); sh.effectColor = Palette.Hex("2E2422"); sh.effectDistance = new Vector2(0, -14);
            _ribbonImg = UiFactory.Panel("Ribbon", logo, LoadAccent, Palette.Hex("2E2422"), 10f);
            _ribbon = (RectTransform)_ribbonImg.transform;
            _ribbon.anchorMin = _ribbon.anchorMax = new Vector2(0.5f, 0); _ribbon.pivot = new Vector2(0.5f, 0);
            _ribbon.anchoredPosition = new Vector2(0, 10); _ribbon.sizeDelta = new Vector2(640, 130);
            _ribbon.localRotation = Quaternion.Euler(0, 0, 2.5f);
            var t2 = UiFactory.Label("Text", _ribbon, "DISPATCH", 96, Color.white);
            Outline(t2, Palette.Hex("2E2422"), 3f);

            // ---- Cards ----
            var viewport = UiFactory.Rect("Viewport", safe, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 430), new Vector2(1080, 640));
            _cards = UiFactory.Rect("Cards", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
            BuildCard(0, GameMode.Load, "LOAD", "Clear the yard: send every container to the ship of its colour.");
            BuildCard(1, GameMode.Unload, "UNLOAD", "Empty the ship: send every container to the truck of its colour.");

            // Arrows (swiping is never the only way to change mode).
            _left = Arrow("Left", viewport, -1);
            _right = Arrow("Right", viewport, 1);

            // Dots.
            // Dots sit on a solid cream pill so they never mix with the scene (or the symbols on containers) behind.
            var dotsBg = UiFactory.Panel("DotsBg", safe, pal.uiCream, pal.uiShadow, 6f);
            var dbr = (RectTransform)dotsBg.transform;
            dbr.anchorMin = dbr.anchorMax = new Vector2(0.5f, 0); dbr.pivot = new Vector2(0.5f, 0);
            dbr.anchoredPosition = new Vector2(0, 372); dbr.sizeDelta = new Vector2(150, 56);
            var dots = UiFactory.Rect("Dots", safe, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 380), new Vector2(200, 40));
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                var d = UiFactory.Rect("Dot" + i, dots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 0.5f) * 60, 0), new Vector2(34, 34));
                var img = d.gameObject.AddComponent<Image>();
                img.sprite = UiFactory.Disc;
                var b = d.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => Select(k, true));
                _dots[i] = img;
            }

            // Play button.
            _play = UiFactory.Button("Play", safe, LoadAccent, Palette.Hex("2E2422"), OnPlay, "Play");
            _playImg = _play.GetComponent<Image>();
            var prt = (RectTransform)_play.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0); prt.pivot = new Vector2(0.5f, 0);
            prt.anchoredPosition = new Vector2(0, 150); prt.sizeDelta = new Vector2(760, 180);
            _playText = UiFactory.Label("Text", _play.transform, "PLAY", 70, Color.white);
            Outline(_playText, new Color(0, 0, 0, 0.25f), 2f);

            // Fade used while the scene behind switches mode.
            var fade = UiFactory.Rect("Fade", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _fade = fade.gameObject.AddComponent<Image>();
            _fade.color = new Color(0.12f, 0.08f, 0.08f, 0f);
            _fade.raycastTarget = false;
        }

        private void BuildCard(int i, GameMode mode, string title, string body)
        {
            var card = UiFactory.Panel("Card_" + title, _cards, _pal.uiCream, _pal.uiShadow, 14f);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(i * CardPitch, 0); rt.sizeDelta = new Vector2(800, 560);
            // Mode stripe along the top of the card.
            var stripe = UiFactory.Panel("Stripe", rt, Accent(mode));
            var srt = (RectTransform)stripe.transform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1);
            srt.anchoredPosition = new Vector2(0, 0); srt.sizeDelta = new Vector2(0, 26);
            var chip = UiFactory.Rect("Chip", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(0, 50));
            UiFactory.Label("Text", chip, mode == GameMode.Load ? "MODE 1  ·  HARBOUR" : "MODE 2  ·  DOCKSIDE", 34, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            var tr = UiFactory.Rect("Title", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -105), new Vector2(0, 130));
            UiFactory.Label("Text", tr, title, 120, Palette.Darken(Accent(mode), 0.1f));
            var br = UiFactory.Rect("Body", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -245), new Vector2(-110, 120));
            UiFactory.Label("Text", br, body, 40, _pal.uiText, TextAnchor.MiddleCenter, FontStyle.Normal);

            // Progress: "Level 12" and a bar towards 100.
            var lr = UiFactory.Rect("Level", rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(-110, 60));
            _levelTexts[i] = UiFactory.Label("Text", lr, "", 44, _pal.uiText);
            var bar = UiFactory.Panel("Bar", rt, Palette.Hex("E6D6C2"));
            var bart = (RectTransform)bar.transform;
            bart.anchorMin = bart.anchorMax = new Vector2(0.5f, 0); bart.pivot = new Vector2(0.5f, 0);
            bart.anchoredPosition = new Vector2(0, 48); bart.sizeDelta = new Vector2(560, 30);
            var fill = UiFactory.Panel("Fill", bar.transform, Accent(mode));
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0, 0.5f);
            frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(30, 0);
            _barFills[i] = fill;
        }

        private Button Arrow(string name, Transform parent, int dir)
        {
            var b = UiFactory.Button(name, parent, _pal.uiCream, _pal.uiShadow, () => Select(_index + dir, true), name == "Left" ? "Previous mode" : "Next mode");
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(dir * 470, 0); rt.sizeDelta = new Vector2(110, 110);
            // Chevron from two bars.
            for (int k = 0; k < 2; k++)
            {
                var bar = UiFactory.Rect("Chevron", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(dir * 4, k == 0 ? 11 : -11), new Vector2(40, 12));
                bar.localRotation = Quaternion.Euler(0, 0, (k == 0 ? -45 : 45) * dir);
                var img = bar.gameObject.AddComponent<Image>();
                img.color = _pal.uiText;
                img.raycastTarget = false;
            }
            return b;
        }

        private static void Scrim(string name, Transform root, bool top)
        {
            var rt = UiFactory.Rect(name, root, new Vector2(0, top ? 0.72f : 0), new Vector2(1, top ? 1 : 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<RawImage>();
            var tex = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                float a = top ? Mathf.SmoothStep(0f, 0.45f, t) : Mathf.SmoothStep(0.6f, 0f, t);
                tex.SetPixel(0, y, new Color(0.12f, 0.07f, 0.07f, a));
            }
            tex.Apply();
            img.texture = tex;
            img.raycastTarget = false;
        }

        private static void Outline(Text t, Color c, float d)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
        }

        // ------------------------------------------------------------------
        /// <summary>Opens the menu on a mode; refreshes levels and the lock state.</summary>
        public void Show(GameMode mode)
        {
            gameObject.SetActive(true);
            _index = (int)mode;
            _cardsX = -_index * CardPitch;
            _cards.anchoredPosition = new Vector2(_cardsX, 0);
            _fade.color = new Color(_fade.color.r, _fade.color.g, _fade.color.b, 0f);
            Refresh();
            StartCoroutine(Tween.Pop(_ribbon, 0.1f, 0.3f));
        }

        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            for (int i = 0; i < 2; i++)
            {
                var m = (GameMode)i;
                bool locked = m == GameMode.Unload && !SaveData.UnloadUnlocked;
                int level = SaveData.LevelFor(m);
                int done = Mathf.Clamp(level - 1, 0, LevelGenerator.AuthoredLevelCount);
                _levelTexts[i].text = locked ? $"Unlocks after Load level {SaveData.UnloadUnlockLevel - 1}" : $"Level {level}";
                float share = locked ? (SaveData.Level - 1f) / (SaveData.UnloadUnlockLevel - 1f) : done / (float)LevelGenerator.AuthoredLevelCount;
                ((RectTransform)_barFills[i].transform).sizeDelta = new Vector2(Mathf.Max(30f, 560f * Mathf.Clamp01(share)), 0);
                _dots[i].color = i == _index ? Accent(m) : Palette.Hex("CDBFAE");
            }
            var sel = Selected;
            bool selLocked = sel == GameMode.Unload && !SaveData.UnloadUnlocked;
            _ribbonImg.color = Accent(sel);
            _playImg.color = selLocked ? Palette.Hex("A39A94") : Accent(sel);
            _play.interactable = !selLocked;
            _playText.text = selLocked ? "LOCKED" : $"PLAY LEVEL {SaveData.LevelFor(sel)}";
            _left.gameObject.SetActive(_index > 0);
            _right.gameObject.SetActive(_index < 1);
        }

        private void Select(int index, bool animate)
        {
            index = Mathf.Clamp(index, 0, 1);
            bool changed = index != _index;
            _index = index;
            if (_slide != null) StopCoroutine(_slide);
            _slide = StartCoroutine(SlideTo(-_index * CardPitch));
            Refresh();
            if (changed)
            {
                Sfx.Play(Selected == GameMode.Unload ? "berth" : "horn", 0.5f);
                StartCoroutine(SwitchScene(Selected));
            }
        }

        private IEnumerator SlideTo(float x)
        {
            float from = _cards.anchoredPosition.x;
            yield return Tween.Run(0.3f, e => _cards.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, x, e), 0), Tween.EaseOutCubic);
            _cardsX = x;
            StartCoroutine(Tween.Pop(_ribbon, 0.08f, 0.25f));
        }

        private IEnumerator SwitchScene(GameMode mode)
        {
            yield return Tween.Run(0.15f, e => SetFade(e * 0.85f));
            ModeChanged?.Invoke(mode);
            yield return null;
            yield return Tween.Run(0.3f, e => SetFade((1f - e) * 0.85f));
        }

        private void SetFade(float a) => _fade.color = new Color(_fade.color.r, _fade.color.g, _fade.color.b, a);

        private void OnPlay()
        {
            if (Selected == GameMode.Unload && !SaveData.UnloadUnlocked) return;
            PlayClicked?.Invoke(Selected);
        }

        // ------------------------------------------------------------------ swipe
        public void OnBeginDrag(PointerEventData e)
        {
            if (_slide != null) StopCoroutine(_slide);
            _dragStartX = e.position.x;
        }

        public void OnDrag(PointerEventData e)
        {
            float scale = transform.lossyScale.x > 0 ? 1f / transform.lossyScale.x : 1f;
            float dx = (e.position.x - _dragStartX) * scale;
            // Resist dragging past the ends.
            float x = _cardsX + dx;
            if (x > 0) x *= 0.35f;
            if (x < -CardPitch) x = -CardPitch + (x + CardPitch) * 0.35f;
            _cards.anchoredPosition = new Vector2(x, 0);
        }

        public void OnEndDrag(PointerEventData e)
        {
            float scale = transform.lossyScale.x > 0 ? 1f / transform.lossyScale.x : 1f;
            float dx = (e.position.x - _dragStartX) * scale;
            int target = _index;
            if (dx < -SwipeThreshold) target++;
            else if (dx > SwipeThreshold) target--;
            Select(target, true);
        }

        private void Update()
        {
            // Keyboard / controller: left and right step between modes, Enter plays.
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.leftArrowKey.wasPressedThisFrame) Select(_index - 1, true);
            if (kb.rightArrowKey.wasPressedThisFrame) Select(_index + 1, true);
            if (kb.enterKey.wasPressedThisFrame) OnPlay();
#else
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Select(_index - 1, true);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Select(_index + 1, true);
            if (Input.GetKeyDown(KeyCode.Return)) OnPlay();
#endif
        }
    }
}
