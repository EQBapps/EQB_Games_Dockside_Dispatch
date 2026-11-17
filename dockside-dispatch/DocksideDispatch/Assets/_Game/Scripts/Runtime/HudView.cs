using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    public enum BoosterType { ExtraBerth, CraneLift, Shuffle }

    /// <summary>Top bar (pause, level, coins), booster bar, toasts and the win / lose / pause panels.</summary>
    public class HudView : MonoBehaviour
    {
        public const float TopMargin = 0.11f;     // share of screen height the top bar covers
        public const float BottomMargin = 0.13f;  // share of screen height the booster bar covers

        public event Action PauseClicked;
        public event Action<BoosterType> BoosterClicked;

        private Palette _pal;
        private Text _level, _ships, _coins, _toast, _hint;
        private CanvasGroup _toastGroup;
        private readonly Dictionary<BoosterType, Text> _boosterCounts = new Dictionary<BoosterType, Text>();
        private readonly Dictionary<BoosterType, Image> _boosterBg = new Dictionary<BoosterType, Image>();
        private RectTransform _overlay;
        private Coroutine _toastRoutine;

        public static HudView Create(Palette pal)
        {
            var canvas = UiFactory.CreateCanvas("HUD");
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.Build(pal, canvas.transform);
            return hud;
        }

        private void Build(Palette pal, Transform root)
        {
            _pal = pal;
            var safe = UiFactory.Rect("SafeArea", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // ---- Top bar ----
            var top = UiFactory.Rect("TopBar", safe, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-48, 150));

            var pause = UiFactory.Button("Pause", top, pal.uiCream, pal.uiShadow, () => PauseClicked?.Invoke(), "Pause");
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f); prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(120, 120);
            UiFactory.Icon("pause", pause.transform, pal.uiText, 70);

            var levelPill = UiFactory.Panel("LevelPill", top, pal.uiCream, pal.uiShadow);
            var lrt = (RectTransform)levelPill.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.sizeDelta = new Vector2(330, 140);
            var lvlBox = UiFactory.Rect("LevelText", levelPill.transform, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _level = UiFactory.Label("Level", lvlBox, "LEVEL 1", 50, pal.uiText);
            var shipsBox = UiFactory.Rect("ShipsText", levelPill.transform, new Vector2(0, 0.06f), new Vector2(1, 0.46f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _ships = UiFactory.Label("Ships", shipsBox, "Ships 0 of 0", 32, pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);

            var coinPill = UiFactory.Panel("CoinPill", top, pal.uiCream, pal.uiShadow);
            var crt = (RectTransform)coinPill.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f); crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(250, 104);
            var coin = UiFactory.Panel("Coin", coinPill.transform, Palette.Hex("F7C431"), Palette.Hex("C9952A"), 6f);
            var cort = (RectTransform)coin.transform;
            cort.anchorMin = cort.anchorMax = new Vector2(0, 0.5f); cort.pivot = new Vector2(0, 0.5f);
            cort.anchoredPosition = new Vector2(18, 3); cort.sizeDelta = new Vector2(66, 66);
            var coinText = UiFactory.Rect("CoinText", coinPill.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            coinText.offsetMin = new Vector2(92, 0); coinText.offsetMax = new Vector2(-16, 0);
            _coins = UiFactory.Label("Coins", coinText, "0", 44, pal.uiText, TextAnchor.MiddleCenter);

            // ---- Crane-lift hint (shown under the top bar while the booster is armed) ----
            var hintPanel = UiFactory.Panel("CraneHint", safe, pal.uiAccent, Palette.Darken(pal.uiAccent, 0.3f), 6f);
            var hrt = (RectTransform)hintPanel.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.anchoredPosition = new Vector2(0, -196); hrt.sizeDelta = new Vector2(760, 84);
            _hint = UiFactory.Label("Hint", hintPanel.transform, "Crane Lift: tap any container", 38, Color.white);
            hintPanel.gameObject.SetActive(false);

            // ---- Booster bar ----
            var bar = UiFactory.Panel("BoosterBar", safe, pal.uiBar);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, -30); brt.sizeDelta = new Vector2(60, 270);
            var row = UiFactory.Rect("Row", bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(900, 170));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 42; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            AddBooster(row, BoosterType.ExtraBerth, "berth", "Extra Berth");
            AddBooster(row, BoosterType.CraneLift, "crane", "Crane Lift");
            AddBooster(row, BoosterType.Shuffle, "shuffle", "Shuffle");

            // ---- Toast ----
            var toast = UiFactory.Panel("Toast", safe, new Color(0.18f, 0.13f, 0.12f, 0.9f));
            var trt = (RectTransform)toast.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0); trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = new Vector2(0, 290); trt.sizeDelta = new Vector2(900, 100);
            _toast = UiFactory.Label("ToastText", toast.transform, "", 36, Color.white);
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0; _toastGroup.blocksRaycasts = false;

            // ---- Modal overlay (win / lose / pause) ----
            _overlay = UiFactory.Rect("Overlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dim = _overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.07f, 0.06f, 0.55f);
            _overlay.gameObject.SetActive(false);
        }

        private void AddBooster(Transform row, BoosterType type, string icon, string label)
        {
            var b = UiFactory.Button(label, row, _pal.uiCream, Palette.Hex("2E2422"), () => BoosterClicked?.Invoke(type), label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(260, 170);
            var iconHolder = UiFactory.Rect("IconHolder", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(70, 70));
            UiFactory.Icon(icon, iconHolder, _pal.uiText, 70);
            var lab = UiFactory.Rect("Label", b.transform, new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(0.5f, 0), new Vector2(0, 8), Vector2.zero);
            UiFactory.Label("Text", lab, label, 32, _pal.uiText);
            var badge = UiFactory.Panel("Badge", b.transform, _pal.uiAccent);
            var bart = (RectTransform)badge.transform;
            bart.anchorMin = bart.anchorMax = new Vector2(1, 1); bart.pivot = new Vector2(0.5f, 0.5f);
            bart.anchoredPosition = new Vector2(-14, -10); bart.sizeDelta = new Vector2(66, 66);
            _boosterCounts[type] = UiFactory.Label("Count", badge.transform, "0", 36, Color.white);
            _boosterBg[type] = b.GetComponent<Image>();
        }

        // ------------------------------------------------------------------
        public bool IsModalOpen => _overlay != null && _overlay.gameObject.activeSelf;
        public void SetLevel(int n) => _level.text = $"LEVEL {n}";
        public void SetShips(int departed, int total) => _ships.text = $"Ships {departed} of {total}";
        public void SetCoins(int coins) => _coins.text = coins.ToString("N0");

        public void SetBooster(BoosterType type, int count, bool armed = false)
        {
            _boosterCounts[type].text = count > 0 ? count.ToString() : "+";
            _boosterBg[type].color = armed ? Palette.Hex("FFD98A") : _pal.uiCream;
        }

        public void SetCraneHint(bool on) => _hint.transform.parent.gameObject.SetActive(on);

        public void Toast(string message, float seconds = 1.8f)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message, seconds));
        }

        private IEnumerator ToastRoutine(string message, float seconds)
        {
            _toast.text = message;
            yield return Tween.Run(0.15f, e => _toastGroup.alpha = e);
            yield return new WaitForSeconds(seconds);
            yield return Tween.Run(0.25f, e => _toastGroup.alpha = 1 - e);
        }

        // ------------------------------------------------------------------
        public void HidePanels()
        {
            foreach (Transform child in _overlay) Destroy(child.gameObject);
            _overlay.gameObject.SetActive(false);
        }

        public void ShowWin(int level, int coinsEarned, Action next)
        {
            var card = Card("Level complete!", $"Level {level} cleared\n+{coinsEarned} coins");
            AddCardButton(card, "Next level", _pal.uiGood, next, 0);
        }

        public void ShowLose(string reason, bool canContinue, Action secondChance, Action retry)
        {
            var card = Card("Out of room!", reason);
            int i = 0;
            if (canContinue) AddCardButton(card, "Second chance (+2 slots)", _pal.uiGood, secondChance, i++);
            AddCardButton(card, "Retry level", _pal.uiAccent, retry, i);
        }

        public void ShowPause(Action resume, Action restart, Action skip)
        {
            var card = Card("Paused", "Prototype build");
            AddCardButton(card, "Resume", _pal.uiGood, resume, 0);
            AddCardButton(card, "Restart level", _pal.uiAccent, restart, 1);
            AddCardButton(card, "Skip level (test)", Palette.Hex("8A8F98"), skip, 2);
        }

        private RectTransform Card(string title, string body)
        {
            HidePanels();
            _overlay.gameObject.SetActive(true);
            var card = UiFactory.Panel("Card", _overlay, _pal.uiCream, _pal.uiShadow, 16f);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(860, 900);
            var t = UiFactory.Rect("Title", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(-80, 110));
            UiFactory.Label("TitleText", t, title, 76, _pal.uiText);
            var b = UiFactory.Rect("Body", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(-100, 180));
            UiFactory.Label("BodyText", b, body, 44, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            StartCoroutine(Tween.Run(0.3f, e => { if (rt) rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, e); }, Tween.EaseOutBack));
            return rt;
        }

        private void AddCardButton(RectTransform card, string label, Color color, Action onClick, int index)
        {
            var b = UiFactory.Button(label, card, color, Palette.Darken(color, 0.3f), onClick, label);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(680, 140);
            rt.anchoredPosition = new Vector2(0, 70 + (2 - index) * 170); // stacked from the top: 410, 240, 70
            UiFactory.Label("Text", b.transform, label, 46, Color.white);
        }
    }

    /// <summary>Keeps HUD content inside the device safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        private Rect _last;
        private void Update()
        {
            var safe = Screen.safeArea;
            if (safe == _last || Screen.width == 0 || Screen.height == 0) return;
            _last = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
